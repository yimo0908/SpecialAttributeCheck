using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using OmenTools.Dalamud.Services.Game.Object.Abstractions;
using OmenTools.OmenService;
using SpecialAttributeCheck.Data;

namespace SpecialAttributeCheck.Services;

/// <summary>
/// 小队补正检测：
/// x = 辅助职业大师（Status 4226）层数，对所有小队成员读取；
/// y = 装备特殊修正之和（含套装补正）——本人直接读装备栏，其余成员通过查看装备流程读取。
/// </summary>
public sealed class ScanService
{
    private readonly Plugin plugin;
    private readonly IPluginLog log;
    private readonly IPartyList partyList;
    private readonly ITargetManager targetManager;
    private readonly InspectService inspect;

    private readonly List<ScanTarget> targets = [];
    private bool finalized;

    public ScanService(Plugin plugin, IPluginLog log, IPartyList partyList, ITargetManager targetManager)
    {
        this.plugin = plugin;
        this.log = log;
        this.partyList = partyList;
        this.targetManager = targetManager;
        inspect = new InspectService(log);
        inspect.EntityInspected += OnEntityInspected;
    }

    public List<PlayerResult> Results { get; private set; } = [];

    public bool IsScanning => inspect.IsBusy;

    public string ProgressText { get; private set; } = string.Empty;

    public string NoticeText { get; private set; } = string.Empty;

    private IObjectTable ObjectTable => DService.Instance().ObjectTable;

    /// <summary>小队检测：检查小队全部成员的补正，并按设置输出。</summary>
    public unsafe void StartPartyScan()
    {
        if (IsScanning)
            return;

        var list = new List<ScanTarget>();

        foreach (var member in partyList)
        {
            var obj = ObjectTable.SearchByEntityID(member.EntityId) as IPlayerCharacter;
            var x = obj != null
                ? ReadSupportJobMasterStacks(&obj.ToBCStruct()->StatusManager)
                : ReadSupportJobMasterStacks(&((PartyMember*)member.Address)->StatusManager);

            list.Add(new ScanTarget
            {
                Name = member.Name.TextValue,
                EntityId = member.EntityId,
                ClassJobId = member.ClassJob.RowId,
                X = x,
                IsLocal = member.EntityId == ObjectTable.LocalPlayer?.EntityID,
            });
        }

        EnsureLocalPlayer(list);
        BeginScan(list);
    }

    /// <summary>周围检测：检查周围 20 米内最多 48 名玩家的补正。</summary>
    public unsafe void StartNearbyScan()
    {
        if (IsScanning)
            return;

        var local = ObjectTable.LocalPlayer;
        if (local == null)
            return;

        var list = new List<ScanTarget>();

        foreach (var obj in ObjectTable.SearchObjects(x => x is IPlayerCharacter, IObjectTable.CharactersRange))
        {
            if (obj is not IPlayerCharacter player)
                continue;

            if (list.Count >= CorrectionData.MaxNearbyPlayers)
                break;

            if (Vector3.Distance(player.Position, local.Position) > CorrectionData.NearbyScanRadius)
                continue;

            list.Add(new ScanTarget
            {
                Name = player.Name,
                EntityId = player.EntityID,
                ClassJobId = player.ClassJob.RowId,
                X = ReadSupportJobMasterStacks(&player.ToBCStruct()->StatusManager),
                IsLocal = player.EntityID == local.EntityID,
            });
        }

        BeginScan(list);
    }

    /// <summary>目标检测：检查当前选中的玩家的补正。</summary>
    public unsafe void StartTargetScan()
    {
        if (IsScanning)
            return;

        var target = targetManager.Target;
        if (target is not Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
        {
            NoticeText = "请先选中一名玩家";
            return;
        }

        NoticeText = string.Empty;

        var local = ObjectTable.LocalPlayer;
        var isLocal = local != null && player.EntityId == local.EntityID;

        var list = new List<ScanTarget>
        {
            new()
            {
                Name = player.Name.TextValue,
                EntityId = player.EntityId,
                ClassJobId = player.ClassJob.RowId,
                X = ReadSupportJobMasterStacks(&((BattleChara*)player.Address)->StatusManager),
                IsLocal = isLocal,
            },
        };

        BeginScan(list);
    }

    /// <summary>清除当前检测结果。</summary>
    public void ClearResults()
    {
        Results = [];
    }

    /// <summary>框架每帧调用，驱动查看装备流程。</summary>
    public void Update(IFramework framework)
    {
        if (!IsScanning)
            return;

        inspect.Update();

        if (IsScanning)
        {
            ProgressText = $"检测中 {Results.Count}/{targets.Count} ...";
            return;
        }

        FinalizeScan();
    }

    private void BeginScan(List<ScanTarget> scanTargets)
    {
        Results = [];
        targets.Clear();
        targets.AddRange(scanTargets);
        finalized = false;
        NoticeText = string.Empty;

        var pendingInspects = new List<uint>();

        foreach (var target in targets)
        {
            if (target.IsLocal)
            {
                var y = CorrectionCalculator.SumSpecialCorrection(ReadLocalEquippedItems());
                target.Y = y;
                EmitResult(target, false, y);
            }
            else
            {
                pendingInspects.Add(target.EntityId);
            }
        }

        inspect.Start(pendingInspects);

        ProgressText = $"检测中 {Results.Count}/{targets.Count} ...";

        if (!IsScanning)
            FinalizeScan();
    }

    private void FinalizeScan()
    {
        if (finalized)
            return;
        finalized = true;

        ProgressText = string.Empty;
    }

    /// <summary>单个非本地玩家查看装备流程结束（成功或失败）时，立即输出该玩家结果。</summary>
    private void OnEntityInspected(uint entityId, uint[]? gear)
    {
        foreach (var target in targets)
        {
            if (target.EntityId != entityId)
                continue;

            if (gear != null)
            {
                var y = CorrectionCalculator.SumSpecialCorrection(gear);
                target.Y = y;
                EmitResult(target, false, y);
            }
            else
            {
                EmitResult(target, true, 0);
            }

            return;
        }
    }

    /// <summary>生成并输出单个玩家结果：加入列表、按补正值排序，并按设置发送小队频道。</summary>
    private void EmitResult(ScanTarget target, bool isYUnknown, int y)
    {
        var useTotal = plugin.Configuration.CalculateTotalCorrection;

        var correctionText = isYUnknown
            ? useTotal ? "?" : $"{target.X}★+?"
            : useTotal ? ((2 * target.X) + y).ToString() : $"{target.X}★+{y}";

        var result = new PlayerResult
        {
            Name = target.Name,
            EntityId = target.EntityId,
            ClassJobId = target.ClassJobId,
            JobName = CorrectionData.GetJobShortName(target.ClassJobId),
            X = target.X,
            Y = y,
            IsYUnknown = isYUnknown,
            CorrectionText = correctionText,
        };

        Results.Add(result);

        // 按补正值 2x+y 从高到低排序，未知（y 读取失败）排在最后
        Results.Sort((a, b) => (b.Total ?? int.MinValue).CompareTo(a.Total ?? int.MinValue));

        if (plugin.Configuration.ShowChatOutput)
            ChatManager.Instance().SendMessage($"/p {result.DisplayLine}");
    }

    private unsafe void EnsureLocalPlayer(List<ScanTarget> list)
    {
        var local = ObjectTable.LocalPlayer;
        if (local == null)
            return;

        foreach (var target in list)
        {
            if (target.IsLocal)
                return;
        }

        list.Add(new ScanTarget
        {
            Name = local.Name,
            EntityId = local.EntityID,
            ClassJobId = local.ClassJob.RowId,
            X = ReadSupportJobMasterStacks(&local.ToBCStruct()->StatusManager),
            IsLocal = true,
        });
    }

    /// <summary>读取“辅助职业大师”状态的层数之和。</summary>
    private static unsafe int ReadSupportJobMasterStacks(StatusManager* statusManager)
    {
        if (statusManager == null)
            return 0;

        var x = 0;
        var count = Math.Min((int)statusManager->NumValidStatuses, 60);

        for (var i = 0; i < count; i++)
        {
            var status = statusManager->Status[i];
            if (status.StatusId == CorrectionData.SupportJobMasterStatusId)
                x += status.Param;
        }

        return x;
    }

    /// <summary>读取本地玩家已装备物品 ID（含武器到戒指全部 13 个栏位）。</summary>
    private static unsafe List<uint> ReadLocalEquippedItems()
    {
        var items = new List<uint>();
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null || !container->IsLoaded)
            return items;

        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item == null)
                continue;

            var itemId = item->ItemId;
            if (itemId != 0)
                items.Add(itemId);
        }

        return items;
    }

    private sealed class ScanTarget
    {
        public required string Name { get; init; }

        public required uint EntityId { get; init; }

        public required uint ClassJobId { get; init; }

        public int X { get; init; }

        public bool IsLocal { get; init; }

        public int? Y { get; set; }
    }
}
