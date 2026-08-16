namespace SpecialAttributeCheck.Data;

/// <summary>单个玩家的补正检测结果。</summary>
public sealed class PlayerResult
{
    public required string Name { get; init; }

    public required string JobName { get; init; }

    public required uint EntityId { get; init; }

    /// <summary>辅助职业大师层数 x。</summary>
    public int X { get; init; }

    /// <summary>装备特殊修正之和 y（含套装补正）。</summary>
    public int Y { get; init; }

    /// <summary>y 是否未知（无法读取装备时）。</summary>
    public bool IsYUnknown { get; init; }

    public string DisplayLine =>
        IsYUnknown ? $"{JobName} {Name} {X}★+?" : $"{JobName} {Name} {X}★+{Y}";
}
