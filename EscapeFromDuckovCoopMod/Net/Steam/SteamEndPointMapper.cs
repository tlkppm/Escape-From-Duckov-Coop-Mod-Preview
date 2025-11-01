// Escape-From-Duckov-Coop-Mod-Preview
// Copyright (C) 2025  Mr.sans and InitLoader's team

using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Net.Steam
{
    /// <summary>
    /// Steam EndPoint映射器 - 虚拟网络IP管理核心
    /// 
    /// 设计目标：
    /// 让Steam P2P对LiteNetLib完全透明，让LiteNetLib以为在用标准UDP网络
    /// 
    /// 工作原理：
    /// 1. 为每个Steam玩家分配一个虚拟IP地址（10.255.x.x）
    /// 2. LiteNetLib使用虚拟IP作为EndPoint进行通信
    /// 3. 本类负责双向映射：SteamID <-> 虚拟IP EndPoint
    /// 4. 当LiteNetLib要发包时，将虚拟IP转换回SteamID，通过Steam API发送
    /// 
    /// 虚拟IP分配规则：
    /// - 网段：10.255.0.0/16（私有IP范围，不会与真实网络冲突）
    /// - 起始：10.255.0.1
    /// - 递增：每个新SteamID自动分配下一个可用IP
    /// 
    /// 关键映射：
    /// SteamID 76561198827560045 <-> 10.255.0.1:9050
    /// SteamID 76561198827560046 <-> 10.255.0.2:9050
    /// 
    /// 注意事项：
    /// - 虚拟IP仅在当前会话有效，重启后重新分配
    /// - 不注册本地玩家的SteamID，因为本地玩家用真实的localhost
    /// </summary>
    public class SteamEndPointMapper : MonoBehaviour
    {
        public static SteamEndPointMapper Instance { get; private set; }
        
        // 双向映射表
        private Dictionary<CSteamID, IPEndPoint> _steamToEndPoint = new Dictionary<CSteamID, IPEndPoint>();  // SteamID -> 虚拟IP映射
        private Dictionary<IPEndPoint, CSteamID> _endPointToSteam = new Dictionary<IPEndPoint, CSteamID>();  // 虚拟IP -> SteamID反向映射
        private int _virtualIpCounter = 1;  // 虚拟IP计数器，从1开始递增
        
        // 虚拟IP网段：10.255.x.x
        private const byte VirtualIpPrefix1 = 10;   // 第一字节
        private const byte VirtualIpPrefix2 = 255;  // 第二字节
        
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[SteamEndPointMapper] 初始化完成");
        }
        
        public System.Collections.IEnumerator WaitForP2PSessionEstablished(CSteamID remoteSteamID, System.Action<bool> callback, float timeout = 10f)
        {
            float elapsed = 0f;
            
            Debug.Log("[SteamEndPointMapper] 等待P2P会话建立: " + remoteSteamID);
            
            while (elapsed < timeout)
            {
                if (SteamManager.Initialized && SteamNetworking.GetP2PSessionState(remoteSteamID, out P2PSessionState_t sessionState))
                {
                    if (sessionState.m_bConnectionActive == 1)
                    {
                        Debug.Log("[SteamEndPointMapper] P2P会话已建立: " + remoteSteamID);
                        callback?.Invoke(true);
                        yield break;
                    }
                }
                
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }
            
            Debug.LogWarning("[SteamEndPointMapper] P2P会话建立超时: " + remoteSteamID);
            callback?.Invoke(false);
        }
        
        public IPEndPoint RegisterSteamID(CSteamID steamID, int port = 27015)
        {
            CSteamID mySteamID = SteamUser.GetSteamID();
            if (steamID == mySteamID)
            {
                Debug.Log("[SteamEndPointMapper] 跳过注册本地玩家的SteamID: " + steamID);
                return null;
            }
            
            if (_steamToEndPoint.TryGetValue(steamID, out IPEndPoint existingEndPoint))
            {
                Debug.Log("[SteamEndPointMapper] Steam ID " + steamID + " 已注册为 " + existingEndPoint);
                return existingEndPoint;
            }
            
            IPEndPoint virtualEndPoint = GenerateVirtualEndPoint(port);
            _steamToEndPoint[steamID] = virtualEndPoint;
            _endPointToSteam[virtualEndPoint] = steamID;
            
            Debug.Log("[SteamEndPointMapper] ========== 虚拟IP映射 ==========");
            Debug.Log("[SteamEndPointMapper] SteamID: " + steamID);
            Debug.Log("[SteamEndPointMapper] 虚拟IP: " + virtualEndPoint.Address + ":" + virtualEndPoint.Port);
            Debug.Log("[SteamEndPointMapper] 当前映射数量: " + _steamToEndPoint.Count);
            
            if (SteamManager.Initialized)
            {
                bool accepted = SteamNetworking.AcceptP2PSessionWithUser(steamID);
                
                byte[] handshake = System.Text.Encoding.UTF8.GetBytes("HANDSHAKE");
                for (int i = 0; i < 3; i++)
                {
                    SteamNetworking.SendP2PPacket(
                        steamID, handshake, (uint)handshake.Length,
                        EP2PSend.k_EP2PSendUnreliableNoDelay, 0
                    );
                }
                
                bool sent = SteamNetworking.SendP2PPacket(
                    steamID, handshake, (uint)handshake.Length,
                    EP2PSend.k_EP2PSendReliable, 0
                );
                
                Debug.Log("[SteamEndPointMapper] NAT穿透握手: " + (sent ? "成功" : "失败"));
            }
            
            return virtualEndPoint;
        }
        
        public bool TryGetSteamID(IPEndPoint endPoint, out CSteamID steamID)
        {
            return _endPointToSteam.TryGetValue(endPoint, out steamID);
        }
        
        public bool TryGetEndPoint(CSteamID steamID, out IPEndPoint endPoint)
        {
            return _steamToEndPoint.TryGetValue(steamID, out endPoint);
        }
        
        public void UnregisterSteamID(CSteamID steamID)
        {
            if (_steamToEndPoint.TryGetValue(steamID, out IPEndPoint endPoint))
            {
                _steamToEndPoint.Remove(steamID);
                _endPointToSteam.Remove(endPoint);
                Debug.Log("[SteamEndPointMapper] 移除映射: " + steamID + " <-> " + endPoint);
            }
        }
        
        public void UnregisterEndPoint(IPEndPoint endPoint)
        {
            if (_endPointToSteam.TryGetValue(endPoint, out CSteamID steamID))
            {
                _endPointToSteam.Remove(endPoint);
                _steamToEndPoint.Remove(steamID);
                Debug.Log("[SteamEndPointMapper] 移除映射: " + steamID + " <-> " + endPoint);
            }
        }
        
        public bool IsVirtualEndPoint(IPEndPoint endPoint)
        {
            if (endPoint == null)
                return false;
                
            byte[] addressBytes = endPoint.Address.GetAddressBytes();
            if (addressBytes.Length == 4)
            {
                return addressBytes[0] == VirtualIpPrefix1 && addressBytes[1] == VirtualIpPrefix2;
            }
            return false;
        }
        
        public List<CSteamID> GetAllSteamIDs()
        {
            return _steamToEndPoint.Keys.ToList();
        }
        
        public List<IPEndPoint> GetAllEndPoints()
        {
            return _endPointToSteam.Keys.ToList();
        }
        
        public void ClearAll()
        {
            _steamToEndPoint.Clear();
            _endPointToSteam.Clear();
            _virtualIpCounter = 1;
            Debug.Log("[SteamEndPointMapper] 已清空所有映射");
        }
        
        public void OnP2PSessionEstablished(CSteamID remoteSteamID)
        {
            if (!_steamToEndPoint.ContainsKey(remoteSteamID))
            {
                RegisterSteamID(remoteSteamID);
            }
        }
        
        public void OnP2PSessionFailed(CSteamID remoteSteamID)
        {
            UnregisterSteamID(remoteSteamID);
        }
        
        private IPEndPoint GenerateVirtualEndPoint(int port)
        {
            byte byte3 = (byte)(_virtualIpCounter / 256);
            byte byte4 = (byte)(_virtualIpCounter % 256);
            _virtualIpCounter++;
            
            if (_virtualIpCounter > 65535)
            {
                _virtualIpCounter = 1;
            }
            
            IPAddress virtualIP = new IPAddress(new byte[] { VirtualIpPrefix1, VirtualIpPrefix2, byte3, byte4 });
            return new IPEndPoint(virtualIP, port);
        }
        
        public string GetMappingStats()
        {
            return "[SteamEndPointMapper] 当前映射数: " + _steamToEndPoint.Count;
        }
        
        private void OnDestroy()
        {
            Debug.Log(GetMappingStats());
            ClearAll();
        }
    }
}

