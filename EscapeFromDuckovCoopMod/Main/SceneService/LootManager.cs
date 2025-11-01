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

﻿using Duckov.Utilities;
using ItemStatsSystem;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using static EscapeFromDuckovCoopMod.LootNet;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod
{
    public static class LootUiGuards
    {
        [ThreadStatic] public static int InLootAddAtDepth;
        public static bool InLootAddAt => InLootAddAtDepth > 0;
        [ThreadStatic] public static int BlockNextSendToInventory;
    }

    static class LootSearchWorldGate
    {

        static readonly Dictionary<ItemStatsSystem.Inventory, bool> _world = new Dictionary<Inventory, bool>();

        public static void EnsureWorldFlag(ItemStatsSystem.Inventory inv)
        {
            if (inv) _world[inv] = true; // 只缓存 true避免一次误判把容器永久当“非世界”
        }

        public static bool IsWorldLootByInventory(ItemStatsSystem.Inventory inv)
        {
            if (!inv) return false;
            if (_world.TryGetValue(inv, out var yes) && yes) return true;

            // 动态匹配（不缓存 false）
            try
            {
                var boxes = UnityEngine.Object.FindObjectsOfType<InteractableLootbox>(true);
                foreach (var b in boxes)
                {
                    if (!b) continue;
                    if (b.Inventory == inv)
                    {
                        bool isWorld = b.GetComponent<Duckov.Utilities.LootBoxLoader>() != null;
                        if (isWorld) _world[inv] = true;
                        return isWorld;
                    }
                }
            }
            catch { }
            return false;
        }

        static MemberInfo _miNeedInspection;

        internal static bool GetNeedInspection(Inventory inv)
        {
            if (inv == null) return false;
            try
            {
                var m = FindNeedInspectionMember(inv.GetType());
                if (m is FieldInfo fi) return (bool)(fi.GetValue(inv) ?? false);
                if (m is PropertyInfo pi) return (bool)(pi.GetValue(inv) ?? false);
            }
            catch { }
            return false;
        }

        static MemberInfo FindNeedInspectionMember(Type t)
        {
            if (_miNeedInspection != null) return _miNeedInspection;
            _miNeedInspection = (MemberInfo)t.GetField("NeedInspection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                               ?? t.GetProperty("NeedInspection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return _miNeedInspection;
        }

        internal static void TrySetNeedInspection(ItemStatsSystem.Inventory inv, bool v)
        {
            if (!inv) return;
            inv.NeedInspection = v;
        }


        internal static void ForceTopLevelUninspected(Inventory inv)
        {
            if (inv == null) return;
            try
            {
                foreach (var it in inv)
                {
                    if (!it) continue;
                    try { it.Inspected = false; } catch { }
                }
            }
            catch { }
        }
    }


    static class WorldLootPrime
    {
        public static void PrimeIfClient(InteractableLootbox lb)
        {
            var mod = EscapeFromDuckovCoopMod.ModBehaviourF.Instance;
            if (mod == null || mod.IsServer) return;
            if (!lb) return;

            var inv = lb.Inventory;
            if (!inv) return;

            // 把它标记成“世界容器”（只缓存 true，避免误判成 false）
            LootSearchWorldGate.EnsureWorldFlag(inv);

            // 已经是需搜索就别重复改（幂等）
            bool need = false;
            try { need = inv.NeedInspection; } catch { }
            if (need) return;

            try { lb.needInspect = true; } catch { }
            try { inv.NeedInspection = true; } catch { }

            // 只把顶层物品置为未鉴定即可（Inventory 可 foreach）
            try
            {
                foreach (var it in inv)
                {
                    if (!it) continue;
                    try { it.Inspected = false; } catch { }
                }
            }
            catch { }
        }
    }

    internal static class DeadLootSpawnContext
    {
        [ThreadStatic] public static CharacterMainControl InOnDead;
    }


    public static class LootboxDetectUtil
    {
        static InteractableLootbox[] cache;

        public static bool IsPrivateInventory(ItemStatsSystem.Inventory inv)
        {
            if (inv == null) return false;
            if (ReferenceEquals(inv, PlayerStorage.Inventory)) return true;  // 仓库
            if (ReferenceEquals(inv, PetProxy.PetInventory)) return true;    // 宠物包
            return false;
        }

        public static bool IsLootboxInventory(Inventory inv)
        {
            if (inv == null) return false;
            // 排除私有库存（仓库/宠物包）
            if (IsPrivateInventory(inv)) return false;

            var dict = InteractableLootbox.Inventories;
            if (dict != null)
            {
                foreach (var kv in dict)
                    if (kv.Value == inv) return true;
            }

            return TryGetInventoryLootBox(inv) != null;
        }

        public static InteractableLootbox TryGetInventoryLootBox(Inventory inv)
        {
            // 先尝试缓存
            if (cache != null)
            {
                foreach (var b in cache)
                {
                    if (b && b.Inventory == inv)
                    {
                        return b;
                    }
                }
            }

            // Fallback
            var boxes = Object.FindObjectsOfType<InteractableLootbox>(true);
            cache = boxes; // 更新缓存
            foreach (var b in boxes)
            {
                if (b && b.Inventory == inv)
                {
                    return b;
                }
            }

            return null;
        }
    }

    public class LootManager : MonoBehaviour
    {
        private NetService Service => NetService.Instance;
        private bool IsServer => Service != null && Service.IsServer;
        private NetManager netManager => Service?.netManager;
        private NetDataWriter writer => Service?.writer;
        private NetPeer connectedPeer => Service?.connectedPeer;
        private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
        private bool networkStarted => Service != null && Service.networkStarted;
        public static LootManager Instance;

        // 服务器：容器快照广播的“抑制窗口”表 sans可用
        public readonly Dictionary<ItemStatsSystem.Inventory, float> _srvLootMuteUntil
            = new Dictionary<ItemStatsSystem.Inventory, float>(new RefEq<ItemStatsSystem.Inventory>());

        // 客户端：uid -> inv
        public readonly Dictionary<int, Inventory> _cliLootByUid = new Dictionary<int, Inventory>();

        public int _nextLootUid = 1; // 服务器侧自增
                                      // 服务器：uid -> inv
        public readonly Dictionary<int, Inventory> _srvLootByUid = new Dictionary<int, Inventory>();

        public readonly Dictionary<int, (int capacity, List<(int pos, ItemSnapshot snap)>)> _pendingLootStatesByUid
          = new Dictionary<int, (int, List<(int, ItemSnapshot)>)>();

        // token -> 目的地
        public readonly System.Collections.Generic.Dictionary<uint, PendingTakeDest> _cliPendingTake
            = new System.Collections.Generic.Dictionary<uint, PendingTakeDest>();


        public void Init()
        {
            Instance = this;
        }

        sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T a, T b) => ReferenceEquals(a, b);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }


        public int ComputeLootKey(Transform t)
        {
            if (!t) return -1;
            var v = t.position * 10f;
            int x = Mathf.RoundToInt(v.x);
            int y = Mathf.RoundToInt(v.y);
            int z = Mathf.RoundToInt(v.z);
            return new Vector3Int(x, y, z).GetHashCode();
        }


        public void PutLootId(NetDataWriter w, Inventory inv)
        {
            int scene = SceneManager.GetActiveScene().buildIndex;
            int posKey = -1;
            int instanceId = -1;

            var dict = InteractableLootbox.Inventories;
            if (inv != null && dict != null)
            {
                foreach (var kv in dict)
                {
                    if (kv.Value == inv) { posKey = kv.Key; break; }
                }
            }

            if (inv != null && (posKey < 0 || instanceId < 0))
            {
                var b = LootboxDetectUtil.TryGetInventoryLootBox(inv);
                if (b != null)
                {
                    posKey = ComputeLootKey(b.transform);
                    instanceId = b.GetInstanceID();
                }
            }

            // 稳定 ID（仅死亡箱子会命中，其它容器写 -1）
            int lootUid = -1;
            if (IsServer)
            {
                // 主机：从 _srvLootByUid 反查
                foreach (var kv in _srvLootByUid)
                {
                    if (kv.Value == inv) { lootUid = kv.Key; break; }
                }
            }
            else
            {
                // 客户端：从 _cliLootByUid 反查（关键修复）
                foreach (var kv in _cliLootByUid)
                {
                    if (kv.Value == inv) { lootUid = kv.Key; break; }
                }
            }

            w.Put(scene);
            w.Put(posKey);
            w.Put(instanceId);
            w.Put(lootUid);
        }



        public bool TryResolveLootById(int scene, int posKey, int iid, out Inventory inv)
        {
            inv = null;

            // 先用 posKey 命中（跨词典）
            if (posKey != 0 && TryGetLootInvByKeyEverywhere(posKey, out inv)) return true;

            // 再按 iid 找 GameObject 上的 InteractableLootbox，取其 Inventory
            if (iid != 0)
            {
                try
                {
                    var all = UnityEngine.Object.FindObjectsOfType<InteractableLootbox>(true);
                    foreach (var b in all)
                    {
                        if (!b) continue;
                        if (b.GetInstanceID() == iid && (scene < 0 || b.gameObject.scene.buildIndex == scene))
                        {
                            inv = b.Inventory; // 走到这一步，get_Inventory 的兜底会触发
                            if (inv) return true;
                        }
                    }
                }
                catch { }
            }

            return false; // 交给 TryResolveLootByHint / Server_TryResolveLootAggressive
        }

        // 兜底协程：超时自动清 Loading
        public System.Collections.IEnumerator ClearLootLoadingTimeout(ItemStatsSystem.Inventory inv, float seconds)
        {
            float t = 0f;
            while (inv && inv.Loading && t < seconds) { t += UnityEngine.Time.deltaTime; yield return null; }
            if (inv && inv.Loading) inv.Loading = false;
        }
        public static int ComputeLootKeyFromPos(Vector3 pos)
        {
            var v = pos * 10f;
            int x = Mathf.RoundToInt(v.x);
            int y = Mathf.RoundToInt(v.y);
            int z = Mathf.RoundToInt(v.z);
            return new Vector3Int(x, y, z).GetHashCode();
        }

        // 通过 inv 找到它对应的 Lootbox 世界坐标；找不到则返回 false
        public bool TryGetLootboxWorldPos(Inventory inv, out Vector3 pos)
        {
            pos = default;
            if (!inv) return false;
            var boxes = GameObject.FindObjectsOfType<InteractableLootbox>();
            foreach (var b in boxes)
            {
                if (!b) continue;
                if (b.Inventory == inv) { pos = b.transform.position; return true; }
            }
            return false;
        }

        // 根据位置提示在半径内兜底解析对应的 lootbox（主机端用）
        private bool TryResolveLootByHint(Vector3 posHint, out Inventory inv, float radius = 2.5f)
        {
            inv = null;
            float best = float.MaxValue;
            var boxes = GameObject.FindObjectsOfType<InteractableLootbox>();
            foreach (var b in boxes)
            {
                if (!b || b.Inventory == null) continue;
                float d = Vector3.Distance(b.transform.position, posHint);
                if (d < radius && d < best) { best = d; inv = b.Inventory; }
            }
            return inv != null;
        }

        // 每次开箱都拉起一次“解卡”兜底，避免第二次打开卡死
        public void KickLootTimeout(Inventory inv, float seconds = 1.5f)
        {
            StartCoroutine(ClearLootLoadingTimeout(inv, seconds));
        }

        // 当前 LootView 是否就是这个容器（用它来识别“战利品容器”）
        public static bool IsCurrentLootInv(ItemStatsSystem.Inventory inv)
        {
            var lv = Duckov.UI.LootView.Instance;
            return lv && inv && object.ReferenceEquals(inv, lv.TargetInventory);
        }

        public bool Server_TryResolveLootAggressive(int scene, int posKey, int iid, Vector3 posHint, out ItemStatsSystem.Inventory inv)
        {
            inv = null;

            // 1) 你原有的两条路径
            if (TryResolveLootById(scene, posKey, iid, out inv)) return true;
            if (TryResolveLootByHint(posHint, out inv)) return true;

            // 2) 兜底：在 posHint 附近 3m 扫一圈，强制确保并注册
            float best = 9f; // 3m^2
            InteractableLootbox bestBox = null;
            foreach (var b in UnityEngine.Object.FindObjectsOfType<InteractableLootbox>())
            {
                if (!b || !b.gameObject.activeInHierarchy) continue;
                if (scene >= 0 && b.gameObject.scene.buildIndex != scene) continue;
                float d2 = (b.transform.position - posHint).sqrMagnitude;
                if (d2 < best) { best = d2; bestBox = b; }
            }
            if (!bestBox) return false;

            // 触发/强制创建 Inventory（原游戏逻辑会注册到 LevelManager.LootBoxInventories）
            inv = bestBox.Inventory;  // 等价于 GetOrCreateInventory(b)
            if (!inv) return false;

            // 保险：把 posKey→inv 显式写入一次
            var dict = InteractableLootbox.Inventories;
            if (dict != null)
            {
                int key = ComputeLootKey(bestBox.transform);
                dict[key] = inv;
            }
            return true;
        }

        public void Server_HandleLootOpenRequest(NetPeer peer, NetPacketReader r)
        {
            if (!IsServer) return;

            // 旧三元标识
            int scene = r.GetInt();
            int posKey = r.GetInt();
            int iid = r.GetInt();

            // 对齐 PutLootId：可能还带了稳定ID
            int lootUid = -1;
            if (r.AvailableBytes >= 4) lootUid = r.GetInt();

            // 请求版本（向后兼容）
            byte reqVer = 0;
            if (r.AvailableBytes >= 1) reqVer = r.GetByte();

            // 位置提示（厘米压缩），防御式读取
            Vector3 posHint = Vector3.zero;
            if (r.AvailableBytes >= 12) posHint = r.GetV3cm();

            // 先用稳定ID命中（AI掉落箱优先命中这里）
            ItemStatsSystem.Inventory inv = null;
            if (lootUid >= 0) _srvLootByUid.TryGetValue(lootUid, out inv);

            if (LootboxDetectUtil.IsPrivateInventory(inv)) { COOPManager.LootNet.Server_SendLootDeny(peer, "no_inv"); return; }

            // 命不中再走你原有“激进解析”：三元标识 + 附近3米扫描并注册
            if (inv == null && !Server_TryResolveLootAggressive(scene, posKey, iid, posHint, out inv))
            {
                COOPManager.LootNet.Server_SendLootDeny(peer, "no_inv");
                return;
            }

            // 只回给发起的这个 peer（不要广播）
            COOPManager.LootNet.Server_SendLootboxState(peer, inv);
        }


        public readonly System.Collections.Generic.Dictionary<uint, (Inventory inv, int pos)> _cliPendingReorder
    = new System.Collections.Generic.Dictionary<uint, (Inventory inv, int pos)>();

        public void NoteLootReorderPending(uint token, Inventory inv, int targetPos)
        {
            if (token != 0 && inv) _cliPendingReorder[token] = (inv, targetPos);
        }

        public static bool TryGetLootInvByKeyEverywhere(int posKey, out Inventory inv)
        {
            inv = null;

            // A) InteractableLootbox.Inventories
            var dictA = InteractableLootbox.Inventories;
            if (dictA != null && dictA.TryGetValue(posKey, out inv) && inv) return true;

            // B) LevelManager.LootBoxInventories
            try
            {
                var lm = LevelManager.Instance;
                var dictB = lm != null ? LevelManager.LootBoxInventories : null;
                if (dictB != null && dictB.TryGetValue(posKey, out inv) && inv)
                {
                    // 顺手回填 A，保持一致
                    try { if (dictA != null) dictA[posKey] = inv; } catch { }
                    return true;
                }
            }
            catch { }

            inv = null;
            return false;
        }


        public InteractableLootbox ResolveDeadLootPrefabOnServer()
        {
            var any = GameplayDataSettings.Prefabs;
            try
            {
                if (any != null && any.LootBoxPrefab_Tomb != null) return any.LootBoxPrefab_Tomb;
            }
            catch { }

            if (any != null)
            {
                return any.LootBoxPrefab;
            }

            return null; // 客户端收到 DEAD_LOOT_SPAWN 时也有兜底寻找预制体的逻辑
        }


    

        // 发送端：把 inv 内 item 的“路径”写进包里
        public void WriteItemRef(NetDataWriter w, Inventory inv, Item item)
        {
            // 找到 inv 中的“根物品”（顶层，不在任何槽位里）
            var root = item;
            while (root != null && root.PluggedIntoSlot != null) root = root.PluggedIntoSlot.Master;
            int rootIndex = inv != null ? inv.GetIndex(root) : -1;
            w.Put(rootIndex);

            // 从 item 逆向收集到根的槽位key，再反转写出
            var keys = new List<string>();
            var cur = item;
            while (cur != null && cur.PluggedIntoSlot != null) { var s = cur.PluggedIntoSlot; keys.Add(s.Key ?? ""); cur = s.Master; }
            keys.Reverse();
            w.Put(keys.Count);
            foreach (var k in keys) w.Put(k ?? "");
        }


        // 接收端：用“路径”从 inv 找回 item
        public Item ReadItemRef(NetPacketReader r, Inventory inv)
        {
            int rootIndex = r.GetInt();
            int keyCount = r.GetInt();
            var it = inv.GetItemAt(rootIndex);
            for (int i = 0; i < keyCount && it != null; i++)
            {
                string key = r.GetString();
                var slot = it.Slots?.GetSlot(key);
                it = slot != null ? slot.Content : null;
            }
            return it;
        }


        // 统一解析容器 Inventory：优先稳定ID，再回落到三元标识
        public ItemStatsSystem.Inventory ResolveLootInv(int scene, int posKey, int iid, int lootUid)
        {
            ItemStatsSystem.Inventory inv = null;

            // 先用稳定ID（主机用 _srvLootByUid；客户端用 _cliLootByUid）
            if (lootUid >= 0)
            {
                if (IsServer)
                {
                    if (_srvLootByUid != null && _srvLootByUid.TryGetValue(lootUid, out inv) && inv)
                        return inv;
                }
                else
                {
                    if (_cliLootByUid != null && _cliLootByUid.TryGetValue(lootUid, out inv) && inv)
                        return inv;
                }
            }

            // 回落到 scene/posKey/iid 三元定位
            if (TryResolveLootById(scene, posKey, iid, out inv) && inv)
                return inv;

            return null;
        }

        public bool Server_IsLootMuted(ItemStatsSystem.Inventory inv)
        {
            if (!inv) return false;
            if (_srvLootMuteUntil.TryGetValue(inv, out var until))
            {
                if (Time.time < until) return true;
                _srvLootMuteUntil.Remove(inv); // 过期清理
            }
            return false;
        }

        public void Server_MuteLoot(ItemStatsSystem.Inventory inv, float seconds)
        {
            if (!inv) return;
            _srvLootMuteUntil[inv] = Time.time + Mathf.Max(0.01f, seconds);
        }









    }






}
