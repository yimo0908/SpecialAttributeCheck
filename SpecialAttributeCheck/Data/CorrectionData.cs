using System.Collections.Generic;

namespace SpecialAttributeCheck.Data;

/// <summary>
/// 新月岛补正检测的静态数据。
/// 地域、状态与物品 ID 均通过 EXDViewer MCP 查询游戏数据核实。
/// </summary>
public static partial class CorrectionData
{
    /// <summary>辅助职业大师（满级辅助职业层数）状态 ID。</summary>
    public const uint SupportJobMasterStatusId = 4226u;

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
}
