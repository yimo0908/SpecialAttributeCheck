using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Textures;
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

    /// <summary>职业名按职能着色：坦克蓝 / 治疗绿 / 近战红 / 远敏黄 / 法系紫。</summary>
    private static readonly Dictionary<JobRole, Vector4> JobRoleColors = new()
    {
        [JobRole.Tank] = new(0.35f, 0.65f, 1f, 1f),
        [JobRole.Healer] = new(0.40f, 0.85f, 0.45f, 1f),
        [JobRole.Melee] = new(1f, 0.40f, 0.40f, 1f),
        [JobRole.Ranged] = Yellow,
        [JobRole.Caster] = new(0.72f, 0.55f, 1f, 1f),
    };

    /// <summary>职业图标显示尺寸（px）。</summary>
    private const float JobIconSize = 20f;

    /// <summary>职业图标与职业名之间的间距（px）。</summary>
    private const float JobIconSpacing = 4f;

    private readonly Plugin plugin;
    private readonly Dictionary<uint, ISharedImmediateTexture> jobIcons = [];
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
            ImGui.TextColored(Yellow, scan.ProgressText);
        else if (!string.IsNullOrEmpty(scan.NoticeText))
            ImGui.TextColored(Gray, scan.NoticeText);

        if (!plugin.Configuration.ShowFloatingWindowOutput)
        {
            ImGui.TextColored(Muted, "悬浮窗输出已关闭");
            return;
        }

        if (scan.Results.Count == 0)
        {
            if (!scan.IsScanning)
                ImGui.TextColored(Gray, "点击上方按钮开始检测");
            return;
        }

        using var table = ImRaii.Table("ResultsTable", 3, ImGuiTableFlags.RowBg);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("职业", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("补正", ImGuiTableColumnFlags.WidthFixed, 100f);

        // 不计算补正总数（x★+y）时，先统计 x★ 与 +y 段的最大宽度，用于 + 对齐
        var maxPrefixWidth = 0f;
        var maxSuffixWidth = 0f;
        if (!plugin.Configuration.CalculateTotalCorrection)
        {
            foreach (var result in scan.Results)
            {
                var plusIndex = result.CorrectionText.IndexOf('+');
                if (plusIndex < 0)
                    continue;

                maxPrefixWidth = Math.Max(maxPrefixWidth, ImGui.CalcTextSize(result.CorrectionText[..plusIndex]).X);
                maxSuffixWidth = Math.Max(maxSuffixWidth, ImGui.CalcTextSize(result.CorrectionText[plusIndex..]).X);
            }
        }

        // 表头：单击“名称”可模糊 / 取消模糊玩家名称；表头文字统一居中
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();
        DrawCenteredHeader("职业");
        ImGui.TableNextColumn();
        if (ImGui.Button("名称", new Vector2(ImGui.GetContentRegionAvail().X, 0f)))
            blurNames = !blurNames;
        ImGuiOm.TooltipHover("单击模糊 / 取消模糊玩家名称");
        ImGui.TableNextColumn();
        DrawCenteredHeader("补正");

        var rowIndex = 0;
        foreach (var result in scan.Results)
        {
            ImGui.TableNextRow();
            ImGui.TableSetBgColor(
                ImGuiTableBgTarget.RowBg0,
                ImGui.GetColorU32(rowIndex % 2 == 0 ? RowEvenBg : RowOddBg));

            ImGui.TableNextColumn();
            DrawJobCell(result);

            ImGui.TableNextColumn();
            DrawCenteredText(blurNames ? BlurName(result.Name) : result.Name);

            ImGui.TableNextColumn();
            if (plugin.Configuration.CalculateTotalCorrection || !result.CorrectionText.Contains('+'))
                DrawCenteredText(result.CorrectionText);
            else
                DrawPlusAlignedCorrection(result.CorrectionText, maxPrefixWidth, maxSuffixWidth);

            rowIndex++;
        }
    }

    /// <summary>职业列：职业图标 + 按职能着色的职业名，整体在单元格内居中。</summary>
    private void DrawJobCell(PlayerResult result)
    {
        var text = result.JobName;
        var textWidth = ImGui.CalcTextSize(text).X;
        CenterCursorInCell(JobIconSize + JobIconSpacing + textWidth);

        var wrap = GetJobIcon(result.ClassJobId).GetWrapOrEmpty();
        ImGui.Image(wrap.Handle, new Vector2(JobIconSize), Vector2.Zero, Vector2.One, Vector4.One, Vector4.Zero);

        ImGui.SameLine(0f, JobIconSpacing);
        if (JobRoleColors.TryGetValue(CorrectionData.GetJobRole(result.ClassJobId), out var jobColor))
            ImGui.TextColored(jobColor, text);
        else
            ImGui.TextUnformatted(text);
    }

    /// <summary>在单元格内按指定内容宽度水平居中光标。</summary>
    private static void CenterCursorInCell(float width)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > width)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((avail - width) * 0.5f));
    }

    /// <summary>在单元格内居中绘制文本。</summary>
    private static void DrawCenteredText(string text)
    {
        CenterCursorInCell(ImGui.CalcTextSize(text).X);
        ImGui.TextUnformatted(text);
    }

    /// <summary>表头文字居中，并与同行的按钮垂直对齐。</summary>
    private static void DrawCenteredHeader(string label)
    {
        CenterCursorInCell(ImGui.CalcTextSize(label).X);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
    }

    /// <summary>不计算补正总数时，x★ 右对齐、+y 紧随后方，使各行的 + 竖直对齐。</summary>
    private static void DrawPlusAlignedCorrection(string text, float maxPrefixWidth, float maxSuffixWidth)
    {
        var plusIndex = text.IndexOf('+');
        var prefix = text[..plusIndex];
        var suffix = text[plusIndex..];

        var prefixWidth = ImGui.CalcTextSize(prefix).X;
        var blockWidth = maxPrefixWidth + maxSuffixWidth;
        var avail = ImGui.GetContentRegionAvail().X;
        var startX = ImGui.GetCursorPosX();
        if (avail > blockWidth)
            startX += ((avail - blockWidth) * 0.5f);

        ImGui.SetCursorPosX(startX + (maxPrefixWidth - prefixWidth));
        ImGui.TextUnformatted(prefix);
        ImGui.SameLine(0f, 0f);
        ImGui.TextUnformatted(suffix);
    }

    /// <summary>按 ClassJobId 获取职业图标共享纹理并缓存（图标 ID = ClassJobId + 62000）。</summary>
    private ISharedImmediateTexture GetJobIcon(uint classJobId)
    {
        if (jobIcons.TryGetValue(classJobId, out var texture))
            return texture;

        texture = DService.Instance().Texture.GetFromGameIcon(new GameIconLookup(CorrectionData.GetJobIconId(classJobId)));
        jobIcons[classJobId] = texture;
        return texture;
    }

    private static string BlurName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return new string('█', name.Length);
    }
}
