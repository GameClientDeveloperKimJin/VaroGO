using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 비행 포탈. 스테이지 마지막 구역에 둔다. 들어가면 화면이 검게 닫히고 공중 비행 씬이 열린다.
    // 오프라인: 내 캐릭터가 들어가면 출발. 온라인: 두 명이 모두 포탈 안에 있어야 출발 (방장 PC가 씬을 바꾸고 모두 따라감).
    // 어느 씬으로 갈지는 flightScenePath로 정한다 (Build Settings에 있어야 온라인·빌드에서 열린다).
    // 판정은 이 오브젝트 위치 기준 원기둥 (radius, height). 콜라이더는 없어도 된다.
    public class FlightPortal : MonoBehaviour
    {
        [Tooltip("들어가면 열 씬 경로 (공중 비행 씬)")]
        [SerializeField] string flightScenePath = "Assets/01_Scenes/PlayScene_Flight.unity";
        [Tooltip("포탈 판정 반지름 (m)")]
        [SerializeField, Min(0.3f)] float radius = 2f;
        [Tooltip("포탈 판정 높이 (m, 이 오브젝트 위치부터 위로)")]
        [SerializeField, Min(0.5f)] float height = 3f;
        [Tooltip("두 명이 들어간 뒤 화면이 검게 닫히는 시간 (초)")]
        [SerializeField, Min(0.1f)] float fadeTime = 1.2f;
        [Tooltip("다음 씬에서 화면이 밝아지는 시간 (초)")]
        [SerializeField, Min(0.1f)] float nextSceneFadeIn = 1.5f;
        [Tooltip("돌아가는 소용돌이 모양 (선택)")]
        [SerializeField] Transform spinVisual;
        [SerializeField] float spinSpeed = 120f;

        float enteredAt = -1f;
        bool loading;
        int inside, needed;
        GUIStyle style;

        public string FlightScenePath => flightScenePath;

        void Update()
        {
            if (spinVisual != null) spinVisual.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
            if (loading) return;

            CountInside();
            bool ready = inside >= needed && needed > 0;
            if (!ready)
            {
                if (enteredAt >= 0f) { enteredAt = -1f; ScreenFade.FadeIn(0.3f); }
                return;
            }
            if (enteredAt < 0f)
            {
                enteredAt = Time.unscaledTime;
                ScreenFade.FadeOut(fadeTime, true, nextSceneFadeIn);
            }
            if (Time.unscaledTime - enteredAt < fadeTime) return;
            loading = true;
            StageLoading.Load(flightScenePath);
        }

        void CountInside()
        {
            inside = 0;
            needed = GameServices.IsOnline ? 2 : 1;
            foreach (var p in PlayerController.All)
            {
                if (p == null || p.Role == PlayerRole.None) continue;
                // 오프라인은 내 캐릭터만 센다 (더미 파트너는 제외)
                if (!GameServices.IsOnline && !p.IsLocal) continue;
                if (Contains(p.transform.position)) inside++;
            }
        }

        bool Contains(Vector3 world)
        {
            Vector3 d = world - transform.position;
            if (d.y < -0.5f || d.y > height) return false;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void OnGUI()
        {
            if (loading || !GameServices.IsOnline || inside <= 0 || inside >= needed) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            const string text = "파트너를 기다리는 중… (1/2)";
            var size = style.CalcSize(new GUIContent(text));
            GUI.Box(new Rect((Screen.width - size.x - 24f) * 0.5f, Screen.height * 0.72f, size.x + 24f, size.y + 12f), text, style);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.8f);
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 p0 = transform.position + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = transform.position + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Gizmos.DrawLine(p0, p1);
                Gizmos.DrawLine(p0 + Vector3.up * height, p1 + Vector3.up * height);
            }
        }
    }
}
