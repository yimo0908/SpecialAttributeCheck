using System.Numerics;
using Dalamud.Interface.Windowing;
using SpecialAttributeCheck.Data;

namespace SpecialAttributeCheck.Windows;

/// <summary>
/// 新月岛补正检测悬浮窗：仅在新月岛（南部/北部/超魔之塔）显示，
/// 提供“小队检测”与“清除”两个按钮，并按设置输出检测结果。
/// </summary>
public sealed class FloatingWindow : Window
{
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1f);
    private static readonly Vector4 Gray = new(0.45f, 0.45f, 0.5f, 1f);
    private static readonly Vector4 Muted = new(0.62f, 0.67f, 0.76f, 1f);

    private readonly Plugin plugin;
    private bool collapsed;

    public FloatingWindow(Plugin plugin)
        : base(
            "##NewMoonCorrectionCheck",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav)
    {
        this.plugin = plugin;
        BgAlpha = 0.85f;
        SizeCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(420f, 220f);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    /// <summary>仅当玩家位于新月岛南部 / 北部 / 超魔之塔时显示。</summary>
    public bool ShouldBeOpen => CorrectionData.IsCrescentTerritory(Plugin.ClientState.TerritoryType);

    public override void Draw()
    {
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            plugin.ToggleConfigUi();

        if (DrawHeader())
            collapsed = !collapsed;

        if (collapsed)
            return;

        ImGui.Separator();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 5f));

        DrawButtons();
        DrawResults();

        ImGui.PopStyleVar();
    }

    private bool DrawHeader()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("新月岛补正检测");
        ImGui.PopStyleColor();

        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(collapsed ? "左键展开悬浮窗\n右键打开设置" : "左键折叠悬浮窗\n右键打开设置");
        return clicked;
    }

    private void DrawButtons()
    {
        if (ImGui.Button("小队检测"))
            plugin.ScanService.StartPartyScan();
        ImGuiOm.TooltipHover("检查小队补正");

        ImGui.SameLine();

        if (ImGui.Button("目标检测"))
            plugin.ScanService.StartTargetScan();
        ImGuiOm.TooltipHover("检查目标玩家的补正");

        ImGui.SameLine();

        if (ImGui.Button("清除"))
            plugin.ScanService.ClearResults();
        ImGuiOm.TooltipHover("清除当前检测结果");
    }

    private void DrawResults()
    {
        ImGui.Spacing();

        var scan = plugin.ScanService;

        if (scan.IsScanning)
        {
            ImGui.TextColored(Yellow, scan.ProgressText);
            return;
        }

        if (!string.IsNullOrEmpty(scan.NoticeText))
        {
            ImGui.TextColored(Gray, scan.NoticeText);
        }

        if (!plugin.Configuration.ShowFloatingWindowOutput)
        {
            ImGui.TextColored(Muted, "悬浮窗输出已关闭");
            return;
        }

        if (scan.Results.Count == 0)
        {
            ImGui.TextColored(Gray, "点击上方按钮开始检测");
            return;
        }

        foreach (var result in scan.Results)
            ImGui.TextUnformatted(result.DisplayLine);
    }
}
