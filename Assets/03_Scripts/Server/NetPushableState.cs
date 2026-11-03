using System;
using Fusion;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// P18 온라인 미는 오브젝트 상태(계약 §3). Net Variant에서 LocalPushableState 옆에 추가된다.
    /// 방장(상태 권한)이 미는 보고를 모아 Pushable(클라)이 움직이고, 위치는 NetworkTransform이 맞춘다.
    /// </summary>
    [RequireComponent(typeof(LocalPushableState))]
    public class NetPushableState : NetworkBehaviour, IPushableState
    {
        bool spawned;

        public bool HasAuthority => spawned && Object != null && Object.IsValid && HasStateAuthority;
        public event Action<PlayerRole, Vector3> PushReported;

        public override void Spawned() => spawned = true;
        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        public void ReportPush(PlayerRole role, Vector3 dir)
        {
            if (!spawned || Object == null || !Object.IsValid) return;
            if (HasStateAuthority) PushReported?.Invoke(role, dir);
            else RPC_Report(role, dir);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Report(PlayerRole role, Vector3 dir) => PushReported?.Invoke(role, dir);
    }
}
