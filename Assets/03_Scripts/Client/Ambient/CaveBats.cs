using System.Collections.Generic;
using UnityEngine;
using VaroGO.Client.Map;

namespace VaroGO.Client.Ambient
{
    // Z3 동굴 박쥐 연출. 동굴 중심선(CaveDarkness.path)을 따라 천장 부근을 박쥐 여러 마리가 천천히 날아다닌다.
    // 연출 전용: 충돌·물리 없음, 각 PC가 따로 재생 (동기화 안 함).
    // 플레이어나 카메라가 동굴 안에 있을 때만 움직이고, 카메라에서 가까운 박쥐만 그린다.
    // 박쥐 모델(Bat Prefab)이 비어 있으면 임시 메시(몸통 + 날개 2장)를 만든다.
    // 모델에 Animator가 있으면 날개는 애니메이션에 맡기고, 없으면 Wing_L / Wing_R 자식을 스크립트로 퍼덕인다.
    public class CaveBats : MonoBehaviour
    {
        [Header("동굴")]
        [Tooltip("동굴 범위를 읽어 올 CaveDarkness. 비우면 씬에서 찾는다")]
        [SerializeField] CaveDarkness cave;

        [Header("박쥐")]
        [Tooltip("박쥐 모델 프리팹. 비우면 임시 메시로 대신한다")]
        [SerializeField] GameObject batPrefab;
        [Tooltip("박쥐 수")]
        [SerializeField, Range(1, 40)] int count = 12;
        [Tooltip("박쥐 크기 배율")]
        [SerializeField, Min(0.05f)] float scale = 1f;

        [Header("나는 범위")]
        [Tooltip("길(중심선) 위로 최소 높이 (m)")]
        [SerializeField] float minHeight = 6f;
        [Tooltip("길(중심선) 위로 최대 높이 (m)")]
        [SerializeField] float maxHeight = 16f;
        [Tooltip("중심선에서 좌우로 벗어날 수 있는 거리 (m). Z3 동굴은 천장 쪽이 좁아 16m 높이에서 왼쪽 벽까지 약 10m")]
        [SerializeField, Min(0f)] float sideRange = 9f;
        [Tooltip("다음 목적지를 고를 때 동굴 방향으로 앞뒤 최대 거리 (m)")]
        [SerializeField, Min(2f)] float wanderDistance = 25f;

        [Header("움직임")]
        [Tooltip("나는 속도 최소·최대 (m/s)")]
        [SerializeField] Vector2 speed = new Vector2(3f, 6f);
        [Tooltip("방향을 트는 빠르기 (클수록 급하게 돈다)")]
        [SerializeField, Min(0.1f)] float turnRate = 2.5f;
        [Tooltip("위아래 흔들림 크기 (m)")]
        [SerializeField, Min(0f)] float bob = 0.6f;

        [Header("플레이어 근처")]
        [Range(0f, 1f), Tooltip("목적지를 고를 때 플레이어 근처를 지나가기로 할 확률")]
        [SerializeField] float nearPassChance = 0.1f;
        [Tooltip("플레이어 근처를 지날 때 머리 위 최소 높이 (m)")]
        [SerializeField, Min(0f)] float nearPassHeight = 3.5f;
        [Tooltip("카메라에서 이 거리(m) 안으로는 들어오지 않게 밀어낸다 (화면 가림 방지)")]
        [SerializeField, Min(0f)] float cameraClearance = 4f;

        [Header("날개 (모델에 Animator가 없을 때)")]
        [Tooltip("초당 날갯짓 횟수")]
        [SerializeField, Min(0f)] float flapRate = 9f;
        [Tooltip("날갯짓 각도 (도)")]
        [SerializeField, Range(0f, 90f)] float flapAngle = 55f;

        [Header("성능")]
        [Tooltip("카메라에서 이 거리(m) 안의 박쥐만 그린다")]
        [SerializeField, Min(5f)] float renderDistance = 45f;

        class Bat
        {
            public Transform root;
            public Renderer[] renderers;
            public Transform wingL, wingR;
            public bool animated;
            public Vector3 velocity, target;
            public float speed, phase, bobPhase;
            public bool visible = true;
        }

        readonly List<Bat> bats = new List<Bat>();
        float[] cumulative;
        float totalLength;
        bool active;
        float findTimer;
        PlayerController player;
        static Material tempMaterial;

        public bool Active => active;

        void Start()
        {
            if (cave == null) cave = FindAnyObjectByType<CaveDarkness>();
            if (cave == null || cave.path == null || cave.path.Length < 2)
            {
                Debug.LogWarning("[CaveBats] CaveDarkness(동굴 중심선)를 찾지 못했습니다", this);
                enabled = false;
                return;
            }
            BuildPath();
            for (int i = 0; i < count; i++) bats.Add(Spawn(i));
            SetActive(false);
        }

        void BuildPath()
        {
            var p = cave.path;
            cumulative = new float[p.Length];
            for (int i = 1; i < p.Length; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(Flat(p[i - 1]), Flat(p[i]));
            totalLength = cumulative[p.Length - 1];
        }

        Bat Spawn(int index)
        {
            var b = new Bat();
            GameObject go = batPrefab != null ? Instantiate(batPrefab, transform) : BuildTempBat();
            go.name = $"Bat_{index:00}";
            go.transform.SetParent(transform, true);
            go.transform.localScale *= scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c); // 연출 전용: 충돌 없음
            b.root = go.transform;
            b.renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in b.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.animated = go.GetComponentInChildren<Animator>() != null;
            b.wingL = FindDeep(go.transform, "Wing_L");
            b.wingR = FindDeep(go.transform, "Wing_R");
            b.speed = Random.Range(speed.x, speed.y);
            b.phase = Random.value * 10f;
            b.bobPhase = Random.value * 10f;
            b.root.position = RandomPoint(Random.value * totalLength);
            b.target = PickTarget(b.root.position);
            b.velocity = (b.target - b.root.position).normalized * b.speed;
            return b;
        }

        GameObject BuildTempBat()
        {
            if (tempMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                tempMaterial = new Material(shader) { name = "TempBat" };
                var c = new Color(0.08f, 0.06f, 0.08f);
                if (tempMaterial.HasProperty("_BaseColor")) tempMaterial.SetColor("_BaseColor", c);
                if (tempMaterial.HasProperty("_Color")) tempMaterial.SetColor("_Color", c);
            }
            var root = new GameObject("Bat");
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.22f, 0.18f, 0.35f);
            body.GetComponent<Renderer>().sharedMaterial = tempMaterial;
            for (int side = -1; side <= 1; side += 2)
            {
                // 날개 피벗은 몸통 옆. 그 안에 판을 바깥쪽으로 밀어 둔다
                var pivot = new GameObject(side < 0 ? "Wing_L" : "Wing_R").transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(0.09f * side, 0.02f, 0f);
                var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wing.name = "Membrane";
                wing.transform.SetParent(pivot, false);
                wing.transform.localPosition = new Vector3(0.28f * side, 0f, -0.03f);
                wing.transform.localScale = new Vector3(0.55f, 0.015f, 0.3f);
                wing.GetComponent<Renderer>().sharedMaterial = tempMaterial;
            }
            return root;
        }

        void Update()
        {
            if (cave == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 cp = cam.transform.position;

            findTimer -= Time.deltaTime;
            if (player == null || findTimer <= 0f)
            {
                findTimer = 1f;
                player = null;
                foreach (var p in PlayerController.All)
                    if (p != null && p.IsLocal) { player = p; break; }
            }

            bool inside = IsInside(cp) || (player != null && IsInside(player.transform.position));
            if (inside != active) SetActive(inside);
            if (!active) return;

            float dt = Time.deltaTime;
            float r2 = renderDistance * renderDistance;
            foreach (var b in bats)
            {
                Vector3 pos = b.root.position;
                if ((b.target - pos).sqrMagnitude < 4f) b.target = PickTarget(pos);

                Vector3 desired = (b.target - pos).normalized * b.speed;
                // 카메라 앞을 가리지 않게 밀어낸다
                Vector3 away = pos - cp;
                float d = away.magnitude;
                if (cameraClearance > 0f && d < cameraClearance && d > 1e-3f)
                {
                    desired += away / d * b.speed * 2f * (1f - d / cameraClearance);
                    if (d < cameraClearance * 0.6f) b.target = PickTarget(pos);
                }
                b.velocity = Vector3.Lerp(b.velocity, desired, 1f - Mathf.Exp(-turnRate * dt));
                pos += b.velocity * dt;
                pos.y += Mathf.Sin(Time.time * 2.3f + b.bobPhase) * bob * dt;
                b.root.position = pos;
                if (b.velocity.sqrMagnitude > 0.01f)
                {
                    // 도는 방향으로 살짝 기울인다
                    var look = Quaternion.LookRotation(b.velocity.normalized, Vector3.up);
                    float bank = Mathf.Clamp(Vector3.Dot(Vector3.Cross(b.root.forward, b.velocity.normalized), Vector3.up) * 60f, -35f, 35f);
                    b.root.rotation = Quaternion.Slerp(b.root.rotation, look * Quaternion.Euler(0f, 0f, -bank), 1f - Mathf.Exp(-6f * dt));
                }

                bool visible = (pos - cp).sqrMagnitude < r2;
                if (visible != b.visible)
                {
                    b.visible = visible;
                    foreach (var r in b.renderers) if (r != null) r.enabled = visible;
                }
                if (visible && !b.animated) Flap(b);
            }
        }

        void Flap(Bat b)
        {
            float a = Mathf.Sin((Time.time + b.phase) * flapRate * Mathf.PI * 2f) * flapAngle;
            if (b.wingL != null) b.wingL.localRotation = Quaternion.Euler(0f, 0f, -a);
            if (b.wingR != null) b.wingR.localRotation = Quaternion.Euler(0f, 0f, a);
        }

        void SetActive(bool on)
        {
            active = on;
            foreach (var b in bats) if (b.root != null) b.root.gameObject.SetActive(on);
        }

        Vector3 PickTarget(Vector3 from)
        {
            // 가끔 플레이어 근처(머리 위)를 지나간다
            if (player != null && Random.value < nearPassChance && IsInside(player.transform.position))
            {
                Vector3 p = player.transform.position;
                Vector2 off = Random.insideUnitCircle * 4f;
                return new Vector3(p.x + off.x, p.y + nearPassHeight + Random.Range(0f, 2f), p.z + off.y);
            }
            float along = Project(from) + Random.Range(-wanderDistance, wanderDistance);
            return RandomPoint(Mathf.Clamp(along, 0f, totalLength));
        }

        Vector3 RandomPoint(float along)
        {
            PointAt(along, out var center, out var right);
            float side = Mathf.Min(sideRange, cave.insideHalfWidth - 2f);
            return center + right * Random.Range(-side, side) + Vector3.up * Random.Range(minHeight, maxHeight);
        }

        // 중심선 위 along(m) 지점과 그 지점의 오른쪽 방향 (수평)
        void PointAt(float along, out Vector3 point, out Vector3 right)
        {
            var p = cave.path;
            int i = 1;
            while (i < p.Length - 1 && cumulative[i] < along) i++;
            float seg = cumulative[i] - cumulative[i - 1];
            float t = seg > 1e-4f ? Mathf.Clamp01((along - cumulative[i - 1]) / seg) : 0f;
            point = Vector3.Lerp(p[i - 1], p[i], t);
            Vector3 dir = Flat(p[i]) - Flat(p[i - 1]);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            right = Vector3.Cross(Vector3.up, dir.normalized);
        }

        // 위치를 중심선에 투영한 거리 (m, 입구 기준)
        float Project(Vector3 world)
        {
            var p = cave.path;
            float best = float.MaxValue, result = 0f;
            Vector3 w = Flat(world);
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector3 a = Flat(p[i]), ab = Flat(p[i + 1]) - a;
                float l2 = ab.sqrMagnitude; if (l2 < 1e-6f) continue;
                float t = Mathf.Clamp01(Vector3.Dot(w - a, ab) / l2);
                float d = (w - (a + ab * t)).sqrMagnitude;
                if (d < best) { best = d; result = cumulative[i] + Mathf.Sqrt(l2) * t; }
            }
            return result;
        }

        // CaveDarkness와 같은 "동굴 안" 판정 (그쪽 함수는 비공개라 같은 규칙으로 다시 계산)
        bool IsInside(Vector3 pt)
        {
            var p = cave.path;
            float best = float.MaxValue, py = 0f;
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector3 a = p[i], b = p[i + 1];
                Vector2 a2 = new Vector2(a.x, a.z), ab = new Vector2(b.x - a.x, b.z - a.z);
                float l2 = ab.sqrMagnitude; if (l2 < 1e-6f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(pt.x, pt.z) - a2, ab) / l2);
                float d = (new Vector2(pt.x, pt.z) - (a2 + ab * t)).magnitude;
                if (d < best) { best = d; py = Mathf.Lerp(a.y, b.y, t); }
            }
            if (pt.z < p[0].z || pt.z > p[p.Length - 1].z) return false;
            return best < cave.insideHalfWidth && pt.y > py - cave.insideHeight && pt.y < py + cave.insideHeight;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        void OnDrawGizmosSelected()
        {
            var c = cave != null ? cave : FindAnyObjectByType<CaveDarkness>();
            if (c == null || c.path == null) return;
            Gizmos.color = new Color(0.6f, 0.3f, 0.8f, 0.8f);
            for (int i = 0; i < c.path.Length - 1; i++)
            {
                Gizmos.DrawLine(c.path[i] + Vector3.up * minHeight, c.path[i + 1] + Vector3.up * minHeight);
                Gizmos.DrawLine(c.path[i] + Vector3.up * maxHeight, c.path[i + 1] + Vector3.up * maxHeight);
            }
        }
    }
}
