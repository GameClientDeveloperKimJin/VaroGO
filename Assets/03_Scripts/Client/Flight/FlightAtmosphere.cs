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

        [Header("번개 번쩍임")]
        [SerializeField, Min(0f)] float lightningFlash = 2.5f;
        [SerializeField, Min(0.05f)] float lightningFlashTime = 0.35f;

        [Header("빨려 들어가는 끝 (착륙 단계)")]
        [Tooltip("소용돌이(던전 입구). 착륙 단계에서 돌며 커진다. 비우면 화면만 검게 닫힌다")]
        [SerializeField] Transform vortex;
        [SerializeField] float vortexSpinSpeed = 180f;
        [Tooltip("착륙 단계 중 이 비율부터 화면이 검게 닫히기 시작 (0~1)")]
        [SerializeField, Range(0f, 1f)] float fadeStart = 0.35f;
        [Tooltip("다음 씬에서 화면이 밝아지는 시간 (초)")]
        [SerializeField, Min(0.1f)] float nextSceneFadeIn = 2f;

        Material skyInstance;
        bool hasExposure;
        float flashUntil = -1f;
        Vector3 vortexBaseScale = Vector3.one;
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
            if (vortex != null) vortexBaseScale = vortex.localScale;
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

            if (rain != null)
            {
                var em = rain.emission;
                em.rateOverTime = phase == FlightPhase.Idle ? rainRateStart * 0.5f : Mathf.Lerp(rainRateStart, rainRateEnd, dark);
            }

            // 소용돌이: 착륙 단계에서 커지며 빠르게 돈다
            if (vortex != null)
            {
                float grow = phase == FlightPhase.Flying ? Mathf.InverseLerp(0.8f, 1f, course.Progress) * 0.6f : (landingK > 0f ? 0.6f + landingK * 0.9f : 0f);
                vortex.gameObject.SetActive(grow > 0.01f);
                vortex.localScale = vortexBaseScale * Mathf.Max(0.01f, grow);
                vortex.Rotate(0f, 0f, vortexSpinSpeed * (1f + landingK * 3f) * Time.deltaTime, Space.Self);
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
