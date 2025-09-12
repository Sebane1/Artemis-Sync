using ArtemisSync.Windows;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using McdfDataImporter;
using McdfLoader;
using McdfLoader.Services.Mediator;
using Penumbra.Api.IpcSubscribers;
using RelayCommonData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TranslatorCore;

namespace ArtemisSync;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    private const string CommandName = "/artemissync";
    private const string UploadCommandName = "/uploadchanges";

    public static Configuration Configuration { get; set; }

    public readonly WindowSystem WindowSystem = new("Artemis Sync");
    private static string _currentCharacterId;
    private bool _initialized;
    private EntryPoint _entryPoint;
    private bool _hasTargettedAPlayer;
    Stopwatch _appearancePollingTimer = new Stopwatch();
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private Queue<Tuple<string, string>> _appearanceQueue = new Queue<Tuple<string, string>>();
    public static string CurrentCharacterId { get => _currentCharacterId; set => _currentCharacterId = value; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        // You might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });
        CommandManager.AddHandler(UploadCommandName, new CommandInfo(UploadChanges)
        {
            HelpMessage = "Uploads current user appearance."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Framework.Update += Framework_Update;
        SseClient.OnSubscribedFileChanged += SseClient_OnSubscribedFileChanged;
        Penumbra.Api.IpcSubscribers.ModSettingChanged.Subscriber(PluginInterface).Event += Plugin_Event;
        Penumbra.Api.IpcSubscribers.GameObjectRedrawn.Subscriber(PluginInterface).Event += Plugin_Event1;
    }

    private void Plugin_Event1(nint arg1, int arg2)
    {
        if (arg2 == 0)
        {
            AppearanceCommunicationManager.RefreshAppearanceOnServers();
        }
    }

    private void Plugin_Event(Penumbra.Api.Enums.ModSettingChange arg1, Guid arg2, string arg3, bool arg4)
    {
        //   throw new NotImplementedException();
    }

    private void SseClient_OnSubscribedFileChanged(object? sender, Tuple<string, string, long> e)
    {
        if (ClientState.IsLoggedIn)
        {
            Framework.Run(() =>
            {
                foreach (var item in ObjectTable.PlayerObjects)
                {
                    if (item.ObjectIndex > 0)
                    {
                        if (e.Item1 == Hashing.SHA512Hash(item.Name.TextValue))
                        {
                            AppearanceCommunicationManager.GetPlayerAppearanceOnServers(item);
                        }
                    }
                }
            });
        }
    }

    private void UploadChanges(string command, string arguments)
    {
        AppearanceCommunicationManager.RefreshAppearanceOnServers();
    }

    private void ClientState_TerritoryChanged(ushort obj)
    {
        try
        {
            if (!string.IsNullOrEmpty(Configuration.CacheFolder))
            {
                AppearanceAccessUtils.AppearanceManager.RemoveAllTemporaryCollections();
                SubscribeGameObjects();
                DisposeData();
            }
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, e.Message);
        }
    }

    private void DisposeData()
    {
        foreach (var item in Directory.GetFiles(Configuration.CacheFolder))
        {
            try
            {
                File.Delete(item);
            }
            catch
            {

            }
        }
    }

    private void Framework_Update(IFramework framework)
    {
        try
        {
            if (ClientState.IsLoggedIn && ClientState.LocalPlayer != null)
            {
                if (!_initialized)
                {
                    _entryPoint = new EntryPoint(PluginInterface, CommandManager, DataManager, Framework, ObjectTable, ClientState, Condition,
                    ChatGui, GameGui, DtrBar, PluginLog, TargetManager, NotificationManager, TextureProvider, ContextMenu, GameInteropProvider, "");
                    AppearanceAccessUtils.CacheLocation = Configuration.CacheFolder;
                    Translator.UiLanguage = Plugin.Configuration.Language;
                    ClientState.Login += ClientState_Login;
                    ClientState.TerritoryChanged += ClientState_TerritoryChanged;
                    GetCharacterId();
                    AppearanceCommunicationManager.StartSSEListeners();
                    SubscribeGameObjects();
                    _initialized = true;
                }
                else
                {
                    if (ClientState.LocalPlayer.TargetObject != null)
                    {
                        if (!_hasTargettedAPlayer)
                        {
                            _hasTargettedAPlayer = true;
                            AppearanceCommunicationManager.GetPlayerAppearanceOnServers(ClientState.LocalPlayer.TargetObject);
                            AppearanceCommunicationManager.SubscribeToAppearanceEventOnServers(Hashing.SHA512Hash(ClientState.LocalPlayer.Name.TextValue));
                        }
                    }
                    else
                    {
                        _hasTargettedAPlayer = false;
                    }
                }
            }
        }
        catch (Exception e)
        {
            PluginLog.Warning(e, e.Message);
        }
    }

    private void ClientState_Login()
    {
        GetCharacterId();
    }
    public void GetCharacterId()
    {
        try
        {
            _currentCharacterId = Hashing.SHA512Hash(ClientState.LocalPlayer.Name.ToString());
        }
        catch (Exception e)
        {

        }
    }

    public void SubscribeGameObjects()
    {
        Framework.Run(() =>
        {
            foreach (var item in ObjectTable.PlayerObjects)
            {
                if (item.ObjectIndex > 0)
                {
                    try
                    {
                        AppearanceCommunicationManager.SubscribeToAppearanceEventOnServers(Hashing.SHA512Hash(item.Name.ToString()));
                        AppearanceCommunicationManager.GetPlayerAppearanceOnServers(item);
                    }
                    catch (Exception e)
                    {
                        PluginLog.Warning(e, e.Message);
                    }
                }
            }
        });
    }

    public void Dispose()
    {
        DisposeData();
        ClientState.Login -= ClientState_Login;
        Framework.Update -= Framework_Update;
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        _entryPoint?.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
