using UnityEngine;

namespace VaroGO.Client
{
    // P19 폭풍 하늘 배경: 먹구름(StormCloud)을 비행 길 위·옆에 코스 전체에 깔아 둔다.
    // 번개 지점 구름(LightningStrike 자식)만 있으면 하늘에 구름이 두어 개뿐이라 어색하므로, 이걸로 하늘을 채운다.
    // 뒤(끝)로 갈수록 빽빽해진다 (어두운 폭풍 속으로 들어가는 느낌). 판정 없음.
    // 먼 배경 번개(lightning)가 칠 때 가장 가까운 구름이 같이 번쩍인다. 따로 무작위 번쩍임도 둘 수 있다. 연출용이라 PC마다 따로 돈다 (동기화 안 함). 배치는 seed로 고정이라 모든 PC가 같은 모양.
    public class FlightStormClouds : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("먹구름 모델 (StormCloud.fbx)")]
        [SerializeField] GameObject model;
        [SerializeField] Material material;
        [Tooltip("배치 무작위 seed. 바꾸면 구름 배치가 달라진다")]
        [SerializeField] int seed = 7;

        [Header("양")]
        [Tooltip("코스 전체에 까는 구름 개수")]
        [SerializeField, Min(0)] int count = 36;
        [Tooltip("끝 쪽이 앞쪽보다 몇 배 빽빽한지 (1 = 고르게)")]
        [SerializeField, Range(1f, 4f)] float endDensity = 2.5f;

        [Header("위치 (코스 기준 m)")]
        [Tooltip("출발 전·끝 뒤로 더 까는 거리 (앞, 뒤)")]
        [SerializeField] Vector2 extraLength = new Vector2(60f, 120f);
        [Tooltip("비행 길 중심에서 좌우로 떨어진 거리 범위. 위쪽 구름은 이 최소값보다 가까워도 된다")]
        [SerializeField] Vector2 sideDistance = new Vector2(24f, 110f);
        [Tooltip("높이 범위 (비행 길 기준)")]
        [SerializeField] Vector2 height = new Vector2(22f, 70f);
        [Tooltip("비행 길 바로 위에 오는 구름의 최소 높이 (화면을 가리지 않게)")]
        [SerializeField, Min(10f)] float minHeightOverLane = 38f;
        [Tooltip("크기 배율 범위 (모델 원본 약 1m 기준)")]
        [SerializeField] Vector2 scaleRange = new Vector2(20f, 46f);

        [Header("먼 번개")]
        [Tooltip("먼 배경 번개 파티클 (VFX_Flight_DistantLightning). 번개가 칠 때마다 가장 가까운 구름이 같이 번쩍인다")]
        [SerializeField] ParticleSystem[] lightning;
        [Tooltip("번개에서 이 거리(m) 안에 구름이 없으면 구름은 번쩍이지 않는다")]
        [SerializeField, Min(1f)] float lightningCloudRange = 120f;
        [Tooltip("번개와 상관없이 무작위 구름이 번쩍이는 간격 (초, 범위). 0이면 안 함 (먼 번개를 연결했으면 0 권장)")]
        [SerializeField] Vector2 flashInterval = new Vector2(2.5f, 7f);
        [Tooltip("번쩍임이 줄어드는 시간 (초)")]
        [SerializeField, Min(0.05f)] float flashTime = 0.4f;

        static readonly int WarnProgressId = Shader.PropertyToID("_WarnProgress");
        static readonly int StrikeFlashId = Shader.PropertyToID("_StrikeFlash");

        Renderer[][] clouds;
        Vector3[] cloudPos;
        float[] flashStarts; // 구름별 번쩍임 시작 시각, -1 = 안 번쩍임
        MaterialPropertyBlock mpb;
        float nextFlash;
        ParticleSystem.Particle[] particleBuf;

        void Awake()
        {
            if (course == null) course = GetComponentInParent<FlightCourse>();
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
            mpb = new MaterialPropertyBlock();
            Build();
            nextFlash = Time.time + Random.Range(flashInterval.x, flashInterval.y);
        }

        void Build()
        {
            if (model == null || course == null) return;
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            float z0 = -extraLength.x, z1 = course.FlightLength + extraLength.y;
            clouds = new Renderer[count][];
            cloudPos = new Vector3[count];
            flashStarts = new float[count];
            for (int i = 0; i < count; i++) flashStarts[i] = -1f;
            for (int i = 0; i < count; i++)
            {
                // 끝 쪽으로 몰리게: u^(1/endDensity) 꼴 분포
                float u = (float)rng.NextDouble();
                float z = Mathf.Lerp(z0, z1, Mathf.Pow(u, 1f / endDensity));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * R(0f, sideDistance.y);
                float y = R(height.x, height.y);
                // 비행 길 가까이(좌우 최소 거리 안)면 충분히 높이 올린다
                if (Mathf.Abs(x) < sideDistance.x) y = Mathf.Max(y, minHeightOverLane + R(0f, 15f));

                var go = Instantiate(model, transform);
                go.name = "StormCloud_BG_" + i;
                go.transform.position = course.transform.TransformPoint(new Vector3(x, y, z));
                go.transform.rotation = course.transform.rotation * Quaternion.Euler(0f, R(0f, 360f), 0f) * model.transform.localRotation;
                go.transform.localScale = Vector3.one * R(scaleRange.x, scaleRange.y);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                var rs = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rs)
                {
                    if (material != null)
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mats.Length; m++) mats[m] = material;
                        r.sharedMaterials = mats;
                    }
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                clouds[i] = rs;
                cloudPos[i] = go.transform.position;
            }
        }

        void Update()
        {
            if (clouds == null || clouds.Length == 0) return;

            // 먼 번개가 이번 프레임에 새로 쳤으면 그 자리에서 가장 가까운 구름을 번쩍인다
            if (lightning != null)
            {
                float dt = Time.deltaTime;
                foreach (var ps in lightning)
                {
                    if (ps == null || ps.particleCount == 0) continue;
                    if (particleBuf == null || particleBuf.Length < ps.main.maxParticles) particleBuf = new ParticleSystem.Particle[ps.main.maxParticles];
                    int n = ps.GetParticles(particleBuf);
                    bool world = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
                    for (int i = 0; i < n; i++)
                    {
                        float age = particleBuf[i].startLifetime - particleBuf[i].remainingLifetime;
                        if (age > dt) continue; // 이미 지난 번개
                        Vector3 p = world ? particleBuf[i].position : ps.transform.TransformPoint(particleBuf[i].position);
                        int c = NearestCloud(p);
                        if (c >= 0) StartFlash(c);
                    }
                }
            }

            // 번개와 상관없는 무작위 번쩍임
            // 밝은 초반(FlightAtmosphere.Stormy 전)에는 번쩍이지 않는다
            if (flashInterval.y > 0f && Time.time >= nextFlash)
            {
                if (FlightAtmosphere.Stormy) StartFlash(Random.Range(0, clouds.Length));
                nextFlash = Time.time + Random.Range(flashInterval.x, flashInterval.y);
            }

            for (int i = 0; i < flashStarts.Length; i++)
            {
                if (flashStarts[i] < 0f) continue;
                float f = 1f - (Time.time - flashStarts[i]) / flashTime;
                if (f <= 0f)
                {
                    // 끝나면 블록을 지워 평소 모습으로 (SRP Batcher 유지)
                    foreach (var r in clouds[i]) if (r != null) r.SetPropertyBlock(null);
                    flashStarts[i] = -1f;
                }
                else SetFlash(clouds[i], f);
            }
        }

        void StartFlash(int i) => flashStarts[i] = Time.time;

        int NearestCloud(Vector3 p)
        {
            int best = -1;
            float bestD = lightningCloudRange * lightningCloudRange;
            for (int i = 0; i < cloudPos.Length; i++)
            {
                float d = (cloudPos[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void SetFlash(Renderer[] rs, float f)
        {
            mpb.SetFloat(WarnProgressId, -1f);
            mpb.SetFloat(StrikeFlashId, f);
            foreach (var r in rs) if (r != null) r.SetPropertyBlock(mpb);
        }

        void OnDrawGizmosSelected()
        {
            var c = course != null ? course : GetComponentInParent<FlightCourse>();
            if (c == null) return;
            Gizmos.color = new Color(0.5f, 0.3f, 0.8f, 0.5f);
            Gizmos.matrix = c.transform.localToWorldMatrix;
            float len = c.FlightLength + extraLength.x + extraLength.y;
            Gizmos.DrawWireCube(new Vector3(0f, (height.x + height.y) * 0.5f, c.FlightLength * 0.5f + (extraLength.y - extraLength.x) * 0.5f),
                new Vector3(sideDistance.y * 2f, height.y - height.x, len));
        }
    }
}
