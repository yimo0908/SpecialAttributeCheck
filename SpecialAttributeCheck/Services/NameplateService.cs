using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SpecialAttributeCheck.Data;

namespace SpecialAttributeCheck.Services;

/// <summary>
/// 玩家名牌补正显示：参照 Distance 的实现方式，监听 NamePlate 绘制，
/// 通过 UI3DModule 把名牌与实体 ID 对应起来，并在匹配到扫描结果的名牌
/// 名字（称号）后方挂一个自定义 AtkTextNode 显示补正结果。
/// </summary>
public unsafe sealed class NameplateService : IDisposable
{
    private const uint NodeIdBase = 0x6C78C500;
    private static readonly int MaxNameplates = AddonNamePlate.NumNamePlateObjects;
    private const ushort DefaultTextNodeWidth = 200;
    private const ushort DefaultTextNodeHeight = 14;

    /// <summary>补正文本与名字文本的间距（未缩放像素）。</summary>
    private const int TextGap = 4;

    private readonly Plugin plugin;
    private readonly IAddonLifecycle addonLifecycle;

    private AddonNamePlate* addon;
    private readonly AtkTextNode*[] textNodes = new AtkTextNode*[MaxNameplates];
    private readonly Dictionary<uint, PlayerResult> resultByEntity = [];
    private List<PlayerResult>? cachedResults;
    private int cachedResultCount = -1;
    private bool cachedEnabled = true;

    public NameplateService(Plugin plugin, IAddonLifecycle addonLifecycle)
    {
        this.plugin = plugin;
        this.addonLifecycle = addonLifecycle;

        addonLifecycle.RegisterListener(AddonEvent.PostDraw, "NamePlate", OnNamePlateDraw);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "NamePlate", OnNamePlateFinalize);
    }

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(OnNamePlateDraw);
        addonLifecycle.UnregisterListener(OnNamePlateFinalize);
        DestroyAllNodes();
    }

    private void OnNamePlateDraw(AddonEvent type, AddonArgs args)
    {
        try
        {
            var currentAddon = (AddonNamePlate*)args.Addon.Address;
            if (currentAddon == null)
                return;

            // 名牌 addon 重建时，旧节点可能已被游戏释放，直接重置引用
            if (addon != currentAddon)
            {
                Array.Clear(textNodes);
                addon = currentAddon;
            }

            UpdateResultMap();

            var uiModule = UIModule.Instance();
            if (uiModule == null)
                return;

            var ui3d = uiModule->GetUI3DModule();
            if (ui3d == null)
                return;

            var count = Math.Min(ui3d->NamePlateObjectInfoCount, MaxNameplates);
            for (var i = 0; i < count; i++)
            {
                var objectInfo = ui3d->NamePlateObjectInfoPointers[i].Value;
                if (objectInfo == null || objectInfo->GameObject == null)
                    continue;

                var nameplateIndex = objectInfo->NamePlateIndex;
                if (nameplateIndex >= MaxNameplates)
                    continue;

                resultByEntity.TryGetValue(objectInfo->GameObject->EntityId, out var result);
                UpdateNameplateNode(nameplateIndex, result);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[补正检测] 名牌绘制异常");
        }
    }

    /// <summary>仅在扫描结果变化时重建实体 ID → 结果映射，避免每帧分配。</summary>
    private void UpdateResultMap()
    {
        var enabled = plugin.Configuration.ShowNameplateOutput
            && CorrectionData.IsCrescentTerritory(Plugin.ClientState.TerritoryType);

        var results = plugin.ScanService.Results;
        if (enabled == cachedEnabled
            && ReferenceEquals(results, cachedResults)
            && results.Count == cachedResultCount)
            return;

        cachedEnabled = enabled;
        cachedResults = results;
        cachedResultCount = results.Count;

        resultByEntity.Clear();
        if (!enabled)
            return;

        foreach (var result in results)
            resultByEntity[result.EntityId] = result;
    }

    /// <summary>更新单个名牌下方的补正文本节点。</summary>
    private void UpdateNameplateNode(int index, PlayerResult? result)
    {
        var nameplate = &addon->NamePlateObjectArray[index];

        // 无结果的名牌直接隐藏，且不为其创建节点
        if (result == null)
        {
            var existing = textNodes[index];
            if (existing != null)
                existing->AtkResNode.ToggleVisibility(false);
            return;
        }

        var correctionText = result.CorrectionText;

        var node = GetOrCreateNode(index);
        if (node == null)
            return;

        var nameText = nameplate->NameText;

        var visible = result != null
            && nameText != null
            && nameplate->RootComponentNode != null
            && nameplate->RootComponentNode->IsVisible();

        node->AtkResNode.ToggleVisibility(visible);
        if (!visible)
            return;

        var nameRes = &nameText->AtkResNode;

        // 跟随名字文本的缩放、颜色与字体，使补正行与名牌观感一致
        node->AtkResNode.SetScale(nameRes->ScaleX, nameRes->ScaleY);
        node->AtkResNode.SetUseDepthBasedPriority(true);
        node->AtkResNode.Color.A = nameRes->Color.A;

        node->TextColor = nameText->TextColor;
        node->EdgeColor = nameText->EdgeColor;
        // 补正字号比名字小 2 号
        node->FontSize = (byte)Math.Max(0, nameText->FontSize - 2);
        node->FontType = nameText->FontType;
        node->LineSpacing = nameText->LineSpacing;
        node->CharSpacing = nameText->CharSpacing;

        // 显示在名字（称号）后方：右对齐到名字文本右缘，垂直居中于名字行
        node->AlignmentType = AlignmentType.Right;
        var positionX = (short)(nameRes->X + (int)(nameRes->Width * nameRes->ScaleX) + TextGap);
        var positionY = (short)(nameRes->Y + (int)(nameRes->Height * nameRes->ScaleY * 0.5f));
        node->AtkResNode.SetPositionShort(positionX, positionY);

        if (node->GetText().ToString() != correctionText)
            node->SetText(correctionText);
    }

    /// <summary>按需为指定名牌创建文本节点并挂到名字容器（参照 Distance 的挂载方式）。</summary>
    private AtkTextNode* GetOrCreateNode(int index)
    {
        var node = textNodes[index];
        if (node != null)
            return node;

        var nameplate = &addon->NamePlateObjectArray[index];
        var nameContainer = nameplate->NameContainer;
        if (nameContainer == null)
            return null;

        node = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
        if (node == null)
            return null;

        node->AtkResNode.Type = NodeType.Text;
        node->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
        node->AtkResNode.DrawFlags = 0;
        node->AtkResNode.SetPositionShort(0, 0);
        node->AtkResNode.SetWidth(DefaultTextNodeWidth);
        node->AtkResNode.SetHeight(DefaultTextNodeHeight);
        node->LineSpacing = 24;
        node->CharSpacing = 1;
        node->AlignmentType = AlignmentType.TopLeft;
        node->FontSize = 12;
        node->TextFlags = TextFlags.Edge | TextFlags.Glare;
        node->AtkResNode.NodeId = NodeIdBase + (uint)index;
        node->AtkResNode.Color.A = 0xFF;
        node->AtkResNode.Color.R = 0xFF;
        node->AtkResNode.Color.G = 0xFF;
        node->AtkResNode.Color.B = 0xFF;

        var lastChild = nameContainer->ChildNode;
        if (lastChild != null)
        {
            while (lastChild->PrevSiblingNode != null)
                lastChild = lastChild->PrevSiblingNode;

            node->AtkResNode.NextSiblingNode = lastChild;
            node->AtkResNode.ParentNode = nameContainer;
            lastChild->PrevSiblingNode = &node->AtkResNode;
        }
        else
        {
            node->AtkResNode.ParentNode = nameContainer;
            nameContainer->ChildNode = &node->AtkResNode;
        }

        if (nameplate->RootComponentNode != null)
            nameplate->RootComponentNode->Component->UldManager.UpdateDrawNodeList();

        textNodes[index] = node;
        return node;
    }

    private void OnNamePlateFinalize(AddonEvent type, AddonArgs args)
    {
        try
        {
            var currentAddon = (AddonNamePlate*)args.Addon.Address;
            if (addon != currentAddon)
                return;

            DestroyAllNodes();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[补正检测] 名牌节点清理异常");
        }
    }

    /// <summary>解除全部文本节点与名牌的挂接并释放内存。</summary>
    private void DestroyAllNodes()
    {
        if (addon == null)
            return;

        for (var i = 0; i < MaxNameplates; i++)
        {
            var node = textNodes[i];
            if (node == null)
                continue;

            var res = &node->AtkResNode;
            if (res->ParentNode != null && res->ParentNode->ChildNode == res)
                res->ParentNode->ChildNode = res->NextSiblingNode;
            if (res->PrevSiblingNode != null)
                res->PrevSiblingNode->NextSiblingNode = res->NextSiblingNode;
            if (res->NextSiblingNode != null)
                res->NextSiblingNode->PrevSiblingNode = res->PrevSiblingNode;

            if (i < MaxNameplates && addon->NamePlateObjectArray[i].RootComponentNode != null)
                addon->NamePlateObjectArray[i].RootComponentNode->Component->UldManager.UpdateDrawNodeList();

            node->AtkResNode.Destroy(false);
            IMemorySpace.Free(node, (ulong)sizeof(AtkTextNode));
            textNodes[i] = null;
        }

        addon = null;
    }
}
