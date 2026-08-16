using System.Collections.Generic;
using System.Linq;
using SpecialAttributeCheck.Data;

namespace SpecialAttributeCheck.Services;

/// <summary>
/// 补正计算：
/// y = 所穿新月岛装备特殊修正之和，并按力/魔之新月魔五件套规则追加套装补正 +2。
/// </summary>
public static class CorrectionCalculator
{
    public static int SumSpecialCorrection(IEnumerable<uint> itemIds)
    {
        var worn = new HashSet<uint>();
        var y = 0;

        foreach (var itemId in itemIds)
        {
            if (itemId == 0)
                continue;

            worn.Add(itemId);
            if (CorrectionData.EquipmentCorrection.TryGetValue(itemId, out var correction))
                y += correction;
        }

        // 力之新月魔五件套：耳饰+项链+手镯+戒指+超力之新月魔戒指
        if (CorrectionData.PowerSetItems.All(worn.Contains))
            y += CorrectionData.SetBonusCorrection;

        // 魔之新月魔五件套：耳饰+项链+手镯+戒指+超魔之新月魔戒指
        if (CorrectionData.MagicSetItems.All(worn.Contains))
            y += CorrectionData.SetBonusCorrection;

        return y;
    }
}
