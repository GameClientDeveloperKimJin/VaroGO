using System;
using UnityEngine;
using UnityEngine.Events;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18 상시 기믹 "도둑맞은 마법의 도토리". 모든 스테이지에 놓는다.
    // 길가의 도토리에 누군가 다가가면 마법 다람쥐가 튀어나와 도토리를 물고 샛길(경로 점)을 따라 달아난다.
    //
    // 동기화: 같은 GameObject의 LocalSwitchState(온라인: NetSwitchState)가 "한 번 일어났다"를 들고 있다.
    // 연출은 켜진 시각(ISwitchDetails.LastOnTime, 공용 GameClock)에서 지난 시간만으로 계산한다
    // → 두 PC가 같은 순간 같은 위치를 보고, 늦게 들어온 PC는 끝난 상태로 바로 보인다. Net 전용 코드 없음.
    //
    // 스테이지마다 바꾸는 것: 경로 점(Path 아래 자식), 숨어 있던 곳(Hide Point), 속도·시간, 끝나고 켤 오브젝트, 이벤트.
    // 겉모습은 Squirrel / Acorn 아래 모델만 바꾸면 된다. 애니메이션은 Phase·이벤트에 연결한다.
    public class AcornThief : MonoBehaviour
    {
        public enum Phase { Waiting, Appear, Grab, Run, Done }

        [Header("연결")]
        [Tooltip("상태(LocalSwitchState)가 붙은 오브젝트. 비우면 자기 자신")]
        [SerializeField] GameObject stateObject;
        [Tooltip("다람쥐 (움직이는 루트)")]
        [SerializeField] Transform squirrel;
        [Tooltip("다람쥐 입. 도토리가 여기 붙어서 같이 간다")]
        [SerializeField] Transform mouth;
        [Tooltip("길가에 놓인 도토리")]
        [SerializeField] Transform acorn;
        [Tooltip("다람쥐가 숨어 있다 튀어나오는 곳 (덤불·나무 뒤)")]
        [SerializeField] Transform hidePoint;
        [Tooltip("도망가는 길 점들의 부모. 자식 순서대로 지나간다 (점 복제·순서 바꾸기만 하면 됨). 비우면 아래 Path 배열")]
        [SerializeField] Transform pathRoot;
        [Tooltip("도망가는 길. 도토리 자리에서 출발해 차례대로 지나간다. 마지막 점 = 샛길 끝 (Path Root가 있으면 무시)")]
        [SerializeField] Transform[] path = Array.Empty<Transform>();

        [Header("발동")]
        [Tooltip("도토리에서 이 거리(m) 안에 플레이어가 들어오면 시작 (둘 중 한 명이면 됨)")]
        [SerializeField] float triggerRadius = 4f;

        [Header("연출 시간")]
        [Tooltip("숨은 곳에서 도토리까지 튀어나오는 시간(초)")]
        [SerializeField] float appearTime = 0.5f;
        [Tooltip("도토리를 물고 멈칫하는 시간(초)")]
        [SerializeField] float grabTime = 0.5f;
        [Tooltip("도망가는 속도(m/초)")]
        [SerializeField] float runSpeed = 6f;
        [Tooltip("깡충깡충 뛰는 높이(m) / 1초당 뛰는 횟수")]
        [SerializeField] float hopHeight = 0.25f;
        [SerializeField] float hopsPerSecond = 4f;
        [Tooltip("끝에 도착하면 작아지며 사라지는 시간(초). 0이면 끝에 남아 있음")]
        [SerializeField] float vanishTime = 0.4f;

        [Header("끝난 뒤")]
        [Tooltip("다람쥐가 끝에 도착하면 켜지는 오브젝트 (샛길 반짝임, 숨겨진 공간 등). 늦게 들어온 사람도 켜진 채로 보인다")]
        [SerializeField] GameObject[] revealOnDone = Array.Empty<GameObject>();

        [Header("이벤트 (각 PC에서 실시간으로 볼 때만)")]
        public UnityEvent onAppear;
        public UnityEvent onGrab;
        public UnityEvent onRun;
        public UnityEvent onDone;

        [Header("소리")]
        [SerializeField] AudioSource audioSource;
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.6f;

        // 이벤트는 이 시간(초) 안에 지난 단계만 낸다 (늦게 들어와 이미 끝난 연출은 조용히 끝 상태로)
        const float LiveEventWindow = 1f;
        const float FaceTurnSpeed = 720f;

        ISwitchState state;
        ISwitchDetails details;
        Vector3 squirrelHome, acornHome, acornGround, squirrelScale;
        Quaternion squirrelHomeRot, acornHomeRot;
        Transform acornParent;
        float[] cumulative = Array.Empty<float>();
        float pathLength;
        Phase phase = Phase.Waiting;
        bool requested;

        public Phase CurrentPhase => phase;
        public event Action<Phase> PhaseChanged; // 애니메이션 스크립트용

        float RunTime => runSpeed > 0f ? pathLength / runSpeed : 0f;

        void Awake()
        {
            state = SwitchStates.Resolve(stateObject != null ? stateObject : gameObject, null, this);
            details = state as ISwitchDetails;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            if (squirrel != null)
            {
                squirrelHome = squirrel.position;
                squirrelHomeRot = squirrel.rotation;
                squirrelScale = squirrel.localScale;
            }
            if (acorn != null)
            {
                acornParent = acorn.parent;
                acornHome = acorn.position;
                acornHomeRot = acorn.rotation;
            }
            // 다람쥐가 도토리를 무는 발밑 자리 = 도토리 아래, 이 오브젝트(바닥에 놓음) 높이
            acornGround = acorn != null ? new Vector3(acornHome.x, transform.position.y, acornHome.z) : transform.position;
            BuildPath();
        }

        void OnEnable()
        {
            if (state != null) state.Changed += OnStateChanged;
            Apply(state != null && state.IsOn, live: false);
        }

        void OnDisable()
        {
            if (state != null) state.Changed -= OnStateChanged;
        }

        void OnStateChanged(bool on)
        {
            requested = false;
            if (!on) Apply(false, live: false); // 구역 리셋 등으로 꺼지면 처음 상태로
        }

        void Update()
        {
            if (state == null) return;
            if (!state.IsOn)
            {
                if (phase != Phase.Waiting) Apply(false, live: false);
                TryTrigger();
                return;
            }
            Apply(true, live: true);
        }

        // 내 PC에서 시뮬레이션하는 플레이어(로컬 + 오프라인 더미)만 본다. 다시 요청하면 시작 시각이 갱신되므로 한 번만
        void TryTrigger()
        {
            if (requested || acorn == null) return;
            float r2 = triggerRadius * triggerRadius;
            foreach (var pc in PlayerController.All)
            {
                if (pc == null || !pc.Simulated) continue;
                if ((pc.transform.position - acornHome).sqrMagnitude > r2) continue;
                requested = true;
                state.RequestSet(true, SwitchStates.LocalRole);
                return;
            }
        }

        void Apply(bool on, bool live)
        {
            if (!on)
            {
                ResetPose();
                SetPhase(Phase.Waiting, live: false);
                return;
            }

            float t = details != null ? (float)(SwitchStates.Now - details.LastOnTime) : float.MaxValue;
            if (t < 0f) t = 0f;
            float appearEnd = appearTime;
            float grabEnd = appearEnd + grabTime;
            float runEnd = grabEnd + RunTime;

            Phase target;
            if (t < appearEnd) target = Phase.Appear;
            else if (t < grabEnd) target = Phase.Grab;
            else if (t < runEnd || (vanishTime > 0f && t < runEnd + vanishTime)) target = Phase.Run;
            else target = Phase.Done;

            // 지나간 단계 이벤트: 막 지난 것만 낸다
            float[] starts = { 0f, appearEnd, grabEnd, runEnd + Mathf.Max(0f, vanishTime) };
            for (var p = phase + 1; p <= target; p++)
            {
                bool recent = live && t - starts[(int)p - 1] <= LiveEventWindow;
                SetPhase(p, recent);
            }

            Pose(t, appearEnd, grabEnd, runEnd);
        }

        void Pose(float t, float appearEnd, float grabEnd, float runEnd)
        {
            if (squirrel == null) return;
            Vector3 start = hidePoint != null ? hidePoint.position : squirrelHome;
            Vector3 pos;
            float scale = 1f;

            if (t < appearEnd)
            {
                float k = appearTime > 0f ? t / appearTime : 1f;
                pos = Vector3.Lerp(start, acornGround, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * hopHeight * 2f);
                scale = Mathf.Clamp01(k * 3f);
                Face(acornGround - start, instant: true);
            }
            else if (t < grabEnd)
            {
                pos = acornGround;
            }
            else if (t < runEnd)
            {
                float d = (t - grabEnd) * runSpeed;
                pos = PointAt(d, out Vector3 dir) + Vector3.up * Hop(t - grabEnd);
                Face(dir, instant: false);
            }
            else
            {
                pos = PointAt(pathLength, out _);
                if (vanishTime > 0f) scale = 1f - Mathf.Clamp01((t - runEnd) / vanishTime);
            }

            squirrel.gameObject.SetActive(scale > 0f);
            squirrel.position = pos;
            squirrel.localScale = squirrelScale * scale;

            // 도토리: 물기 전에는 제자리, 문 뒤에는 입에 붙어 간다. 다람쥐가 사라지면 같이 사라진다
            if (acorn != null)
            {
                bool carried = t >= appearEnd;
                if (carried && mouth != null && acorn.parent != mouth)
                {
                    acorn.SetParent(mouth, worldPositionStays: false);
                    acorn.localPosition = Vector3.zero;
                    acorn.localRotation = Quaternion.identity;
                }
                acorn.gameObject.SetActive(!carried || scale > 0f);
            }
        }

        void ResetPose()
        {
            if (squirrel != null)
            {
                squirrel.position = hidePoint != null ? hidePoint.position : squirrelHome;
                squirrel.rotation = squirrelHomeRot;
                squirrel.localScale = Vector3.zero;
                squirrel.gameObject.SetActive(false);
            }
            if (acorn != null)
            {
                if (acorn.parent != acornParent) acorn.SetParent(acornParent, worldPositionStays: true);
                acorn.position = acornHome;
                acorn.rotation = acornHomeRot;
                acorn.gameObject.SetActive(true);
            }
        }

        void SetPhase(Phase p, bool live)
        {
            if (phase == p) return;
            phase = p;
            foreach (var go in revealOnDone)
                if (go != null) go.SetActive(p == Phase.Done);
            PhaseChanged?.Invoke(p);
            if (!live) return;
            switch (p)
            {
                case Phase.Appear: Play(TempSfx.Oof); onAppear?.Invoke(); break;
                case Phase.Grab: Play(TempSfx.Click); onGrab?.Invoke(); break;
                case Phase.Run: onRun?.Invoke(); break;
                case Phase.Done: Play(TempSfx.Chime); onDone?.Invoke(); break;
            }
        }

        float Hop(float runT) => Mathf.Abs(Mathf.Sin(runT * hopsPerSecond * Mathf.PI)) * hopHeight;

        void Face(Vector3 dir, bool instant)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            squirrel.rotation = instant ? target : Quaternion.RotateTowards(squirrel.rotation, target, FaceTurnSpeed * Time.deltaTime);
        }

        // 경로: 도토리 자리 → path[0] → path[1] ...
        void BuildPath()
        {
            if (pathRoot != null)
            {
                path = new Transform[pathRoot.childCount];
                for (int i = 0; i < path.Length; i++) path[i] = pathRoot.GetChild(i);
            }
            int n = path.Length + 1;
            cumulative = new float[n];
            pathLength = 0f;
            Vector3 prev = acornGround;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == null) { cumulative[i + 1] = pathLength; continue; }
                pathLength += Vector3.Distance(prev, path[i].position);
                cumulative[i + 1] = pathLength;
                prev = path[i].position;
            }
        }

        Vector3 Node(int i) => i == 0 ? acornGround : (path[i - 1] != null ? path[i - 1].position : Node(i - 1));

        Vector3 PointAt(float d, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (path.Length == 0) return acornGround;
            d = Mathf.Clamp(d, 0f, pathLength);
            for (int i = 1; i < cumulative.Length; i++)
            {
                if (d > cumulative[i] && i < cumulative.Length - 1) continue;
                Vector3 a = Node(i - 1), b = Node(i);
                float seg = cumulative[i] - cumulative[i - 1];
                dir = b - a;
                return seg > 0f ? Vector3.Lerp(a, b, (d - cumulative[i - 1]) / seg) : b;
            }
            return Node(cumulative.Length - 1);
        }

        void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSource != null) audioSource.PlayOneShot(clip, volume);
            else AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying && pathRoot != null) BuildPath();
            Vector3 from = acorn != null ? acorn.position : transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            if (hidePoint != null) Gizmos.DrawLine(hidePoint.position, from);
            foreach (var p in path)
            {
                if (p == null) continue;
                Gizmos.DrawLine(from, p.position);
                Gizmos.DrawWireSphere(p.position, 0.2f);
                from = p.position;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(acorn != null ? acorn.position : transform.position, triggerRadius);
        }
    }
}
