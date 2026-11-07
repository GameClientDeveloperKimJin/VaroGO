using System.Linq;
using Fusion;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 세션 공용 상태. 방을 만들면(LobbyScene) 마스터가 스폰한다.
    /// 스폰 플래그 DontDestroyOnLoad(코스 씬으로 넘어가도 유지) + SharedModeStateAuthMasterClient(상태 권한 = 항상 현재 마스터,
    /// 마스터가 나가면 새 마스터로 넘어감). 프리팹: 02_Prefabs/Net/NetSession.prefab
    /// - 대기실 팀·준비 (로비 기획서 p10·p11): 팀 = 역할 칸(레드 = A, 블루 = B). 요청은 RPC, 먼저 온 요청만 승인
    /// - 역할 배정 (S2-05): 코스 중 다시 들어온 사람에게 빈 역할을 자동으로 준다. 대기실에서는 자동 배정하지 않는다
    /// - 파트너 끊김 일시정지 (S2-04): 코스 중 혼자 남으면 시계 정지, 둘이 모이면 재개, 60초 넘으면 로비 대기로
    /// - 구역 리셋 (S2-07): 요청 → 상태 권한이 스위치를 되돌림 → 모든 PC에 ZoneReset
    /// 상태 권한 쪽 로직은 모두 FixedUpdateNetwork에서 돌아서, 마스터가 바뀌어도 새 마스터가 그대로 이어받는다.
    /// </summary>
    public class NetSession : NetworkBehaviour
    {
        public const float ReconnectTimeoutSeconds = 60f;
        const float RoleFallbackSeconds = 3f;

        public static NetSession Instance { get; private set; }

        // 역할 칸 = 대기실 팀 칸 (None = 비어 있음)
        [Networked] PlayerRef RedPlayer { get; set; }
        [Networked] PlayerRef BluePlayer { get; set; }

        // 대기실 준비 (팀 칸별)
        [Networked] NetworkBool ReadyRed { get; set; }
        [Networked] NetworkBool ReadyBlue { get; set; }

        // 대기실 P1(방장) 슬롯 표시용. 해산 판정에는 쓰지 않는다 (NetBootstrap.IsRoomHost)
        [Networked] PlayerRef HostPlayerNet { get; set; }

        // S2-04 코스 진행 중 여부·일시정지
        [Networked] NetworkBool InCourse { get; set; }
        [Networked] NetworkBool PausedNet { get; set; }
        [Networked] int PauseStartTick { get; set; }
        [Networked] int PausedTicks { get; set; }

        bool spawned;
        // 상태 권한에서만: 나간 역할의 골 진입 표시를 지워야 하는지 (골 권한이 넘어올 때까지 다시 시도)
        bool pendingClearRed, pendingClearBlue;
        bool timeoutRequested;

        /// <summary>파트너가 끊겨 시계가 멈춰 있는지.</summary>
        public bool Paused => spawned && PausedNet;

        /// <summary>멈춘 뒤 지난 초. 멈추지 않았으면 0.</summary>
        public float LostSeconds => Paused ? Mathf.Max(0, Runner.Tick.Raw - PauseStartTick) * Runner.DeltaTime : 0f;

        public override void Spawned()
        {
            spawned = true;
            Instance = this;
            // 스폰하는 사람 = 방을 만든 마스터
            if (HasStateAuthority && HostPlayerNet == PlayerRef.None) HostPlayerNet = Runner.LocalPlayer;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            spawned = false;
            if (Instance == this) Instance = null;
        }

        // ---- 대기실 팀·준비 (로비 기획서 p10·p11) ----

        /// <summary>대기실 P1(방장) 슬롯에 둘 플레이어. 스폰 전에는 None.</summary>
        public PlayerRef HostPlayer => spawned ? HostPlayerNet : PlayerRef.None;

        /// <summary>그 플레이어가 준비했는지. 팀이 없으면 false.</summary>
        public bool IsReady(PlayerRef player)
        {
            switch (RoleOf(player))
            {
                case PlayerRole.A_Red: return ReadyRed;
                case PlayerRole.B_Blue: return ReadyBlue;
                default: return false;
            }
        }

        /// <summary>두 팀이 모두 차 있고 둘 다 준비. 방장 시작 버튼 조건.</summary>
        public bool BothReady => spawned && RedPlayer != PlayerRef.None && BluePlayer != PlayerRef.None && ReadyRed && ReadyBlue;

        /// <summary>내 팀 선택 요청. 상대가 가진 팀이거나 준비 중이면 상태 권한이 거절한다.</summary>
        public void RequestTeam(PlayerRole team)
        {
            if (!spawned) return;
            RPC_RequestTeam((int)team);
        }

        /// <summary>내 준비 요청. 팀이 없거나 2명 미만이면 상태 권한이 거절한다.</summary>
        public void RequestReady(bool ready)
        {
            if (!spawned) return;
            RPC_SetReady(ready);
        }

        /// <summary>상태 권한에서만. 재접속 시간 초과로 대기실에 돌아온 남은 사람을 P1(방장) 슬롯에 둔다.</summary>
        public void SetHostToSelf()
        {
            if (spawned && HasStateAuthority) HostPlayerNet = Runner.LocalPlayer;
        }

        // 상태 권한이 자기 자신에게 보낸 RPC는 Source가 비어 있을 수 있다 → 내 PlayerRef로 본다
        PlayerRef SourceOf(RpcInfo info) => info.Source == PlayerRef.None ? Runner.LocalPlayer : info.Source;

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_RequestTeam(int team, RpcInfo info = default)
        {
            var player = SourceOf(info);
            var want = (PlayerRole)team;
            if (InCourse || (want != PlayerRole.A_Red && want != PlayerRole.B_Blue)) return;
            if (IsReady(player)) return;                          // 준비 중엔 팀 변경 불가 (p10 ④)
            var current = RoleOf(player);
            if (current == want) return;
            if ((want == PlayerRole.A_Red ? RedPlayer : BluePlayer) != PlayerRef.None)
            {
                Debug.Log($"[Net] 팀 요청 거절: {player} → {want} (이미 선택됨)");
                return;                                           // 먼저 온 요청만 승인 (p11 동시 클릭)
            }
            if (current == PlayerRole.A_Red) RedPlayer = PlayerRef.None;
            else if (current == PlayerRole.B_Blue) BluePlayer = PlayerRef.None;
            if (want == PlayerRole.A_Red) RedPlayer = player;
            else BluePlayer = player;
            Debug.Log($"[Net] 팀 선택: {player} → {want}");
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_SetReady(NetworkBool ready, RpcInfo info = default)
        {
            var role = RoleOf(SourceOf(info));
            if (ready && (role == PlayerRole.None || Runner.ActivePlayers.Count() < 2)) return;
            if (role == PlayerRole.A_Red) ReadyRed = ready;
            else if (role == PlayerRole.B_Blue) ReadyBlue = ready;
        }

        void ClearReadyAll()
        {
            ReadyRed = false;
            ReadyBlue = false;
        }

        // ---- 역할 (S2-05) ----

        /// <summary>그 플레이어에게 배정된 역할. 아직 없으면 None.</summary>
        public PlayerRole RoleOf(PlayerRef player)
        {
            if (!spawned || player == PlayerRef.None) return PlayerRole.None;
            if (RedPlayer == player) return PlayerRole.A_Red;
            if (BluePlayer == player) return PlayerRole.B_Blue;
            return PlayerRole.None;
        }

        /// <summary>NetSession이 끝내 안 올 때(프리팹 미연결 등) 쓰는 예전 규칙을 써도 되는 시간.</summary>
        public static float RoleFallbackDelay => RoleFallbackSeconds;

        void UpdateRoles()
        {
            var active = Runner.ActivePlayers.ToList();

            // 나간 사람 칸 비우기. 그 역할의 골 진입 표시도 지운다 (C-1031-2 ③)
            if (RedPlayer != PlayerRef.None && !active.Contains(RedPlayer))
            {
                Debug.Log($"[Net] 역할 칸 비움: A_Red ({RedPlayer})");
                RedPlayer = PlayerRef.None;
                ReadyRed = false;
                pendingClearRed = true;
            }
            if (BluePlayer != PlayerRef.None && !active.Contains(BluePlayer))
            {
                Debug.Log($"[Net] 역할 칸 비움: B_Blue ({BluePlayer})");
                BluePlayer = PlayerRef.None;
                ReadyBlue = false;
                pendingClearBlue = true;
            }

            // 빈 역할 자동 배정은 코스 중(S2-05 재접속)에만. 대기실에서는 각자 팀을 고른다 (로비 기획서 p10)
            if (InCourse)
            {
                Assign(Runner.LocalPlayer);
                foreach (var p in active.OrderBy(p => p.PlayerId))
                    Assign(p);
            }

            if (pendingClearRed && NetGoalState.ClearRoleAll(PlayerRole.A_Red)) pendingClearRed = false;
            if (pendingClearBlue && NetGoalState.ClearRoleAll(PlayerRole.B_Blue)) pendingClearBlue = false;
        }

        void Assign(PlayerRef p)
        {
            if (p == PlayerRef.None || RoleOf(p) != PlayerRole.None) return;
            if (RedPlayer == PlayerRef.None) RedPlayer = p;
            else if (BluePlayer == PlayerRef.None) BluePlayer = p;
            else return;
            Debug.Log($"[Net] 역할 배정: {p} → {RoleOf(p)}");
        }

        // ---- 일시정지·공유 시계 (S2-04) ----

        /// <summary>상태 권한에서만. 호스트가 코스를 시작할 때 부른다.</summary>
        public void MarkInCourse(bool value)
        {
            if (!spawned || !HasStateAuthority) return;
            if (!value) Resume();
            InCourse = value;
            timeoutRequested = false;
        }

        /// <summary>
        /// NetGameClock이 부른다. SimulationTime에서 멈춰 있던 시간을 뺀다.
        /// 멈춘 동안은 멈춘 순간 값에 고정, 재개하면 그 값에서 이어진다(튀지 않음).
        /// </summary>
        public double AdjustClock(double simulationTime)
        {
            if (!spawned) return simulationTime;
            double dt = Runner.DeltaTime;
            double t = PausedNet ? System.Math.Min(simulationTime, PauseStartTick * dt) : simulationTime;
            return t - PausedTicks * dt;
        }

        void UpdatePause()
        {
            int players = Runner.ActivePlayers.Count();
            if (InCourse && !PausedNet && players < 2)
            {
                PausedNet = true;
                PauseStartTick = Runner.Tick.Raw;
                Debug.Log($"[Net] 파트너 끊김 → 일시정지 (tick={PauseStartTick})");
            }
            else if (PausedNet && players >= 2
                     && NetPlayer.Find(PlayerRole.A_Red) != null && NetPlayer.Find(PlayerRole.B_Blue) != null)
            {
                // 다시 들어온 사람이 코스에 스폰돼 역할까지 받은 뒤 재개
                Resume();
                Debug.Log($"[Net] 파트너 재접속 → 재개 (멈춘 틱 합계={PausedTicks})");
            }

            if (PausedNet && !timeoutRequested && LostSeconds >= ReconnectTimeoutSeconds)
            {
                timeoutRequested = true;
                Debug.Log($"[Net] 재접속 대기 {ReconnectTimeoutSeconds}초 초과 → 로비 대기로");
                ClearReadyAll();
                InCourse = false;
                Resume();
                NetBootstrap.Instance?.ReturnToLobbyAfterTimeout();
            }
        }

        void Resume()
        {
            if (!PausedNet) return;
            PausedTicks += Mathf.Max(0, Runner.Tick.Raw - PauseStartTick);
            PausedNet = false;
        }

        // ---- 구역 리셋 (S2-07) ----

        /// <summary>리셋 장치를 쓴 로컬 PC에서 호출 (NetZoneReset).</summary>
        public void RequestZoneReset(int zone)
        {
            if (!spawned) return;
            RPC_ZoneReset(zone);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_ZoneReset(int zone)
        {
            int count = NetSwitchState.ResetZone(zone);
            Debug.Log($"[Net] 구역 리셋 처리 zone={zone} 스위치 {count}개");
            RPC_ZoneResetDone(zone);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_ZoneResetDone(int zone)
        {
            NetZoneReset.Raise(zone);
        }

        // ----

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // 혼자 남으면 준비 해제 (p11 "P2 이탈 → 블루 · 준비 해제")
            if ((ReadyRed || ReadyBlue) && Runner.ActivePlayers.Count() < 2)
                ClearReadyAll();

            UpdateRoles();
            UpdatePause();
        }
    }
}
