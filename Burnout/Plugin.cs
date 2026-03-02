using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using Burnout.Windows;

namespace Burnout;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private static readonly string[] CommandAliases = { "/burnout", "/burn", "/calendar", "/schedule", "/workplan" };
    public Configuration Configuration { get; }
    private readonly WindowSystem windowSystem = new("Burnout");
    private readonly BurnoutWindow mainWindow;

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        mainWindow = new BurnoutWindow(Configuration, () => Configuration.Save());
        windowSystem.AddWindow(mainWindow);

        var cmdInfo = new CommandInfo(OnCommand) { HelpMessage = "Open the weekly schedule planner." };
        foreach (var cmd in CommandAliases)
            CommandManager.AddHandler(cmd, cmdInfo);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;

        Log.Information("Burnout loaded.");
    }

    private void OnCommand(string command, string args)
    {
        mainWindow.OpenMain();
    }

    public static void SendChatCommand(string command)
    {
        Framework.RunOnTick(() =>
        {
            try
            {
                ECommons.Automation.Chat.SendMessage(command);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send command: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.OpenSettings;
        foreach (var cmd in CommandAliases)
            CommandManager.RemoveHandler(cmd);
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        ECommonsMain.Dispose();
    }
}
