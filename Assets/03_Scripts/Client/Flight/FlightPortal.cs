using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 비행 포탈. 스테이지 마지막 구역에 둔다. 출발하면 화면이 검게 닫히고 공중 비행 씬이 열린다.
    // 출발 조건
    //   - Require Interact 켬(기본): 포탈 안에서 E(상호작용). 온라인은 두 명이 모두 안에 있어야 누를 수 있다.
    //   - 끔: 들어가기만 하면 출발 (온라인은 두 명 모두 안에 있어야 함).
    // 온라인 동기화: E를 누르면 스위치 상태(stateObject, 비우면 이 오브젝트의 ISwitchState)를 켠다.
    // 상태가 켜지면 모든 PC가 화면을 닫고 StageLoading을 부른다 (방장 PC만 실제로 씬을 바꾸고 모두 따라감).
    // 어느 씬으로 갈지는 flightScenePath로 정한다 (Build Settings에 있어야 온라인·빌드에서 열린다).
    // 판정은 이 오브젝트 위치 기준 원기둥 (radius, height). 콜라이더는 없어도 된다.
    public class FlightPortal : MonoBehaviour
    {
        [Tooltip("들어가면 열 씬 경로 (공중 비행 씬)")]
        [SerializeField] string flightScenePath = "Assets/01_Scenes/PlayScene_Flight.unity";
        [Tooltip("켜면 포탈 안에서 E(상호작용)를 눌러야 출발. 끄면 들어가기만 해도 출발")]
        [SerializeField] bool requireInteract = true;
        [Tooltip("출발 상태를 맞추는 스위치 상태 (온라인 동기화용). 비우면 이 오브젝트에서 찾고, 없으면 각 PC가 따로 판단")]
        [SerializeField] GameObject stateObject;
        [Tooltip("포탈 판정 반지름 (m)")]
        [SerializeField, Min(0.3f)] float radius = 2f;
        [Tooltip("포탈 판정 높이 (m, 이 오브젝트 위치부터 위로)")]
        [SerializeField, Min(0.5f)] float height = 3f;
        [Tooltip("출발 뒤 화면이 검게 닫히는 시간 (초)")]
        [SerializeField, Min(0.1f)] float fadeTime = 1.2f;
        [Tooltip("다음 씬에서 화면이 밝아지는 시간 (초)")]
        [SerializeField, Min(0.1f)] float nextSceneFadeIn = 1.5f;
        [Tooltip("돌아가는 소용돌이 모양 (선택)")]
        [SerializeField] Transform spinVisual;
        [SerializeField] float spinSpeed = 120f;

        ISwitchState state;
        PlayerController subscribed;
        float enteredAt = -1f;
        bool launched, loading;
        int inside, needed;
        bool localInside;
        GUIStyle style;

        public string FlightScenePath => flightScenePath;

        void Awake()
        {
            var target = stateObject != null ? stateObject : (GetComponent<ISwitchState>() != null ? gameObject : null);
            if (target != null) state = SwitchStates.Resolve(target, null, this);
        }

        void OnEnable()
        {
            if (state == null) return;
            state.Changed += OnStateChanged;
            if (state.IsOn) Launch();
        }

        void OnDisable()
        {
            if (state != null) state.Changed -= OnStateChanged;
            Subscribe(null);
        }

        void OnStateChanged(bool on)
        {
            if (on) Launch();
        }

        void Update()
        {
            if (spinVisual != null) spinVisual.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
            if (loading) return;

            CountInside();
            bool ready = inside >= needed && needed > 0;

            if (!launched)
            {
                Subscribe(requireInteract && ready && localInside ? LocalPlayer() : null);
                if (!requireInteract && ready) RequestLaunch();
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

        // 출발 요청. 상태가 있으면 상태를 켜고(동기화 → OnStateChanged에서 Launch), 없으면 바로 출발
        void RequestLaunch()
        {
            if (launched) return;
            if (state != null)
            {
                state.RequestSet(true, SwitchStates.LocalRole);
                if (state.IsOn) Launch();
            }
            else Launch();
        }

        void Launch()
        {
            if (launched) return;
            launched = true;
            Subscribe(null);
        }

        void OnInteract()
        {
            if (launched || inside < needed) return;
            RequestLaunch();
            AudioSource.PlayClipAtPoint(TempSfx.Chime, Camera.main != null ? Camera.main.transform.position : transform.position, 0.8f);
        }

        void Subscribe(PlayerController player)
        {
            if (subscribed == player) return;
            if (subscribed != null) subscribed.Interacted -= OnInteract;
            subscribed = player;
            if (subscribed != null) subscribed.Interacted += OnInteract;
        }

        static PlayerController LocalPlayer()
        {
            foreach (var p in PlayerController.All)
                if (p != null && p.IsLocal && p.Role != PlayerRole.None) return p;
            return null;
        }

        void CountInside()
        {
            inside = 0;
            localInside = false;
            needed = GameServices.IsOnline ? 2 : 1;
            foreach (var p in PlayerController.All)
            {
                if (p == null || p.Role == PlayerRole.None) continue;
                // 오프라인은 내 캐릭터만 센다 (더미 파트너는 제외)
                if (!GameServices.IsOnline && !p.IsLocal) continue;
                if (!Contains(p.transform.position)) continue;
                inside++;
                if (p.IsLocal) localInside = true;
            }
        }

        bool Contains(Vector3 world)
        {
            Vector3 d = world - transform.position;
            if (d.y < -0.5f || d.y > height) return false;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        // 그레이박스용 임시 안내. UI가 들어오면 교체한다
        void OnGUI()
        {
            if (launched || !localInside) return;
            string text;
            if (inside < needed) text = "파트너를 기다리는 중… (1/2)";
            else if (requireInteract) text = "E: 포탈로 들어가기";
            else return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
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
