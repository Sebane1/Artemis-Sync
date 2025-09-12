using Dalamud.Configuration;
using LanguageConversionProxy;
using System;
using System.Collections.Generic;

namespace ArtemisSync;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public Dictionary<string, List<ServerEntry>> ServerEntries { get => _serverEntries; set => _serverEntries = value; }
    public string? CacheFolder { get => _cacheFolder; set => _cacheFolder = value; }
    public LanguageEnum Language { get => _language; set => _language = value; }

    Dictionary<string, List<ServerEntry>> _serverEntries = new Dictionary<string, List<ServerEntry>>();
    private string? _cacheFolder;
    private LanguageEnum _language;

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
