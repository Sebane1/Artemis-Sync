using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace ArtemisSync;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public Dictionary<string, List<ServerEntry>> ServerEntries { get => _serverEntries; set => _serverEntries = value; }

    Dictionary<string, List<ServerEntry>> _serverEntries = new Dictionary<string, List<ServerEntry>>();

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
