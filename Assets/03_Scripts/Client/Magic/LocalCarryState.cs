using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18: 오프라인 줍기·던지기 상태. 이 PC가 항상 권한이다. 온라인은 서버의 NetCarryState가 대신한다
    public class LocalCarryState : MonoBehaviour, ICarryState
    {
        PlayerRole holder;

        public bool HasAuthority => true;
        public PlayerRole Holder => holder;
        public event Action HolderChanged;
        public event Action<Vector3, Vector3> Thrown;

        public void RequestPickUp(PlayerRole role)
        {
            if (holder != PlayerRole.None || role == PlayerRole.None) return;
            holder = role;
            HolderChanged?.Invoke();
        }

        public void RequestThrow(PlayerRole role, Vector3 origin, Vector3 velocity)
        {
            if (holder == PlayerRole.None || holder != role) return;
            holder = PlayerRole.None;
            HolderChanged?.Invoke();
            Thrown?.Invoke(origin, velocity);
        }
    }
}
