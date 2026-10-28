using Fusion;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// Player_Net에 붙는 네트워크 래퍼(S-05, S-06). 게임플레이는 IPlayerSimulation(클라)이 하고,
    /// 여기서는 호출 순서와 역할 배정, 순간이동 보간 끊기만 한다 (계약 §3.5).
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class NetPlayer : NetworkBehaviour
    {
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

        public override void Spawned()
        {
            // 계약 §2: OfflinePlayerDriver가 먼저 SetLocal을 불렀어도 덮어쓴다
            sim.SetLocal(HasStateAuthority);
            if (!HasStateAuthority) return;

            Local = this;
            sim.Teleported += OnTeleported;

            // Master = A_Red, 나머지 = B_Blue. Solo(Single)는 항상 A
            bool isA = Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient;
            Role = isA ? PlayerRole.A_Red : PlayerRole.B_Blue;
            NetBootstrap.Instance?.SetLocalRole(Role);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (sim != null) sim.Teleported -= OnTeleported;
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
    }
}
