using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 타이틀 화면 연출(DOTween). BootMenu 캔버스에 붙인다. 로컬 연출 — 동기화·timeScale 없음.
    /// 진입: 검은 화면 페이드 인 → 배경 줌아웃(1.05→1) → 로고 낙하 바운스 + 스파클 → 버튼(MainPanel의 UIPanelFx Stagger, firstPlayDelay로 시점 조절)
    /// 반복: 레이어별 둥실(위아래)·흐름(구름 좌우), 로고 숨쉬기, 가끔 스파클, 마우스 패럴랙스(패드 조작 중이면 끔)
    /// 클릭은 막지 않는다 — 페이드 덮개·스파클은 raycastTarget=false.
    /// 레이어 아트가 오면 layers 목록에 RectTransform만 넣고 값을 조절하면 된다.
    /// </summary>
    public class TitleScreenFx : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public RectTransform target;
            [Tooltip("마우스 패럴랙스로 움직이는 최대 거리(px). 멀수록 작게, 가까울수록 크게")]
            public float parallax = 10f;
            [Tooltip("기본 크기 배율. 패럴랙스로 가장자리가 보이면 조금 키운다 (전체 화면 배경은 1.02 정도)")]
            public float baseScale = 1f;
            [Header("둥실 (위아래)")]
            public float bobAmount = 0f;
            public float bobPeriod = 4f;
            [Range(0f, 1f)] public float bobPhase = 0f;
            [Header("흐름 (좌우)")]
            [Tooltip("RawImage면 텍스처가 이음새 없이 계속 흐른다(초당 UV). 그 외에는 좌우로 천천히 오간다(px)")]
            public float driftAmount = 0f;
            public float driftPeriod = 20f;
            [Header("숨쉬기 (크기)")]
            [Tooltip("1이면 끔. 1.02면 1~1.02 사이로 커졌다 작아진다")]
            public float breathScale = 1f;
            public float breathPeriod = 3f;
            [Range(0f, 1f)] public float breathPhase = 0f;
            [Tooltip("진입 줌아웃을 적용할지 (로고·버튼 같은 앞쪽 UI는 끈다)")]
            public bool introZoom = true;

            [NonSerialized] public Vector2 basePos;
            [NonSerialized] public Vector3 baseLocalScale;
            [NonSerialized] public RawImage raw;
            [NonSerialized] public Rect baseUv;
        }

        [Header("레이어 (뒤 → 앞 순서)")]
        [SerializeField] List<Layer> layers = new();

        [Header("진입")]
        [Tooltip("이 로더(3D 배경)가 다 뜰 때까지 검은 화면을 유지한다. 비우면 씬에서 찾는다(없으면 바로 시작)")]
        [SerializeField] VaroGO.Client.Loading.AddressablePrefabLoader waitForLoader;
        [Tooltip("로더가 이 시간(초)이 지나도 안 끝나면 그냥 시작한다")]
        [SerializeField] float maxWait = 15f;
        [Tooltip("같이 진입 연출을 시작할 카메라 (앞으로 들어가기)")]
        [SerializeField] TitleCameraFx cameraFx;
        [Tooltip("버튼 패널 (UIPanelFx, holdFirstPlay 켬). 진입 연출의 buttonsAt 시점에 등장시킨다")]
        [SerializeField] UIPanelFx buttonPanel;
        [SerializeField] float buttonsAt = 1.1f;
        [SerializeField] float fadeInTime = 0.6f;
        [SerializeField] float introZoomFrom = 1.05f;
        [SerializeField] float introZoomTime = 1.4f;

        [Header("로고 (비어 있으면 낙하·숨쉬기 생략, 스파클만 sparkleArea에)")]
        [SerializeField] RectTransform logo;
        [SerializeField] float logoDropHeight = 420f;
        [SerializeField] float logoDropDelay = 0.35f;
        [SerializeField] float logoDropTime = 0.7f;
        [Tooltip("내려앉는 방식. OutBack = 한 번 살짝 지나쳤다 자리잡음(부드러움), OutBounce = 통통 튐")]
        [SerializeField] Ease logoDropEase = Ease.OutBack;
        [SerializeField] float logoDropOvershoot = 0.9f;
        [SerializeField] float logoBreathScale = 1.03f;
        [SerializeField] float logoBreathPeriod = 2.4f;

        [Header("스파클")]
        [Tooltip("끄면 반짝이를 띄우지 않는다")]
        [SerializeField] bool enableSparkles = true;
        [Tooltip("반짝이가 나타날 영역. 비우면 로고 영역")]
        [SerializeField] RectTransform sparkleArea;
        [Tooltip("반짝이 스프라이트(아트). 비우면 가는 막대 두 개를 겹친 십자 반짝이")]
        [SerializeField] Sprite sparkleSprite;
        [SerializeField] Color sparkleColor = new(1f, 0.97f, 0.8f, 1f);
        [SerializeField] Vector2 sparkleSize = new(26f, 54f);
        [SerializeField] int introSparkles = 6;
        [Tooltip("평소 반짝이 간격(초, 최소~최대)")]
        [SerializeField] Vector2 sparkleInterval = new(0.8f, 2.2f);

        [Header("마우스 패럴랙스")]
        [SerializeField] bool mouseParallax = true;
        [SerializeField] float parallaxSmooth = 0.25f;

        RectTransform root;
        Image fade;
        float zoom = 1f;
        float logoScale = 1f;
        Vector2 parallaxNow, parallaxVel;
        bool padMode;
        float nextSparkle;
        bool introDone;
        Vector2 logoBasePos;
        readonly List<RectTransform> sparklePool = new();
        Sequence intro;
        Tween breath;

        void Awake()
        {
            root = (RectTransform)transform;
            foreach (var l in layers)
            {
                if (l.target == null) continue;
                l.basePos = l.target.anchoredPosition;
                l.baseLocalScale = l.target.localScale;
                l.raw = l.target.GetComponent<RawImage>();
                if (l.raw != null) l.baseUv = l.raw.uvRect;
            }
            if (logo != null) logoBasePos = logo.anchoredPosition;

            // 첫 프레임부터 검은 덮개 (맨 앞, 클릭 통과)
            fade = NewImage("IntroFade (fx)", root, null, Color.black);
            var frt = fade.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.sizeDelta = Vector2.zero;
            frt.SetAsLastSibling();
        }

        System.Collections.IEnumerator Start()
        {
            if (waitForLoader == null) waitForLoader = VaroGO.Client.Loading.AddressablePrefabLoader.FindInScene();
            float until = Time.unscaledTime + maxWait;
            while (waitForLoader != null && !waitForLoader.IsDone && Time.unscaledTime < until) yield return null;
            yield return null; // 생성 직후 한 프레임 (셰이더·파티클 첫 프레임 끊김을 검은 화면 뒤로)
            PlayIntro();
        }

        void OnDestroy()
        {
            intro?.Kill();
            breath?.Kill();
            DOTween.Kill(this);
        }

        void PlayIntro()
        {
            intro?.Kill();
            intro = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).SetTarget(this);

            if (cameraFx != null) cameraFx.PlayIntro();
            if (buttonPanel != null) intro.InsertCallback(buttonsAt, buttonPanel.Release);

            fade.rectTransform.SetAsLastSibling();
            intro.Insert(0f, fade.DOFade(0f, fadeInTime).SetEase(Ease.InQuad));
            intro.InsertCallback(fadeInTime, () => { if (fade != null) Destroy(fade.gameObject); });

            zoom = introZoomFrom;
            intro.Insert(0f, DOTween.To(() => zoom, v => zoom = v, 1f, introZoomTime).SetEase(Ease.OutCubic));

            if (logo != null)
            {
                var g = logo.GetComponent<CanvasGroup>();
                if (g == null) g = logo.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                logo.anchoredPosition = logoBasePos + Vector2.up * logoDropHeight;
                intro.Insert(logoDropDelay, g.DOFade(1f, logoDropTime * 0.4f));
                var drop = logo.DOAnchorPos(logoBasePos, logoDropTime);
                if (logoDropEase == Ease.OutBack) drop.SetEase(Ease.OutBack, logoDropOvershoot); else drop.SetEase(logoDropEase);
                intro.Insert(logoDropDelay, drop);
                // 자리에 닿는 순간: 통통 튀는 방식은 첫 바닥(약 36%), 그 외는 거의 끝(약 70%)
                float land = logoDropDelay + logoDropTime * (logoDropEase == Ease.OutBounce ? 0.36f : 0.7f);
                intro.InsertCallback(land, () => SparkleBurst(introSparkles));
                intro.Insert(land, DOTween.To(() => logoScale, v => logoScale = v, 1f, 0.4f).From(1.05f).SetEase(Ease.OutSine));
            }
            else
            {
                intro.InsertCallback(logoDropDelay + 0.25f, () => SparkleBurst(introSparkles));
            }

            intro.OnComplete(() =>
            {
                introDone = true;
                nextSparkle = Time.unscaledTime + Random.Range(sparkleInterval.x, sparkleInterval.y);
                if (logo != null)
                {
                    logoScale = 1f;
                    breath = DOTween.To(() => logoScale, v => logoScale = v, logoBreathScale, logoBreathPeriod * 0.5f)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject).SetTarget(this);
                }
            });
        }

        void LateUpdate()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            Vector2 target = Vector2.zero;
            if (mouseParallax) target = ParallaxInput();
            parallaxNow = Vector2.SmoothDamp(parallaxNow, target, ref parallaxVel, parallaxSmooth, Mathf.Infinity, dt);

            foreach (var l in layers)
            {
                if (l.target == null) continue;
                var off = parallaxNow * l.parallax;
                if (l.bobAmount != 0f && l.bobPeriod > 0f)
                    off.y += Mathf.Sin((t / l.bobPeriod + l.bobPhase) * Mathf.PI * 2f) * l.bobAmount;
                if (l.driftAmount != 0f && l.driftPeriod > 0f)
                {
                    if (l.raw != null)
                    {
                        var uv = l.baseUv;
                        uv.x = Mathf.Repeat(l.baseUv.x + t * l.driftAmount, 1f);
                        l.raw.uvRect = uv;
                    }
                    else off.x += Mathf.Sin((t / l.driftPeriod) * Mathf.PI * 2f) * l.driftAmount;
                }
                l.target.anchoredPosition = l.basePos + off;
                float breathe = 1f;
                if (l.breathScale != 1f && l.breathPeriod > 0f)
                    breathe = Mathf.Lerp(1f, l.breathScale, 0.5f - 0.5f * Mathf.Cos((t / l.breathPeriod + l.breathPhase) * Mathf.PI * 2f));
                l.target.localScale = l.baseLocalScale * (l.baseScale * breathe * (l.introZoom ? zoom : 1f));
            }

            if (logo != null) logo.localScale = Vector3.one * logoScale;

            if (introDone && t >= nextSparkle)
            {
                SparkleBurst(Random.Range(1, 3));
                nextSparkle = t + Random.Range(sparkleInterval.x, sparkleInterval.y);
            }
        }

        // 마우스 위치 → -1..1. 패드 입력이 들어오면 끄고, 마우스가 움직이면 다시 켠다
        Vector2 ParallaxInput()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.dpad.ReadValue().sqrMagnitude > 0.04f
                || pad.buttonSouth.isPressed || pad.buttonEast.isPressed)) padMode = true;
            var mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            if (mouse.delta.ReadValue().sqrMagnitude > 4f) padMode = false;
            if (padMode) return Vector2.zero;
            var p = mouse.position.ReadValue();
            if (Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;
            var n = new Vector2(p.x / Screen.width * 2f - 1f, p.y / Screen.height * 2f - 1f);
            // 마우스 반대쪽으로 살짝 밀려야 깊이감이 난다
            return -new Vector2(Mathf.Clamp(n.x, -1f, 1f), Mathf.Clamp(n.y, -1f, 1f));
        }

        void SparkleBurst(int count)
        {
            if (!enableSparkles) return;
            var area = sparkleArea != null ? sparkleArea : logo;
            if (area == null) return;
            for (int i = 0; i < count; i++)
            {
                var s = GetSparkle(area);
                var r = area.rect;
                s.localPosition = new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));
                s.gameObject.SetActive(true);
                s.localScale = Vector3.zero;
                s.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
                float size = Random.Range(0.6f, 1.2f);
                float delay = i * Random.Range(0.04f, 0.12f);
                var seq = DOTween.Sequence().SetUpdate(true).SetLink(s.gameObject).SetTarget(this);
                seq.AppendInterval(delay);
                seq.Append(s.DOScale(size, 0.18f).SetEase(Ease.OutBack));
                seq.Join(s.DOLocalRotate(new Vector3(0f, 0f, 45f), 0.5f, RotateMode.LocalAxisAdd).SetEase(Ease.OutSine));
                seq.Append(s.DOScale(0f, 0.32f).SetEase(Ease.InQuad));
                var ss = s;
                seq.OnComplete(() => { if (ss != null) ss.gameObject.SetActive(false); });
            }
        }

        // 반짝이는 영역(area)의 자식으로 만든다 → 그 영역의 그리기 순서를 따른다 (팝업 위로 올라오지 않음)
        RectTransform GetSparkle(RectTransform area)
        {
            foreach (var s in sparklePool) if (s != null && !s.gameObject.activeSelf && s.parent == area) return s;
            RectTransform rt;
            if (sparkleSprite != null)
            {
                var img = NewImage("Sparkle (fx)", area, sparkleSprite, sparkleColor);
                rt = img.rectTransform;
                rt.sizeDelta = Vector2.one * sparkleSize.y;
            }
            else
            {
                var go = new GameObject("Sparkle (fx)", typeof(RectTransform));
                rt = (RectTransform)go.transform;
                rt.SetParent(area, false);
                var a = NewImage("V", rt, null, sparkleColor).rectTransform;
                a.sizeDelta = new Vector2(sparkleSize.x * 0.22f, sparkleSize.y);
                var b = NewImage("H", rt, null, sparkleColor).rectTransform;
                b.sizeDelta = new Vector2(sparkleSize.y * 0.75f, sparkleSize.x * 0.22f);
                var c = NewImage("Core", rt, null, sparkleColor).rectTransform;
                c.sizeDelta = Vector2.one * sparkleSize.x * 0.45f;
                c.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            rt.gameObject.SetActive(false);
            sparklePool.Add(rt);
            return rt;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var img = go.GetComponent<Image>();
            go.transform.SetParent(parent, false);
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
