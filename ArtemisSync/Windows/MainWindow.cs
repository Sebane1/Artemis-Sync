using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using McdfDataImporter;
using RelayUploadProtocol;

namespace ArtemisSync.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private Plugin Plugin;
    private int _selectedItem;
    private string _currentIpEntry;
    private string _currentAuthenticationKey;

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

        GoatImagePath = goatImagePath;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (Plugin.Configuration.ServerEntries == null)
        {
            Plugin.Configuration.ServerEntries = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ServerEntry>>();
        }
        if (!Plugin.Configuration.ServerEntries.ContainsKey(Plugin.CurrentCharacterId))
        {
            Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId] = new System.Collections.Generic.List<ServerEntry>();
        }
        ImGui.InputText("IP Address", ref _currentIpEntry);
        ImGui.InputText("Auth Key", ref _currentAuthenticationKey);
        if (ImGui.Button("Add Server"))
        {
            Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId].Add(new ServerEntry()
            {
                IpAddress = _currentIpEntry,
                AuthKey = _currentAuthenticationKey,
            });
            Plugin.Configuration.Save();
            string filePath = Path.Combine(AppearanceAccessUtils.CacheLocation, Plugin.CurrentCharacterId + ".hex");
            AppearanceAccessUtils.AppearanceManager.CreateMCDF(filePath);
            ClientManager.PutPersistedFile(Plugin.CurrentCharacterId, _currentAuthenticationKey, Plugin.CurrentCharacterId + "_Appearance", filePath);
        }
        var stringList = Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId].Select(entry => entry.ToString()).ToList();
        ImGui.ListBox(new ImU8String("servers"), ref _selectedItem, stringList, 10);
        if (ImGui.Button("Remove Selected Server"))
        {
            Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId].RemoveAt(_selectedItem);
        }
    }
}
