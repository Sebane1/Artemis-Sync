using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using McdfDataImporter;
using RelayCommonData;
using RelayUploadProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArtemisSync
{
    public class AppearanceCommunicationManager
    {
        private static bool alreadyUploadingAppearance;
        private static ConcurrentDictionary<string, long> alreadySyncedUsers = new ConcurrentDictionary<string, long>();


        public void Initialize()
        {

        }

        public static void CheckForValidUserData()
        {
            if (Plugin.Configuration.ServerEntries == null)
            {
                Plugin.Configuration.ServerEntries = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ServerEntry>>();
            }
            if (!Plugin.Configuration.ServerEntries.ContainsKey(Plugin.CurrentCharacterId))
            {
                Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId] = new System.Collections.Generic.List<ServerEntry>();
            }
        }

        /// <summary>
        /// Uploads appearance data on all connected servers
        /// </summary>
        public static void RefreshAppearanceOnServers()
        {
            foreach (var item in Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId])
            {
                UploadAppearance(item.IpAddress, item.AuthKey);
            }
        }

        /// <summary>
        /// Uploads appearance data to the specified server
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="authenticationKey"></param>
        public static void UploadAppearance(string ipAddress, string authenticationKey)
        {
            if (!alreadyUploadingAppearance)
            {
                alreadyUploadingAppearance = true;
                Task.Run(async () =>
                {
                    string filePath = Path.Combine(AppearanceAccessUtils.CacheLocation, Plugin.CurrentCharacterId + ".hex");
                    AppearanceAccessUtils.AppearanceManager.CreateMCDF(filePath);
                    string appearanceFile = "_Appearance";
                    string passkey = Hashing.SHA512Hash(appearanceFile + Plugin.CurrentCharacterId + ipAddress);
                    await ClientManager.PutPersistedFile(ipAddress, Plugin.CurrentCharacterId, authenticationKey, appearanceFile, filePath, passkey);
                    alreadyUploadingAppearance = false;
                });
            }
        }

        public static async void SubscribeToAppearanceEventOnServers(string targetSessionId)
        {
            foreach (var item in Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId])
            {
                await SseClient.SubscribeAsync(item.IpAddress, Plugin.CurrentCharacterId, item.AuthKey, targetSessionId, "_Appearance");
            }
        }

        public static async void SubscribeToAppearanceEventOnServer(string ipAddress, string authKey, string targetSessionId)
        {
            await SseClient.SubscribeAsync(ipAddress, Plugin.CurrentCharacterId, ipAddress, targetSessionId, "_Appearance");
        }

        /// <summary>
        /// Uploads appearance data on all connected servers
        /// </summary>
        public static void StartSSEListeners()
        {
            foreach (var item in Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId])
            {
                StartSSEListener(item.IpAddress, Plugin.CurrentCharacterId);
            }
        }

        public static async void StartSSEListener(string ipAddress, string sessionId)
        {
            await SseClient.ListenForFileChanges(ipAddress, sessionId);
        }

        /// <summary>
        /// Checks servers until one of them gives us appearance data for the player we've asked for.
        /// </summary>
        /// <param name="gameObject"></param>
        public static async void GetPlayerAppearanceOnServers(IGameObject gameObject)
        {
            foreach (var item in Plugin.Configuration.ServerEntries[Plugin.CurrentCharacterId])
            {
                if (await DownloadAppearance(item.IpAddress, item.AuthKey, gameObject))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Downloads appearance data from the specified server
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="authenticationKey"></param>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static async Task<bool> DownloadAppearance(string ipAddress, string authenticationKey, IGameObject gameObject)
        {
            try
            {
                string targetSessionId = Hashing.SHA512Hash(gameObject.Name.TextValue);
                string appearanceFile = "_Appearance";
                string passkey = Hashing.SHA512Hash(appearanceFile + targetSessionId + ipAddress);
                bool gotUserData = alreadySyncedUsers.ContainsKey(targetSessionId);
                long lastTimeChanged = await ClientManager.CheckLastTimePersistedFileChanged(ipAddress, Plugin.CurrentCharacterId, authenticationKey, targetSessionId, appearanceFile);
                bool fileChanged = false;
                if (gotUserData)
                {
                    fileChanged = lastTimeChanged > alreadySyncedUsers[targetSessionId];
                }
                if (!gotUserData || fileChanged)
                {
                    alreadySyncedUsers[targetSessionId] = lastTimeChanged;
                    string fileData = await ClientManager.GetPersistedFile(ipAddress, Plugin.CurrentCharacterId, authenticationKey, targetSessionId, appearanceFile, AppearanceAccessUtils.CacheLocation, passkey);
                    await Plugin.Framework.Run(async () =>
                     {
                         AppearanceAccessUtils.AppearanceManager.LoadAppearance(fileData, gameObject);
                     });
                }
                return true;
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Warning(e, e.Message);
                return false;
            }
        }
    }
}
