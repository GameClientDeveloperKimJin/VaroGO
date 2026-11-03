using System.Linq;
using Fusion;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 세션 공용 상태. 방에 들어간 직후 마스터가 스폰한다.
    /// 스폰 플래그 DontDestroyOnLoad(코스 씬으로 넘어가도 유지) + SharedModeStateAuthMasterClient(상태 권한 = 항상 현재 마스터,
    /// 마스터가 나가면 새 마스터로 넘어감). 프리팹: 02_Prefabs/Net/NetSession.prefab
    /// - 역할 선택·준비 (S2-11d): 01_Lobby에서 각자 Red/Blue 칸을 고르고 준비. 둘 다 준비하면 바로 코스 (S2-11e)
    /// - 역할 배정 (S2-05): PlayerRef ↔ 역할 칸. 나간 사람 칸은 비워 두고, 코스 중 다시 들어온 사람에게만 빈 역할을 자동으로 준다
    /// - 파트너 끊김 일시정지 (S2-04): 코스 중 혼자 남으면 시계 정지, 둘이 모이면 재개, 60초 넘으면 로비 대기로
    /// - 구역 리셋 (S2-07): 요청 → 상태 권한이 스위치를 되돌림 → 모든 PC에 ZoneReset
    /// 상태 권한 쪽 로직은 모두 FixedUpdateNetwork에서 돌아서, 마스터가 바뀌어도 새 마스터가 그대로 이어받는다.
    /// </summary>
    public class NetSession : NetworkBehaviour
    {
        public const float ReconnectTimeoutSeconds = 60f;
        const float RoleFallbackSeconds = 3f;

        public static NetSession Instance { get; private set; }

        // S2-05·S2-11d 역할 칸 (None = 비어 있음)과 칸별 준비
        [Networked] PlayerRef RedPlayer { get; set; }
        [Networked] PlayerRef BluePlayer { get; set; }
        [Networked] NetworkBool RedReady { get; set; }
        [Networked] NetworkBool BlueReady { get; set; }

        // S2-04 코스 진행 중 여부·일시정지
        [Networked] NetworkBool InCourse { get; set; }
        [Networked] NetworkBool PausedNet { get; set; }
        [Networked] int PauseStartTick { get; set; }
        [Networked] int PausedTicks { get; set; }

        bool spawned;
        // 상태 권한에서만: 나간 역할의 골 진입 표시를 지워야 하는지 (골 권한이 넘어올 때까지 다시 시도)
        bool pendingClearRed, pendingClearBlue;
        bool timeoutRequested;
        bool startRequested;

        /// <summary>코스가 진행 중인지 (로비 단계 = false).</summary>
        public bool IsInCourse => spawned && InCourse;

        /// <summary>파트너가 끊겨 시계가 멈춰 있는지.</summary>
        public bool Paused => spawned && PausedNet;

        /// <summary>멈춘 뒤 지난 초. 멈추지 않았으면 0.</summary>
        public float LostSeconds => Paused ? Mathf.Max(0, Runner.Tick.Raw - PauseStartTick) * Runner.DeltaTime : 0f;

        public override void Spawned()
        {
            spawned = true;
            Instance = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            spawned = false;
            if (Instance == this) Instance = null;
        }

        // ---- 역할 선택·준비 (S2-11d) ----

        /// <summary>그 플레이어에게 배정된 역할. 아직 없으면 None.</summary>
        public PlayerRole RoleOf(PlayerRef player)
        {
            if (!spawned || player == PlayerRef.None) return PlayerRole.None;
            if (RedPlayer == player) return PlayerRole.A_Red;
            if (BluePlayer == player) return PlayerRole.B_Blue;
            return PlayerRole.None;
        }

        /// <summary>그 역할 칸을 가진 플레이어. 비어 있으면 None.</summary>
        public PlayerRef OwnerOf(PlayerRole role)
        {
            if (!spawned) return PlayerRef.None;
            return role == PlayerRole.A_Red ? RedPlayer : role == PlayerRole.B_Blue ? BluePlayer : PlayerRef.None;
        }

        /// <summary>그 역할 칸이 준비했는지.</summary>
        public bool IsReady(PlayerRole role)
        {
            if (!spawned) return false;
            return role == PlayerRole.A_Red ? RedReady : role == PlayerRole.B_Blue && BlueReady;
        }

        /// <summary>로컬 PC에서 호출. 역할 칸 요청 (None = 선택 취소). 결과는 RoleOf로 확인.</summary>
        public void RequestRole(PlayerRole role)
        {
            if (!spawned) return;
            RPC_SelectRole((int)role, Runner.LocalPlayer);
        }

        /// <summary>로컬 PC에서 호출. 역할 칸을 가진 경우에만 처리된다. 결과는 IsReady로 확인.</summary>
        public void RequestReady(bool ready)
        {
            if (!spawned) return;
            RPC_SetReady(ready, Runner.LocalPlayer);
        }

        // 보낸 사람: RpcInfo.Source(확인 필요 #4)가 비어 있으면 인자로 받은 PlayerRef를 쓴다
        static PlayerRef SenderOf(RpcInfo info, PlayerRef fallback) => info.Source.IsRealPlayer ? info.Source : fallback;

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_SelectRole(int roleValue, PlayerRef sender, RpcInfo info = default)
        {
            var who = SenderOf(info, sender);
            var role = (PlayerRole)roleValue;
            Debug.Log($"[Net] 역할 선택 {who}→{role} (Source={info.Source}, 인자={sender})");
            if (InCourse)
            {
                Debug.Log("[Net] 역할 선택 거절: 코스 진행 중");
                return;
            }

            var current = RoleOf(who);
            if (role == current) return;

            if (role != PlayerRole.None)
            {
                var owner = OwnerOf(role);
                if (owner != PlayerRef.None && owner != who)
                {
                    Debug.Log($"[Net] 역할 선택 거절: {role} 칸은 {owner}");
                    return;
                }
            }

            // 본인의 다른 칸을 비우고 (준비도 같이 해제) 새 칸에 앉힌다. 새 칸은 준비 안 된 상태로 시작
            ClearSlot(current);
            if (role == PlayerRole.A_Red) { RedPlayer = who; RedReady = false; }
            else if (role == PlayerRole.B_Blue) { BluePlayer = who; BlueReady = false; }
            LogReady();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_SetReady(NetworkBool ready, PlayerRef sender, RpcInfo info = default)
        {
            var who = SenderOf(info, sender);
            var role = RoleOf(who);
            if (InCourse) return;
            if (role == PlayerRole.None)
            {
                Debug.Log($"[Net] 준비 거절: {who} 역할 없음");
                return;
            }
            if (role == PlayerRole.A_Red) RedReady = ready;
            else BlueReady = ready;
            LogReady();
        }

        void ClearSlot(PlayerRole role)
        {
            if (role == PlayerRole.A_Red) { RedPlayer = PlayerRef.None; RedReady = false; }
            else if (role == PlayerRole.B_Blue) { BluePlayer = PlayerRef.None; BlueReady = false; }
        }

        void LogReady() => Debug.Log($"[Net] 준비 red={(bool)RedReady}({RedPlayer}) blue={(bool)BlueReady}({BluePlayer})");

        // S2-11e: 상태 권한에서 매 틱. 2명, 두 칸 모두 차 있음, 둘 다 준비 → 1회 코스 시작
        void UpdateAutoStart()
        {
            bool allReady = !InCourse
                && Runner.ActivePlayers.Count() >= 2
                && RedPlayer != PlayerRef.None && BluePlayer != PlayerRef.None
                && RedReady && BlueReady;
            if (!allReady)
            {
                startRequested = false;
                return;
            }
            if (startRequested) return;
            startRequested = true;
            NetBootstrap.Instance?.StartCourseFromRoom();
        }

        /// <summary>NetSession이 끝내 안 올 때(프리팹 미연결 등) 쓰는 예전 규칙을 써도 되는 시간.</summary>
        public static float RoleFallbackDelay => RoleFallbackSeconds;

        void UpdateRoles()
        {
            var active = Runner.ActivePlayers.ToList();

            // 나간 사람 칸·준비 비우기. 그 역할의 골 진입 표시도 지운다 (C-1031-2 ③)
            if (RedPlayer != PlayerRef.None && !active.Contains(RedPlayer))
            {
                Debug.Log($"[Net] 역할 칸 비움: A_Red ({RedPlayer})");
                ClearSlot(PlayerRole.A_Red);
                pendingClearRed = true;
                LogReady();
            }
            if (BluePlayer != PlayerRef.None && !active.Contains(BluePlayer))
            {
                Debug.Log($"[Net] 역할 칸 비움: B_Blue ({BluePlayer})");
                ClearSlot(PlayerRole.B_Blue);
                pendingClearBlue = true;
                LogReady();
            }

            // 빈 역할 자동 배정은 코스 중에만 (S2-05 재접속). 로비에서는 각자 고른다 (S2-11d)
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
                Debug.Log($"[Net] 재접속 대기 {ReconnectTimeoutSeconds}초 초과 → 01_Lobby로");
                RedReady = false;
                BlueReady = false;
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

            // 나간 사람의 칸·준비는 UpdateRoles가 비운다 (S2-02·S2-11d)
            UpdateRoles();
            UpdatePause();
            UpdateAutoStart();
        }
    }
}
