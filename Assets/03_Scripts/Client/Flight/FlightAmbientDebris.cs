using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 배경 잔해: 피하지 않아도 되는 작은 나무 조각들이 비행기 옆·위·아래로 계속 휙휙 지나간다 (던전으로 빨려 들어가는 느낌).
    // 비행 길(좌우 폭) 바깥에만 나오고 판정이 없다. 연출용이라 PC마다 따로 무작위로 돈다 (동기화 안 함).
    // 진행할수록 많아지고 빨라진다. 착륙(빨려 들어가는) 단계에서 가장 세다.
    public class FlightAmbientDebris : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("지나갈 모델들 (무작위로 고름)")]
        [SerializeField] GameObject[] models;
        [Tooltip("models와 같은 순서의 머티리얼 (비우면 모델 그대로)")]
        [SerializeField] Material[] materials;

        [Header("양")]
        [Tooltip("출발 직후 동시에 날아다니는 개수")]
        [SerializeField, Min(0)] int countStart = 12;
        [Tooltip("끝(착륙) 무렵 동시에 날아다니는 개수")]
        [SerializeField, Min(1)] int countEnd = 45;

        [Header("위치 (비행기 기준 m)")]
        [Tooltip("비행기 중심에서 이 거리보다 가까이 오지 않는다 (비행 길 밖)")]
        [SerializeField, Min(1f)] float minRadius = 8f;
        [SerializeField, Min(2f)] float maxRadius = 26f;
        [Tooltip("위아래로 퍼지는 비율 (1 = 동그랗게, 0.5 = 납작하게)")]
        [SerializeField, Range(0.1f, 1f)] float verticalSpread = 0.6f;
        [Tooltip("앞쪽 이 거리 사이에서 나타난다")]
        [SerializeField] Vector2 spawnAhead = new Vector2(60f, 130f);
        [Tooltip("비행기 뒤로 이만큼 지나가면 다시 앞에서 나온다")]
        [SerializeField, Min(1f)] float behindLimit = 15f;

        [Header("움직임")]
        [Tooltip("잔해 스스로 날아오는 속도 범위 (m/초). 비행기 속도에 더해져 보인다")]
        [SerializeField] Vector2 flySpeed = new Vector2(10f, 22f);
        [Tooltip("끝 무렵 속도 배율 (빨려 들어갈수록 빨라짐)")]
        [SerializeField, Min(1f)] float endSpeedMultiplier = 1.8f;
        [Tooltip("크기 범위 (모델 원본 약 1m 기준 배율)")]
        [SerializeField] Vector2 scaleRange = new Vector2(0.3f, 1.3f);
        [Tooltip("빙글 도는 속도 범위 (도/초)")]
        [SerializeField] Vector2 spinSpeed = new Vector2(60f, 240f);

        struct Piece { public Transform t; public Vector3 offset; public float speed; public Vector3 spinAxis; public float spin; }

        Piece[] pieces;
        System.Random rng;

        void Awake()
        {
            if (course == null) course = GetComponentInParent<FlightCourse>();
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
            rng = new System.Random(GetInstanceID());
            int n = Mathf.Max(countStart, countEnd);
            pieces = new Piece[n];
            if (models == null || models.Length == 0) return;
            for (int i = 0; i < n; i++)
            {
                int m = rng.Next(models.Length);
                if (models[m] == null) continue;
                var go = Instantiate(models[m], transform);
                go.name = "AmbientDebris_" + i;
                if (materials != null && m < materials.Length && materials[m] != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = materials[m];
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                go.transform.localScale = Vector3.one * Rand(scaleRange.x, scaleRange.y);
                go.SetActive(false);
                pieces[i] = new Piece
                {
                    t = go.transform,
                    spinAxis = Random3().normalized,
                    spin = Rand(spinSpeed.x, spinSpeed.y),
                };
                Respawn(ref pieces[i], true);
            }
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Vector3 Random3() => new Vector3(Rand(-1f, 1f), Rand(-1f, 1f), Rand(-1f, 1f)) + Vector3.one * 0.01f;

        // 비행 길 밖 링 위 한 점, 앞쪽에서 다시 나온다. first면 앞쪽 전체에 흩뿌린다 (처음부터 빈 하늘이 아니게)
        void Respawn(ref Piece p, bool first)
        {
            float a = Rand(0f, Mathf.PI * 2f);
            float r = Rand(minRadius, Mathf.Max(minRadius + 0.1f, maxRadius));
            float z = first ? Rand(-behindLimit, spawnAhead.y) : Rand(spawnAhead.x, spawnAhead.y);
            p.offset = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * verticalSpread, z);
            p.speed = Rand(flySpeed.x, flySpeed.y);
        }

        void Update()
        {
            if (course == null || pieces == null) return;
            var phase = course.Phase;
            bool active = phase == FlightPhase.Flying || phase == FlightPhase.Landing || phase == FlightPhase.Countdown;
            float progress = phase == FlightPhase.Landing ? 1f : course.Progress;
            int count = active ? Mathf.RoundToInt(Mathf.Lerp(countStart, countEnd, progress)) : 0;
            // 카운트다운 동안은 잔해만 조금 (아직 출발 전)
            if (phase == FlightPhase.Countdown) count = Mathf.Min(count, countStart / 2);

            float dt = Time.deltaTime;
            float raftSpeed = phase == FlightPhase.Flying ? course.ForwardSpeed : (phase == FlightPhase.Landing ? course.BaseForwardSpeed : 0f);
            float mult = Mathf.Lerp(1f, endSpeedMultiplier, progress);
            Vector3 origin = course.Raft != null ? course.Raft.transform.position : course.transform.position;
            Quaternion rot = course.transform.rotation;

            for (int i = 0; i < pieces.Length; i++)
            {
                ref var p = ref pieces[i];
                if (p.t == null) continue;
                bool on = i < count;
                if (p.t.gameObject.activeSelf != on)
                {
                    p.t.gameObject.SetActive(on);
                    if (on) Respawn(ref p, false);
                }
                if (!on) continue;
                p.offset.z -= (raftSpeed + p.speed * mult) * dt;
                if (p.offset.z < -behindLimit) Respawn(ref p, false);
                p.t.position = origin + rot * p.offset;
                p.t.Rotate(p.spinAxis, p.spin * dt, Space.Self);
            }
        }
    }
}
