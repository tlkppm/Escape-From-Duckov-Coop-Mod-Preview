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

﻿using Cysharp.Threading.Tasks;
using Duckov.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EscapeFromDuckovCoopMod
{
    [HarmonyPatch(typeof(LevelManager), "StartInit")]
    static class Patch_Level_StartInit_Gate
    {
        static bool Prefix(LevelManager __instance, SceneLoadingContext context)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null) return true;
            if (mod.IsServer) return true;

            bool needGate = SceneNet.Instance.sceneVoteActive || (mod.networkStarted && !mod.IsServer);
            if (!needGate) return true;

            RunAsync(__instance, context).Forget();
            return false;
        }

        static async UniTaskVoid RunAsync(LevelManager self, SceneLoadingContext ctx)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null) return;

            await SceneNet.Instance.Client_SceneGateAsync();

            try
            {
                var m = AccessTools.Method(typeof(LevelManager), "InitLevel", new Type[] { typeof(SceneLoadingContext) });
                if (m != null) m.Invoke(self, new object[] { ctx });
            }
            catch (Exception e)
            {
                Debug.LogError("[SCENE] StartInit gate -> InitLevel failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(MapSelectionEntry), "OnPointerClick")]
    static class Patch_Mapen_OnPointerClick
    {
        static bool Prefix(MapSelectionEntry __instance, PointerEventData eventData)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted) return true;
            if (!mod.IsServer) return false;
            SceneNet.Instance.IsMapSelectionEntry = true;
            SceneNet.Instance.Host_BeginSceneVote_Simple(__instance.SceneID, "", false, false, false, "OnPointerClick");
            return false;
        }
    }






}
