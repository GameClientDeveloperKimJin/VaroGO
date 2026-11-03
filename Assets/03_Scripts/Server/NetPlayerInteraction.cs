using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 플레이어 밀기 온라인 구현 (S2-09, 계약 §3.9). 전달만 한다. 넉백 판정·적용은 밀린 PC의 클라 코드 (R6).
    /// 미는 PC: RequestPush → 대상 NetPlayer의 상태 권한으로 RPC → 밀린 PC에서만 Pushed 1회.
    /// 0.5초 안의 중복 밀기는 밀린 PC에서 버린다 (S2-10).
    /// P18 구출: RequestRescueShake → 대상 NetPlayer의 상태 권한으로 RPC → 묶인·매달린 PC에서만 RescueShaken 1회.
    /// </summary>
    public class NetPlayerInteraction : IPlayerInteraction
    {
        static NetPlayerInteraction current;

        public event Action<PlayerRole, Vector3> Pushed;
        public event Action<PlayerRole> RescueShaken;

        public NetPlayerInteraction()
        {
            current = this;
        }

        public void RequestPush(PlayerRole target, Vector3 dir)
        {
            var self = NetPlayer.Local;
            var other = NetPlayer.Find(target);
            if (self == null || other == null || other == self) return;
            other.SendPush(self.Role, dir);
        }

        public void RequestRescueShake(PlayerRole target)
        {
            var self = NetPlayer.Local;
            var other = NetPlayer.Find(target);
            if (self == null || other == null || other == self) return;
            other.SendRescueShake(self.Role);
        }

        /// <summary>NetPlayer.RPC_RescueShake가 구출받는 PC(상태 권한)에서 부른다.</summary>
        internal static void RaiseRescueShaken(PlayerRole from)
        {
            current?.RescueShaken?.Invoke(from);
        }

        /// <summary>NetPlayer.RPC_Push가 밀린 PC(상태 권한)에서 부른다.</summary>
        internal static void RaisePushed(PlayerRole from, Vector3 dir)
        {
            current?.Pushed?.Invoke(from, dir);
        }
    }
}
