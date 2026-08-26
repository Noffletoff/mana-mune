using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ManaMune.Ui;

namespace ManaMune;

public sealed class Plugin : IDalamudPlugin
{
    private const string MainCommand = "/manamune";

    private readonly ICommandManager _commands;
    private readonly IChatGui _chat;
    private readonly IPluginLog _log;
    private readonly IDalamudPluginInterface _pi;

    private readonly Config _config;
    private readonly CustomizePlusBridge _cp;
    private readonly ManaWatcher _watcher;

    private readonly WindowSystem _windows = new("ManaMune");
    private readonly ConfigWindow _configWindow;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        ICommandManager commands,
        IChatGui chat,
        IClientState clientState,
        IObjectTable objects,
        IFramework framework)
    {
        _pi = pluginInterface;
        _log = log;
        _commands = commands;
        _chat = chat;

        _config = _pi.GetPluginConfig() as Config ?? new Config();
        _config.Initialise(_pi);

        _cp = new CustomizePlusBridge(_pi, log);
        _watcher = new ManaWatcher(_config, _cp, clientState, objects, framework, log);

        _configWindow = new ConfigWindow(_config, _watcher);
        _windows.AddWindow(_configWindow);

        _pi.UiBuilder.Draw += _windows.Draw;
        _pi.UiBuilder.OpenConfigUi += ToggleWindow;
        _pi.UiBuilder.OpenMainUi += ToggleWindow;

        _commands.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open ManaMune. /manamune on | off | debug",
            ShowInHelp = true,
        });
    }

    public void Dispose()
    {
        _commands.RemoveHandler(MainCommand);

        _pi.UiBuilder.Draw -= _windows.Draw;
        _pi.UiBuilder.OpenConfigUi -= ToggleWindow;
        _pi.UiBuilder.OpenMainUi -= ToggleWindow;

        _windows.RemoveAllWindows();
        _configWindow.Dispose();

        // Takes the temporary profile off, so unloading leaves the character
        // wearing exactly what Customize+ would have given it anyway.
        _watcher.Dispose();
    }

    private void ToggleWindow() => _configWindow.IsOpen = !_configWindow.IsOpen;

    private void OnCommand(string _, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                ToggleWindow();
                break;

            case "on":
                SetEnabled(true);
                break;

            case "off":
                SetEnabled(false);
                break;

            case "debug":
                Debug();
                break;

            default:
                _chat.Print("ManaMune: /manamune [on|off|debug]");
                break;
        }
    }

    private void SetEnabled(bool value)
    {
        _config.Enabled = value;
        _config.Save();
        _watcher.SettingsChanged();
        _chat.Print($"ManaMune: {(value ? "on" : "off")}.");
    }

    /// <summary>
    /// Dump what the plugin can see of Customize+ to the log. The profile list
    /// is printed raw because its tuple carries two strings whose order is not
    /// documented anywhere - this is how to confirm which one is the name.
    /// </summary>
    private void Debug()
    {
        var version = _cp.ApiVersion();
        _chat.Print($"ManaMune: Customize+ available={_cp.Available}, "
                  + $"api={(version == null ? "?" : $"{version.Value.Breaking}.{version.Value.Feature}")}");
        _chat.Print($"ManaMune: base profile={_watcher.BaseProfileId?.ToString() ?? "none"}, "
                  + $"{_watcher.BaseBoneCount} bones, applied={_watcher.Applied}, "
                  + $"mana={_watcher.LastPercent}%, factor={_watcher.LastFactor:0.000}");
        _chat.Print($"ManaMune: bones = {string.Join(", ", _config.BoneNames())}");

        var profiles = _cp.ProfileList();
        foreach (var p in profiles)
        {
            _log.Information(
                "ManaMune profile: id={Id} s1='{S1}' s2='{S2}' chars={Chars} prio={Prio} enabled={On}",
                p.Item1, p.Item2, p.Item3,
                string.Join("/", p.Item4.Select(c => c.Item1)), p.Item5, p.Item6);
        }

        _chat.Print($"ManaMune: {profiles.Count} profiles written to the log (/xllog).");
    }
}
