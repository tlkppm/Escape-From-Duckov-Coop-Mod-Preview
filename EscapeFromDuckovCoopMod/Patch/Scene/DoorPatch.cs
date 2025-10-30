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

﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EscapeFromDuckovCoopMod
{
    // ========= 客户端：拦截 Door.Open -> 发送请求给主机 =========
    [HarmonyPatch(typeof(Door), nameof(Door.Open))]
    static class Patch_Door_Open_ClientToServer
    {
        static bool Prefix(Door __instance)
        {
            var m = ModBehaviourF.Instance;
            if (m == null || !m.networkStarted) return true;
            if (m.IsServer) return true;                 // 主机放行
            if (Door_._applyingDoor) return true;  // 正在应用网络下发，放行

            COOPManager.Door.Client_RequestDoorSetState(__instance, closed: false);
            return false; // 客户端不直接开门
        }
    }

    // ========= 客户端：拦截 Door.Close -> 发送请求给主机 =========
    [HarmonyPatch(typeof(Door), nameof(Door.Close))]
    static class Patch_Door_Close_ClientToServer
    {
        static bool Prefix(Door __instance)
        {
            var m = ModBehaviourF.Instance;
            if (m == null || !m.networkStarted) return true;
            if (m.IsServer) return true;
            if (Door_._applyingDoor) return true;

            COOPManager.Door.Client_RequestDoorSetState(__instance, closed: true);
            return false;
        }
    }

    // ========= 客户端：拦截 Door.Switch -> 发送请求给主机 =========
    [HarmonyPatch(typeof(Door), nameof(Door.Switch))]
    static class Patch_Door_Switch_ClientToServer
    {
        static bool Prefix(Door __instance)
        {
            var m = ModBehaviourF.Instance;
            if (m == null || !m.networkStarted) return true;
            if (m.IsServer) return true;
            if (Door_._applyingDoor) return true;

            bool isOpen = false;
            try { isOpen = __instance.IsOpen; } catch { }
            COOPManager.Door.Client_RequestDoorSetState(__instance, closed: isOpen /* open->关，close->开 */);
            return false;
        }
    }

    // ========= 主机：任何地方调用 SetClosed 都广播给所有客户端 =========
    [HarmonyPatch(typeof(Door), "SetClosed")]
    static class Patch_Door_SetClosed_BroadcastOnServer
    {
        static void Postfix(Door __instance, bool _closed)
        {
            var m = ModBehaviourF.Instance;
            if (m == null || !m.networkStarted || !m.IsServer) return;

            int key = 0;
            try { key = (int)AccessTools.Field(typeof(Door), "doorClosedDataKeyCached").GetValue(__instance); } catch { }
            if (key == 0) key = COOPManager.Door.ComputeDoorKey(__instance.transform);
            if (key == 0) return;

            COOPManager.Door.Server_BroadcastDoorState(key, _closed);
        }
    }




}
