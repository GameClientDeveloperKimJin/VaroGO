using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    /// <summary>
    /// 핑 표시. 받은 핑(내 것·파트너 것)을 월드에 고리 파동 + 기둥으로 그리고, 화면에 글자와 거리를 띄운다.
    ///   Step   (여기 밟아!) : 초록 고리가 퍼져 나가는 착지 지점 + 경쾌한 알림음
    ///   Danger (위험/멈춰!) : 빨간 고리가 빠르게 깜빡이는 경고 + 경고음
    /// 화면 밖이면 화면 가장자리에 붙여 보여준다. 보내는 사람마다 핑은 1개만 남는다(새로 찍으면 이전 것 교체).
    /// 씬에 따로 놓지 않는다. 게임 시작 시 스스로 하나 만들어 씬이 바뀌어도 유지한다.
    /// 그레이박스용 연출(LineRenderer + OnGUI). 아트가 들어오면 교체한다.
    /// </summary>
    public class PingMarkers : MonoBehaviour
    {
        const float StepLifetime = 5f;
        const float DangerLifetime = 6f;
        const float FadeTime = 0.5f;
        const float BeamHeight = 2.5f;
        const float EdgeMargin = 56f;
        const int RingSegments = 40;
        const int RingCount = 3;

        static readonly Color StepColor = new Color(0.3f, 1f, 0.55f);
        static readonly Color DangerColor = new Color(1f, 0.22f, 0.18f);

        // 온라인 핑 구현이 없으면(오프라인·서버 미구현) 내 화면에만 보이는 기본 구현을 쓴다
        static readonly OfflinePingSystem fallback = new OfflinePingSystem();
        public static IPingSystem Service => GameServices.Ping ?? fallback;

        class Ping
        {
            public PingType type;
            public PlayerRole sender;
            public Vector3 position;
            public Vector3 normal;
            public float startTime;
            public float lifetime;
            public GameObject root;
            public LineRenderer[] rings;
            public LineRenderer beam;
        }

        readonly List<Ping> pings = new List<Ping>();
        IPingSystem subscribed;
        AudioSource audioSource;
        Material lineMaterial;
        AudioClip stepClip, dangerClip;
        GUIStyle labelStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("PingSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<PingMarkers>();
            go.AddComponent<PingWheel>();
        }

        void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 파트너가 멀리서 찍어도 들리게 2D로
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            stepClip = TempSfx.Chime;
            dangerClip = MakeWarning();
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.PingReceived -= OnPingReceived;
            subscribed = null;
        }

        void Update()
        {
            // 서버가 RegisterPing을 늦게 하거나 해제해도 따라간다
            var current = Service;
            if (current != subscribed)
            {
                if (subscribed != null) subscribed.PingReceived -= OnPingReceived;
                subscribed = current;
                subscribed.PingReceived += OnPingReceived;
            }

            float now = Time.time;
            for (int i = pings.Count - 1; i >= 0; i--)
            {
                var p = pings[i];
                float age = now - p.startTime;
                if (age >= p.lifetime || p.root == null)
                {
                    if (p.root != null) Destroy(p.root);
                    pings.RemoveAt(i);
                    continue;
                }
                Animate(p, age);
            }
        }

        void OnPingReceived(PingType type, Vector3 position, Vector3 normal, PlayerRole sender)
        {
            // 같은 사람이 찍은 이전 핑은 지운다
            for (int i = pings.Count - 1; i >= 0; i--)
            {
                if (pings[i].sender != sender) continue;
                if (pings[i].root != null) Destroy(pings[i].root);
                pings.RemoveAt(i);
            }

            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            var p = new Ping
            {
                type = type,
                sender = sender,
                position = position,
                normal = normal.normalized,
                startTime = Time.time,
                lifetime = type == PingType.Danger ? DangerLifetime : StepLifetime,
            };
            Build(p);
            pings.Add(p);

            audioSource.PlayOneShot(type == PingType.Danger ? dangerClip : stepClip, 0.7f);
        }

        void Build(Ping p)
        {
            p.root = new GameObject($"Ping_{p.type}_{p.sender}");
            p.root.transform.SetParent(transform, false);
            // 고리는 맞은 면에 붙인다(벽에 찍은 위험 핑은 벽면에 세워진다). 살짝 띄워 겹쳐 깜빡이는 것을 막는다
            p.root.transform.SetPositionAndRotation(p.position + p.normal * 0.03f,
                Quaternion.FromToRotation(Vector3.up, p.normal));

            p.rings = new LineRenderer[RingCount];
            for (int r = 0; r < RingCount; r++)
            {
                var lr = NewLine($"Ring{r}", p.root.transform, 0.07f);
                lr.loop = true;
                lr.positionCount = RingSegments;
                p.rings[r] = lr;
            }

            // 멀리서도 보이게 위로 솟은 기둥 (바닥에 찍은 핑만 위로, 벽이면 벽에서 바깥으로)
            p.beam = NewLine("Beam", p.root.transform, 0.05f);
            p.beam.positionCount = 2;
            p.beam.SetPosition(0, Vector3.zero);
            p.beam.SetPosition(1, Vector3.up * BeamHeight);
        }

        LineRenderer NewLine(string name, Transform parent, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = lineMaterial;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            return lr;
        }

        void Animate(Ping p, float age)
        {
            bool danger = p.type == PingType.Danger;
            Color baseColor = danger ? DangerColor : StepColor;
            float fade = Mathf.Clamp01((p.lifetime - age) / FadeTime) * Mathf.Clamp01(age / 0.1f);

            // 고리 파동: 안쪽에서 바깥으로 퍼지며 옅어진다. 위험 핑은 더 빠르고 작게
            float period = danger ? 0.6f : 1.2f;
            float minR = danger ? 0.2f : 0.25f;
            float maxR = danger ? 1.1f : 1.4f;
            for (int r = 0; r < p.rings.Length; r++)
            {
                float k = Mathf.Repeat(age / period + (float)r / p.rings.Length, 1f);
                float radius = Mathf.Lerp(minR, maxR, k);
                float alpha = (1f - k) * fade;
                SetCircle(p.rings[r], radius);
                SetColor(p.rings[r], baseColor, alpha);
            }

            // 위험 핑 기둥은 깜빡인다
            float beamAlpha = danger ? (0.5f + 0.5f * Mathf.Sign(Mathf.Sin(age * Mathf.PI * 4f))) : 0.8f;
            SetColor(p.beam, baseColor, beamAlpha * fade);
        }

        static readonly Vector3[] circleBuffer = new Vector3[RingSegments];

        static void SetCircle(LineRenderer lr, float radius)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                circleBuffer[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            lr.SetPositions(circleBuffer);
        }

        static void SetColor(LineRenderer lr, Color c, float alpha)
        {
            c.a = alpha;
            lr.startColor = c;
            lr.endColor = c;
        }

        void OnGUI()
        {
            if (pings.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };

            Vector3 viewer = cam.transform.position;
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null) viewer = localPlayer.transform.position;
            PlayerRole localRole = SwitchStates.LocalRole;

            float now = Time.time;
            foreach (var p in pings)
            {
                float age = now - p.startTime;
                float fade = Mathf.Clamp01((p.lifetime - age) / FadeTime);
                Vector3 world = p.position + Vector3.up * 0.6f;
                Vector3 vp = cam.WorldToViewportPoint(world);
                bool behind = vp.z < 0f;
                bool onScreen = !behind && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;

                Vector2 screen;
                if (onScreen)
                {
                    screen = new Vector2(vp.x * Screen.width, vp.y * Screen.height);
                }
                else
                {
                    // 화면 밖 → 그 방향의 화면 가장자리에 붙인다 (카메라 뒤면 방향을 뒤집는다)
                    Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                    if (behind) dir = -dir;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                    dir.Normalize();
                    float halfW = Screen.width * 0.5f - EdgeMargin;
                    float halfH = Screen.height * 0.5f - EdgeMargin;
                    float k = Mathf.Min(Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                                        Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
                    screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + dir * k;
                }

                bool danger = p.type == PingType.Danger;
                string title = danger ? "!! 위험 !!" : "여기 밟아!";
                string who = p.sender == localRole ? "" : " (파트너)";
                float dist = Vector3.Distance(viewer, p.position);
                string text = $"{title}{who}\n{dist:0}m";

                var rect = new Rect(screen.x - 110f, Screen.height - screen.y - 50f, 220f, 56f);
                Color c = danger ? DangerColor : StepColor;
                // 위험 핑 글자는 깜빡인다
                if (danger && Mathf.Sin(age * Mathf.PI * 4f) < 0f) c = Color.Lerp(c, Color.white, 0.5f);

                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.8f * fade);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, labelStyle);
                GUI.color = new Color(c.r, c.g, c.b, fade);
                GUI.Label(rect, text, labelStyle);
                GUI.color = prev;
            }
        }

        static PlayerController FindLocalPlayer()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].IsLocal) return all[i];
            return null;
        }

        // "삐-뽀-삐" 높낮이가 바뀌는 짧은 경고음
        static AudioClip MakeWarning()
        {
            const int rate = 44100;
            float[] notes = { 988f, 740f, 988f };
            const float noteLength = 0.11f;
            int noteSamples = Mathf.CeilToInt(rate * noteLength);
            var data = new float[noteSamples * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            {
                for (int i = 0; i < noteSamples; i++)
                {
                    float t = (float)i / rate;
                    float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * notes[n] * t));
                    float env = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((noteLength - t) / 0.02f);
                    data[n * noteSamples + i] = square * env * 0.3f;
                }
            }
            var clip = AudioClip.Create("TempPingWarning", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
