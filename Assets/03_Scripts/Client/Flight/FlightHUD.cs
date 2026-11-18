using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 비행 화면 표시. 상단 진행 바·남은 거리·A/D 키 안내 = 아트 HUD(hudPrefab, uGUI).
    // 아직 아트가 없는 것(내구도·카운트다운·피격·실패·도착 문구)은 임시 OnGUI. hudPrefab을 비우면 전부 임시 OnGUI.
    // 내구도·진행도·구간 이름, A/D 조작 안내(누르면 밝아짐), 카운트다운, 피격 경고, 실패·다시 시작, 탑승 안내, 도착(STAGE 2)
    public class FlightHUD : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("도착 제목이 보이는 시간 (초)")]
        [SerializeField, Min(0.5f)] float arrivalTitleTime = 4f;
        [Tooltip("아트 HUD 프리팹 (UI_FlightHUD: 상단 진행 바·남은 거리, A/D 키 안내). 비우면 예전 임시 화면(OnGUI)으로 그린다")]
        [SerializeField] GameObject hudPrefab;
        [Tooltip("씬에 미리 놓아 둔 아트 HUD (UI_FlightHUD 인스턴스). 연결하면 hudPrefab을 새로 만들지 않고 이걸 쓴다")]
        [SerializeField] GameObject hudInstance;
        [Tooltip("A/D 키 안내: 탑승(카운트다운)부터 보이고, 비행체가 출발한 뒤 이 시간(초)이 지나면 사라진다 (0 = 출발하자마자)")]
        [SerializeField, Min(0f)] float keyPromptTime = 1f;
        [Tooltip("안 눌린 키·상대 키에 곱하는 색 (흐리게)")]
        [SerializeField] Color keyIdleTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Header("내구도 (판자 10칸 · 상태 4단계)")]
        [Tooltip("상태 테두리 색: 안전(61~100%) / 주의(31~60%) / 위험(1~30%) / 파괴(0%)")]
        [SerializeField] Color borderSafe = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] Color borderCaution = new Color(0.95f, 0.65f, 0.2f, 1f);
        [SerializeField] Color borderDanger = new Color(0.9f, 0.3f, 0.25f, 1f);
        [SerializeField] Color borderBroken = new Color(0.35f, 0.38f, 0.45f, 1f);
        [Tooltip("% 글자 색: 안전 / 주의 / 위험 / 파괴")]
        [SerializeField] Color textSafe = Color.white;
        [SerializeField] Color textCaution = new Color(1f, 0.8f, 0.45f, 1f);
        [SerializeField] Color textDanger = new Color(1f, 0.5f, 0.45f, 1f);
        [SerializeField] Color textBroken = new Color(0.85f, 0.55f, 0.55f, 1f);

        // 연출 세션(DOTween/Feel)용: 내구도 상태가 바뀔 때 (0 안전 / 1 주의 / 2 위험 / 3 파괴). 맞은 순간은 FlightCourse.Hit
        public event System.Action<int> DurabilityStateChanged;
        public int DurabilityState { get; private set; } = -1;
        // 연출용 부품 (판자 낙하·흔들림·붉은 플래시·깜빡임은 연출 세션이 이 부품을 움직인다)
        public RectTransform Tray => tray;
        public UnityEngine.UI.Graphic DangerVignette => vignette;
        public GameObject PlankWood(int i) => i >= 0 && i < plankWood.Count ? plankWood[i] : null;
        public int PlanksLeft { get; private set; }
        // 연출 세션용: 카운트다운 숫자가 바뀔 때("3"/"2"/"1") · 출발 글자가 나올 때. 글자 부품도 열어 둔다
        public event System.Action<string> CountdownChanged;
        public event System.Action GoShown;
        public TMPro.TMP_Text CountdownText => countdownText;
        public TMPro.TMP_Text GoText => goText;

        TMPro.TMP_Text countdownText, goText;
        string lastCountdown;
        bool goWasShown;

        // 아트 HUD 부품
        GameObject hudRoot, keyA, keyD, durabilityBar;
        GameObject[] barParts; // 상단: TopBar(있으면) 또는 GoalLabel·ProgressBar
        UnityEngine.UI.Graphic border, vignette;
        TMPro.TMP_Text percentText;
        RectTransform tray;
        readonly System.Collections.Generic.List<GameObject> plankWood = new System.Collections.Generic.List<GameObject>();
        readonly System.Collections.Generic.List<GameObject> plankCrack = new System.Collections.Generic.List<GameObject>();
        UnityEngine.UI.Image fill;
        RectTransform marker;
        UnityEngine.UI.Graphic[] keyAGraphics, keyDGraphics;
        float boardedAt = -1f;
        FlightPhase lastPhase;

        float hitAt = -10f;
        float lightningAt = -1f;
        float unlockedAt = -1f;
        GUIStyle big, mid, small, box;
        Texture2D white;

        void Awake()
        {
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
            BuildArtHud();
        }

        bool hudSpawned;

        void OnDestroy()
        {
            if (hudSpawned && hudRoot != null) Destroy(hudRoot);
        }

        void BuildArtHud()
        {
            if (hudInstance != null) hudRoot = hudInstance;
            else if (hudPrefab != null)
            {
                hudRoot = Instantiate(hudPrefab);
                hudRoot.name = hudPrefab.name;
                hudSpawned = true;
            }
            else return;
            var t = hudRoot.transform;
            keyA = t.Find("KeyPrompt_A")?.gameObject;
            keyD = t.Find("KeyPrompt_D")?.gameObject;
            // 상단 바: 예전 구조(TopBar 판 아래) 또는 판 없이 바로 아래(GoalLabel·ProgressBar) 둘 다 지원
            var top = t.Find("TopBar");
            Transform pb = top != null ? top.Find("ProgressBar") : t.Find("ProgressBar");
            if (top != null) barParts = new[] { top.gameObject };
            else
            {
                var parts = new System.Collections.Generic.List<GameObject>();
                foreach (var n in new[] { "GoalLabel", "ProgressBar" }) { var c = t.Find(n); if (c != null) parts.Add(c.gameObject); }
                barParts = parts.ToArray();
            }
            fill = pb != null ? pb.Find("Fill")?.GetComponent<UnityEngine.UI.Image>() : null;
            marker = pb != null ? pb.Find("MarkerTrack/Marker") as RectTransform : null;
            durabilityBar = t.Find("DurabilityBar")?.gameObject;
            tray = t.Find("DurabilityBar/Tray") as RectTransform;
            border = t.Find("DurabilityBar/Tray/Border")?.GetComponent<UnityEngine.UI.Graphic>();
            percentText = t.Find("DurabilityBar/PercentText")?.GetComponent<TMPro.TMP_Text>();
            vignette = t.Find("DangerVignette")?.GetComponent<UnityEngine.UI.Graphic>();
            countdownText = t.Find("CountdownText")?.GetComponent<TMPro.TMP_Text>();
            goText = t.Find("GoText")?.GetComponent<TMPro.TMP_Text>();
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (goText != null) goText.gameObject.SetActive(false);
            var planks = t.Find("DurabilityBar/Tray/Planks");
            if (planks != null)
                foreach (Transform pl in planks)
                {
                    var wood = pl.Find("Wood");
                    plankWood.Add(wood != null ? wood.gameObject : null);
                    plankCrack.Add(wood != null && wood.Find("Crack") != null ? wood.Find("Crack").gameObject : null);
                }
            SetVignette(0f);
            if (keyA != null) keyAGraphics = keyA.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            if (keyD != null) keyDGraphics = keyD.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            SetArtHud(false, false);
        }

        void SetArtHud(bool bar, bool keys)
        {
            if (barParts != null) foreach (var b in barParts) if (b != null && b.activeSelf != bar) b.SetActive(bar);
            if (durabilityBar != null && durabilityBar.activeSelf != bar) durabilityBar.SetActive(bar);
            if (keyA != null && keyA.activeSelf != keys) keyA.SetActive(keys);
            if (keyD != null && keyD.activeSelf != keys) keyD.SetActive(keys);
        }

        // 아트 HUD 갱신: 진행 바·비행기 마커, 탑승(카운트다운)부터 출발 뒤 keyPromptTime초까지 A/D 키 안내
        void Update()
        {
            if (hudRoot == null) return;
            if (course == null || course.State == null) { SetArtHud(false, false); return; }
            var phase = course.Phase;
            // 탑승 = 카운트다운 시작 (다시 도전할 때마다 다시 보여 준다)
            // 출발 = 비행 시작 (다시 도전할 때마다 카운트다운부터 다시 보여 준다)
            if (phase == FlightPhase.Countdown) boardedAt = -1f;
            else if (phase == FlightPhase.Flying && lastPhase != FlightPhase.Flying) boardedAt = Time.time;
            lastPhase = phase;
            UpdateTitles(phase, course.PhaseTime);

            bool bar = phase == FlightPhase.Countdown || phase == FlightPhase.Flying || phase == FlightPhase.Failed || phase == FlightPhase.Landing;
            bool keys = phase == FlightPhase.Countdown
                        || (phase == FlightPhase.Flying && boardedAt >= 0f && Time.time - boardedAt < keyPromptTime);
            SetArtHud(bar, keys);
            if (!bar) return;

            float p = course.Progress;
            if (fill != null) fill.fillAmount = p;
            if (marker != null)
            {
                marker.anchorMin = new Vector2(p, marker.anchorMin.y);
                marker.anchorMax = new Vector2(p, marker.anchorMax.y);
            }
            UpdateDurability();
            if (keys)
            {
                // 빨강 = A, 파랑 = D. 누르면 밝게, 안 눌림·온라인에서 상대 키는 흐리게
                var role = SwitchStates.LocalRole;
                bool online = GameServices.IsOnline;
                bool aOn = (course.LocalRedPressed || course.State.IsPressing(PlayerRole.A_Red)) && (!online || role == PlayerRole.A_Red);
                bool dOn = (course.LocalBluePressed || course.State.IsPressing(PlayerRole.B_Blue)) && (!online || role == PlayerRole.B_Blue);
                Tint(keyAGraphics, aOn);
                Tint(keyDGraphics, dOn);
            }
        }

        // 내구도: 판자 10칸 = ceil(남은 비율 × 10), 글자 = 비율 %(반올림).
        // 상태 4단계(안전 61~100 / 주의 31~60 / 위험 1~30 / 파괴 0)에 따라 테두리·글자 색을 바꾼다.
        // 위험이면 마지막 판자에 금(Crack)을 켠다.
        // 연출(판자 낙하·판 흔들림·붉은 플래시·위험 깜빡임·비네트)은 연출 세션 몫 — DurabilityStateChanged / FlightCourse.Hit으로 트리거 (연출_목록.md)
        void UpdateDurability()
        {
            int max = Mathf.Max(1, course.MaxDurability);
            float ratio = Mathf.Clamp01((float)course.Durability / max);
            int pct = Mathf.RoundToInt(ratio * 100f);
            int n = plankWood.Count;
            int left = n > 0 ? Mathf.CeilToInt(ratio * n - 0.0001f) : 0;

            int state = pct <= 0 ? 3 : pct <= 30 ? 2 : pct <= 60 ? 1 : 0;
            for (int i = 0; i < n; i++)
            {
                bool on = i < left;
                if (plankWood[i] != null && plankWood[i].activeSelf != on) plankWood[i].SetActive(on);
                bool crack = state == 2 && i == left - 1;
                if (plankCrack[i] != null && plankCrack[i].activeSelf != crack) plankCrack[i].SetActive(crack);
            }
            if (border != null) border.color = state == 0 ? borderSafe : state == 1 ? borderCaution : state == 2 ? borderDanger : borderBroken;
            if (percentText != null)
            {
                percentText.text = pct + "%";
                percentText.color = state == 0 ? textSafe : state == 1 ? textCaution : state == 2 ? textDanger : textBroken;
            }
            PlanksLeft = left;
            if (state != DurabilityState)
            {
                DurabilityState = state;
                DurabilityStateChanged?.Invoke(state);
            }
        }

        // 카운트다운 3·2·1 (CountdownText) / 비행 시작 뒤 1초 "출발!" (GoText). 바뀌는 순간 연출 세션용 이벤트를 쏜다
        void UpdateTitles(FlightPhase phase, float t)
        {
            string num = null;
            bool go = false;
            if (phase == FlightPhase.Countdown)
            {
                float left = course.CountdownDuration - t;
                if (left > 2f) num = "3"; else if (left > 1f) num = "2"; else if (left > 0f) num = "1"; else go = true;
            }
            else if (phase == FlightPhase.Flying && t < 1f) go = true;

            if (countdownText != null)
            {
                bool on = num != null;
                if (countdownText.gameObject.activeSelf != on) countdownText.gameObject.SetActive(on);
                if (on && num != lastCountdown) { countdownText.text = num; CountdownChanged?.Invoke(num); }
            }
            lastCountdown = num;
            if (goText != null && goText.gameObject.activeSelf != go) goText.gameObject.SetActive(go);
            if (go && !goWasShown) GoShown?.Invoke();
            goWasShown = go;
        }

        void SetVignette(float a)
        {
            if (vignette == null) return;
            var c = vignette.color;
            if (!Mathf.Approximately(c.a, a)) vignette.color = new Color(c.r, c.g, c.b, a);
        }

        void Tint(UnityEngine.UI.Graphic[] gs, bool lit)
        {
            if (gs == null) return;
            var c = lit ? Color.white : keyIdleTint;
            foreach (var g in gs) if (g != null) g.color = c;
        }

        void OnEnable() { LightningStrike.Struck += OnLightning; if (course != null) { course.Hit += OnHit; course.UnlockedChanged += OnUnlocked; } }
        void OnDisable() { LightningStrike.Struck -= OnLightning; if (course != null) { course.Hit -= OnHit; course.UnlockedChanged -= OnUnlocked; } }
        void OnLightning(Vector3 at) => lightningAt = Time.time;
        void OnUnlocked() => unlockedAt = Time.time;
        void OnHit(int left) => hitAt = Time.time;

        void EnsureStyles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 64, fontStyle = FontStyle.Bold };
            mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
            white = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            if (course == null || course.State == null) return;
            EnsureStyles();
            var phase = course.Phase;
            float t = course.PhaseTime;
            float w = Screen.width, h = Screen.height;

            // 골 CLEAR 직후: 수정구 안내
            if (phase == FlightPhase.Idle)
            {
                if (course.Unlocked && unlockedAt >= 0f && Time.time - unlockedAt < 8f)
                    Shadow(new Rect(0, h * 0.2f, w, 40), "수정구가 나타났어요! 만져서 하늘길을 열어 보세요", mid, new Color(0.7f, 0.92f, 1f));
                return;
            }

            if (phase == FlightPhase.Boarding)
            {
                var st = course.State;
                bool meOn = st.IsBoarded(SwitchStates.LocalRole) || !GameServices.IsOnline && (st.IsBoarded(PlayerRole.A_Red) || st.IsBoarded(PlayerRole.B_Blue));
                string msg = meOn ? "친구가 타기를 기다리는 중…" : "뗏목 위로 올라타세요";
                Shadow(new Rect(0, h * 0.2f, w, 40), msg, mid, Color.white);
                return;
            }

            bool hud = phase == FlightPhase.Countdown || phase == FlightPhase.Flying || phase == FlightPhase.Failed || phase == FlightPhase.Landing;
            if (hud)
            {
                // 아트 HUD가 있으면 내구도·진행 바·조작 안내는 그쪽이 그린다
                if (hudRoot == null)
                {
                    DrawDurability(w);
                    DrawProgress(w);
                    DrawControls(w, h);
                }
            }

            bool artTitles = countdownText != null && goText != null; // 아트 HUD가 카운트다운·출발! 글자를 그린다
            if (phase == FlightPhase.Countdown && !artTitles)
            {
                float left = course.CountdownDuration - t;
                string c =left > 2f ? "3" : left > 1f ? "2" : left > 0f ? "1" : "출발!";
                Shadow(new Rect(0, h * 0.3f, w, 80), c, big, new Color(1f, 0.95f, 0.6f));
            }
            else if (phase == FlightPhase.Flying)
            {
                if (t < 1f && !artTitles) Shadow(new Rect(0, h * 0.3f, w, 80), "출발!", big, new Color(1f, 0.95f, 0.6f));
                if (Time.time - hitAt < 0.8f)
                {
                    float a = 1f - (Time.time - hitAt) / 0.8f;
                    GUI.color = new Color(1f, 0.2f, 0.2f, 0.35f * a);
                    GUI.DrawTexture(new Rect(0, 0, w, h), white);
                    GUI.color = Color.white;
                    // 가운데 "쿵!" 글자는 뺌 (KJ 2026-11-19). 붉은 번쩍임만
                }
                else if (course.Durability == 1)
                    Shadow(new Rect(0, 90, w, 30), "내구도 1! 조심하세요", small, new Color(1f, 0.5f, 0.4f, 0.6f + 0.4f * Mathf.Sin(Time.time * 8f)));
            }
            else if (phase == FlightPhase.Failed)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / course.FailRestartDelay) * 0.6f);
                GUI.DrawTexture(new Rect(0, 0, w, h), white);
                GUI.color = Color.white;
                // 가운데 실패 문구("번개에 맞았어요!"/"뗏목이 부서졌어요!" + "N초 뒤 다시")는 뺌 (KJ 2026-11-19). 화면 어두워짐만
            }
            else if (phase == FlightPhase.Arrived && t < arrivalTitleTime)
            {
                float a = Mathf.Clamp01(t * 2f) * Mathf.Clamp01((arrivalTitleTime - t) * 2f);
                Shadow(new Rect(0, h * 0.3f, w, 80), "STAGE 2", big, new Color(1f, 1f, 1f, a));
                Shadow(new Rect(0, h * 0.3f + 72, w, 40), "새로운 땅에 도착했어요", mid, new Color(1f, 0.95f, 0.75f, a));
            }
            if (phase == FlightPhase.Arrived && course.HasNextScene && t > arrivalTitleTime * 0.5f)
            {
                string msg = course.LoadingNext ? "스테이지 2로 이동 중…" : $"{Mathf.CeilToInt(Mathf.Max(0f, course.NextSceneDelay - t))}초 뒤 스테이지 2로";
                Shadow(new Rect(0, h * 0.3f + 120, w, 30), msg, small, Color.white);
            }
        }

        void DrawDurability(float w)
        {
            int max = course.MaxDurability, cur = course.Durability;
            float size = 34f, gap = 8f;
            float x = 24f, y = 20f;
            GUI.Label(new Rect(x, y, 120, 24), "뗏목 내구도", small);
            y += 26f;
            bool shake = Time.time - hitAt < 0.5f;
            for (int i = 0; i < max; i++)
            {
                var r = new Rect(x + i * (size + gap), y, size, size);
                if (shake && i == cur) r.x += Mathf.Sin(Time.time * 60f) * 4f;
                GUI.color = i < cur ? new Color(0.55f, 0.85f, 1f) : new Color(0.25f, 0.25f, 0.3f, 0.7f);
                GUI.DrawTexture(r, white);
            }
            GUI.color = Color.white;
        }

        void DrawProgress(float w)
        {
            float bw = Mathf.Min(520f, w * 0.5f), x = (w - bw) * 0.5f, y = 24f;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, y, bw, 12), white);
            GUI.color = new Color(1f, 0.85f, 0.4f);
            GUI.DrawTexture(new Rect(x, y, bw * course.Progress, 12), white);
            GUI.color = Color.white;
            Shadow(new Rect(x, y + 14, bw, 26), $"{course.ZoneId}  {course.ZoneTitle}", small, Color.white);
            // 속도: 안 부딪히고 날수록 빨라진다 (기본 속도 대비 배율)
            if (course.Phase == FlightPhase.Flying)
            {
                float k = Mathf.InverseLerp(course.BaseForwardSpeed, course.MaxForwardSpeed, course.ForwardSpeed);
                var c = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.25f), k);
                string label = k >= 0.999f ? "최고 속도!" : $"속도 ×{course.ForwardSpeed / course.BaseForwardSpeed:0.00}";
                Shadow(new Rect(x, y + 38, bw, 24), label, small, c);
            }
        }

        void DrawControls(float w, float h)
        {
            var role = SwitchStates.LocalRole;
            bool online = GameServices.IsOnline;
            float bw = 150f, bh = 54f, y = h - bh - 30f;
            // 빨강 = A(왼쪽), 파랑 = D(오른쪽). 온라인에서는 내 버튼만 진하게
            DrawKey(new Rect(w * 0.5f - bw - 20f, y, bw, bh), "A  ◀ 왼쪽", new Color(1f, 0.35f, 0.3f), course.LocalRedPressed || course.State.IsPressing(PlayerRole.A_Red), !online || role == PlayerRole.A_Red);
            DrawKey(new Rect(w * 0.5f + 20f, y, bw, bh), "오른쪽 ▶  D", new Color(0.3f, 0.55f, 1f), course.LocalBluePressed || course.State.IsPressing(PlayerRole.B_Blue), !online || role == PlayerRole.B_Blue);
            Shadow(new Rect(0, y - 28f, w, 24), "같이 누르면 직진", small, new Color(1f, 1f, 1f, 0.7f));
        }

        void DrawKey(Rect r, string text, Color color, bool pressed, bool mine)
        {
            float alpha = mine ? 1f : 0.45f;
            GUI.color = pressed ? new Color(color.r, color.g, color.b, alpha) : new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.75f * alpha);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            Shadow(r, text, mid, new Color(1f, 1f, 1f, alpha));
        }

        static void Shadow(Rect r, string text, GUIStyle style, Color color)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = c;
        }
    }
}
