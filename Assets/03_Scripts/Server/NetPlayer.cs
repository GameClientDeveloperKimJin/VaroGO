using System.Collections.Generic;
using Fusion;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// Player_Net에 붙는 네트워크 래퍼(S-05, S-06). 게임플레이는 IPlayerSimulation(클라)이 하고,
    /// 여기서는 호출 순서와 역할 배정, 순간이동 보간 끊기만 한다 (계약 §3.5).
    /// P18: 로컬 이벤트(PlayerEvent)를 다른 PC로 중계하고, 밀기·구출 요청을 대상 플레이어의 PC로 보낸다.
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class NetPlayer : NetworkBehaviour
    {
        static readonly List<NetPlayer> all = new List<NetPlayer>();

        /// <summary>이 PC의 로컬 플레이어. 스폰되면 설정, 사라지면 해제.</summary>
        public static NetPlayer Local { get; private set; }

        [Networked] public PlayerRole Role { get; set; }

        IPlayerSimulation sim;
        NetworkTransform netTransform;

        void Awake()
        {
            sim = GetComponent<IPlayerSimulation>();
            netTransform = GetComponent<NetworkTransform>();
        }

        public static NetPlayer FindByRole(PlayerRole role)
        {
            if (role == PlayerRole.None) return null;
            foreach (var p in all)
                if (p != null && p.Object != null && p.Object.IsValid && p.Role == role) return p;
            return null;
        }

        public override void Spawned()
        {
            all.Add(this);
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

            // P18: 플레이어 밀기·구출 (오프라인 구현을 덮어쓴다)
            GameServices.RegisterSystems(null, null, null, NetPlayerInteraction.Instance);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            all.Remove(this);
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

        // P18: 내 시뮬레이션 이벤트 → 다른 PC의 같은 플레이어에서 재생 (애니메이션·연출)
        void OnLocalEvent(PlayerEvent e)
        {
            if (Object == null || !Object.IsValid) return;
            RPC_Event(e);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        void RPC_Event(PlayerEvent e) => sim.PlayRemoteEvent(e);

        // 밀린 사람(이 플레이어의 주인 PC)에서 판정한다 (R6)
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Push(PlayerRole from, Vector3 dir) => NetPlayerInteraction.Instance.RaisePushed(from, dir);

        // 묶인 사람(이 플레이어의 주인 PC)에서 횟수를 센다
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RescueShake(PlayerRole from) => NetPlayerInteraction.Instance.RaiseRescueShaken(from);
    }
}
