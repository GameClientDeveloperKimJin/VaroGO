using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 공중 비행 보너스 구간 본체.
    // 코스 기준 좌표: 이 오브젝트 위치가 출발점, 앞(z)으로 자동 전진, x = 좌우, y = 높이.
    // 스테이지 흐름: 스테이지 1 골 CLEAR(같은 씬) → 수정구 켜짐 → 비행 → 착륙 → 다음 씬(스테이지 2) 로드.
    // 진행: Idle(수정구 대기) → Summoning(조력자 등장·뗏목 조립) → Boarding(탑승) → Countdown → Flying → Landing → Arrived(스테이지 2)
    //       내구도 0 → Failed → (잠시 뒤) Countdown부터 다시 (T0 출발점, 내구도·장애물·진행도 초기화)
    // 판정(이동·충돌·내구도·성공)은 권한 PC(오프라인 = 나, 온라인 = 방장)만 하고 결과를 상태로 보낸다.
    [DefaultExecutionOrder(1000)]
    public class FlightCourse : MonoBehaviour
    {
        [Serializable]
        public struct Zone
        {
            public string id;      // T1 ~ T6
            public string title;   // 화면 표시 이름
            public float endDistance; // 이 구간이 끝나는 거리 (m)
        }

        [Header("상태")]
        [Tooltip("비행 상태 오브젝트 (비우면 자신)")]
        [SerializeField] GameObject stateObject;

        [Header("연결")]
        [SerializeField] FlightRaft raft;
        [Tooltip("탑승 대기 중에만 켜지는 뗏목 바닥 (걸어 올라갈 수 있게)")]
        [SerializeField] Collider raftFloor;
        [Tooltip("착륙 지점 (스테이지 2 땅 위)")]
        [SerializeField] Transform landingPad;
        [Tooltip("내린 뒤 빨강이 서는 곳")]
        [SerializeField] Transform dismountRed;
        [Tooltip("내린 뒤 파랑이 서는 곳")]
        [SerializeField] Transform dismountBlue;

        [Header("앞뒤 스테이지 연결")]
        [Tooltip("이 골이 CLEAR되어야 수정구가 켜진다 (골 상태 컴포넌트가 붙은 GameObject). 비우면 처음부터 켜져 있음")]
        [SerializeField] GameObject unlockGoalObject;
        [Tooltip("착륙 뒤 열 다음 씬 경로 (Build Settings에 있어야 온라인·빌드에서 열린다). 비우면 씬 전환 없음")]
        [SerializeField] string nextScenePath = "Assets/01_Scenes/PlayScene_Stage2.unity";
        [Tooltip("착륙(도착) 뒤 다음 씬으로 넘어가기까지 (초)")]
        [SerializeField, Min(0f)] float nextSceneDelay = 4f;

        [Header("비행")]
        [Tooltip("앞으로 가는 기본 속도 (m/초). 출발할 때와 부딪힌 직후 속도")]
        [SerializeField, Min(1f)] float forwardSpeed = 12f;
        [Tooltip("안 부딪히고 날면 1초마다 늘어나는 속도 (m/초). 0 = 속도 고정")]
        [SerializeField, Min(0f)] float speedGainPerSecond = 0.4f;
        [Tooltip("최고 속도 (m/초)")]
        [SerializeField, Min(1f)] float maxForwardSpeed = 20f;
        [Tooltip("출발점부터 착륙 시작까지 거리 (m)")]
        [SerializeField, Min(10f)] float flightLength = 520f;
        [Tooltip("좌우로 갈 수 있는 폭의 절반 (m)")]
        [SerializeField, Min(1f)] float halfWidth = 6f;
        [Tooltip("좌우 최고 속도 (m/초)")]
        [SerializeField, Min(0.5f)] float maxLateralSpeed = 6f;
        [Tooltip("A/D를 누를 때 좌우로 빨라지는 정도")]
        [SerializeField, Min(0.5f)] float lateralAccel = 14f;
        [Tooltip("손을 떼거나 둘 다 누를 때 좌우 속도가 줄어드는 정도 (클수록 빨리 멈춤)")]
        [SerializeField, Min(0.5f)] float lateralDecel = 24f;
        [Tooltip("거리에 따른 높이 변화 (가로 0~1 = 출발~착륙 시작, 세로 = m)")]
        [SerializeField] AnimationCurve heightProfile = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [Tooltip("뗏목 판정 상자 절반 크기 (좌우, 높이, 앞뒤)")]
        [SerializeField] Vector3 raftHalfExtents = new Vector3(1.5f, 0.6f, 2f);

        [Header("내구도")]
        [Tooltip("한 번 맞은 뒤 무적 시간 (초)")]
        [SerializeField, Min(0f)] float hitProtection = 1.5f;
        [Tooltip("내구도 0이 된 뒤 다시 시작까지 (초)")]
        [SerializeField, Min(0.5f)] float failRestartDelay = 3f;

        [Header("연출 시간")]
        [Tooltip("조력자 등장 + 뗏목 조립 + 설명 (초)")]
        [SerializeField, Min(1f)] float summonDuration = 9f;
        [Tooltip("출발 카운트다운 (초)")]
        [SerializeField, Min(0.5f)] float countdown = 3f;
        [Tooltip("착륙 비행 시간 (초)")]
        [SerializeField, Min(0.5f)] float landingDuration = 4f;

        [Header("구간")]
        [SerializeField] Zone[] zones =
        {
            new Zone { id = "T1", title = "적응 구간", endDistance = 80f },
            new Zone { id = "T2", title = "낙석 구간", endDistance = 200f },
            new Zone { id = "T3", title = "엇갈림 구간", endDistance = 300f },
            new Zone { id = "T4", title = "구름 협곡", endDistance = 420f },
            new Zone { id = "T5", title = "고요한 하늘", endDistance = 520f },
        };

        IFlightState state;
        IGoalState unlockGoal;
        bool loadingNext;
        readonly List<FlightHazard> hazards = new List<FlightHazard>();

        // 모든 PC에서 보는 값
        FlightPhase lastPhase = (FlightPhase)(-1);
        int lastAttempt = -1;
        int lastDurability = -1;
        float displayLateral, displayLatVel;
        float lastFlyDistance;
        float hitFlashUntil = -1f;    // Time.time 기준
        Vector3 failStartLocal;
        bool dismounted;

        // 권한 PC 전용
        float authLateral, authLatVel;
        double protectUntil = double.NegativeInfinity;

        // 로컬 입력 보고
        bool reportedRed, reportedBlue;
        bool redKey, blueKey;

        public IFlightState State => state;
        public FlightPhase Phase => state != null ? state.Phase : FlightPhase.Idle;
        public float PhaseTime => state != null ? (float)(SwitchStates.Now - state.PhaseStartTime) : 0f;
        public int Durability => state != null ? state.Durability : MaxDurability;
        public int MaxDurability => state is LocalFlightState l ? l.MaxDurability : (local != null ? local.MaxDurability : 4);
        public int Attempt => state != null ? state.Attempt : 0;
        public float FlightLength => flightLength;
        public float Distance => Phase == FlightPhase.Flying ? Mathf.Min(flightLength, FlyDistanceNow()) : (Phase == FlightPhase.Landing || Phase == FlightPhase.Arrived ? flightLength : (Phase == FlightPhase.Failed ? lastFlyDistance : 0f));
        // 지금 앞으로 가는 속도 (비행 중이 아니면 기본 속도)
        public float ForwardSpeed => Phase == FlightPhase.Flying ? SpeedAt(SegmentTime()) : forwardSpeed;
        public float BaseForwardSpeed => forwardSpeed;
        public float MaxForwardSpeed => Mathf.Max(forwardSpeed, maxForwardSpeed);
        public float Progress => Mathf.Clamp01(Distance / flightLength);
        public float SummonDuration => summonDuration;
        public float CountdownDuration => countdown;
        public float FailRestartDelay => failRestartDelay;
        public FlightRaft Raft => raft;
        public bool IsProtected => Time.time < hitFlashUntil;
        public bool LocalRedPressed => redKey;
        public bool LocalBluePressed => blueKey;
        public bool IsSeatedPhase(FlightPhase p) => p == FlightPhase.Countdown || p == FlightPhase.Flying || p == FlightPhase.Failed || p == FlightPhase.Landing;
        public bool ShouldUseFlightCam => Phase == FlightPhase.Countdown || Phase == FlightPhase.Flying || Phase == FlightPhase.Failed || Phase == FlightPhase.Landing;

        public int ZoneIndex
        {
            get
            {
                var p = Phase;
                if (p == FlightPhase.Idle || p == FlightPhase.Summoning || p == FlightPhase.Boarding || p == FlightPhase.Countdown) return -1;
                if (p == FlightPhase.Landing || p == FlightPhase.Arrived) return zones.Length; // T6
                float d = Distance;
                for (int i = 0; i < zones.Length; i++) if (d < zones[i].endDistance) return i;
                return zones.Length - 1;
            }
        }
        public string ZoneId => ZoneIndex < 0 ? "T0" : ZoneIndex >= zones.Length ? "T6" : zones[ZoneIndex].id;
        public string ZoneTitle => ZoneIndex < 0 ? "전망대" : ZoneIndex >= zones.Length ? "착륙" : zones[ZoneIndex].title;

        public event Action<FlightPhase> PhaseChanged;   // 모든 PC
        public event Action<int> Hit;                    // 모든 PC. 남은 내구도
        public event Action<int> ZoneChanged;            // 모든 PC

        LocalFlightState local;
        int lastZone = -2;

        // 골이 CLEAR되어 비행을 시작할 수 있는지 (골 연결이 없으면 항상 true)
        public bool Unlocked => unlockGoal == null || unlockGoal.IsCleared;
        public bool HasNextScene => !string.IsNullOrEmpty(nextScenePath);
        public float NextSceneDelay => nextSceneDelay;
        public bool LoadingNext => loadingNext;
        public event Action UnlockedChanged;                   // 모든 PC. 골 CLEAR로 수정구가 켜진 순간

        public float TimeToReach(float distance) => distance / forwardSpeed;

        // ---------- 속도 (안 부딪히면 점점 빨라짐, 부딪히면 기본 속도로) ----------
        // 구간 = 마지막으로 기본 속도가 된 순간(출발·피격)부터. 구간 시작 시각·거리는 상태로 동기화 → 모든 PC가 같은 거리를 계산한다

        double SegmentStart()
        {
            if (state == null) return 0d;
            double phaseStart = state.PhaseStartTime;
            return state.SegmentStartTime >= phaseStart ? state.SegmentStartTime : phaseStart;
        }

        float SegmentBaseDistance() => state != null && state.SegmentStartTime >= state.PhaseStartTime ? state.SegmentStartDistance : 0f;

        float SegmentTime() => Mathf.Max(0f, (float)(SwitchStates.Now - SegmentStart()));

        float SpeedAt(float segTime) => Mathf.Min(MaxForwardSpeed, forwardSpeed + speedGainPerSecond * segTime);

        // 구간 안에서 간 거리: 최고 속도까지는 등가속, 그 뒤로는 등속
        float SegmentDistance(float segTime)
        {
            if (speedGainPerSecond <= 0f) return forwardSpeed * segTime;
            float rampTime = (MaxForwardSpeed - forwardSpeed) / speedGainPerSecond;
            if (segTime <= rampTime) return forwardSpeed * segTime + 0.5f * speedGainPerSecond * segTime * segTime;
            float rampDist = forwardSpeed * rampTime + 0.5f * speedGainPerSecond * rampTime * rampTime;
            return rampDist + MaxForwardSpeed * (segTime - rampTime);
        }

        float FlyDistanceNow() => SegmentBaseDistance() + SegmentDistance(SegmentTime());

        void Awake()
        {
            if (raft == null) raft = GetComponentInChildren<FlightRaft>(true);
            // 뗏목 위치·기울기는 코스 기준 좌표로 쓴다 (코스 바로 아래 자식이어야 한다)
            if (raft != null && raft.transform.parent != transform) raft.transform.SetParent(transform, true);
            GetComponentsInChildren(true, hazards);
            foreach (var h in hazards) h.Bind(this);
        }

        void Start()
        {
            var target = stateObject != null ? stateObject : gameObject;
            local = target.GetComponent<LocalFlightState>();
            state = SwitchStates.Pick<IFlightState, LocalFlightState>(target, null, this);
            if (state != null) state.SummonRequested += OnSummonRequested;
            if (unlockGoalObject != null)
            {
                unlockGoal = SwitchStates.ResolveGoal(unlockGoalObject, null, this);
                if (unlockGoal != null) unlockGoal.ChapterCleared += OnGoalCleared;
            }
        }

        void OnGoalCleared() => UnlockedChanged?.Invoke();

        void OnDestroy()
        {
            if (state != null) state.SummonRequested -= OnSummonRequested;
            if (unlockGoal != null) unlockGoal.ChapterCleared -= OnGoalCleared;
        }

        // ---------- 공개 요청 ----------

        public void RequestSummon()
        {
            if (state == null || state.Phase != FlightPhase.Idle || !Unlocked) return;
            state.RequestSummon(SwitchStates.LocalRole);
        }

        void OnSummonRequested()
        {
            if (state == null || !state.HasAuthority || state.Phase != FlightPhase.Idle || !Unlocked) return;
            state.SetPhase(FlightPhase.Summoning, SwitchStates.Now, MaxDurability, state.Attempt);
        }

        // ---------- 매 프레임 ----------

        void Update()
        {
            if (state == null) return;
            ReadLocalInput();
            if (state.HasAuthority) TickAuthority();
            WatchChanges();
            UpdateHazards();
            UpdateRaft();
            UpdatePlayers();
            UpdateNextScene();
        }

        // 도착 뒤 nextSceneDelay초가 지나면 다음 스테이지 씬으로 (온라인 = 방장이 바꾸고 모두 따라감)
        void UpdateNextScene()
        {
            if (loadingNext || !HasNextScene || state.Phase != FlightPhase.Arrived || PhaseTime < nextSceneDelay) return;
            loadingNext = true;
            StageLoading.Load(nextScenePath);
        }

        void ReadLocalInput()
        {
            bool blocked = PlayerInputReader.Blocked || PlayerInputReader.MenuOpen || PlayerInputReader.PauseOpen;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool a = !blocked && ((kb != null && kb.aKey.isPressed) || (pad != null && pad.leftStick.x.ReadValue() < -0.5f));
            bool d = !blocked && ((kb != null && kb.dKey.isPressed) || (pad != null && pad.leftStick.x.ReadValue() > 0.5f));
            bool flying = state.Phase == FlightPhase.Flying;
            var role = SwitchStates.LocalRole;
            if (!GameServices.IsOnline)
            {
                // 오프라인 싱글 테스트: 한 사람이 A(빨강)와 D(파랑)를 모두 누른다
                redKey = a; blueKey = d;
            }
            else
            {
                redKey = role == PlayerRole.A_Red && a;
                blueKey = role == PlayerRole.B_Blue && d;
            }
            bool sendRed = flying && redKey, sendBlue = flying && blueKey;
            if (!GameServices.IsOnline || role == PlayerRole.A_Red)
                if (sendRed != reportedRed) { reportedRed = sendRed; state.ReportPressing(PlayerRole.A_Red, sendRed); }
            if (!GameServices.IsOnline || role == PlayerRole.B_Blue)
                if (sendBlue != reportedBlue) { reportedBlue = sendBlue; state.ReportPressing(PlayerRole.B_Blue, sendBlue); }
        }

        void TickAuthority()
        {
            float t = PhaseTime;
            switch (state.Phase)
            {
                case FlightPhase.Summoning:
                    if (t >= summonDuration) state.SetPhase(FlightPhase.Boarding, SwitchStates.Now, MaxDurability, state.Attempt);
                    break;

                case FlightPhase.Boarding:
                    bool ready = GameServices.IsOnline
                        ? state.IsBoarded(PlayerRole.A_Red) && state.IsBoarded(PlayerRole.B_Blue)
                        : state.IsBoarded(PlayerRole.A_Red) || state.IsBoarded(PlayerRole.B_Blue);
                    if (ready) StartAttempt(state.Attempt);
                    break;

                case FlightPhase.Countdown:
                    if (t >= countdown)
                    {
                        authLateral = 0f; authLatVel = 0f;
                        protectUntil = double.NegativeInfinity;
                        state.SetLateral(0f, 0f);
                        double now = SwitchStates.Now;
                        state.SetSegment(now, 0f);
                        state.SetPhase(FlightPhase.Flying, now, state.Durability, state.Attempt);
                    }
                    break;

                case FlightPhase.Flying:
                    TickFlying(t);
                    break;

                case FlightPhase.Failed:
                    if (t >= failRestartDelay) StartAttempt(state.Attempt + 1);
                    break;

                case FlightPhase.Landing:
                    if (t >= landingDuration) state.SetPhase(FlightPhase.Arrived, SwitchStates.Now, state.Durability, state.Attempt);
                    break;
            }
        }

        void StartAttempt(int attempt)
        {
            authLateral = 0f; authLatVel = 0f;
            state.SetLateral(0f, 0f);
            state.SetPhase(FlightPhase.Countdown, SwitchStates.Now, MaxDurability, attempt);
        }

        void TickFlying(float flyTime)
        {
            float dt = Time.deltaTime;
            // 조종: 파랑 D = 오른쪽(+1), 빨강 A = 왼쪽(-1). 둘 다 / 둘 다 안 누름 = 0
            int steer = (state.IsPressing(PlayerRole.B_Blue) ? 1 : 0) - (state.IsPressing(PlayerRole.A_Red) ? 1 : 0);
            if (steer == 0)
                authLatVel = Mathf.MoveTowards(authLatVel, 0f, lateralDecel * dt);
            else
            {
                float target = steer * maxLateralSpeed;
                // 반대 방향으로 미끄러지는 중이면 감속 값으로 빠르게 돌린다
                float rate = Mathf.Sign(authLatVel) != steer && Mathf.Abs(authLatVel) > 0.01f ? lateralDecel : lateralAccel;
                authLatVel = Mathf.MoveTowards(authLatVel, target, rate * dt);
            }
            authLateral += authLatVel * dt;
            if (authLateral > halfWidth) { authLateral = halfWidth; if (authLatVel > 0f) authLatVel = 0f; }
            if (authLateral < -halfWidth) { authLateral = -halfWidth; if (authLatVel < 0f) authLatVel = 0f; }
            state.SetLateral(authLateral, authLatVel);

            float s = FlyDistanceNow();
            if (s >= flightLength)
            {
                state.SetPhase(FlightPhase.Landing, SwitchStates.Now, state.Durability, state.Attempt);
                return;
            }

            // 충돌: 같은 장애물은 한 번만, 맞은 뒤 무적 시간 동안은 피해 없음
            Vector3 center = new Vector3(authLateral, HeightAt(s), s);
            foreach (var h in hazards)
            {
                if (h == null || !h.isActiveAndEnabled) continue;
                if (!h.Overlaps(center, raftHalfExtents)) continue;
                h.HitThisAttempt = true;
                h.PlayHit();
                if (SwitchStates.Now < protectUntil) continue;
                protectUntil = SwitchStates.Now + hitProtection;
                // 부딪히면 속도가 기본으로 돌아간다 (여기서부터 다시 가속)
                state.SetSegment(SwitchStates.Now, s);
                int left = Mathf.Max(0, state.Durability - 1);
                if (left <= 0) state.SetPhase(FlightPhase.Failed, SwitchStates.Now, 0, state.Attempt);
                else state.SetPhase(FlightPhase.Flying, state.PhaseStartTime, left, state.Attempt);
                break;
            }
        }

        // ---------- 모든 PC ----------

        void WatchChanges()
        {
            var phase = state.Phase;
            int attempt = state.Attempt, durability = state.Durability;

            if (attempt != lastAttempt)
            {
                foreach (var h in hazards) if (h != null) h.ResetAttempt();
                hitFlashUntil = -1f;
            }

            if (phase != lastPhase)
            {
                if (phase == FlightPhase.Failed)
                {
                    lastFlyDistance = Mathf.Clamp(Mathf.Max(lastFlyDistance, 0f), 0f, flightLength);
                    failStartLocal = new Vector3(displayLateral, HeightAt(lastFlyDistance), lastFlyDistance);
                    FlightFx.Burst(RaftWorld(failStartLocal), new Color(0.5f, 0.8f, 1f), 6f, 0.8f);
                    PlaySfx(TempSfx.Scream, RaftWorld(failStartLocal));
                }
                if (phase == FlightPhase.Countdown)
                {
                    foreach (var h in hazards) if (h != null) h.ResetAttempt();
                    displayLateral = 0f; displayLatVel = 0f;
                    lastFlyDistance = 0f;
                }
                if (phase == FlightPhase.Arrived && lastPhase >= 0) PlaySfx(TempSfx.Fanfare, transform.position);
                if (phase != FlightPhase.Arrived) dismounted = false;
                lastPhase = phase;
                PhaseChanged?.Invoke(phase);
            }

            if (lastDurability >= 0 && durability < lastDurability && attempt == lastAttempt)
            {
                hitFlashUntil = Time.time + hitProtection;
                var p = raft != null ? raft.transform.position : transform.position;
                PlaySfx(TempSfx.Thud, p);
                PlaySfx(TempSfx.Oof, p);
                FlightFx.Burst(p, new Color(0.6f, 0.4f, 1f), 5f, 0.6f);
                Hit?.Invoke(durability);
            }
            lastDurability = durability;
            lastAttempt = attempt;

            int zone = ZoneIndex;
            if (zone != lastZone) { lastZone = zone; ZoneChanged?.Invoke(zone); }
        }

        void UpdateHazards()
        {
            bool flying = state.Phase == FlightPhase.Flying;
            float s = flying ? Distance : 0f;
            foreach (var h in hazards) if (h != null) h.Tick(flying, s);

            // 권한이 없는 PC: 화면에서 겹치면 부서짐 연출만 (피해는 권한 PC가 정한다)
            if (!flying || state.HasAuthority) return;
            Vector3 center = new Vector3(displayLateral, HeightAt(s), s);
            foreach (var h in hazards)
            {
                if (h == null || !h.isActiveAndEnabled || h.HitThisAttempt) continue;
                if (!h.Overlaps(center, raftHalfExtents)) continue;
                h.HitThisAttempt = true;
                h.PlayHit();
            }
        }

        float HeightAt(float distance) => heightProfile != null && heightProfile.length > 0 ? heightProfile.Evaluate(Mathf.Clamp01(distance / flightLength)) : 0f;

        Vector3 RaftWorld(Vector3 courseLocal) => transform.TransformPoint(courseLocal);

        void UpdateRaft()
        {
            if (raft == null) return;
            var phase = state.Phase;
            float t = PhaseTime;
            float dt = Time.deltaTime;

            // 좌우: 권한 PC는 그대로, 다른 PC는 받은 값으로 부드럽게 따라간다
            if (state.HasAuthority) { displayLateral = state.Lateral; displayLatVel = state.LateralVelocity; }
            else
            {
                float predicted = state.Lateral + state.LateralVelocity * 0.05f;
                displayLateral = Mathf.Lerp(displayLateral, predicted, 1f - Mathf.Exp(-12f * dt));
                displayLatVel = Mathf.Lerp(displayLatVel, state.LateralVelocity, 1f - Mathf.Exp(-12f * dt));
            }

            float bob = Mathf.Sin(Time.time * 1.6f) * 0.12f;
            Vector3 local;
            float roll = 0f, pitch = 0f;
            float assembly = 1f;

            switch (phase)
            {
                case FlightPhase.Idle:
                    assembly = 0f;
                    local = Vector3.zero;
                    break;
                case FlightPhase.Summoning:
                    // 조력자가 먼저 나오고(2초) 그 뒤 판자·밧줄·룬이 차례로 붙는다
                    assembly = Mathf.Clamp01((t - 2f) / Mathf.Max(0.5f, summonDuration - 4f));
                    local = new Vector3(0f, bob, 0f);
                    break;
                case FlightPhase.Boarding:
                    local = new Vector3(0f, bob * 0.5f, 0f);
                    break;
                case FlightPhase.Countdown:
                    local = new Vector3(0f, bob + Mathf.Clamp01(t / countdown) * 0.6f, 0f);
                    roll = Mathf.Sin(Time.time * 7f) * Mathf.Clamp01(t / countdown) * 2f;
                    break;
                case FlightPhase.Flying:
                {
                    float s = Distance;
                    lastFlyDistance = s;
                    local = new Vector3(displayLateral, HeightAt(s) + 0.6f + bob, s);
                    roll = -displayLatVel / maxLateralSpeed * 14f;
                    pitch = Mathf.Sin(Time.time * 2.1f) * 1.5f;
                    if (IsProtected) roll += Mathf.Sin(Time.time * 40f) * 6f * ((hitFlashUntil - Time.time) / Mathf.Max(0.01f, hitProtection));
                    break;
                }
                case FlightPhase.Failed:
                {
                    // 마법이 풀려 빙글 돌며 가라앉는다
                    float k = Mathf.Clamp01(t / failRestartDelay);
                    local = failStartLocal + new Vector3(0f, 0.6f - k * k * 6f, k * 4f);
                    roll = k * 35f + Mathf.Sin(Time.time * 18f) * 6f * (1f - k);
                    pitch = k * 20f;
                    break;
                }
                case FlightPhase.Landing:
                {
                    float k = Mathf.Clamp01(t / landingDuration);
                    float e = k * k * (3f - 2f * k);
                    Vector3 from = new Vector3(state.Lateral, HeightAt(flightLength) + 0.6f, flightLength);
                    Vector3 to = landingPad != null ? transform.InverseTransformPoint(landingPad.position) : from + new Vector3(0f, 0f, 30f);
                    Vector3 mid = new Vector3((from.x + to.x) * 0.5f, Mathf.Max(from.y, to.y) + 3f, (from.z + to.z) * 0.5f);
                    local = Vector3.Lerp(Vector3.Lerp(from, mid, e), Vector3.Lerp(mid, to, e), e);
                    pitch = Mathf.Lerp(0f, 6f, Mathf.Sin(k * Mathf.PI));
                    break;
                }
                default: // Arrived
                    local = landingPad != null ? transform.InverseTransformPoint(landingPad.position) : new Vector3(0f, 0f, flightLength + 30f);
                    break;
            }

            raft.transform.localPosition = local;
            raft.SetTilt(roll, pitch);
            raft.SetAssembly(assembly);
            raft.SetBlink(phase == FlightPhase.Flying && IsProtected);
            if (raftFloor != null) raftFloor.enabled = phase == FlightPhase.Boarding;
        }

        // ---------- 플레이어 탑승·고정·하차 ----------

        void UpdatePlayers()
        {
            if (raft == null) return;
            var phase = state.Phase;
            var players = PlayerController.All;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;
                var role = p.Role;
                if (role == PlayerRole.None) continue;
                // 내 캐릭터 + 오프라인 더미만 직접 다룬다. 온라인 상대 캐릭터는 LateUpdate에서 자리에 붙인다
                bool mine = p.IsLocal || (!GameServices.IsOnline && p.Simulated);
                if (!mine) continue;
                var seatT = SeatFor(role);

                if (phase == FlightPhase.Boarding)
                {
                    if (!p.IsSeated && p.IsLocal && IsOnRaft(p.transform.position))
                    {
                        SeatPlayer(p, seatT);
                        // 오프라인: 더미(상대 역할)도 함께 태운다
                        if (!GameServices.IsOnline)
                            foreach (var other in players)
                                if (other != null && other != p && other.Simulated && !other.IsSeated) SeatPlayer(other, SeatFor(other.Role));
                    }
                }
                else if (IsSeatedPhase(phase))
                {
                    if (!p.IsSeated) SeatPlayer(p, seatT);
                    else if (p.Seat != seatT) p.SetSeat(seatT);
                }
                else if (phase == FlightPhase.Arrived)
                {
                    if (p.IsSeated && !dismounted) Dismount(p);
                }
            }
            if (phase == FlightPhase.Arrived && !dismounted)
            {
                bool any = false;
                foreach (var p in players) if (p != null && p.IsSeated && (p.IsLocal || (!GameServices.IsOnline && p.Simulated))) any = true;
                if (!any) dismounted = true;
            }
        }

        void SeatPlayer(PlayerController p, Transform seatT)
        {
            if (seatT == null) return;
            p.SetSeat(seatT);
            var role = p.Role;
            if (p.IsLocal || !GameServices.IsOnline) state.ReportBoarded(role, true);
        }

        void Dismount(PlayerController p)
        {
            var role = p.Role;
            var spot = role == PlayerRole.B_Blue ? dismountBlue : dismountRed;
            p.SetSeat(null);
            Vector3 pos = spot != null ? spot.position : p.transform.position + Vector3.up * 0.5f;
            Quaternion rot = spot != null ? spot.rotation : transform.rotation;
            p.Teleport(pos, rot);
            if (p.IsLocal || !GameServices.IsOnline) state.ReportBoarded(role, false);
        }

        Transform SeatFor(PlayerRole role) => role == PlayerRole.B_Blue ? raft.SeatBlue : raft.SeatRed;

        bool IsOnRaft(Vector3 world)
        {
            var l = raft.transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x) < raftHalfExtents.x + 0.4f && Mathf.Abs(l.z) < raftHalfExtents.z + 0.4f && l.y > -0.8f && l.y < 2.5f;
        }

        void LateUpdate()
        {
            if (state == null || raft == null || !GameServices.IsOnline) return;
            if (!IsSeatedPhase(state.Phase)) return;
            // 온라인 상대 캐릭터: 탑승했으면 내 화면의 뗏목 자리에 붙인다 (뗏목 위치가 PC마다 조금 달라도 떠 보이지 않게)
            foreach (var p in PlayerController.All)
            {
                if (p == null || p.IsLocal) continue;
                var role = p.Role;
                if (!state.IsBoarded(role)) continue;
                var seatT = SeatFor(role);
                if (seatT != null) p.transform.SetPositionAndRotation(seatT.position, seatT.rotation);
            }
        }

        static void PlaySfx(AudioClip clip, Vector3 at)
        {
            if (clip == null) return;
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(clip, cam != null ? cam.transform.position : at, 0.8f);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            int steps = 26;
            Vector3 prevL = new Vector3(-halfWidth, HeightAt(0f), 0f), prevR = new Vector3(halfWidth, HeightAt(0f), 0f);
            for (int i = 1; i <= steps; i++)
            {
                float s = flightLength * i / steps;
                var l = new Vector3(-halfWidth, HeightAt(s), s);
                var r = new Vector3(halfWidth, HeightAt(s), s);
                Gizmos.DrawLine(prevL, l);
                Gizmos.DrawLine(prevR, r);
                prevL = l; prevR = r;
            }
            Gizmos.color = new Color(1f, 1f, 0.3f, 0.8f);
            foreach (var z in zones)
                Gizmos.DrawLine(new Vector3(-halfWidth, HeightAt(z.endDistance), z.endDistance), new Vector3(halfWidth, HeightAt(z.endDistance), z.endDistance));
        }
    }
}
