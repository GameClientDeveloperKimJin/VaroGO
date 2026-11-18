using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 비행 분위기: 진행도(출발 0 → 착륙 시작 1)에 따라 흐린 저녁 → 어두운 폭풍 밤으로 바뀐다.
    // 해(Directional Light)·주변광·안개·하늘 밝기·비 양을 섞고, 번개(LightningStrike)가 치면 잠깐 번쩍인다.
    // 마지막 착륙 단계에서 소용돌이(vortex)가 커지며 화면이 검게 닫히고, 다음 씬(스테이지 2)이 밝아지며 열린다 (ScreenFade).
    // 모든 PC에서 각자 계산한다 (진행도는 동기화된 값이라 화면이 같다).
    public class FlightAtmosphere : MonoBehaviour
    {
        [SerializeField] FlightCourse course;

        [Header("해 (Directional Light)")]
        [SerializeField] Light sun;
        [SerializeField] Color sunStart = new Color(1f, 0.72f, 0.5f);
        [SerializeField] Color sunEnd = new Color(0.25f, 0.3f, 0.5f);
        [SerializeField, Min(0f)] float sunIntensityStart = 1.1f;
        [SerializeField, Min(0f)] float sunIntensityEnd = 0.15f;

        [Header("주변광 · 안개")]
        [SerializeField] Color ambientStart = new Color(0.55f, 0.5f, 0.55f);
        [SerializeField] Color ambientEnd = new Color(0.08f, 0.09f, 0.16f);
        [SerializeField] bool useFog = true;
        [SerializeField] Color fogStart = new Color(0.55f, 0.5f, 0.58f);
        [SerializeField] Color fogEnd = new Color(0.05f, 0.06f, 0.12f);
        [SerializeField, Min(0f)] float fogDensityStart = 0.004f;
        [SerializeField, Min(0f)] float fogDensityEnd = 0.02f;

        [Header("하늘")]
        [Tooltip("하늘(스카이박스) 머티리얼. 비우면 지금 RenderSettings.skybox를 복사해 쓴다")]
        [SerializeField] Material skybox;
        [Tooltip("하늘 밝기 속성 이름 (Procedural/6 Sided 하늘은 _Exposure)")]
        [SerializeField] string exposureProperty = "_Exposure";
        [SerializeField, Min(0f)] float exposureStart = 1f;
        [SerializeField, Min(0f)] float exposureEnd = 0.15f;

        [Header("어두워지는 모양")]
        [Tooltip("가로 = 진행도 0~1, 세로 = 어두운 정도 0~1")]
        [SerializeField] AnimationCurve darknessCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("비")]
        [Tooltip("비 파티클 (카메라를 따라다니게 두면 좋다). 진행할수록 많아진다")]
        [SerializeField] ParticleSystem rain;
        [SerializeField, Min(0f)] float rainRateStart = 150f;
        [SerializeField, Min(0f)] float rainRateEnd = 900f;
        [Tooltip("비행기 옆을 스치는 바람 줄. 날고 있을 때(비행·착륙)만 나오고, 진행할수록 많아진다")]
        [SerializeField] ParticleSystem wind;
        [Tooltip("바람 줄 양 (초당 개수): 비행 시작 → 끝. 어두워지는 곡선(Darkness Curve)을 따른다")]
        [SerializeField, Min(0f)] float windRateStart = 5f;
        [SerializeField, Min(0f)] float windRateEnd = 60f;
        [Tooltip("먼 배경 번개 (보기만, 판정 없음). 바람처럼 날고 있을 때(비행·착륙)만 친다")]
        [SerializeField] ParticleSystem[] distantLightning;

        [Header("번개 번쩍임")]
        [SerializeField, Min(0f)] float lightningFlash = 2.5f;
        [SerializeField, Min(0.05f)] float lightningFlashTime = 0.35f;

        [Header("빨려 들어가는 끝 (착륙 단계)")]
        [Tooltip("소용돌이(던전 입구). 착륙 단계에서 돌며 커진다. 비우면 화면만 검게 닫힌다")]
        [SerializeField] Transform vortex;
        [SerializeField] float vortexSpinSpeed = 180f;
        [Tooltip("탑승이 끝나면(카운트다운부터) 소용돌이가 이 시간(초)에 걸쳐 서서히 나타난다")]
        [SerializeField, Min(0.1f)] float vortexAppearTime = 4f;
        [Tooltip("나타난 직후 투명도 (0 = 안 보임, 1 = 다 보임). 진행할수록 1까지 올라간다")]
        [SerializeField, Range(0f, 1f)] float vortexStartAlpha = 0.6f;
        [Tooltip("출발할 때 크기 배율. 진행할수록 1, 착륙 단계에서 Vortex Landing Scale까지 커진다")]
        [SerializeField, Min(0.01f)] float vortexStartScale = 0.35f;
        [SerializeField, Min(0.01f)] float vortexLandingScale = 1.5f;
        [Tooltip("착륙 단계 중 이 비율부터 화면이 검게 닫히기 시작 (0~1)")]
        [SerializeField, Range(0f, 1f)] float fadeStart = 0.35f;
        [Tooltip("다음 씬에서 화면이 밝아지는 시간 (초)")]
        [SerializeField, Min(0.1f)] float nextSceneFadeIn = 2f;

        Material skyInstance;
        bool hasExposure;
        float flashUntil = -1f;
        Vector3 vortexBaseScale = Vector3.one;
        Renderer[] vortexRenderers;
        MaterialPropertyBlock vortexMpb;
        float vortexShownTime;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        bool fadeCarried;

        void Awake()
        {
            if (course == null) course = GetComponentInParent<FlightCourse>();
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
            var src = skybox != null ? skybox : RenderSettings.skybox;
            if (src != null)
            {
                skyInstance = new Material(src);
                RenderSettings.skybox = skyInstance;
                hasExposure = !string.IsNullOrEmpty(exposureProperty) && skyInstance.HasProperty(exposureProperty);
            }
            if (vortex != null)
            {
                vortexBaseScale = vortex.localScale;
                vortexRenderers = vortex.GetComponentsInChildren<Renderer>(true);
                vortexMpb = new MaterialPropertyBlock();
            }
            if (useFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        void OnEnable() => LightningStrike.Struck += OnStruck;
        void OnDisable() => LightningStrike.Struck -= OnStruck;

        void OnDestroy()
        {
            if (skyInstance != null) Destroy(skyInstance);
        }

        void OnStruck(Vector3 at) => flashUntil = Time.time + lightningFlashTime;

        void Update()
        {
            if (course == null) return;
            var phase = course.Phase;
            float progress = course.Progress;
            float landingK = 0f;
            if (phase == FlightPhase.Landing) landingK = Mathf.Clamp01(course.PhaseTime / Mathf.Max(0.1f, course.LandingDuration));
            if (phase == FlightPhase.Landing || phase == FlightPhase.Arrived) { progress = 1f; if (phase == FlightPhase.Arrived) landingK = 1f; }

            float dark = Mathf.Clamp01(darknessCurve.Evaluate(progress));
            float flash = Time.time < flashUntil ? (flashUntil - Time.time) / lightningFlashTime : 0f;

            if (sun != null)
            {
                sun.color = Color.Lerp(sunStart, sunEnd, dark);
                sun.intensity = Mathf.Lerp(sunIntensityStart, sunIntensityEnd, dark) + flash * lightningFlash;
            }
            Color amb = Color.Lerp(ambientStart, ambientEnd, dark);
            RenderSettings.ambientLight = Color.Lerp(amb, Color.white, flash * 0.6f);
            if (useFog)
            {
                RenderSettings.fogColor = Color.Lerp(fogStart, fogEnd, dark);
                RenderSettings.fogDensity = Mathf.Lerp(fogDensityStart, fogDensityEnd, dark);
            }
            if (hasExposure) skyInstance.SetFloat(exposureProperty, Mathf.Lerp(exposureStart, exposureEnd, dark) + flash * 0.8f);

            if (wind != null)
            {
                bool blow = phase == FlightPhase.Flying || phase == FlightPhase.Landing;
                var wem = wind.emission;
                if (wem.enabled != blow) wem.enabled = blow; // 끌 때 이미 나온 줄은 자연스럽게 사라진다
                if (blow && !wind.isPlaying) wind.Play();
                if (blow) wem.rateOverTime = Mathf.Lerp(windRateStart, windRateEnd, dark);
            }
            if (distantLightning != null)
            {
                bool storm = phase == FlightPhase.Flying || phase == FlightPhase.Landing;
                foreach (var ps in distantLightning)
                {
                    if (ps == null) continue;
                    var dem = ps.emission;
                    if (dem.enabled != storm) dem.enabled = storm;
                    if (storm && !ps.isPlaying) ps.Play();
                }
            }
            if (rain != null)
            {
                var em = rain.emission;
                em.rateOverTime = phase == FlightPhase.Idle ? rainRateStart * 0.5f : Mathf.Lerp(rainRateStart, rainRateEnd, dark);
            }

            // 소용돌이: 탑승이 끝나면(카운트다운부터) 멀리서 작고 흐리게 서서히 나타나고,
            // 진행할수록 커지고 진해진다. 착륙 단계에서 더 커지며 빠르게 돈다
            if (vortex != null)
            {
                bool shown = phase == FlightPhase.Countdown || phase == FlightPhase.Flying || phase == FlightPhase.Failed
                             || phase == FlightPhase.Landing || phase == FlightPhase.Arrived;
                vortexShownTime = shown ? vortexShownTime + Time.deltaTime : 0f;
                float appear = Mathf.Clamp01(vortexShownTime / vortexAppearTime);
                if (vortex.gameObject.activeSelf != shown) vortex.gameObject.SetActive(shown);
                if (shown)
                {
                    float scale = landingK > 0f || phase == FlightPhase.Arrived
                        ? Mathf.Lerp(1f, vortexLandingScale, landingK)
                        : Mathf.Lerp(vortexStartScale, 1f, progress);
                    vortex.localScale = vortexBaseScale * scale;
                    vortex.Rotate(0f, 0f, vortexSpinSpeed * (1f + landingK * 3f) * Time.deltaTime, Space.Self);
                    float alpha = appear * Mathf.Lerp(vortexStartAlpha, 1f, progress);
                    vortexMpb.SetFloat(AlphaId, alpha);
                    foreach (var r in vortexRenderers) if (r != null) r.SetPropertyBlock(vortexMpb);
                }
            }

            // 화면 닫기: 착륙 단계 뒤쪽에서 검게 → 다음 씬에서 밝아진다
            if (phase == FlightPhase.Landing || phase == FlightPhase.Arrived)
            {
                if (course.HasNextScene && !fadeCarried)
                {
                    fadeCarried = true;
                    float remain = Mathf.Max(0.2f, course.LandingDuration * (1f - Mathf.Max(fadeStart, landingK)));
                    if (landingK < fadeStart) ScreenFade.Set(0f);
                    StartCoroutine(FadeLater(Mathf.Max(0f, (fadeStart - landingK) * course.LandingDuration), remain));
                }
            }
            else if (fadeCarried)
            {
                // 다시 시작 등으로 돌아오면 화면을 연다
                fadeCarried = false;
                StopAllCoroutines();
                ScreenFade.FadeIn(0.3f);
            }
        }

        System.Collections.IEnumerator FadeLater(float wait, float duration)
        {
            if (wait > 0f) yield return new WaitForSeconds(wait);
            ScreenFade.FadeOut(duration, true, nextSceneFadeIn);
        }
    }
}
