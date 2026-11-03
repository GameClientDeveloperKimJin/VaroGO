using System;
using Fusion;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// P18 온라인 줍기·던지기 상태(계약 §3). Net Variant에서 LocalCarryState 옆에 추가된다.
    /// 누가 들고 있는지는 Networked 값, 날아가는 물리는 방장(상태 권한)이 돌리고 위치는 NetworkTransform이 맞춘다.
    /// </summary>
    [RequireComponent(typeof(LocalCarryState))]
    public class NetCarryState : NetworkBehaviour, ICarryState, IStateAuthorityChanged
    {
        [Networked, OnChangedRender(nameof(OnHolderNetChanged))] PlayerRole HolderNet { get; set; }

        bool spawned;
        PlayerRole observed;

        public bool HasAuthority => spawned && Object != null && Object.IsValid && HasStateAuthority;
        public PlayerRole Holder => observed;
        public event Action HolderChanged;
        public event Action<Vector3, Vector3> Thrown;

        public override void Spawned()
        {
            spawned = true;
            observed = HolderNet;
            HolderChanged?.Invoke(); // 권한이 정해졌으니 물리 다시 적용
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        // 방장이 나가 권한이 넘어오면 물리를 다시 적용
        public void StateAuthorityChanged() => HolderChanged?.Invoke();

        public void RequestPickUp(PlayerRole role)
        {
            if (!spawned || Object == null || !Object.IsValid || role == PlayerRole.None) return;
            RPC_PickUp(role);
        }

        public void RequestThrow(PlayerRole role, Vector3 origin, Vector3 velocity)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            RPC_Throw(role, origin, velocity);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_PickUp(PlayerRole role)
        {
            if (HolderNet != PlayerRole.None) return; // 먼저 온 사람이 가져간다
            HolderNet = role;
            Sync();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Throw(PlayerRole role, Vector3 origin, Vector3 velocity)
        {
            if (HolderNet == PlayerRole.None || HolderNet != role) return;
            HolderNet = PlayerRole.None;
            Sync();
            Thrown?.Invoke(origin, velocity);
        }

        void OnHolderNetChanged() => Sync();

        void Sync()
        {
            var h = HolderNet;
            if (observed == h) return;
            observed = h;
            HolderChanged?.Invoke();
        }
    }
}
