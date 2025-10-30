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
using UnityEngine;

namespace EscapeFromDuckovCoopMod
{
    [HarmonyPatch(typeof(Health), "Hurt", new[] { typeof(global::DamageInfo) })]
    static class Patch_AIHealth_Hurt_HostAuthority
    {
        [HarmonyPriority(Priority.High)]
        static bool Prefix(Health __instance, ref global::DamageInfo damageInfo)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted) return true;
            if (mod.IsServer) return true;            // 主机照常
            bool isMain = false; try { isMain = __instance.IsMainCharacterHealth; } catch { }
            if (isMain) return true;

            if (__instance.gameObject.GetComponent<AutoRequestHealthBar>() != null)
            {
                return false;
            }

            // 是否 AI
            CharacterMainControl victim = null;
            try { victim = __instance.TryGetCharacter(); } catch { }
            if (!victim) { try { victim = __instance.GetComponentInParent<CharacterMainControl>(); } catch { } }

            bool victimIsAI = victim &&
                              (victim.GetComponent<AICharacterController>() != null ||
                               victim.GetComponent<NetAiTag>() != null);
            if (!victimIsAI) return true;

            // —— 不处理 AI→AI —— 
            var attacker = damageInfo.fromCharacter;
            bool attackerIsAI = attacker &&
                                (attacker.GetComponent<AICharacterController>() != null ||
                                 attacker.GetComponent<NetAiTag>() != null);
            if (attackerIsAI)
                return false; // 直接阻断，AI↔AI 不做任何本地效果


            //  LocalHitKillFx.ClientPlayForAI(victim, damageInfo, predictedDead: false);

            return false;
        }

        // 主机在结算后广播 AI 当前血量（你已有的广播逻辑，保留）
        static void Postfix(Health __instance, global::DamageInfo damageInfo)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted || !mod.IsServer) return;

            var cmc = __instance.TryGetCharacter();
            if (!cmc) { try { cmc = __instance.GetComponentInParent<CharacterMainControl>(); } catch { } }
            if (!cmc) return;

            var tag = cmc.GetComponent<NetAiTag>();
            if (!tag) return;

            if (ModBehaviourF.LogAiHpDebug)
                Debug.Log($"[AI-HP][SERVER] Hurt => broadcast aiId={tag.aiId} cur={__instance.CurrentHealth}");
            COOPManager.AIHealth.Server_BroadcastAiHealth(tag.aiId, __instance.MaxHealth, __instance.CurrentHealth);
        }
    }

    // ========== 客户端：拦截 Health.Hurt（AI 被打） -> 仅本机玩家命中时播放本地特效/数字，然后发给主机 ==========
    [HarmonyPatch(typeof(Health), "Hurt")]
    static class Patch_Health
    {
        static bool Prefix(Health __instance, ref global::DamageInfo __0)
        {
            var mod = EscapeFromDuckovCoopMod.ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted) return true;

            if (__instance.gameObject.GetComponent<AutoRequestHealthBar>() != null)
            {
                return false;
            }

            // 受击者是不是 AI/NPC
            global::CharacterMainControl victimCmc = null;
            try { victimCmc = __instance ? __instance.TryGetCharacter() : null; } catch { }
            bool isAiVictim = (victimCmc && victimCmc != global::CharacterMainControl.Main);

            // 攻击者是不是本机玩家
            var from = __0.fromCharacter;
            bool fromLocalMain = (from == global::CharacterMainControl.Main);

            // 仅客户端 + 仅本机玩家打到 AI 时，走“拦截→本地播特效→网络上报”
            if (!mod.IsServer && isAiVictim && fromLocalMain)
            {
                // 预测是否致死（用于提前播死亡特效/击杀标记，手感更好）
                bool predictedDead = false;
                try
                {
                    float cur = __instance.CurrentHealth;
                    predictedDead = (cur > 0f && __0.damageValue >= cur - 0.001f);
                }
                catch { }
                // LocalHitKillFx.RememberLastBaseDamage(__0.damageValue);
                // 鸭科夫联机Mod.LocalHitKillFx.ClientPlayForAI(victimCmc, __0, predictedDead);

                return false;
            }

            // 其它情况放行（包括 AI→AI、AI→障碍物、远端玩家→AI 等）
            return true;
        }
    }


    [HarmonyPatch(typeof(Health), "Hurt", new[] { typeof(global::DamageInfo) })]
    static class Patch_CoopPlayer_Health_Hurt
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(Health __instance, ref global::DamageInfo damageInfo)
        {
            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted) return true;

            if (!mod.IsServer)
            {
                bool isMain = false; try { isMain = __instance.IsMainCharacterHealth; } catch { }
                if (isMain) return true;
            }

            bool isProxy = __instance.gameObject.GetComponent<AutoRequestHealthBar>() != null;

            if (mod.IsServer && isProxy)
            {
                var owner = HealthTool.Server_FindOwnerPeerByHealth(__instance);
                if (owner != null)
                {
                    try { HealthM.Instance.Server_ForwardHurtToOwner(owner, damageInfo); }
                    catch (System.Exception e) { UnityEngine.Debug.LogWarning("[HP] forward to owner failed: " + e); }
                }
                return false;
            }

            if (!mod.IsServer && isProxy) return false;
            return true;
        }
    }



}
