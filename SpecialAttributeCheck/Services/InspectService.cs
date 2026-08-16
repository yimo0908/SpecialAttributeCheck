using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using OmenTools.Interop.Game.ExecuteCommand.Implementations;

namespace SpecialAttributeCheck.Services;

/// <summary>
/// 通过游戏“查看装备”流程读取其他玩家的已装备物品 ID。
/// 关键点（对照实测日志修正）：
/// 1) 请求走 OmenTools 的查看命令（InspectCommand.Inspect），与右键查看同一入口，
///    不再直接调用 AgentInspect.ExamineCharacter（实测该成员函数在当前客户端下未触发请求，status 恒为 0）；
/// 2) 请求前等待可能残留的查看窗口完全关闭，避免请求被忽略；
/// 3) 数据就绪以 Examine 容器出现物品为准（不依赖 FetchCharacterDataStatus 字段），AgentInspect 缓存作回退；
/// 4) 每目标最多重试 2 次：attempts 只在切换到下一目标时重置，不再在重发请求时清零。
/// </summary>
public unsafe sealed class InspectService
{
    private const double RequestGapMs = 1000.0;
    private const double WindowCloseWaitMs = 300.0;
    private const double WindowCloseTimeoutMs = 2000.0;
    private const double TimeoutMs = 6000.0;
    private const int MaxAttempts = 2;
    private const int InspectSlotCount = 13;

    private readonly IPluginLog log;

    private readonly List<uint> queue = [];
    private readonly Dictionary<uint, uint[]> gearByEntity = [];

    private int queueIndex;
    private int attempts;
    private bool inFlight;
    private uint inFlightEntity;
    private DateTime inFlightSince;
    private DateTime nextRequestAt = DateTime.MinValue;
    private DateTime closeWaitSince = DateTime.MinValue;

    public InspectService(IPluginLog log)
    {
        this.log = log;
    }

    public bool IsBusy => queueIndex < queue.Count || inFlight;

    public int TotalCount => queue.Count;

    public int CompletedCount => queueIndex;

    public IReadOnlyDictionary<uint, uint[]> GearByEntity => gearByEntity;

    public void Start(IEnumerable<uint> entityIds)
    {
        queue.Clear();
        queue.AddRange(entityIds.Distinct());
        queueIndex = 0;
        attempts = 0;
        inFlight = false;
        gearByEntity.Clear();
        nextRequestAt = DateTime.UtcNow;
    }

    /// <summary>每帧调用，驱动查看装备队列。</summary>
    public void Update()
    {
        if (!inFlight && queueIndex >= queue.Count)
            return;

        var now = DateTime.UtcNow;

        if (!inFlight)
        {
            if (now < nextRequestAt)
                return;

            // 等待可能残留的查看窗口完全关闭，避免新请求被忽略
            var pendingAddon = GetInspectAddon();
            if (pendingAddon != null)
            {
                if (closeWaitSince == DateTime.MinValue)
                    closeWaitSince = now;
                pendingAddon->Close(true);

                if ((now - closeWaitSince).TotalMilliseconds < WindowCloseTimeoutMs)
                {
                    nextRequestAt = now.AddMilliseconds(WindowCloseWaitMs);
                    return;
                }
            }

            closeWaitSince = DateTime.MinValue;
            inFlightEntity = queue[queueIndex];

            try
            {
                InspectCommand.Inspect(inFlightEntity);
                inFlight = true;
                inFlightSince = now;
                log.Debug($"[补正检测] 请求查看装备 EntityId={inFlightEntity} 尝试={attempts + 1}/{MaxAttempts}");
            }
            catch (Exception ex)
            {
                log.Error(ex, "[补正检测] 请求查看装备异常");
                FailCurrent();
            }
            return;
        }

        // 主路径：Examine 容器出现物品即视为数据就绪（对齐 DailyRoutines 的成熟实现）
        var items = TryReadExamineContainer();

        // 回退：AgentInspect 缓存物品（要求数据状态就绪且目标匹配）
        if (items == null)
        {
            var agent = AgentInspect.Instance();
            if (agent != null && agent->CurrentEntityId == inFlightEntity && agent->FetchCharacterDataStatus == 2)
                items = TryReadAgentItems(agent);
        }

        if (items is { Count: > 0 })
        {
            CompleteCurrent(items);
            return;
        }

        if ((now - inFlightSince).TotalMilliseconds < TimeoutMs)
            return;

        var manager = InventoryManager.Instance();
        var container = manager != null ? manager->GetInventoryContainer(InventoryType.Examine) : null;
        var addonOpen = GetInspectAddon() != null;

        log.Warning(
            $"[补正检测] EntityId={inFlightEntity} 超时 status={(AgentInspect.Instance() != null ? AgentInspect.Instance()->FetchCharacterDataStatus : -1)} " +
            $"查看窗口={addonOpen} 容器加载={container != null && container->IsLoaded} 尝试={attempts + 1}/{MaxAttempts}");

        if (attempts + 1 < MaxAttempts)
        {
            attempts++;
            inFlight = false;
            nextRequestAt = now.AddMilliseconds(RequestGapMs);
            return;
        }

        FailCurrent();
    }

    private static List<uint>? TryReadExamineContainer()
    {
        if (!InventoryType.Examine.TryGetItems(_ => true, out var examineItems))
            return null;

        var items = new List<uint>();
        foreach (var slot in examineItems)
        {
            if (slot.ItemId != 0)
                items.Add(slot.ItemId);
        }

        return items;
    }

    private static List<uint>? TryReadAgentItems(AgentInspect* agent)
    {
        var items = new List<uint>();
        for (var i = 0; i < InspectSlotCount; i++)
        {
            var itemId = agent->Items[i].ItemId;
            if (itemId != 0)
                items.Add(itemId);
        }

        return items.Count > 0 ? items : null;
    }

    private static unsafe AddonCharacterInspect* GetInspectAddon()
        => (AddonCharacterInspect*)CharacterInspect;

    private void CompleteCurrent(List<uint> items)
    {
        gearByEntity[inFlightEntity] = [.. items];
        FinishCurrent();
    }

    private void FailCurrent()
    {
        FinishCurrent();
    }

    private void FinishCurrent()
    {
        try
        {
            var inspectAddon = GetInspectAddon();
            if (inspectAddon != null)
                inspectAddon->Close(true);

            var agent = AgentInspect.Instance();
            if (agent != null)
                agent->FetchCharacterDataStatus = 0;
        }
        catch
        {
            // 忽略关闭查看窗口时的异常
        }

        attempts = 0;
        closeWaitSince = DateTime.MinValue;
        queueIndex++;
        inFlight = false;
        nextRequestAt = DateTime.UtcNow.AddMilliseconds(RequestGapMs);
    }
}
