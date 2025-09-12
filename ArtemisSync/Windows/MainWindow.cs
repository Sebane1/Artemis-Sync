using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using LanguageConversionProxy;
using Lumina.Excel.Sheets;
using McdfDataImporter;
using RelayUploadProtocol;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using TranslatorCore;

namespace ArtemisSync.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private Plugin Plugin;
    private FileDialogManager _fileDialogManager;
    private int _selectedItem;
    private string _currentIpEntry;
    private string _currentAuthenticationKey;
    private bool alreadyUploadingAppearance;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("Artemis Sync##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _fileDialogManager = new FileDialogManager();
        GoatImagePath = goatImagePath;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        _fileDialogManager.Draw();
        if (ImGui.BeginTabBar("ConfigTabs"))
        {
            if (!string.IsNullOrEmpty(Plugin.Configuration.CacheFolder) && Directory.Exists(Plugin.Configuration.CacheFolder))
            {
                if (ImGui.BeginTabItem(Translator.LocalizeUI("Users")))
                {
                    DrawUsers();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem(Translator.LocalizeUI("Servers")))
                {
                    DrawServers();
                    ImGui.EndTabItem();
                }
            }
            if (ImGui.BeginTabItem(Translator.LocalizeUI("Settings")))
            {
                DrawInitialSetup();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        if (ImGui.Button(Translator.LocalizeUI("Donate To Further Development"), new Vector2(Size.Value.X, 50)))
        {
            ProcessStartInfo ProcessInfo = new ProcessStartInfo();
            Process Process = new Process();
            ProcessInfo = new ProcessStartInfo("https://ko-fi.com/sebastina");
            ProcessInfo.UseShellExecute = true;
            Process = Process.Start(ProcessInfo);
        }
    }

    private void DrawInitialSetup()
    {
        if (ImGui.Button(Translator.LocalizeUI("Pick Empty Folder For Download Cache (Cannot Require Admin Rights)")))
        {
            _fileDialogManager.Reset();
            ImGui.OpenPopup(Translator.LocalizeUI("OpenPathDialog##editorwindow"));
        }
        if (ImGui.BeginPopup(Translator.LocalizeUI("OpenPathDialog##editorwindow")))
        {
            _fileDialogManager.SaveFolderDialog(Translator.LocalizeUI("Pick location"), "QuestReborn", (isOk, folder) =>
            {
                if (isOk)
                {
                    if (!folder.Contains("Program Files") && !folder.Contains("FINAL FANTASY XIV - A Realm Reborn"))
                    {
                        Directory.CreateDirectory(folder);
                        Plugin.Configuration.CacheFolder = folder;
                        Plugin.Configuration.CacheFolder = folder;
                        Translator.LoadCache(Path.Combine(Plugin.Configuration.CacheFolder, "languageCache.json"));
                        Plugin.Configuration.Save();
                        AppearanceAccessUtils.CacheLocation = Plugin.Configuration.CacheFolder;
                    }
                }
            }, null, true);
            ImGui.EndPopup();
        }
        int currentSelection = (int)Plugin.Configuration.Language;
        if (ImGui.Combo(Translator.LocalizeUI("Language"), ref currentSelection, Translator.LanguageStrings, Translator.LanguageStrings.Length))
        {
            Plugin.Configuration.Language = (LanguageEnum)currentSelection;
            Translator.UiLanguage = Plugin.Configuration.Language;
            Plugin.Configuration.Save();
        }
    }

    private void DrawUsers()
    {
    }

    private void DrawServers()
    {
        AppearanceCommunicationManager.CheckForValidUserData();
        ImGui.InputText("IP Address", ref _currentIpEntry);
        ImGui.InputText("Auth Key", ref _currentAuthenticationKey);
        if (ImGui.Button("Add Server"))
        {
            if (!string.IsNullOrEmpty(_currentIpEntry))
            {
                Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId].Add(new ServerEntry()
                {
                    IpAddress = _currentIpEntry,
                    AuthKey = _currentAuthenticationKey,
                    UtcJoinTime = DateTime.UtcNow.Ticks
                });
                Plugin.Configuration.Save();
                AppearanceCommunicationManager.StartSSEListener(_currentIpEntry, Plugin.CurrentCharacterId);
                AppearanceCommunicationManager.UploadAppearance(_currentIpEntry, _currentAuthenticationKey);
                _currentIpEntry = "";
                _currentAuthenticationKey = "";
            }
        }
        var stringList = Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId].Select(entry => entry.ToString()).ToList();
        ImGui.ListBox(new ImU8String("Servers"), ref _selectedItem, stringList, 10);
        if (ImGui.Button("Upload Appearance Data"))
        {
            AppearanceCommunicationManager.RefreshAppearanceOnServers();
        }
        ImGui.SameLine();
        if (ImGui.Button("Remove Selected Server"))
        {
            Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId].RemoveAt(_selectedItem);
        }
    }
}
