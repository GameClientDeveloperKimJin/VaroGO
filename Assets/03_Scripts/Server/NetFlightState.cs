using System;
using Fusion;
using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// P19 온라인 공중 비행 상태. Net Variant에서 LocalFlightState 옆에 추가된다 (로컬 구현은 지우지 않음).
    /// 상태 권한(마스터 클라이언트, Is Master Client Object)이 이동·충돌·내구도·성공을 판정하고 값을 쓴다.
    /// 다른 PC는 탑승·A/D 입력·소환 요청을 RPC로 보낸다. 설정값(최대 내구도)은 LocalFlightState에서 읽는다.
    /// Spawned 전에는 LocalFlightState 값을 돌려주고 권한도 없다.
    /// </summary>
    [RequireComponent(typeof(LocalFlightState))]
    public class NetFlightState : NetworkBehaviour, IFlightState
    {
        [Networked] FlightPhase PhaseNet { get; set; }
        [Networked] double PhaseStartNet { get; set; }
        [Networked] int DurabilityNet { get; set; }
        [Networked] int AttemptNet { get; set; }
        [Networked] float LateralNet { get; set; }
        [Networked] float LateralVelocityNet { get; set; }
        [Networked] double SegmentStartNet { get; set; }
        [Networked] float SegmentDistanceNet { get; set; }
        [Networked] NetworkBool RedBoarded { get; set; }
        [Networked] NetworkBool BlueBoarded { get; set; }
        [Networked] NetworkBool RedPressing { get; set; }
        [Networked] NetworkBool BluePressing { get; set; }
        [Networked] NetworkBool Initialized { get; set; }

        LocalFlightState local;
        bool spawned;
        // Changed는 값이 바뀐 것을 본 PC마다 한 번 (권한 PC는 SetPhase에서 바로)
        FlightPhase seenPhase;
        double seenStart;
        int seenDurability, seenAttempt;

        LocalFlightState Local => local != null ? local : local = GetComponent<LocalFlightState>();
        bool Ready => spawned && Object != null && Object.IsValid;

        public bool HasAuthority => Ready && HasStateAuthority;
        public FlightPhase Phase => Ready ? PhaseNet : Local.Phase;
        public double PhaseStartTime => Ready ? PhaseStartNet : Local.PhaseStartTime;
        public int Durability => Ready ? DurabilityNet : Local.Durability;
        public int Attempt => Ready ? AttemptNet : Local.Attempt;
        public float Lateral => Ready ? LateralNet : Local.Lateral;
        public float LateralVelocity => Ready ? LateralVelocityNet : Local.LateralVelocity;
        public double SegmentStartTime => Ready ? SegmentStartNet : Local.SegmentStartTime;
        public float SegmentStartDistance => Ready ? SegmentDistanceNet : Local.SegmentStartDistance;
        public event Action Changed;
        public event Action SummonRequested;

        public override void Spawned()
        {
            if (HasStateAuthority && !Initialized)
            {
                PhaseNet = FlightPhase.Idle;
                PhaseStartNet = Runner.SimulationTime;
                DurabilityNet = Local.MaxDurability;
                AttemptNet = 0;
                Initialized = true;
            }
            spawned = true;
            Observe(); // 늦게 들어온 PC: 이미 진행 중이면 여기서 Changed 1회
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => spawned = false;

        public bool IsBoarded(PlayerRole role)
        {
            if (!Ready) return Local.IsBoarded(role);
            return role == PlayerRole.A_Red ? RedBoarded : role == PlayerRole.B_Blue && BlueBoarded;
        }

        public bool IsPressing(PlayerRole role)
        {
            if (!Ready) return Local.IsPressing(role);
            return role == PlayerRole.A_Red ? RedPressing : role == PlayerRole.B_Blue && BluePressing;
        }

        // ---- 로컬 PC → 권한 PC ----

        public void RequestSummon(PlayerRole requester)
        {
            if (!Ready) return;
            if (HasStateAuthority) SummonRequested?.Invoke();
            else RPC_Summon(requester);
        }

        public void ReportBoarded(PlayerRole role, bool boarded)
        {
            if (!Ready) return;
            if (HasStateAuthority) ApplyBoarded(role, boarded);
            else RPC_Boarded(role, boarded);
        }

        public void ReportPressing(PlayerRole role, bool pressing)
        {
            if (!Ready) return;
            if (HasStateAuthority) ApplyPressing(role, pressing);
            else RPC_Pressing(role, pressing);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Summon(PlayerRole requester) => SummonRequested?.Invoke();

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Boarded(PlayerRole role, NetworkBool boarded) => ApplyBoarded(role, boarded);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Pressing(PlayerRole role, NetworkBool pressing) => ApplyPressing(role, pressing);

        void ApplyBoarded(PlayerRole role, bool boarded)
        {
            if (role == PlayerRole.A_Red) RedBoarded = boarded;
            else if (role == PlayerRole.B_Blue) BlueBoarded = boarded;
        }

        void ApplyPressing(PlayerRole role, bool pressing)
        {
            if (role == PlayerRole.A_Red) RedPressing = pressing;
            else if (role == PlayerRole.B_Blue) BluePressing = pressing;
        }

        // ---- 권한 PC만 ----

        public void SetPhase(FlightPhase phase, double startTime, int durability, int attempt)
        {
            if (!HasAuthority) return;
            PhaseNet = phase;
            PhaseStartNet = startTime;
            DurabilityNet = durability;
            AttemptNet = attempt;
            if (phase == FlightPhase.Idle || phase == FlightPhase.Summoning || phase == FlightPhase.Countdown && attempt == 0)
                RedPressing = BluePressing = false;
            Observe();
        }

        public void SetSegment(double startTime, float distance)
        {
            if (!HasAuthority) return;
            SegmentStartNet = startTime;
            SegmentDistanceNet = distance;
        }

        public void SetLateral(float lateral, float velocity)
        {
            if (!HasAuthority) return;
            LateralNet = lateral;
            LateralVelocityNet = velocity;
        }

        public override void Render() => Observe();

        void Observe()
        {
            if (!Ready) return;
            var p = PhaseNet; var s = PhaseStartNet; int d = DurabilityNet, a = AttemptNet;
            if (p == seenPhase && s == seenStart && d == seenDurability && a == seenAttempt) return;
            seenPhase = p; seenStart = s; seenDurability = d; seenAttempt = a;
            Changed?.Invoke();
        }
    }
}
