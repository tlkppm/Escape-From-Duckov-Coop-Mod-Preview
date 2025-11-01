// Escape-From-Duckov-Coop-Mod-Preview
// Copyright (C) 2025  Mr.sans and InitLoader's team
//
// This program is not a free software.
// It's distributed under a license based on AGPL-3.0,
// with strict additional restrictions:
//  YOU MUST NOT use this software for commercial purposes.
//  YOU MUST NOT use this software to run a headless game server.
//  YOU MUST include a conspicuous notice of attribution to
//  Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview as the original author.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SodaCraft.Localizations;

namespace EscapeFromDuckovCoopMod
{
    public static class CoopLocalization
    {
        private static Dictionary<string, string> currentTranslations = new Dictionary<string, string>();
        private static string currentLanguageCode = "en-US";
        private static bool isInitialized = false;
        private static SystemLanguage lastSystemLanguage = SystemLanguage.Unknown;

        public static void Initialize()
        {
            if (isInitialized) return;

            DetectAndLoadLanguage();
            isInitialized = true;

            Debug.Log($"[CoopLocalization] Initialized with language: {currentLanguageCode}");
        }

        public static void CheckLanguageChange()
        {
            if (!isInitialized) return;

            var currentSystemLang = LocalizationManager.CurrentLanguage;
            if (currentSystemLang != lastSystemLanguage)
            {
                Debug.Log($"[CoopLocalization] Language changed from {lastSystemLanguage} to {currentSystemLang}, reloading translations...");
                DetectAndLoadLanguage();
            }
        }

        private static void DetectAndLoadLanguage()
        {
            var systemLang = LocalizationManager.CurrentLanguage;
            lastSystemLanguage = systemLang;

            switch (systemLang)
            {
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                case SystemLanguage.Chinese:
                    currentLanguageCode = "zh-CN";
                    break;
                case SystemLanguage.Korean:
                    currentLanguageCode = "ko-KR";
                    break;
                case SystemLanguage.Japanese:
                    currentLanguageCode = "ja-JP";
                    break;
                case SystemLanguage.Portuguese:
                    currentLanguageCode = "pt-BR";
                    break;
                case SystemLanguage.English:
                default:
                    currentLanguageCode = "en-US";
                    break;
            }

            LoadTranslations(currentLanguageCode);
        }

        private static void LoadTranslations(string languageCode)
        {
            currentTranslations.Clear();

            try
            {
                string modPath = Path.GetDirectoryName(typeof(CoopLocalization).Assembly.Location);
                string localizationPath = Path.Combine(modPath, "Localization", $"{languageCode}.json");

                if (!File.Exists(localizationPath))
                {
                    Debug.LogWarning($"[CoopLocalization] Translation file not found: {localizationPath}, using fallback translations");
                    LoadFallbackTranslations();
                    return;
                }

                string json = File.ReadAllText(localizationPath);

                ParseJsonTranslations(json);

                if (currentTranslations.Count > 0)
                {
                    Debug.Log($"[CoopLocalization] Loaded {currentTranslations.Count} translations from {localizationPath}");
                }
                else
                {
                    Debug.LogWarning($"[CoopLocalization] Failed to parse translation file, using fallback");
                    LoadFallbackTranslations();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoopLocalization] Error loading translations: {e.Message}");
                LoadFallbackTranslations();
            }
        }

        private static void ParseJsonTranslations(string json)
        {
            try
            {
                int startIndex = json.IndexOf("\"translations\"");
                if (startIndex == -1) return;

                int arrayStart = json.IndexOf('[', startIndex);
                if (arrayStart == -1) return;

                int arrayEnd = json.LastIndexOf(']');
                if (arrayEnd == -1) return;

                string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);

                int braceCount = 0;
                int entryStart = -1;

                for (int i = 0; i < arrayContent.Length; i++)
                {
                    char c = arrayContent[i];

                    if (c == '{')
                    {
                        if (braceCount == 0) entryStart = i;
                        braceCount++;
                    }
                    else if (c == '}')
                    {
                        braceCount--;
                        if (braceCount == 0 && entryStart != -1)
                        {
                            string entry = arrayContent.Substring(entryStart, i - entryStart + 1);
                            ParseSingleEntry(entry);
                            entryStart = -1;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoopLocalization] JSON parsing error: {e.Message}");
            }
        }

        private static void ParseSingleEntry(string entry)
        {
            try
            {
                string key = null;
                string value = null;

                int keyIndex = entry.IndexOf("\"key\"");
                if (keyIndex != -1)
                {
                    int keyValueStart = entry.IndexOf(':', keyIndex);
                    if (keyValueStart != -1)
                    {
                        int keyQuoteStart = entry.IndexOf('\"', keyValueStart);
                        int keyQuoteEnd = entry.IndexOf('\"', keyQuoteStart + 1);
                        if (keyQuoteStart != -1 && keyQuoteEnd != -1)
                        {
                            key = entry.Substring(keyQuoteStart + 1, keyQuoteEnd - keyQuoteStart - 1);
                        }
                    }
                }

                int valueIndex = entry.IndexOf("\"value\"");
                if (valueIndex != -1)
                {
                    int valueValueStart = entry.IndexOf(':', valueIndex);
                    if (valueValueStart != -1)
                    {
                        int valueQuoteStart = entry.IndexOf('\"', valueValueStart);
                        int valueQuoteEnd = entry.IndexOf('\"', valueQuoteStart + 1);
                        if (valueQuoteStart != -1 && valueQuoteEnd != -1)
                        {
                            value = entry.Substring(valueQuoteStart + 1, valueQuoteEnd - valueQuoteStart - 1);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    currentTranslations[key] = value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoopLocalization] Entry parsing error: {e.Message}");
            }
        }

        private static void LoadFallbackTranslations()
        {
            currentTranslations.Clear();
            currentTranslations["ui.window.title"] = "Co-op Mod Control Panel";
            currentTranslations["ui.window.playerStatus"] = "Player Status";
            currentTranslations["ui.mode.current"] = "Current Mode";
            currentTranslations["ui.mode.server"] = "Server";
            currentTranslations["ui.mode.client"] = "Client";
            currentTranslations["ui.mode.switchTo"] = "Switch to {0} Mode";
            currentTranslations["ui.hostList.title"] = "LAN Host List";
            currentTranslations["ui.hostList.empty"] = "(Waiting for broadcast, no hosts found)";
            currentTranslations["ui.hostList.connect"] = "Connect";
            currentTranslations["ui.manualConnect.title"] = "Manual IP and Port Connection:";
            currentTranslations["ui.manualConnect.ip"] = "IP:";
            currentTranslations["ui.manualConnect.port"] = "Port:";
            currentTranslations["ui.manualConnect.button"] = "Manual Connect";
            currentTranslations["ui.manualConnect.portError"] = "Invalid port format";
            currentTranslations["ui.status.label"] = "Status:";
            currentTranslations["ui.status.notConnected"] = "Not Connected";
            currentTranslations["ui.status.connecting"] = "Connecting...";
            currentTranslations["ui.status.connected"] = "Connected";
            currentTranslations["ui.server.listenPort"] = "Server Listening Port:";
            currentTranslations["ui.server.connections"] = "Current Connections:";
            currentTranslations["ui.playerStatus.toggle"] = "Show Player Status Window (Toggle key: {0})";
            currentTranslations["ui.playerStatus.id"] = "ID:";
            currentTranslations["ui.playerStatus.name"] = "Name:";
            currentTranslations["ui.playerStatus.latency"] = "Latency:";
            currentTranslations["ui.playerStatus.inGame"] = "In Game:";
            currentTranslations["ui.playerStatus.yes"] = "Yes";
            currentTranslations["ui.playerStatus.no"] = "No";
            currentTranslations["ui.debug.printLootBoxes"] = "[Debug] Print all lootboxes in this map";
            currentTranslations["ui.vote.mapVote"] = "Map Vote / Ready  [{0}]";
            currentTranslations["ui.vote.pressKey"] = "Press {0} to toggle ready (Current: {1})";
            currentTranslations["ui.vote.ready"] = "Ready";
            currentTranslations["ui.vote.notReady"] = "Not Ready";
            currentTranslations["ui.vote.playerReadyStatus"] = "Player Ready Status:";
            currentTranslations["ui.vote.readyIcon"] = "✅ Ready";
            currentTranslations["ui.vote.notReadyIcon"] = "⌛ Not Ready";
            currentTranslations["ui.spectator.mode"] = "Spectator Mode: LMB ▶ Next | RMB ◀ Previous | Spectating";
            currentTranslations["scene.waitingForHost"] = "[Coop] Waiting for host to finish loading… (Auto-enter after 100s if delayed)";
            currentTranslations["scene.hostReady"] = "Host ready, entering…";
            currentTranslations["net.connectionSuccess"] = "Connected successfully: {0}";
            currentTranslations["net.connectedTo"] = "Connected to {0}";
            currentTranslations["net.disconnected"] = "Disconnected: {0}, Reason: {1}";
            currentTranslations["net.connectionLost"] = "Connection Lost";
            currentTranslations["net.networkError"] = "Network error: {0} from {1}";
            currentTranslations["net.hostDiscovered"] = "Host discovered: {0}";
            currentTranslations["net.serverStarted"] = "Server started, listening on port {0}";
            currentTranslations["net.serverStartFailed"] = "Server start failed, check if port is already in use";
            currentTranslations["net.clientStarted"] = "Client started";
            currentTranslations["net.clientStartFailed"] = "Client start failed";
            currentTranslations["net.networkStarted"] = "Network started";
            currentTranslations["net.networkStopped"] = "Network stopped";
            currentTranslations["net.ipEmpty"] = "IP is empty";
            currentTranslations["net.invalidPort"] = "Invalid port";
            currentTranslations["net.serverModeCannotConnect"] = "Server mode cannot connect to other hosts";
            currentTranslations["net.alreadyConnecting"] = "Already connecting.";
            currentTranslations["net.clientNetworkStartFailed"] = "Failed to start client network: {0}";
            currentTranslations["net.clientNetworkStartFailedStatus"] = "Client network start failed";
            currentTranslations["net.clientNotStarted"] = "Client not started";
            currentTranslations["net.connectingTo"] = "Connecting to: {0}:{1}";
            currentTranslations["net.connectionFailedLog"] = "Failed to connect to host: {0}";
            currentTranslations["net.connectionFailed"] = "Connection failed";
        }

        public static string Get(string key, params object[] args)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (currentTranslations.TryGetValue(key, out string value))
            {
                if (args != null && args.Length > 0)
                {
                    try
                    {
                        return string.Format(value, args);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CoopLocalization] Format error for key '{key}': {e.Message}");
                        return value;
                    }
                }
                return value;
            }

            Debug.LogWarning($"[CoopLocalization] Missing translation for key: {key}");
            return $"[{key}]";
        }

        public static void SetLanguage(string languageCode)
        {
            if (currentLanguageCode == languageCode) return;

            currentLanguageCode = languageCode;
            LoadTranslations(languageCode);
            Debug.Log($"[CoopLocalization] Language changed to: {languageCode}");
        }

        public static string GetCurrentLanguage()
        {
            return currentLanguageCode;
        }
    }
}

