using System;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 오프라인 비행 상태. 이 PC가 항상 권한을 가진다. 온라인 Net Variant에서는 옆에 NetFlightState가 붙는다.
    // 설정값(최대 내구도)은 이 컴포넌트 한 곳에만 둔다. NetFlightState도 여기서 읽는다
    public class LocalFlightState : MonoBehaviour, IFlightState
    {
        [Tooltip("뗏목 최대 내구도 (부딪힐 때마다 1씩 깎인다)")]
        [SerializeField, Min(1)] int maxDurability = 4;

        public int MaxDurability => maxDurability;

        FlightPhase phase = FlightPhase.Idle;
        double phaseStart;
        int durability = -1;
        int attempt;
        float lateral, lateralVelocity;
        bool redBoarded, blueBoarded, redPressing, bluePressing;

        public bool HasAuthority => true;
        public FlightPhase Phase => phase;
        public double PhaseStartTime => phaseStart;
        public int Durability => durability < 0 ? maxDurability : durability;
        public int Attempt => attempt;
        public float Lateral => lateral;
        public float LateralVelocity => lateralVelocity;
        public event Action Changed;
        public event Action SummonRequested;

        public bool IsBoarded(PlayerRole role) => role == PlayerRole.A_Red ? redBoarded : role == PlayerRole.B_Blue && blueBoarded;
        public bool IsPressing(PlayerRole role) => role == PlayerRole.A_Red ? redPressing : role == PlayerRole.B_Blue && bluePressing;

        public void RequestSummon(PlayerRole requester) => SummonRequested?.Invoke();

        public void ReportBoarded(PlayerRole role, bool boarded)
        {
            if (role == PlayerRole.A_Red) redBoarded = boarded;
            else if (role == PlayerRole.B_Blue) blueBoarded = boarded;
        }

        public void ReportPressing(PlayerRole role, bool pressing)
        {
            if (role == PlayerRole.A_Red) redPressing = pressing;
            else if (role == PlayerRole.B_Blue) bluePressing = pressing;
        }

        public void SetPhase(FlightPhase newPhase, double startTime, int newDurability, int newAttempt)
        {
            bool changed = newPhase != phase || startTime != phaseStart || newDurability != Durability || newAttempt != attempt;
            phase = newPhase;
            phaseStart = startTime;
            durability = newDurability;
            attempt = newAttempt;
            if (changed) Changed?.Invoke();
        }

        public void SetLateral(float value, float velocity)
        {
            lateral = value;
            lateralVelocity = velocity;
        }
    }
}
