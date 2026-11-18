using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VaroGO.Shared;

namespace VaroGO.Client
{
    /// <summary>
    /// 비행 HUD 내구도 연출(DOTween). FlightHUD 오브젝트에 같이 붙인다. 각 PC 로컬 재생, timeScale 불사용.
    /// 상태 판정·색·판자 켜고 끄기는 FlightHUD가 하고, 이 컴포넌트는 그 위에 연출만 얹는다
    /// (FlightHUD가 매 프레임 값을 다시 쓰므로 LateUpdate에서 덮어쓴다).
    ///
    /// ① 피격(FlightCourse.Hit): 빠지는 판자 복제본이 아래로 떨어지며 회전·페이드, 내구도 바 흔들림, 화면 테두리(DangerVignette) 붉은 플래시
    /// ② 위험 단계(내구도 1~30%): 테두리·% 글자 1Hz 깜빡임, 비네트는 약하게 숨쉬기
    /// UI_FlightHUD 안 오브젝트는 이름 경로로 찾는다 (DurabilityBar/Tray/Border, DurabilityBar/Tray/Planks/*/Wood, DurabilityBar/PercentText, DangerVignette).
    /// </summary>
    public class FlightHudFx : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("HUD 루트 이름 (런타임에 이 이름으로 찾는다)")]
        [SerializeField] string hudName = "UI_FlightHUD";

        [Header("판자 떨어짐")]
        [SerializeField, Min(0.05f)] float plankFallTime = 0.4f;
        [SerializeField] float plankFallDistance = 220f;
        [SerializeField] float plankSpin = 70f;

        [Header("피격 흔들림 · 플래시")]
        [SerializeField, Min(0.05f)] float shakeTime = 0.25f;
        [SerializeField] float shakeStrength = 9f;
        [Range(0f, 1f)]
        [SerializeField] float flashAlpha = 0.45f;
        [SerializeField, Min(0.05f)] float flashTime = 0.5f;

        [Header("위험 단계 (내구도 1~30%)")]
        [Tooltip("깜빡임 횟수 (초당)")]
        [SerializeField] float blinkHz = 1f;
        [Range(0f, 1f)]
        [SerializeField] float blinkMinAlpha = 0.3f;
        [Tooltip("화면 가장자리 붉은 테두리(비네트)가 켜지는 내구도 비율 (%) 이하. 0%(파괴)는 제외. 칸 4개면 1칸 = 25%")]
        [SerializeField, Range(1, 100)] int vignetteDangerPercent = 25;
        [Tooltip("비네트가 켜졌을 때 진하기 (0~1). 낮을수록 은은함")]
        [Range(0f, 1f)]
        [SerializeField] float vignetteAlpha = 0.28f;
        [Tooltip("비네트 숨쉬기 폭 (0이면 가만히 켜 둠)")]
        [Range(0f, 0.5f)]
        [SerializeField] float vignetteBreath = 0.2f;

        [Header("카운트다운 숫자 (3·2·1)")]
        [SerializeField] float countStartScale = 1.6f;
        [SerializeField, Min(0.05f)] float countPopTime = 0.25f;
        [Tooltip("숫자가 바뀐 뒤 이 시간(초)이 지나면 흐려지기 시작")]
        [SerializeField, Min(0.1f)] float countHold = 0.62f;
        [SerializeField, Min(0.05f)] float countFadeTime = 0.3f;
        [SerializeField] float countEndScale = 0.85f;

        [Header("출발! 글자")]
        [SerializeField] float goStartScale = 0.5f;
        [SerializeField] float goOverScale = 1.2f;
        [SerializeField, Min(0.05f)] float goPopTime = 0.32f;
        [Tooltip("글자가 켜진 뒤 이 시간(초)이 지나면 떠오르며 사라짐")]
        [SerializeField, Min(0.1f)] float goHold = 0.7f;
        [SerializeField, Min(0.05f)] float goFadeTime = 0.3f;
        [SerializeField] float goRiseDistance = 40f;

        GameObject hud;
        TMP_Text countText, goText;
        bool countWasActive, goWasActive;
        string lastCountString;
        Tween countTween, goTween;
        Vector2 goBasePos;
        RectTransform durabilityBar;
        Graphic border, vignette;
        TMP_Text percent;
        Transform planks;
        Vector2 barBase;
        bool barBaseValid;
        int lastDurability = -1;
        float flash;
        Tween flashTween, shakeTween;
        FlightCourse subscribed;

        void Awake()
        {
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
        }

        void OnEnable() => Subscribe();

        void OnDisable()
        {
            if (subscribed != null) subscribed.Hit -= OnHit;
            subscribed = null;
            flashTween?.Kill();
            shakeTween?.Kill();
            flash = 0f;
            RestoreBar();
            StopText(ref countTween, countText, null);
            StopText(ref goTween, goText, goBasePos);
            countWasActive = goWasActive = false;
            lastCountString = null;
        }

        void Subscribe()
        {
            if (course == null || subscribed == course) return;
            course.Hit += OnHit;
            subscribed = course;
        }

        bool Resolve()
        {
            if (hud != null) return true;
            hud = GameObject.Find(hudName);
            if (hud == null) return false;
            var t = hud.transform;
            durabilityBar = t.Find("DurabilityBar") as RectTransform;
            border = t.Find("DurabilityBar/Tray/Border")?.GetComponent<Graphic>();
            percent = t.Find("DurabilityBar/PercentText")?.GetComponent<TMP_Text>();
            vignette = t.Find("DangerVignette")?.GetComponent<Graphic>();
            planks = t.Find("DurabilityBar/Tray/Planks");
            barBaseValid = false;
            return true;
        }

        void RestoreBar()
        {
            if (durabilityBar != null && barBaseValid) durabilityBar.anchoredPosition = barBase;
        }

        // 위험 = 내구도 1~30% (FlightHUD 판정과 같다)
        bool IsDanger(out int percentValue)
        {
            percentValue = 100;
            if (course == null || course.State == null) return false;
            int max = Mathf.Max(1, course.MaxDurability);
            percentValue = Mathf.RoundToInt(Mathf.Clamp01((float)course.Durability / max) * 100f);
            return percentValue >= 1 && percentValue <= 30;
        }

        void OnHit(int left)
        {
            if (!Resolve() || durabilityBar == null || !durabilityBar.gameObject.activeInHierarchy) return;

            // 빠지는 판자 = 맞기 전 켜져 있던 칸 중 새로 꺼질 칸들
            int max = Mathf.Max(1, course.MaxDurability);
            int n = planks != null ? planks.childCount : 0;
            int prev = lastDurability >= 0 ? lastDurability : Mathf.Min(max, left + 1);
            int before = Mathf.CeilToInt(Mathf.Clamp01((float)prev / max) * n - 0.0001f);
            int after = Mathf.CeilToInt(Mathf.Clamp01((float)left / max) * n - 0.0001f);
            float delay = 0f;
            for (int i = before - 1; i >= after && i >= 0; i--)
            {
                DropPlank(i, delay);
                delay += 0.05f;
            }

            Shake();
            Flash();
            lastDurability = left;
        }

        void DropPlank(int index, float delay)
        {
            var slot = planks.GetChild(index);
            var wood = slot.Find("Wood") as RectTransform;
            if (wood == null) return;

            var clone = Instantiate(wood.gameObject, durabilityBar, false);
            clone.name = "Wood_Falling (fx)";
            clone.SetActive(true);
            foreach (var le in clone.GetComponents<LayoutElement>()) Destroy(le);
            var crack = clone.transform.Find("Crack");
            if (crack != null) Destroy(crack.gameObject);

            var rt = (RectTransform)clone.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = wood.rect.size;
            rt.position = wood.TransformPoint(wood.rect.center);
            rt.rotation = Quaternion.identity;
            rt.localScale = Vector3.one * (wood.lossyScale.x / Mathf.Max(0.0001f, durabilityBar.lossyScale.x));
            rt.SetAsLastSibling();

            var group = clone.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            float spin = Random.Range(0.6f, 1f) * plankSpin * (Random.value < 0.5f ? -1f : 1f);
            var end = rt.anchoredPosition + new Vector2(Random.Range(-25f, 25f), -plankFallDistance);

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(clone);
            seq.AppendInterval(delay);
            seq.Append(rt.DOAnchorPos(end, plankFallTime).SetEase(Ease.InQuad));
            seq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, spin), plankFallTime, RotateMode.FastBeyond360).SetEase(Ease.Linear));
            seq.Join(group.DOFade(0f, plankFallTime * 0.5f).SetDelay(plankFallTime * 0.5f));
            seq.OnComplete(() => { if (clone != null) Destroy(clone); });
        }

        void Shake()
        {
            if (!barBaseValid) { barBase = durabilityBar.anchoredPosition; barBaseValid = true; }
            shakeTween?.Kill();
            durabilityBar.anchoredPosition = barBase;
            shakeTween = durabilityBar.DOShakeAnchorPos(shakeTime, new Vector2(shakeStrength, shakeStrength * 0.4f), 28, 90f, false, true)
                .SetUpdate(true).SetLink(gameObject)
                .OnKill(RestoreBar);
        }

        void Flash()
        {
            flashTween?.Kill();
            flash = flashAlpha;
            flashTween = DOTween.To(() => flash, v => flash = v, 0f, flashTime).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        }

        void LateUpdate()
        {
            Subscribe();
            if (course == null || course.State == null) return;
            if (!Resolve()) return;
            UpdateTexts();
            if (course.Phase == FlightPhase.Countdown || course.Phase == FlightPhase.Idle)
            {
                lastDurability = -1;
                if (flash > 0f) { flashTween?.Kill(); flash = 0f; }
            }

            bool danger = IsDanger(out int pct);
            var phase = course.Phase;
            bool flying = phase == FlightPhase.Flying;
            if (flying) lastDurability = course.Durability;

            float t = Time.unscaledTime;
            // 1Hz 깜빡임: 1 ↔ blinkMinAlpha
            float blink = danger ? Mathf.Lerp(blinkMinAlpha, 1f, 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f * blinkHz)) : 1f;
            if (danger)
            {
                SetAlphaMul(border, blink);
                if (percent != null) { var c = percent.color; c.a *= blink; percent.color = c; }
            }

            // 비네트는 FlightHUD 값을 쓰지 않고 여기서 전부 정한다 (다시 시작해도 남지 않게 매 프레임 덮어씀)
            if (vignette != null)
            {
                float a = flash;
                if (flying && pct >= 1 && pct <= vignetteDangerPercent)
                {
                    float breath = vignetteBreath > 0f ? 1f - vignetteBreath * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI)) : 1f;
                    a = Mathf.Max(a, vignetteAlpha * breath);
                }
                var c = vignette.color;
                if (!Mathf.Approximately(c.a, a)) { c.a = a; vignette.color = c; }
            }
        }

        // 카운트다운·출발 글자: 켜고 끄기·글자 바꾸기는 FlightHUD가 한다.
        // 여기서는 "켜졌다 / 글자가 바뀌었다"를 감지해 크기·투명도 연출만 얹는다.
        void UpdateTexts()
        {
            if (countText == null) countText = hud.transform.Find("CountdownText")?.GetComponent<TMP_Text>();
            if (goText == null) goText = hud.transform.Find("GoText")?.GetComponent<TMP_Text>();

            bool act = countText != null && countText.gameObject.activeInHierarchy;
            string str = act ? countText.text : null;
            if (act && (!countWasActive || str != lastCountString)) PlayCount();
            else if (!act && countWasActive) StopText(ref countTween, countText, null);
            countWasActive = act;
            lastCountString = str;

            bool goAct = goText != null && goText.gameObject.activeInHierarchy;
            if (goAct && !goWasActive) PlayGo();
            else if (!goAct && goWasActive) StopText(ref goTween, goText, goBasePos);
            goWasActive = goAct;
        }

        static CanvasGroup GroupOf(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        // 연출을 멈추고 글자를 원래 상태(크기 1·불투명·원래 위치)로 돌린다
        static void StopText(ref Tween tween, TMP_Text text, Vector2? basePos)
        {
            tween?.Kill();
            tween = null;
            if (text == null) return;
            text.rectTransform.localScale = Vector3.one;
            if (basePos.HasValue) text.rectTransform.anchoredPosition = basePos.Value;
            var g = text.GetComponent<CanvasGroup>();
            if (g != null) g.alpha = 1f;
        }

        void PlayCount()
        {
            StopText(ref countTween, countText, null);
            var rt = countText.rectTransform;
            var g = GroupOf(countText);
            rt.localScale = Vector3.one * countStartScale;
            g.alpha = 0f;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(countText.gameObject);
            seq.Append(rt.DOScale(1f, countPopTime).SetEase(Ease.OutBack, 2.2f));
            seq.Join(g.DOFade(1f, countPopTime * 0.4f));
            seq.AppendInterval(Mathf.Max(0f, countHold - countPopTime));
            seq.Append(g.DOFade(0f, countFadeTime).SetEase(Ease.InQuad));
            seq.Join(rt.DOScale(countEndScale, countFadeTime).SetEase(Ease.InQuad));
            countTween = seq;
        }

        void PlayGo()
        {
            StopText(ref goTween, goText, goBasePos);
            var rt = goText.rectTransform;
            var g = GroupOf(goText);
            goBasePos = rt.anchoredPosition;
            rt.localScale = Vector3.one * goStartScale;
            g.alpha = 0f;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(goText.gameObject);
            seq.Append(rt.DOScale(goOverScale, goPopTime * 0.6f).SetEase(Ease.OutQuad));
            seq.Join(g.DOFade(1f, goPopTime * 0.3f));
            seq.Append(rt.DOScale(1f, goPopTime * 0.4f).SetEase(Ease.InOutQuad));
            seq.AppendInterval(Mathf.Max(0f, goHold - goPopTime));
            seq.Append(rt.DOAnchorPos(goBasePos + new Vector2(0f, goRiseDistance), goFadeTime).SetEase(Ease.OutQuad));
            seq.Join(g.DOFade(0f, goFadeTime).SetEase(Ease.InQuad));
            goTween = seq;
        }

        static void SetAlphaMul(Graphic g, float mul)
        {
            if (g == null) return;
            var c = g.color;
            c.a *= mul;
            g.color = c;
        }
    }
}
