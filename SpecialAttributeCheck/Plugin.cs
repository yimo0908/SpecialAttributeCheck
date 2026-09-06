using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SpecialAttributeCheck.Services;
using SpecialAttributeCheck.Windows;

namespace SpecialAttributeCheck;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    private const string CommandName = "/nmc";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SpecialAttributeCheck");

    internal ConfigWindow ConfigWindow { get; init; }

    internal FloatingWindow FloatingWindow { get; init; }

    internal ScanService ScanService { get; init; }

    internal NameplateService NameplateService { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // OmenTools 初始化：服务容器、ImGui 辅助与生命周期管理
        DService.Init(PluginInterface);

        ScanService = new ScanService(this, Log, PartyList, TargetManager);
        NameplateService = new NameplateService(this, AddonLifecycle);

        ConfigWindow = new ConfigWindow(this);
        FloatingWindow = new FloatingWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(FloatingWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开新月岛补正检测设置",
        });

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        Framework.Update += ScanService.Update;

        Log.Information($"=== {PluginInterface.Manifest.Name} 已加载 ===");
    }

    public void Dispose()
    {
        NameplateService.Dispose();

        Framework.Update -= ScanService.Update;

        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);

        DService.Uninit();
    }

    private void OnDraw()
    {
        FloatingWindow.IsOpen = FloatingWindow.ShouldBeOpen;
        WindowSystem.Draw();
    }

    private void OnCommand(string command, string args)
    {
        ToggleConfigUi();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
