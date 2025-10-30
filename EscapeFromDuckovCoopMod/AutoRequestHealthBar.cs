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

﻿using Duckov.UI;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EscapeFromDuckovCoopMod
{
    [DisallowMultipleComponent]
    public class AutoRequestHealthBar : MonoBehaviour
    {
        [SerializeField] int attempts = 30;      // 最长重试次数（总计约 3 秒）
        [SerializeField] float interval = 0.1f;  // 每次重试间隔

        static readonly System.Reflection.FieldInfo FI_character =
            typeof(Health).GetField("characterCached", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        static readonly System.Reflection.FieldInfo FI_hasChar =
            typeof(Health).GetField("hasCharacter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        void OnEnable()
        {
            StartCoroutine(Bootstrap());
        }

        IEnumerator Bootstrap()
        {
            // 等一帧，确保层级/场景/UI 管线基本就绪??
            yield return null;
            yield return null;

            var cmc = GetComponent<CharacterMainControl>();
            var h = GetComponentInChildren<Health>(true);
            if (!h) yield break;

            // 绑定 Health⇄Character（远端克隆常见问题）
            try { FI_character?.SetValue(h, cmc); FI_hasChar?.SetValue(h, true); } catch { }

            for (int i = 0; i < attempts; i++)
            {
                if (!h) yield break;

                try { h.showHealthBar = true; } catch { }
                try { h.RequestHealthBar(); } catch { }

                try { h.OnMaxHealthChange?.Invoke(h); } catch { }
                try { h.OnHealthChange?.Invoke(h); } catch { }

                yield return new WaitForSeconds(interval);
            }
        }
    }
}
