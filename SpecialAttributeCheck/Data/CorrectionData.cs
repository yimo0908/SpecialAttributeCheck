using System.Collections.Generic;
using OmenTools.Info.Game.Enums;

namespace SpecialAttributeCheck.Data;

/// <summary>战斗职业职能分类，用于悬浮窗职业名着色。</summary>
public enum JobRole
{
    None = 0,
    Tank,
    Healer,
    Melee,
    Ranged,
    Caster,
}

/// <summary>
/// 新月岛补正检测的静态数据。
/// 地域、状态与物品 ID 均通过 EXDViewer MCP 查询游戏数据核实。
/// </summary>
public static partial class CorrectionData
{
    /// <summary>辅助职业大师（满级辅助职业层数）状态 ID。</summary>
    public const uint SupportJobMasterStatusId = 4226u;

    /// <summary>周围检测的半径（米）。</summary>
    public const float NearbyScanRadius = 20f;

    /// <summary>周围检测最多检测的玩家数。</summary>
    public const int MaxNearbyPlayers = 48;

    /// <summary>套装补正额外计入值（超力/超魔之新月魔戒指的特殊修正+2）。</summary>
    public const int SetBonusCorrection = 2;

    /// <summary>
    /// 允许显示悬浮窗的地域。
    /// 1252 = 新月岛南部；1346 = 新月岛北部。
    /// “超魔之塔”（ContentFinderCondition 1114）在同一版本数据中同样位于地域 1346 内，
    /// 因此无需额外 TerritoryType ID。
    /// </summary>
    public static readonly uint[] CrescentTerritoryIds = [1252u, 1346u];

    /// <summary>力之新月魔五件套（含超力之新月魔戒指）。</summary>
    public static readonly uint[] PowerSetItems = [49826u, 49827u, 49828u, 49829u, 49830u];

    /// <summary>魔之新月魔五件套（含超魔之新月魔戒指）。</summary>
    public static readonly uint[] MagicSetItems = [49831u, 49832u, 49833u, 49834u, 49835u];

    /// <summary>ClassJob 行 ID 到中文职业名的映射（经 EXDViewer MCP 查询核实）。</summary>
    public static readonly Dictionary<uint, string> JobNames = new()
    {
        [1] = "剑术师",
        [2] = "格斗家",
        [3] = "斧术师",
        [4] = "枪术师",
        [5] = "弓箭手",
        [6] = "幻术师",
        [7] = "咒术师",
        [8] = "刻木匠",
        [9] = "锻铁匠",
        [10] = "铸甲匠",
        [11] = "雕金匠",
        [12] = "制革匠",
        [13] = "裁衣匠",
        [14] = "炼金术士",
        [15] = "烹调师",
        [16] = "采矿工",
        [17] = "园艺工",
        [18] = "捕鱼人",
        [19] = "骑士",
        [20] = "武僧",
        [21] = "战士",
        [22] = "龙骑士",
        [23] = "吟游诗人",
        [24] = "白魔法师",
        [25] = "黑魔法师",
        [26] = "秘术师",
        [27] = "召唤师",
        [28] = "学者",
        [29] = "双剑师",
        [30] = "忍者",
        [31] = "机工士",
        [32] = "暗黑骑士",
        [33] = "占星术士",
        [34] = "武士",
        [35] = "赤魔法师",
        [36] = "青魔法师",
        [37] = "绝枪战士",
        [38] = "舞者",
        [39] = "钐镰客",
        [40] = "贤者",
        [41] = "蝰蛇剑士",
        [42] = "绘灵法师",
    };

    /// <summary>ClassJob 行 ID 到两字职业简称的映射（国服常用简称，非游戏内字段）。</summary>
    public static readonly Dictionary<uint, string> JobShortNames = new()
    {
        [1] = "剑术",
        [2] = "格斗",
        [3] = "斧术",
        [4] = "枪术",
        [5] = "弓箭",
        [6] = "幻术",
        [7] = "咒术",
        [8] = "刻木",
        [9] = "锻铁",
        [10] = "铸甲",
        [11] = "雕金",
        [12] = "制革",
        [13] = "裁衣",
        [14] = "炼金",
        [15] = "烹调",
        [16] = "采矿",
        [17] = "园艺",
        [18] = "捕鱼",
        [19] = "骑士",
        [20] = "武僧",
        [21] = "战士",
        [22] = "龙骑",
        [23] = "诗人",
        [24] = "白魔",
        [25] = "黑魔",
        [26] = "秘术",
        [27] = "召唤",
        [28] = "学者",
        [29] = "双剑",
        [30] = "忍者",
        [31] = "机工",
        [32] = "黑骑",
        [33] = "占星",
        [34] = "武士",
        [35] = "赤魔",
        [36] = "青魔",
        [37] = "绝枪",
        [38] = "舞者",
        [39] = "钐镰",
        [40] = "贤者",
        [41] = "蝰蛇",
        [42] = "绘灵",
    };

    /// <summary>ClassJob 行 ID 到职能分类的映射（战斗职业）。</summary>
    public static readonly Dictionary<uint, JobRole> JobRoles = new()
    {
        [19] = JobRole.Tank,
        [21] = JobRole.Tank,
        [32] = JobRole.Tank,
        [37] = JobRole.Tank,

        [24] = JobRole.Healer,
        [28] = JobRole.Healer,
        [33] = JobRole.Healer,
        [40] = JobRole.Healer,

        [20] = JobRole.Melee,
        [22] = JobRole.Melee,
        [30] = JobRole.Melee,
        [34] = JobRole.Melee,
        [39] = JobRole.Melee,
        [41] = JobRole.Melee,

        [23] = JobRole.Ranged,
        [31] = JobRole.Ranged,
        [38] = JobRole.Ranged,

        [25] = JobRole.Caster,
        [27] = JobRole.Caster,
        [35] = JobRole.Caster,
        [42] = JobRole.Caster,
    };

    public static bool IsCrescentTerritory(uint territoryType)
    {
        foreach (var id in CrescentTerritoryIds)
        {
            if (id == territoryType)
                return true;
        }
        return false;
    }

    public static string GetJobName(uint classJobId)
        => JobNames.TryGetValue(classJobId, out var name) ? name : $"职业{classJobId}";

    public static string GetJobShortName(uint classJobId)
        => JobShortNames.TryGetValue(classJobId, out var name) ? name : GetJobName(classJobId);

    public static JobRole GetJobRole(uint classJobId)
        => JobRoles.TryGetValue(classJobId, out var role) ? role : JobRole.None;

    /// <summary>ClassJob 行 ID 对应的普通职业图标 ID（ClassJob 行 ID + 62000，与 OmenTools ClassJobIconType.Normal 一致）。</summary>
    public static uint GetJobIconId(uint classJobId)
        => classJobId + (uint)ClassJobIconType.Normal;
}
