using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 장난기 많은 마법사 조력자 (임시 모습: 몸통 구 + 모자). 정체는 밝히지 않는다.
    // 수정구를 만지면 펑 하고 나타나 뗏목을 만들고, 비행 중에는 뗏목 옆을 따라다니며 놀리거나 응원한다.
    // 대사는 같은 말이 너무 자주 나오지 않게 종류마다 쉬는 시간을 둔다. 다시 도전할 때는 설명·구간 대사를 생략한다.
    public class FlightHelper : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("보이는 모습 (비우면 자식 Visual)")]
        [SerializeField] Transform visual;
        [Tooltip("비행 중 뗏목 기준 위치 (오른쪽 위)")]
        [SerializeField] Vector3 raftOffset = new Vector3(3f, 2.2f, 0.5f);
        [Tooltip("놀리는 대사 사이 최소 간격 (초)")]
        [SerializeField, Min(0f)] float teaseCooldown = 8f;
        [Tooltip("대사가 화면에 남는 시간 (초)")]
        [SerializeField, Min(0.5f)] float lineDuration = 3.5f;
        [Tooltip("도착 뒤 사라지기까지 (초)")]
        [SerializeField, Min(0f)] float leaveDelay = 5f;

        Vector3 homePosition;
        bool shown;
        string line;
        float lineUntil;
        readonly Dictionary<string, float> lastSaid = new Dictionary<string, float>();
        readonly HashSet<string> saidThisAttempt = new HashSet<string>();
        int failCount;
        int lastAttempt = -1;
        GUIStyle style, shadow;

        static readonly string[] HitLines = { "아야! 내 뗏목!", "조심 좀 해~", "방금 그거 일부러 맞은 거지?", "흠~ 바위랑 친해지고 싶었어?" };
        static readonly string[] RetryLines = { "다시 간다! 이번엔 잘해 봐~", "괜찮아, 바위는 원래 딱딱해.", "처음 보는 척 할게. 출발~" };

        public string CurrentLine => Time.time < lineUntil ? line : null;

        void Awake()
        {
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
            if (visual == null) visual = transform.Find("Visual");
            homePosition = transform.position;
            SetShown(false, false);
        }

        void OnEnable()
        {
            if (course == null) return;
            course.PhaseChanged += OnPhase;
            course.Hit += OnHit;
            course.ZoneChanged += OnZone;
        }

        void OnDisable()
        {
            if (course == null) return;
            course.PhaseChanged -= OnPhase;
            course.Hit -= OnHit;
            course.ZoneChanged -= OnZone;
        }

        bool Retry => course != null && course.Attempt > 0;

        void OnPhase(FlightPhase phase)
        {
            if (course.Attempt != lastAttempt) { lastAttempt = course.Attempt; saidThisAttempt.Clear(); }
            switch (phase)
            {
                case FlightPhase.Countdown:
                    if (!Retry) Say("go", "꽉 잡아~!", 0f);
                    else Say("retry", RetryLines[(course.Attempt - 1) % RetryLines.Length], 20f);
                    break;
                case FlightPhase.Failed:
                    failCount++;
                    Say("fail", failCount <= 2 ? "앗… 마법이 풀렸다. 처음부터 다시!" : "또? 하하, 처음부터 다시~", 0f);
                    break;
                case FlightPhase.Landing:
                    Say("land", "착륙한다~ 꽉 잡아!", 0f);
                    break;
                case FlightPhase.Arrived:
                    Say("arrive", "여기서부턴 너희 힘으로 가야 해. 또 보자~!", 0f);
                    break;
                case FlightPhase.Boarding:
                    Say("board", "자, 뗏목에 올라타!", 0f);
                    break;
            }
        }

        void OnHit(int left)
        {
            if (left == 1) { Say("last", "한 번만 더 맞으면 끝이야!", 0f); return; }
            if (left <= 0) return;
            Say("hit", HitLines[Random.Range(0, HitLines.Length)], teaseCooldown);
        }

        void OnZone(int zone)
        {
            if (Retry || course.Phase != FlightPhase.Flying) return; // 다시 도전할 때는 구간 설명 생략
            switch (zone)
            {
                case 0: Say("z1", "천천히 감 잡아 봐. 아직은 아무것도 없어.", 0f); break;
                case 1: Say("z2", "어라? 위에서 뭐가 떨어지는데? 붉은 빛을 피해!", 0f); break;
                case 2: Say("z3", "왼쪽, 오른쪽, 번갈아 온다!", 0f); break;
                case 3: Say("z4", "바위 사이로 살살~ 둘이 호흡 맞춰!", 0f); break;
                case 4: Say("z5", "저기 봐! 다음 땅이 보여.", 0f); break;
            }
        }

        // cooldown: 같은 종류 대사를 다시 하기까지 (초). 0이면 이번 도전에서 한 번만
        void Say(string key, string text, float cooldown)
        {
            if (cooldown <= 0f)
            {
                if (saidThisAttempt.Contains(key)) return;
                saidThisAttempt.Add(key);
            }
            else if (lastSaid.TryGetValue(key, out var at) && Time.time - at < cooldown) return;
            lastSaid[key] = Time.time;
            line = text;
            lineUntil = Time.time + lineDuration;
        }

        void Update()
        {
            if (course == null || course.State == null) return;
            var phase = course.Phase;
            float t = course.PhaseTime;

            // 소환 연출 대사 (처음 한 번)
            if (phase == FlightPhase.Summoning)
            {
                if (course.Attempt != lastAttempt) { lastAttempt = course.Attempt; saidThisAttempt.Clear(); }
                if (t > 0.6f) Say("s1", "짜잔~! 수정구를 깨운 게 너희야?", 0f);
                if (t > 3.2f) Say("s2", "저 앞까지 걸어가긴 너무 멀지? 탈 걸 하나 만들어 줄게!", 0f);
                if (t > 6.4f) Say("s3", "빨강은 A로 왼쪽, 파랑은 D로 오른쪽! 같이 누르면… 그냥 직진이야.", 0f);
            }

            bool want = phase != FlightPhase.Idle && !(phase == FlightPhase.Arrived && t > leaveDelay);
            if (phase == FlightPhase.Summoning && t < 0.4f) want = false;
            if (want != shown) SetShown(want, true);
            if (!shown) return;

            Vector3 target;
            var raft = course.Raft;
            if (raft != null && (phase == FlightPhase.Countdown || phase == FlightPhase.Flying || phase == FlightPhase.Landing || phase == FlightPhase.Failed))
                target = raft.transform.position + course.transform.rotation * raftOffset;
            else if (raft != null && phase == FlightPhase.Arrived)
                target = raft.transform.position + course.transform.rotation * new Vector3(2.5f, 1.5f, 1.5f);
            else
                target = homePosition;
            target += Vector3.up * Mathf.Sin(Time.time * 2.3f) * 0.25f;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-6f * Time.deltaTime));

            // 플레이어 쪽을 보며 살짝 흔들흔들
            var cam = Camera.main;
            if (cam != null)
            {
                var look = cam.transform.position - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f) * 8f), 1f - Mathf.Exp(-5f * Time.deltaTime));
            }
        }

        void SetShown(bool value, bool poof)
        {
            shown = value;
            if (visual != null) visual.gameObject.SetActive(value);
            if (poof)
            {
                FlightFx.Burst(transform.position, new Color(1f, 0.85f, 1f), 3f, 0.5f);
                if (value) AudioSource.PlayClipAtPoint(TempSfx.Chime, Camera.main != null ? Camera.main.transform.position : transform.position, 0.7f);
            }
        }

        // 임시 자막 (UI가 들어오면 교체)
        void OnGUI()
        {
            var text = CurrentLine;
            if (string.IsNullOrEmpty(text)) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, wordWrap = true };
                style.normal.textColor = new Color(1f, 0.92f, 1f);
                shadow = new GUIStyle(style);
                shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }
            var r = new Rect(Screen.width * 0.15f, Screen.height * 0.78f, Screen.width * 0.7f, 60f);
            string label = "조력자: " + text;
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), label, shadow);
            GUI.Label(r, label, style);
        }
    }
}
