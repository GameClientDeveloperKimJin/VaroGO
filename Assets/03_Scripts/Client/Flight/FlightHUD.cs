using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 비행 화면 표시 (임시 OnGUI, UI가 들어오면 교체).
    // 내구도·진행도·구간 이름, A/D 조작 안내(누르면 밝아짐), 카운트다운, 피격 경고, 실패·다시 시작, 탑승 안내, 도착(STAGE 2)
    public class FlightHUD : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("도착 제목이 보이는 시간 (초)")]
        [SerializeField, Min(0.5f)] float arrivalTitleTime = 4f;

        float hitAt = -10f;
        GUIStyle big, mid, small, box;
        Texture2D white;

        void Awake()
        {
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
        }

        void OnEnable() { if (course != null) course.Hit += OnHit; }
        void OnDisable() { if (course != null) course.Hit -= OnHit; }
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
                DrawDurability(w);
                DrawProgress(w);
                DrawControls(w, h);
            }

            if (phase == FlightPhase.Countdown)
            {
                float left = course.CountdownDuration - t;
                string c = left > 2f ? "3" : left > 1f ? "2" : left > 0f ? "1" : "출발!";
                Shadow(new Rect(0, h * 0.3f, w, 80), c, big, new Color(1f, 0.95f, 0.6f));
            }
            else if (phase == FlightPhase.Flying)
            {
                if (t < 1f) Shadow(new Rect(0, h * 0.3f, w, 80), "출발!", big, new Color(1f, 0.95f, 0.6f));
                if (Time.time - hitAt < 0.8f)
                {
                    float a = 1f - (Time.time - hitAt) / 0.8f;
                    GUI.color = new Color(1f, 0.2f, 0.2f, 0.35f * a);
                    GUI.DrawTexture(new Rect(0, 0, w, h), white);
                    GUI.color = Color.white;
                    Shadow(new Rect(0, h * 0.32f, w, 80), "쿵!", big, new Color(1f, 0.4f, 0.4f, a));
                }
                else if (course.Durability == 1)
                    Shadow(new Rect(0, 90, w, 30), "내구도 1! 조심하세요", small, new Color(1f, 0.5f, 0.4f, 0.6f + 0.4f * Mathf.Sin(Time.time * 8f)));
            }
            else if (phase == FlightPhase.Failed)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / course.FailRestartDelay) * 0.6f);
                GUI.DrawTexture(new Rect(0, 0, w, h), white);
                GUI.color = Color.white;
                Shadow(new Rect(0, h * 0.35f, w, 80), "뗏목이 부서졌어요!", big, new Color(1f, 0.6f, 0.5f));
                int sec = Mathf.CeilToInt(Mathf.Max(0f, course.FailRestartDelay - t));
                Shadow(new Rect(0, h * 0.35f + 80, w, 40), $"{sec}초 뒤 처음부터 다시", mid, Color.white);
            }
            else if (phase == FlightPhase.Arrived && t < arrivalTitleTime)
            {
                float a = Mathf.Clamp01(t * 2f) * Mathf.Clamp01((arrivalTitleTime - t) * 2f);
                Shadow(new Rect(0, h * 0.3f, w, 80), "STAGE 2", big, new Color(1f, 1f, 1f, a));
                Shadow(new Rect(0, h * 0.3f + 72, w, 40), "새로운 땅에 도착했어요", mid, new Color(1f, 0.95f, 0.75f, a));
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
