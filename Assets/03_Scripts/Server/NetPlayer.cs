using System.Collections.Generic;
using Fusion;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// Player_Net에 붙는 네트워크 래퍼(S-05, S-06). 게임플레이는 IPlayerSimulation(클라)이 하고,
    /// 여기서는 호출 순서와 역할 배정, 순간이동 보간 끊기, 원격 이벤트·밀기·핑 전달만 한다 (계약 §3.5, §3.9, §3.11).
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class NetPlayer : NetworkBehaviour
    {
        const float MinPushInterval = 0.5f; // S2-10: 밀린 PC에서 이보다 짧은 간격의 밀기는 버린다

        /// <summary>이 PC의 로컬 플레이어. 스폰되면 설정, 사라지면 해제.</summary>
        public static NetPlayer Local { get; private set; }

        /// <summary>이 PC에 스폰돼 있는 모든 플레이어(로컬·원격).</summary>
        public static readonly List<NetPlayer> All = new List<NetPlayer>();

        [Networked] public PlayerRole Role { get; set; }

        IPlayerSimulation sim;
        NetworkTransform netTransform;
        float lastPushTime = float.NegativeInfinity;

        void Awake()
        {
            sim = GetComponent<IPlayerSimulation>();
            netTransform = GetComponent<NetworkTransform>();
        }

        public override void Spawned()
        {
            All.Add(this);

            // 계약 §2: OfflinePlayerDriver가 먼저 SetLocal을 불렀어도 덮어쓴다
            sim.SetLocal(HasStateAuthority);
            if (!HasStateAuthority) return;

            Local = this;
            sim.Teleported += OnTeleported;
            sim.EventRaised += OnLocalEvent;

            // Master = A_Red, 나머지 = B_Blue. Solo(Single)는 항상 A
            bool isA = Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient;
            Role = isA ? PlayerRole.A_Red : PlayerRole.B_Blue;
            NetBootstrap.Instance?.SetLocalRole(Role);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            if (sim != null)
            {
                sim.Teleported -= OnTeleported;
                sim.EventRaised -= OnLocalEvent;
            }
            if (Local == this) Local = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority) sim.Simulate(Runner.DeltaTime);
        }

        // PlayerController.Teleport가 위치·회전을 먼저 적용한 뒤 이벤트를 낸다 (확인 필요: Fusion 2.1.3 NetworkTransform.Teleport 동작)
        void OnTeleported(Vector3 position)
        {
            netTransform.Teleport(position, transform.rotation);
        }

        // ---- 원격 이벤트 (계약 §3.5) ----
        // 로컬 플레이어(상태 권한)에서 난 이벤트만 보낸다. 원격 쪽 PlayRemoteEvent가 다시 낸 이벤트는 구독하지 않으므로 되돌아오지 않는다

        void OnLocalEvent(PlayerEvent e)
        {
            RPC_RemoteEvent(e);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
        void RPC_RemoteEvent(PlayerEvent e)
        {
            sim.PlayRemoteEvent(e);
        }

        // ---- 밀기 (S2-09·10, 계약 §3.9) ----

        /// <summary>role 역할의 플레이어를 찾는다(스폰된 것만). 없으면 null.</summary>
        public static NetPlayer Find(PlayerRole role)
        {
            foreach (var p in All)
                if (p != null && p.Object != null && p.Object.IsValid && p.Role == role) return p;
            return null;
        }

        /// <summary>미는 PC에서 호출. 대상 플레이어의 상태 권한(= 밀린 사람 PC)으로 보낸다.</summary>
        public void SendPush(PlayerRole from, Vector3 dir)
        {
            RPC_Push(from, dir);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Push(PlayerRole from, Vector3 dir)
        {
            float now = Time.unscaledTime;
            if (now - lastPushTime < MinPushInterval) return;
            lastPushTime = now;
            NetPlayerInteraction.RaisePushed(from, dir);
        }

        // ---- 핑 (C-1101-3, 계약 §3.11) ----

        /// <summary>핑을 찍은 PC의 로컬 플레이어에서 호출. 보낸 PC 포함 모든 PC에서 PingReceived 1회.</summary>
        public void SendPing(PingType type, Vector3 position, Vector3 normal, PlayerRole sender)
        {
            RPC_Ping(type, position, normal, sender);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_Ping(PingType type, Vector3 position, Vector3 normal, PlayerRole sender)
        {
            NetPingSystem.RaiseReceived(type, position, normal, sender);
        }
    }
}
