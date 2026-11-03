using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// P18 온라인 플레이어 상호작용(계약 §3). 요청은 대상 역할의 NetPlayer로 RPC를 보내고,
    /// 이벤트는 대상의 주인 PC에서만 발생한다.
    /// </summary>
    public class NetPlayerInteraction : IPlayerInteraction
    {
        public static readonly NetPlayerInteraction Instance = new NetPlayerInteraction();

        public event Action<PlayerRole, Vector3> Pushed;
        public event Action<PlayerRole> RescueShaken;

        static PlayerRole LocalRole => NetPlayer.Local != null ? NetPlayer.Local.Role : PlayerRole.None;

        public void RequestPush(PlayerRole target, Vector3 dir)
        {
            var p = NetPlayer.FindByRole(target);
            if (p == null || p == NetPlayer.Local) return;
            p.RPC_Push(LocalRole, dir);
        }

        public void RequestRescueShake(PlayerRole target)
        {
            var p = NetPlayer.FindByRole(target);
            if (p == null || p == NetPlayer.Local) return;
            p.RPC_RescueShake(LocalRole);
        }

        internal void RaisePushed(PlayerRole from, Vector3 dir) => Pushed?.Invoke(from, dir);
        internal void RaiseRescueShaken(PlayerRole from) => RescueShaken?.Invoke(from);
    }
}
