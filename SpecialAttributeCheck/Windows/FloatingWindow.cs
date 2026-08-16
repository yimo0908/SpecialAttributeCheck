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
    private static readonly Vector4 RowEvenBg = new(0.05f, 0.06f, 0.09f, 0.55f);
    private static readonly Vector4 RowOddBg = new(0.11f, 0.13f, 0.17f, 0.55f);

    private readonly Plugin plugin;
    private bool collapsed;
    private bool blurNames;

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

        if (ImGui.Button("周围检测"))
            plugin.ScanService.StartNearbyScan();
        ImGuiOm.TooltipHover("检查周围20米内最多48名玩家的补正");

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

        using var table = ImRaii.Table("ResultsTable", 3, ImGuiTableFlags.RowBg);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("职业", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("补正", ImGuiTableColumnFlags.WidthFixed, 100f);

        // 表头：单击“名称”可模糊 / 取消模糊玩家名称
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();
        ImGui.TableHeader("职业");
        ImGui.TableNextColumn();
        if (ImGui.Button("名称", new Vector2(ImGui.GetContentRegionAvail().X, 0f)))
            blurNames = !blurNames;
        ImGuiOm.TooltipHover("单击模糊 / 取消模糊玩家名称");
        ImGui.TableNextColumn();
        ImGui.TableHeader("补正");

        var rowIndex = 0;
        foreach (var result in scan.Results)
        {
            ImGui.TableNextRow();
            ImGui.TableSetBgColor(
                ImGuiTableBgTarget.RowBg0,
                ImGui.GetColorU32(rowIndex % 2 == 0 ? RowEvenBg : RowOddBg));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(result.JobName);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(blurNames ? BlurName(result.Name) : result.Name);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(result.CorrectionText);

            rowIndex++;
        }
    }

    private static string BlurName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return new string('█', name.Length);
    }
}
