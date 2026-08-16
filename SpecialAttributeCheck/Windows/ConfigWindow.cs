using System.Numerics;
using Dalamud.Interface.Windowing;

namespace SpecialAttributeCheck.Windows;

/// <summary>插件设置界面（仅设置，无主界面）。</summary>
public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("新月岛补正检测 - 设置###NewMoonCorrectionCheckConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(380, 200);
        SizeCondition = ImGuiCond.Always;

        this.plugin = plugin;
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        ImGui.TextUnformatted("输出设置");
        ImGui.Separator();

        var showFloating = cfg.ShowFloatingWindowOutput;
        if (ImGui.Checkbox("悬浮窗输出（默认开启）", ref showFloating))
        {
            cfg.ShowFloatingWindowOutput = showFloating;
            cfg.Save();
        }

        var showChat = cfg.ShowChatOutput;
        if (ImGui.Checkbox("聊天输出（默认关闭，发送至小队频道）", ref showChat))
        {
            cfg.ShowChatOutput = showChat;
            cfg.Save();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("命令：/nmc 打开设置；右键悬浮窗也可打开设置。");
    }
}
