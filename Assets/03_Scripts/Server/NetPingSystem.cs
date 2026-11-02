using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 핑 온라인 구현 (C-1101-3, 계약 §3.11). 받은 값을 그대로 모든 PC에 전달만 한다(위치 검증·보정 없음).
    /// 로컬 NetPlayer의 RPC로 보내고, 보낸 PC 포함 모든 PC에서 PingReceived 1회.
    /// 로컬 플레이어가 아직 없으면(코스 로드 전) 내 화면에만 띄운다.
    /// </summary>
    public class NetPingSystem : IPingSystem
    {
        static NetPingSystem current;

        public event Action<PingType, Vector3, Vector3, PlayerRole> PingReceived;

        public NetPingSystem()
        {
            current = this;
        }

        public void RequestPing(PingType type, Vector3 position, Vector3 normal, PlayerRole sender)
        {
            var self = NetPlayer.Local;
            if (self != null) self.SendPing(type, position, normal, sender);
            else PingReceived?.Invoke(type, position, normal, sender);
        }

        /// <summary>NetPlayer.RPC_Ping이 모든 PC에서 부른다.</summary>
        internal static void RaiseReceived(PingType type, Vector3 position, Vector3 normal, PlayerRole sender)
        {
            current?.PingReceived?.Invoke(type, position, normal, sender);
        }
    }
}
