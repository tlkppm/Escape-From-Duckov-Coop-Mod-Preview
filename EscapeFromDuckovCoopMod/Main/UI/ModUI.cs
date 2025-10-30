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

﻿using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace EscapeFromDuckovCoopMod
{
    public class ModUI : MonoBehaviour
    {
        public static ModUI Instance;

        public bool showUI = true;
        public bool showPlayerStatusWindow;
        public KeyCode toggleWindowKey = KeyCode.P;

        private readonly List<string> _hostList = new List<string>();
        private readonly HashSet<string> _hostSet = new HashSet<string>();
        public readonly KeyCode readyKey = KeyCode.J;
        private string _manualIP = "127.0.0.1";
        private string _manualPort = "9050";
        private int _port = 9050;
        private string _status = "未连接";
        private Rect mainWindowRect = new Rect(10, 10, 400, 700);
        private Vector2 playerStatusScrollPos = Vector2.zero;
        private Rect playerStatusWindowRect = new Rect(420, 10, 300, 400);

        private NetService Service => NetService.Instance;
        private bool IsServer => Service != null && Service.IsServer;
        private NetManager netManager => Service?.netManager;
        private NetDataWriter writer => Service?.writer;
        private NetPeer connectedPeer => Service?.connectedPeer;
        private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
        private bool networkStarted => Service != null && Service.networkStarted;

        private string manualIP
        {
            get => Service?.manualIP ?? _manualIP;
            set
            {
                _manualIP = value;
                if (Service != null) Service.manualIP = value;
            }
        }

        private string manualPort
        {
            get => Service?.manualPort ?? _manualPort;
            set
            {
                _manualPort = value;
                if (Service != null) Service.manualPort = value;
            }
        }

        private string status
        {
            get => Service?.status ?? _status;
            set
            {
                _status = value;
                if (Service != null) Service.status = value;
            }
        }

        private int port => Service?.port ?? _port;

        private List<string> hostList => Service?.hostList ?? _hostList;
        private HashSet<string> hostSet => Service?.hostSet ?? _hostSet;

        private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
        private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
        private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;
        private Dictionary<string, PlayerStatus> clientPlayerStatuses => Service?.clientPlayerStatuses;

        private void OnGUI()
        {
            if (showUI)
            {
                mainWindowRect = GUI.Window(94120, mainWindowRect, DrawMainWindow, "联机Mod控制面板");

                if (showPlayerStatusWindow) playerStatusWindowRect = GUI.Window(94121, playerStatusWindowRect, DrawPlayerStatusWindow, "玩家状态");
            }

            if (SceneNet.Instance.sceneVoteActive)
            {
                var h = 220f;
                var area = new Rect(10, Screen.height * 0.5f - h * 0.5f, 320, h);
                GUILayout.BeginArea(area, GUI.skin.box);
                GUILayout.Label($"地图投票 / 准备  [{SceneInfoCollection.GetSceneInfo(SceneNet.Instance.sceneTargetId).DisplayName}]");
                GUILayout.Label($"按 {readyKey} 切换准备（当前：{(SceneNet.Instance.localReady ? "已准备" : "未准备")}）");

                GUILayout.Space(8);
                GUILayout.Label("玩家准备状态：");
                foreach (var pid in SceneNet.Instance.sceneParticipantIds)
                {
                    var r = false;
                    SceneNet.Instance.sceneReady.TryGetValue(pid, out r);
                    GUILayout.Label($"• {pid}  —— {(r ? "✅ 就绪" : "⌛ 未就绪")}");
                }

                GUILayout.EndArea();
            }

            if (Spectator.Instance._spectatorActive)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerCenter,
                    fontSize = 18
                };
                style.normal.textColor = Color.white;

                //string who = "";
                try
                {
                    // var cmc = (_spectateIdx >= 0 && _spectateIdx < _spectateList.Count) ? _spectateList[_spectateIdx] : null;
                    // who = cmc ? (cmc.name ?? "队友") : "队友";
                }
                catch
                {
                }

                GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 30),
                    "观战模式：左键 ▶ 下一个 | 右键 ◀ 上一个  | 正在观战", style);
            }
        }


        public void Init()
        {
            Instance = this;
            var svc = Service;
            if (svc != null)
            {
                _manualIP = svc.manualIP;
                _manualPort = svc.manualPort;
                _status = svc.status;
                _port = svc.port;
                _hostList.Clear();
                _hostSet.Clear();
                _hostList.AddRange(svc.hostList);
                foreach (var host in svc.hostSet) _hostSet.Add(host);
            }
        }

        private void DrawMainWindow(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"当前模式: {(IsServer ? "服务器" : "客户端")}");

            if (GUILayout.Button("切换到" + (IsServer ? "客户端" : "服务器") + "模式"))
            {
                var target = !IsServer;
                NetService.Instance.StartNetwork(target);
            }

            GUILayout.Space(10);

            if (!IsServer)
            {
                GUILayout.Label("🔍 局域网主机列表");

                if (hostList.Count == 0)
                    GUILayout.Label("（等待广播回应，暂无主机）");
                else
                    foreach (var host in hostList)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("连接", GUILayout.Width(60)))
                        {
                            var parts = host.Split(':');
                            if (parts.Length == 2 && int.TryParse(parts[1], out var p))
                            {
                                if (netManager == null || !netManager.IsRunning || IsServer || !networkStarted) NetService.Instance.StartNetwork(false);

                                NetService.Instance.ConnectToHost(parts[0], p);
                            }
                        }

                        GUILayout.Label(host);
                        GUILayout.EndHorizontal();
                    }

                GUILayout.Space(20);
                GUILayout.Label("手动输入 IP 和端口连接:");
                GUILayout.BeginHorizontal();
                GUILayout.Label("IP:", GUILayout.Width(40));
                manualIP = GUILayout.TextField(manualIP, GUILayout.Width(150));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("端口:", GUILayout.Width(40));
                manualPort = GUILayout.TextField(manualPort, GUILayout.Width(150));
                GUILayout.EndHorizontal();
                if (GUILayout.Button("手动连接"))
                {
                    if (int.TryParse(manualPort, out var p))
                    {
                        if (netManager == null || !netManager.IsRunning || IsServer || !networkStarted) NetService.Instance.StartNetwork(false);

                        NetService.Instance.ConnectToHost(manualIP, p);
                    }
                    else
                    {
                        status = "端口格式错误";
                    }
                }

                GUILayout.Space(20);
                GUILayout.Label("状态: " + status);
            }
            else
            {
                GUILayout.Label($"服务器监听端口: {port}");
                GUILayout.Label($"当前连接数: {netManager?.ConnectedPeerList.Count ?? 0}");
            }

            GUILayout.Space(10);
            showPlayerStatusWindow = GUILayout.Toggle(showPlayerStatusWindow, $"显示玩家状态窗口 (切换键: {toggleWindowKey})");

            if (GUILayout.Button("[Debug] 打印出该地图的所有lootbox"))
                foreach (var i in LevelManager.LootBoxInventories)
                    try
                    {
                        Debug.Log($"Name {i.Value.name}" + $" DisplayNameKey {i.Value.DisplayNameKey}" + $" Key {i.Key}");
                    }
                    catch
                    {
                    }
            //if (GUILayout.Button("[Debug] 所有maplist"))
            //{
            //    const string keyword = "MapSelectionEntry";

            //    var trs = Object.FindObjectsByType<Transform>(
            //        FindObjectsInactive.Include, FindObjectsSortMode.None);

            //    var gos = trs
            //        .Select(t => t.gameObject)
            //        .Where(go => go.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            //        .ToList();

            //    foreach (var i in gos)
            //    {
            //        try
            //        {
            //            var map = i.GetComponentInChildren<MapSelectionEntry>();
            //            if (map != null)
            //            {
            //                Debug.Log($"BeaconIndex {map.BeaconIndex}" + $" SceneID {map.SceneID}" + $" name {map.name}");
            //            }
            //        }
            //        catch { continue; }
            //    }

            //}


            GUILayout.EndVertical();
            GUI.DragWindow();
        }


        private void DrawPlayerStatusWindow(int windowID)
        {
            if (GUI.Button(new Rect(playerStatusWindowRect.width - 25, 5, 20, 20), "×")) showPlayerStatusWindow = false;

            playerStatusScrollPos = GUILayout.BeginScrollView(playerStatusScrollPos, GUILayout.ExpandWidth(true));

            if (localPlayerStatus != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"ID: {localPlayerStatus.EndPoint}", GUILayout.Width(180));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label($"名称: {localPlayerStatus.PlayerName}", GUILayout.Width(180));
                GUILayout.Label($"延迟: {localPlayerStatus.Latency}ms", GUILayout.Width(100));
                GUILayout.Label($"游戏中: {(localPlayerStatus.IsInGame ? "是" : "否")}");
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }

            if (IsServer)
                foreach (var kvp in playerStatuses)
                {
                    var st = kvp.Value;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"ID: {st.EndPoint}", GUILayout.Width(180));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"名称: {st.PlayerName}", GUILayout.Width(180));
                    GUILayout.Label($"延迟: {st.Latency}ms", GUILayout.Width(100));
                    GUILayout.Label($"游戏中: {(st.IsInGame ? "是" : "否")}");
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }
            else
                foreach (var kvp in clientPlayerStatuses)
                {
                    var st = kvp.Value;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"ID: {st.EndPoint}", GUILayout.Width(180));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"名称: {st.PlayerName}", GUILayout.Width(180));
                    GUILayout.Label($"延迟: {st.Latency}ms", GUILayout.Width(100));
                    GUILayout.Label($"游戏中: {(st.IsInGame ? "是" : "否")}");
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}