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
    public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("크기")]
        [SerializeField] float hoverScale = 1.06f;
        [SerializeField] float pressScale = 0.94f;
        [SerializeField] float hoverTime = 0.12f;
        [SerializeField] float pressTime = 0.06f;

        [Header("클릭 통통")]
        [SerializeField] float punch = 0.12f;
        [SerializeField] float punchTime = 0.25f;

        Selectable selectable;
        Vector3 baseScale;
        Tween scaleTween;
        bool hover;
        bool captured;

        void Awake() => selectable = GetComponent<Selectable>();

        void OnEnable()
        {
            if (!captured) { baseScale = transform.localScale; captured = true; }
            transform.localScale = baseScale;
            hover = false;
        }

        void OnDisable()
        {
            scaleTween?.Kill();
            if (captured) transform.localScale = baseScale;
        }

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
        }

        public void OnPointerExit(PointerEventData e)
        {
            hover = false;
            ScaleTo(1f, hoverTime, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Active) ScaleTo(pressScale, pressTime, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (Active) ScaleTo(hover ? hoverScale : 1f, hoverTime, Ease.OutBack);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Active) return;
            scaleTween?.Kill();
            transform.localScale = baseScale * (hover ? hoverScale : 1f);
            scaleTween = transform.DOPunchScale(Vector3.one * punch, punchTime, 6, 0.6f).SetUpdate(true).SetLink(gameObject)
                .OnKill(() => { if (this != null) transform.localScale = baseScale * (hover ? hoverScale : 1f); });
        }
    }
}
