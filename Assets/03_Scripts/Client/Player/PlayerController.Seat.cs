using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19: 탈것 좌석 고정. 앉아 있는 동안 이동·점프·중력·충돌을 하지 않고 좌석 Transform에 붙는다.
    // 위치는 그대로 transform에 쓰므로 온라인에서는 NetworkTransform이 상대 PC에 보낸다.
    public partial class PlayerController
    {
        Transform seat;
        bool seatPose = true; // false = 좌석에 붙어 이동만 하고 앉는 애니는 안 함 (예: 지름길 버섯 비행)

        public bool IsSeated => seat != null;
        public Transform Seat => seat;

        // 연출 중 입력 잠금 (예: 비행 승강장 소환 연출). 켜져 있으면 이동·점프·상호작용을 받지 않고 제자리에 서 있는다 (중력은 받음).
        // 잠그는 쪽(FlightCourse 등)이 매 프레임 정하고, 끝나면 false로 돌린다
        public bool InputLocked { get; set; }

        void SimulateInputLocked(float deltaTime)
        {
            input.ConsumeJump();
            ResetProne();
            horizontalVelocity = Vector3.zero;
            pushVelocity = Vector3.zero;
            UpdateVertical(deltaTime);
            if (cc != null && cc.enabled) cc.Move(Vector3.up * verticalVelocity * deltaTime);
            UpdateAnimator();
        }

        // target = null이면 내린다. 내린 뒤 위치는 호출한 쪽이 Teleport로 정한다
        public void SetSeat(Transform target) => SetSeat(target, true);

        public void SetSeat(Transform target, bool sitPose)
        {
            if (target != null) seatPose = sitPose;
            if (seat == target) return;
            seat = target;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            pushVelocity = Vector3.zero;
            knockedBack = false;
            jumping = false;
            groundObstacle = null;
            if (cc != null) cc.enabled = target == null;
            if (target != null) StickToSeat();
            grounded = true;
        }

        void SimulateSeated()
        {
            input.ConsumeJump();
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            grounded = true;
            StickToSeat();
            UpdateAnimator();
        }

        void StickToSeat()
        {
            if (seat == null) return;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        // ---------- 조종석 애니메이션 ----------
        // Animator Bool "Seated" = 좌석에 앉아 있음, Float "Steer" = 조종 (-1 왼쪽 ~ 0 ~ +1 오른쪽).
        // 원격 플레이어는 SetSeat가 불리지 않으므로(FlightCourse가 LateUpdate에서 자리만 붙임) 비행 단계로 앉았는지 본다.
        // 조종 입력은 IFlightState.IsPressing(동기화됨)에서 읽는다: 빨강 = A(왼쪽), 파랑 = D(오른쪽)
        static readonly int SeatedHash = Animator.StringToHash("Seated");
        static readonly int SteerHash = Animator.StringToHash("Steer");
        const float SteerSmoothing = 10f;
        static FlightCourse flightCourse;
        static float flightCourseSearchAt = float.NegativeInfinity;
        float seatSteer;

        static FlightCourse FindFlightCourse()
        {
            if (flightCourse == null && Time.unscaledTime - flightCourseSearchAt > 1f)
            {
                flightCourseSearchAt = Time.unscaledTime;
                flightCourse = FindFirstObjectByType<FlightCourse>();
            }
            return flightCourse;
        }

        bool SeatedForAnim(out IFlightState state)
        {
            state = null;
            // 방금 내린 내 캐릭터도 도착 환호 판정을 위해 한 프레임 더 코스를 본다
            var course = IsSeated || !Simulated || animSeatedPrev ? FindFlightCourse() : null;
            if (course != null) state = course.State;
            if (IsSeated) return seatPose;
            if (Simulated || state == null || Role == PlayerRole.None) return false;
            // 원격: 비행 단계이거나, 탑승 단계에서 이미 탔다고 보고됨
            return course.IsSeatedPhase(state.Phase) || (state.Phase == FlightPhase.Boarding && state.IsBoarded(Role));
        }

        // ---------- 비행 연출 애니 (SeatAnim Int) ----------
        // 0 = 안 앉음, 1 = Sit(조종 블렌드), 2/3 = 탑승(빨강 왼쪽 / 파랑 오른쪽), 4 = 충돌, 5 = 번개 감전, 6 = 착륙 소용돌이.
        // 도착 후 내리면 Trigger "ArriveCheer". 전부 동기화된 단계·내구도·번개 이벤트로 각 PC가 정하므로 원격 화면도 같다.
        static readonly int SeatAnimHash = Animator.StringToHash("SeatAnim");
        static readonly int ArriveCheerHash = Animator.StringToHash("ArriveCheer");
        const float SeatBoardTime = 0.65f;   // SitBoardFromLeft/Right 길이
        const float SeatHitTime = 0.6f;      // SitHit 길이
        const float ShockStruckWindow = 1f;  // 실패 직전 이 시간 안에 번개가 쳤으면 감전으로 본다
        static float lastStruckAt = float.NegativeInfinity;
        static bool struckHooked;
        bool animSeatedPrev;
        FlightPhase seatPrevPhase = FlightPhase.Idle;
        int seatPrevDurability = -1, seatPrevAttempt = -1;
        float seatBoardUntil, seatHitUntil;
        float failedSeenAt = float.NegativeInfinity;

        static void OnLightningStruck(Vector3 _) => lastStruckAt = Time.time;

        int SeatAnimValue(bool seated, IFlightState state)
        {
            float now = Time.time;
            var phase = state != null ? state.Phase : FlightPhase.Idle;
            if (state != null)
            {
                if (phase != seatPrevPhase)
                {
                    if (phase == FlightPhase.Failed) failedSeenAt = now;
                    seatPrevPhase = phase;
                }
                int dur = state.Durability, att = state.Attempt;
                if (seated && seatPrevDurability >= 0 && dur < seatPrevDurability && att == seatPrevAttempt) seatHitUntil = now + SeatHitTime;
                seatPrevDurability = dur;
                seatPrevAttempt = att;
            }
            // 탑승 단계에서 막 앉음 → 옆에서 뛰어 앉기 (실패 후 재시도로 다시 앉을 때는 안 씀)
            if (seated && !animSeatedPrev && phase == FlightPhase.Boarding) seatBoardUntil = now + SeatBoardTime;
            if (seated && !animSeatedPrev && HasAnimParam(ArriveCheerHash, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(ArriveCheerHash);
            // 도착 단계에서 내림 → 환호
            if (!seated && animSeatedPrev && phase == FlightPhase.Arrived) SetAnimTrigger(ArriveCheerHash);
            animSeatedPrev = seated;

            if (!seated) return 0;
            if (phase == FlightPhase.Failed && lastStruckAt > failedSeenAt - ShockStruckWindow) return 5;
            if (phase == FlightPhase.Landing || phase == FlightPhase.Arrived) return 6; // 내릴 때까지 소용돌이 유지
            if (now < seatHitUntil) return 4;
            if (now < seatBoardUntil) return Role == PlayerRole.B_Blue ? 3 : 2;
            return 1;
        }

        void UpdateSeatAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (!struckHooked) { struckHooked = true; LightningStrike.Struck += OnLightningStruck; }
            bool seated = SeatedForAnim(out var state);
            float target = 0f;
            if (seated && state != null)
            {
                var role = Role;
                if (role == PlayerRole.A_Red && state.IsPressing(PlayerRole.A_Red)) target = -1f;
                else if (role == PlayerRole.B_Blue && state.IsPressing(PlayerRole.B_Blue)) target = 1f;
            }
            seatSteer = Mathf.MoveTowards(seatSteer, target, SteerSmoothing * Time.deltaTime);
            SetAnimBool(SeatedHash, seated);
            SetAnimFloat(SteerHash, seatSteer);
            int v = SeatAnimValue(seated, state);
            if (HasAnimParam(SeatAnimHash, AnimatorControllerParameterType.Int)) animator.SetInteger(SeatAnimHash, v);
        }

        // 뗏목은 매 프레임 움직이고 Simulate는 틱마다 불릴 수 있으므로 화면에서는 프레임마다 다시 붙인다
        void LateUpdate()
        {
            if (seat != null && Simulated) StickToSeat();
            UpdateSeatAnimator();
        }
    }
}
