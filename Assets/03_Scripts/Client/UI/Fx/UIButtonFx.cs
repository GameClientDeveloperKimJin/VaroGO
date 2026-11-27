using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 버튼 연출(DOTween): 마우스 올리면 살짝 커지고, 누르면 눌리고, 클릭하면 통 튄다.
    /// Selectable이 꺼져 있으면(interactable=false) 반응하지 않는다. 시간 정지(timeScale 0)에도 돈다.
    /// 직접 붙이지 않아도 UIFxAuto가 캔버스 안 모든 버튼에 자동으로 붙여 준다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [Header("크기")]
        [SerializeField] float hoverScale = 1.06f;
        [SerializeField] float pressScale = 0.94f;
        [SerializeField] float hoverTime = 0.12f;
        [SerializeField] float pressTime = 0.06f;

        [Header("클릭 통통")]
        [SerializeField] float punch = 0.12f;
        [SerializeField] float punchTime = 0.25f;

        [Header("화살표 (이름이 Arrow인 자식이 있으면 올렸을 때 좌우로 흔들린다)")]
        [SerializeField] float arrowWiggle = 8f;
        [SerializeField] float arrowPeriod = 0.5f;

        [Header("빛 (켜면 올렸을 때 버튼 그림 위에 밝은 빛이 겹친다)")]
        [SerializeField] bool glow = false;
        [SerializeField] Color glowColor = new(1f, 0.95f, 0.75f, 0.28f);
        [SerializeField] float glowTime = 0.15f;
        [Tooltip("빛이 천천히 숨쉬는 폭 (0이면 고정)")]
        [Range(0f, 1f)]
        [SerializeField] float glowPulse = 0.35f;

        Image glowImg;
        Tween glowTween;

        Selectable selectable;
        Vector3 baseScale;
        Tween scaleTween;
        Tween arrowTween;
        RectTransform arrow;
        Vector2 arrowBase;
        bool hover;
        bool padSelected;
        bool captured;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            arrow = transform.Find("Arrow") as RectTransform;
            if (arrow != null) arrowBase = arrow.anchoredPosition;
        }

        void OnEnable()
        {
            if (!captured) { baseScale = transform.localScale; captured = true; }
            transform.localScale = baseScale;
            hover = false;
            padSelected = false;
        }

        void OnDisable()
        {
            scaleTween?.Kill();
            SetArrow(false);
            if (captured) transform.localScale = baseScale;
        }

        bool Lit => hover || padSelected;

        // 버튼 그림을 복사한 밝은 덮개(자식, 클릭 통과). 처음 켤 때 만든다
        void SetGlow(bool on)
        {
            if (!glow) return;
            if (glowImg == null)
            {
                if (!on) return;
                var src = GetComponent<Image>();
                if (src == null) return;
                var go = new GameObject("Glow (fx)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.SetAsFirstSibling(); // 글자보다 뒤, 버튼 그림보다 앞
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
                glowImg = go.GetComponent<Image>();
                glowImg.sprite = src.sprite;
                glowImg.type = src.type;
                glowImg.preserveAspect = src.preserveAspect;
                glowImg.raycastTarget = false;
                glowImg.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            }
            glowTween?.Kill();
            if (!isActiveAndEnabled) { glowImg.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f); return; }
            if (on && Active)
            {
                // 무한 반복은 Sequence 안에 못 넣으므로 켜짐 → 끝나면 숨쉬기 트윈으로 이어 붙인다
                glowTween = glowImg.DOFade(glowColor.a, glowTime).SetUpdate(true).SetLink(gameObject).OnComplete(() =>
                {
                    if (glowPulse <= 0f || glowImg == null) return;
                    glowTween = glowImg.DOFade(glowColor.a * (1f - glowPulse), 0.9f).SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
                });
            }
            else glowTween = glowImg.DOFade(0f, glowTime).SetUpdate(true).SetLink(gameObject);
        }

        void SetArrow(bool on)
        {
            SetGlow(on);
            if (arrow == null) return;
            arrowTween?.Kill();
            arrow.anchoredPosition = arrowBase;
            if (on && Active)
                arrowTween = arrow.DOAnchorPosX(arrowBase.x + arrowWiggle, arrowPeriod * 0.5f).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        // 패드·키보드로 선택했을 때만 (마우스 클릭으로 선택된 건 hover가 처리)
        public void OnSelect(BaseEventData e)
        {
            if (e is PointerEventData) return;
            padSelected = true;
            if (Active) ScaleTo(hoverScale, hoverTime, Ease.OutQuad);
            SetArrow(true);
        }

        public void OnDeselect(BaseEventData e)
        {
            padSelected = false;
            if (!hover) { ScaleTo(1f, hoverTime, Ease.OutQuad); SetArrow(false); }
        }

        public void OnSubmit(BaseEventData e) => Punch();

        bool Active => selectable == null || selectable.IsInteractable();

        void ScaleTo(float factor, float time, Ease ease)
        {
            scaleTween?.Kill();
            scaleTween = transform.DOScale(baseScale * factor, time).SetEase(ease).SetUpdate(true).SetLink(gameObject);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hover = true;
            if (Active) ScaleTo(hoverScale, hoverTime, Ease.OutQuad);
            SetArrow(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            hover = false;
            if (padSelected) return;
            ScaleTo(1f, hoverTime, Ease.OutQuad);
            SetArrow(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Active) ScaleTo(pressScale, pressTime, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (Active) ScaleTo(Lit ? hoverScale : 1f, hoverTime, Ease.OutBack);
        }

        public void OnPointerClick(PointerEventData e) => Punch();

        void Punch()
        {
            if (!Active) return;
            scaleTween?.Kill();
            transform.localScale = baseScale * (Lit ? hoverScale : 1f);
            scaleTween = transform.DOPunchScale(Vector3.one * punch, punchTime, 6, 0.6f).SetUpdate(true).SetLink(gameObject)
                .OnKill(() => { if (this != null) transform.localScale = baseScale * (Lit ? hoverScale : 1f); });
        }
    }
}
