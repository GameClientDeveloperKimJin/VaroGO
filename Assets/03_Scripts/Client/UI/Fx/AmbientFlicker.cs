using UnityEngine;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 분위기 깜빡임 (빛줄기·조명 등). 같은 오브젝트의 Light 세기, 또는 Renderer 머티리얼 색의 알파를 천천히 오르내린다.
    /// MaterialPropertyBlock을 써서 공용 머티리얼을 바꾸지 않는다. 로컬 연출, timeScale 무관.
    /// </summary>
    public class AmbientFlicker : MonoBehaviour
    {
        [Tooltip("최소~최대 배율 (기준 값에 곱함)")]
        [SerializeField] Vector2 range = new(0.55f, 1f);
        [Tooltip("한 번 오르내리는 대략적 시간(초)")]
        [SerializeField] float period = 5f;
        [Tooltip("0 = 부드러운 호흡, 1 = 불규칙한 깜빡임 섞음")]
        [Range(0f, 1f)]
        [SerializeField] float jitter = 0.2f;
        [Tooltip("머티리얼 색 속성 이름 (Renderer일 때). 비우면 _BaseColor → _TintColor → _Color 순서로 찾는다")]
        [SerializeField] string colorProperty = "";

        Light lt;
        float baseIntensity;
        Renderer rend;
        MaterialPropertyBlock block;
        int colorId;
        Color baseColor;
        float seed;

        /// <summary>코드로 붙일 때 값 지정 (AddComponent 직후)</summary>
        public void Configure(Vector2 range, float period, float jitter)
        {
            this.range = range;
            this.period = period;
            this.jitter = jitter;
        }

        void Awake()
        {
            seed = Random.value * 100f;
            lt = GetComponent<Light>();
            if (lt != null) { baseIntensity = lt.intensity; return; }
            rend = GetComponent<Renderer>();
            if (rend == null || rend.sharedMaterial == null) return;
            var mat = rend.sharedMaterial;
            foreach (var p in string.IsNullOrEmpty(colorProperty) ? new[] { "_BaseColor", "_TintColor", "_Color" } : new[] { colorProperty })
                if (mat.HasProperty(p)) { colorId = Shader.PropertyToID(p); baseColor = mat.GetColor(colorId); break; }
            if (colorId == 0) rend = null;
            else block = new MaterialPropertyBlock();
        }

        void OnDisable()
        {
            if (lt != null) lt.intensity = baseIntensity;
            if (rend != null) rend.SetPropertyBlock(null);
        }

        void Update()
        {
            float t = Time.unscaledTime;
            float slow = period > 0f ? Mathf.PerlinNoise(t / period, seed) : 0.5f;
            float fast = Mathf.PerlinNoise(t * 3f, seed + 7f);
            float k = Mathf.Lerp(range.x, range.y, Mathf.Clamp01(Mathf.Lerp(slow, slow * 0.6f + fast * 0.4f, jitter)));

            if (lt != null) lt.intensity = baseIntensity * k;
            else if (rend != null)
            {
                rend.GetPropertyBlock(block);
                var c = baseColor; c.a *= k;
                block.SetColor(colorId, c);
                rend.SetPropertyBlock(block);
            }
        }
    }
}
