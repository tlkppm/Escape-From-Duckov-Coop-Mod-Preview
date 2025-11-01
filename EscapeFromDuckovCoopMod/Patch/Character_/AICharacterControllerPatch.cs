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
using NodeCanvas.StateMachines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeFromDuckovCoopMod
{
    [HarmonyPatch(typeof(AICharacterController), "Init")]
    static class Patch_AI_Init
    {
        static void Postfix(AICharacterController __instance, CharacterMainControl _characterMainControl)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted) return;
            if (!AITool.IsRealAI(_characterMainControl)) return;

            var cmc = _characterMainControl;
            if (COOPManager.AIHandle.freezeAI) AITool.TryFreezeAI(cmc);

            // 1) 取/补标签
            var tag = cmc.GetComponent<NetAiTag>() ?? cmc.gameObject.AddComponent<NetAiTag>();

            //  客户端不在这里分配/登记 aiId，避免和主机分配顺序打架
            if (!mod.IsServer)
            {
                AITool.MarkAiSceneReady(); // 让客户端开始吞变换队列
                return;
            }

            // 2) 主机端：若还没 aiId，就按 rootId + 序号 分配
            if (tag.aiId == 0)
            {
                int rootId = 0;
                var root = cmc.GetComponentInParent<CharacterSpawnerRoot>();
                rootId = (root && root.SpawnerGuid != 0)
                    ? root.SpawnerGuid
                    : AITool.StableHash(AITool.TransformPath(root ? root.transform : cmc.transform));
                int serial = AITool.NextAiSerial(rootId);
                tag.aiId = AITool.DeriveSeed(rootId, serial);
            }

            // 3) 主机登记（客户端会在收到主机快照后再登记）
            COOPManager.AIHandle.RegisterAi(tag.aiId, cmc);
            AITool.MarkAiSceneReady();
        }
    }

    [HarmonyPatch(typeof(AICharacterController), "Update")]
    static class Patch_AICC_ZeroForceTraceMain
    {
        static void Prefix(AICharacterController __instance)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted || !mod.IsServer) return;

            // 直接把强追距离清零，避免原逻辑把目标强行锁到 CharacterMainControl.Main
            __instance.forceTracePlayerDistance = 0f;
        }
    }


    [HarmonyPatch(typeof(FSM), "OnGraphUpdate")]
    static class Patch_FSM_OnGraphUpdate_MainRedirect
    {
        static void Prefix(FSM __instance)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted || !mod.IsServer) return;


            Component agent = null;
            try
            {
                agent = (Component)AccessTools.Property(typeof(NodeCanvas.Framework.Graph), "agent").GetValue(__instance, null);
            }
            catch { }

            if (!agent) return;

            var aiCmc = agent.GetComponentInParent<CharacterMainControl>();
            if (!aiCmc) return;
            if (!AITool.IsRealAI(aiCmc)) return; // 只对真正的AI生效，避免影响玩家自己的图

            // 计算这只 AI 同场景下最近、且活着、且敌对 的玩家（主机 + 各远端）
            var scene = agent.gameObject.scene;
            var best = FindNearestEnemyPlayer(mod, aiCmc, scene, aiCmc.transform.position);
            if (best != null)
                NcMainRedirector.Set(best);
        }

        static void Postfix()
        {
            // 清理现场，避免影响其它对象
            NcMainRedirector.Clear();
        }

        static CharacterMainControl FindNearestEnemyPlayer(ModBehaviourF mod, CharacterMainControl ai, Scene scene, Vector3 aiPos)
        {
            CharacterMainControl best = null;
            float bestD2 = float.MaxValue;

            void Try(CharacterMainControl cmc)
            {
                if (!cmc) return;
                if (!cmc.gameObject.activeInHierarchy) return;
                if (cmc.gameObject.scene != scene) return;
                if (cmc.Team == ai.Team) return;
                if (!LocalPlayerManager.Instance.IsAlive(cmc)) return;

                float d2 = (cmc.transform.position - aiPos).sqrMagnitude;
                if (d2 < bestD2) { bestD2 = d2; best = cmc; }
            }

            // 主机本地玩家
            Try(CharacterMainControl.Main);

            // 服务器维护的各远端玩家克隆
            foreach (var kv in mod.remoteCharacters)
            {
                var go = kv.Value; if (!go) continue;
                var cmc = go.GetComponent<CharacterMainControl>() ?? go.GetComponentInChildren<CharacterMainControl>(true);
                Try(cmc);
            }

            return best;
        }
    }

    [HarmonyPatch(typeof(AICharacterController), "Update")]
    static class Patch_AIC_Update_PickNearestPlayer
    {
        static void Postfix(AICharacterController __instance)
        {
            var mod = ModBehaviourF.Instance;

            if (mod == null || !mod.networkStarted || !mod.IsServer || __instance == null) return;

            var aiCmc = __instance.CharacterMainControl;
            if (!aiCmc) return;

            //商人判断（这就是让商人不主动攻击的真真真真的地方）:)))
            if (__instance.name == "AIController_Merchant_Myst(Clone)") { return; }

            CharacterMainControl best = null;
            float bestD2 = float.MaxValue;

            void Consider(CharacterMainControl cmc)
            {
                if (!cmc) return;

                if (cmc.Team == aiCmc.Team) return;

                // 存活判定
                var h = cmc.Health;
                if (!h) return;
                float hp = 1f;
                try { hp = h.CurrentHealth; } catch { }
                if (hp <= 0f) return;

                // 视距/视角
                Vector3 delta = cmc.transform.position - __instance.transform.position;
                float dist2 = delta.sqrMagnitude;
                float maxDist = (__instance.sightDistance > 0f ? __instance.sightDistance : 50f);
                if (dist2 > maxDist * maxDist) return;

                if (__instance.sightAngle > 1f)
                {
                    Vector3 fwd = __instance.transform.forward; fwd.y = 0f;
                    Vector3 dir = delta; dir.y = 0f;
                    if (dir.sqrMagnitude < 1e-6f) return;
                    float cos = Vector3.Dot(dir.normalized, fwd.normalized);
                    float cosThresh = Mathf.Cos(__instance.sightAngle * 0.5f * Mathf.Deg2Rad);
                    if (cos < cosThresh) return;
                }

                if (dist2 < bestD2)
                {
                    bestD2 = dist2;
                    best = cmc;
                }
            }

            // 1) 主机本体
            Consider(CharacterMainControl.Main);

            // 2) 所有客户端玩家的镜像（主机表）
            if (mod.remoteCharacters != null)
            {
                foreach (var kv in mod.remoteCharacters)
                {
                    var go = kv.Value;
                    if (!go) continue;
                    var cmc = go.GetComponent<CharacterMainControl>();
                    Consider(cmc);
                }
            }

            if (best == null) return;

            //  与现有目标比较若当前目标未死亡/同队且更近，则保留；否则切换 
            var cur = __instance.searchedEnemy;
            if (cur)
            {
                bool bad = false;
                try { if (cur.Team == aiCmc.Team) bad = true; } catch { }
                try { if (cur.health != null && cur.health.CurrentHealth <= 0f) bad = true; } catch { }
                if (cur.gameObject.scene != __instance.gameObject.scene) bad = true;

                if (!bad)
                {
                    float curD2 = (cur.transform.position - __instance.transform.position).sqrMagnitude;

                    if (curD2 <= bestD2 * 0.81f) return;
                }
            }

            // 切换到最近玩家
            var dr = best.mainDamageReceiver;
            if (dr)
            {
                __instance.searchedEnemy = dr;                      // 行为树/FSM普遍用 searchedEnemy
                __instance.SetTarget(dr.transform);
                __instance.SetNoticedToTarget(dr);                  // 同步“已注意到”的来源
            }
        }
    }





}
