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
        VaroGO.Client.PlayerController controller;
        NetworkTransform netTransform;
        float lastPushTime = float.NegativeInfinity;

        void Awake()
        {
            sim = GetComponent<IPlayerSimulation>();
            controller = GetComponent<VaroGO.Client.PlayerController>();
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

            spawnedAt = Time.unscaledTime;
            TryAssignRole();
        }

        // S2-05: 역할은 NetSession의 역할 칸에서 받는다(재접속하면 비어 있던 역할). Solo는 항상 A.
        // 칸이 아직 안 왔으면 None으로 두고(계약 §3.1 허용) 틱마다 다시 본다. 한 번 정하면 바꾸지 않는다.
        float spawnedAt;

        void TryAssignRole()
        {
            if (Role != PlayerRole.None) return;
            PlayerRole value;
            if (Runner.GameMode == GameMode.Single) value = PlayerRole.A_Red;
            else if (NetSession.Instance != null) value = NetSession.Instance.RoleOf(Runner.LocalPlayer);
            else if (Time.unscaledTime - spawnedAt >= NetSession.RoleFallbackDelay)
            {
                // NetSession이 끝내 없을 때(프리팹 미연결 등) 예전 규칙: 마스터 = A
                value = Runner.IsSharedModeMasterClient ? PlayerRole.A_Red : PlayerRole.B_Blue;
                Debug.LogWarning($"[Net] NetSession 없음 → 마스터 기준으로 역할 {value}");
            }
            else value = PlayerRole.None;

            if (value == PlayerRole.None) return;
            Role = value;
            Debug.Log($"[Net] 내 역할 = {value}");
            NetBootstrap.Instance?.SetLocalRole(value);
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
            if (!HasStateAuthority) return;
            TryAssignRole();
            ApplyPendingTeleport();
            inTick = true;
            try { sim.Simulate(Runner.DeltaTime); }
            finally { inTick = false; }
        }

        // 순간이동은 틱 안에서 다시 적용한다.
        // Fusion 2.1 NetworkTransform은 상태 권한 PC에서도 틱 전에 transform을 마지막 시뮬레이션 값으로 되돌린다.
        // 그래서 틱 밖(Update·OnGUI: 부활, 빠른 이동, 디버그 move)에서 한 Teleport는 되돌려질 수 있다 (2026-11-03 MPPM에서 확인).
        // 틱 밖 Teleport는 기억해 두고, 다음 FixedUpdateNetwork에서 PlayerController.Teleport(§5 화이트리스트)로 다시 적용한다.
        bool hasPendingTeleport;
        bool applyingTeleport;
        bool inTick;
        Vector3 pendingPosition;
        Quaternion pendingRotation;

        void OnTeleported(Vector3 position)
        {
            if (applyingTeleport || inTick)
            {
                // 틱 안: 바로 확정하고 보간을 끊는다
                netTransform.Teleport(position, transform.rotation);
                return;
            }
            hasPendingTeleport = true;
            pendingPosition = position;
            pendingRotation = transform.rotation;
        }

        void ApplyPendingTeleport()
        {
            if (!hasPendingTeleport) return;
            hasPendingTeleport = false;
            applyingTeleport = true;
            try { controller.Teleport(pendingPosition, pendingRotation); } // 다시 OnTeleported → netTransform.Teleport
            finally { applyingTeleport = false; }
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
