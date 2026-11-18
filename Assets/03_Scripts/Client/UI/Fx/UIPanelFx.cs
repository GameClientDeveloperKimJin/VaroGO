using DG.Tweening;
using UnityEngine;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 패널 등장 연출(DOTween). 오브젝트가 켜질 때(OnEnable) 자동 재생 — 서버/클라 스크립트가 SetActive로 켜도 그대로 동작한다.
    /// Popup  : 뒷배경(이 오브젝트) 페이드 + 창(window) 통 튀며 등장
    /// Stagger: 자식들이 순서대로 미끄러지며 등장 (메인 메뉴 버튼 줄 등)
    /// Fade   : 단순 페이드 (로딩 화면 등)
    /// Toast  : 아래에서 살짝 올라오며 페이드
    /// 닫을 때는 연출 없이 바로 꺼진다(켜고 끄는 쪽 스크립트를 건드리지 않기 위해).
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPanelFx : MonoBehaviour
    {
        public enum Mode { Popup, Stagger, Fade, Toast }

        [SerializeField] Mode mode = Mode.Popup;
        [Tooltip("Popup: 튀어나올 창. 비우면 이름이 Window인 자식을 찾는다")]
        [SerializeField] RectTransform window;
        [SerializeField] float duration = 0.28f;
        [Header("Stagger")]
        [SerializeField] float staggerDelay = 0.07f;
        [SerializeField] Vector2 staggerOffset = new(-60f, 0f);
        [Header("Popup")]
        [SerializeField] float popFromScale = 0.82f;

        CanvasGroup group;
        Sequence seq;

        void Awake()
        {
            if (mode == Mode.Popup && window == null)
            {
                var t = transform.Find("Window");
                if (t != null) window = t as RectTransform;
            }
        }

        void OnEnable() => Play();

        void OnDisable()
        {
            seq?.Kill();
            Restore();
        }

        public void Play()
        {
            seq?.Kill();
            Restore();
            seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            switch (mode)
            {
                case Mode.Popup: BuildPopup(); break;
                case Mode.Stagger: BuildStagger(); break;
                case Mode.Fade: BuildFade(); break;
                case Mode.Toast: BuildToast(); break;
            }
        }

        CanvasGroup Group(Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        void Restore()
        {
            if (group != null) group.alpha = 1f;
            if (window != null) window.localScale = Vector3.one;
        }

        void BuildPopup()
        {
            group = Group(this);
            group.alpha = 0f;
            seq.Append(group.DOFade(1f, duration * 0.7f).SetEase(Ease.OutQuad));
            if (window != null)
            {
                window.localScale = Vector3.one * popFromScale;
                seq.Join(window.DOScale(1f, duration).SetEase(Ease.OutBack, 1.4f));
            }
        }

        void BuildFade()
        {
            group = Group(this);
            group.alpha = 0f;
            seq.Append(group.DOFade(1f, duration).SetEase(Ease.OutQuad));
        }

        void BuildToast()
        {
            group = Group(this);
            var rt = (RectTransform)transform;
            var end = rt.anchoredPosition;
            group.alpha = 0f;
            rt.anchoredPosition = end + new Vector2(0f, -30f);
            seq.Append(group.DOFade(1f, duration * 0.8f));
            seq.Join(rt.DOAnchorPos(end, duration).SetEase(Ease.OutCubic));
            seq.OnKill(() => { if (rt != null) rt.anchoredPosition = end; });
        }

        void BuildStagger()
        {
            int i = 0;
            foreach (RectTransform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var g = Group(child);
                var end = child.anchoredPosition;
                g.alpha = 0f;
                child.anchoredPosition = end + staggerOffset;
                float at = i++ * staggerDelay;
                seq.Insert(at, g.DOFade(1f, duration).SetEase(Ease.OutQuad));
                seq.Insert(at, child.DOAnchorPos(end, duration).SetEase(Ease.OutCubic));
                var c = child; var gg = g; var e = end;
                seq.OnKill(() => { if (c != null) { c.anchoredPosition = e; gg.alpha = 1f; } });
            }
        }
    }
}
