using TMPro;
using UnityEngine;

namespace VaroGO.Client
{
    // 조력자 대사창 (UI_HelperDialogue 프리팹). 화면 아래 가운데 상자에 이름표 + 대사.
    // 대사는 한 글자씩 빠르게 나타나고(Chars Per Second), 사라질 때 살짝 흐려진다.
    // 상자·이름표 그림은 아트가 들어오면 Panel·NameTag의 Image Sprite만 바꾸면 된다.
    public class FlightDialogueUI : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text bodyText;
        [Tooltip("대사가 다 나온 뒤 켜지는 \"다음\" 표시 (선택)")]
        [SerializeField] GameObject nextIcon;
        [Tooltip("초당 나타나는 글자 수. 0이면 한 번에 전부")]
        [SerializeField, Min(0f)] float charsPerSecond = 45f;
        [Tooltip("나타나고 사라지는 시간 (초)")]
        [SerializeField, Min(0.01f)] float fadeTime = 0.15f;

        // 아래 값은 기준 화면 1920x1080 픽셀 기준. 바꾸면 프리팹에서 바로 보인다
        [Header("배치 · 크기")]
        [SerializeField] RectTransform panel;
        [Tooltip("화면 어디에 붙일지 (0~1). (0.5, 0) = 아래 가운데, (0.5, 1) = 위 가운데")]
        [SerializeField] Vector2 screenAnchor = new Vector2(0.5f, 0f);
        [Tooltip("붙인 곳에서 띄울 거리 (px). 아래 가운데면 y가 바닥에서 상자 아래쪽까지 높이 (내구도 판 위로 오게 170)")]
        [SerializeField] Vector2 offset = new Vector2(0f, 170f);
        [Tooltip("대사 상자 크기 (가로, 세로 px). '대사에 맞춤'을 켜면 가로 = 최대 폭, 세로 = 최소 높이")]
        [SerializeField] Vector2 boxSize = new Vector2(900f, 130f);
        [Tooltip("켜면 대사 길이에 맞춰 상자가 늘고 줄어든다 (짧으면 좁게, 길면 넓게 → 최대 폭을 넘으면 줄이 늘며 세로로 커짐)")]
        [SerializeField] bool fitToText = true;
        [Tooltip("대사에 맞출 때 가장 좁은 폭 (px). 이름표보다 넓게")]
        [SerializeField, Min(50f)] float minWidth = 380f;
        [Tooltip("대사에 맞출 때 가장 높은 높이 (px). 넘으면 글자가 작아진다")]
        [SerializeField, Min(50f)] float maxHeight = 260f;
        [Tooltip("크기가 바뀌는 속도 (클수록 빨리, 0이면 바로)")]
        [SerializeField, Min(0f)] float resizeSpeed = 18f;
        [Tooltip("대사 글자 크기. 상자에 안 들어가면 70%까지 저절로 줄어든다")]
        [SerializeField, Min(8f)] float bodyFontSize = 30f;
        [Tooltip("대사 글자 안쪽 여백 (왼쪽·오른쪽, 아래, 위 px)")]
        [SerializeField] Vector3 bodyPadding = new Vector3(44f, 16f, 30f);
        [Tooltip("이름표 크기 (가로, 세로 px)와 이름 글자 크기")]
        [SerializeField] Vector2 nameTagSize = new Vector2(150f, 44f);
        [SerializeField, Min(8f)] float nameFontSize = 24f;
        [Tooltip("이름표를 상자 왼쪽 끝에서 얼마나 안쪽에 둘지 (px)")]
        [SerializeField] float nameTagInset = 30f;
        [Tooltip("\"다음\" 표시 크기 (px)")]
        [SerializeField] Vector2 nextIconSize = new Vector2(32f, 25f);

        string current;
        float shownAt;
        bool visible;
        Vector2 targetSize;

        void Awake()
        {
            ApplyLayout();
            if (group != null) group.alpha = 0f;
            if (nextIcon != null) nextIcon.SetActive(false);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // OnValidate 안에서 RectTransform을 바로 바꾸면 경고가 나서 한 박자 뒤에 적용
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) ApplyLayout(); };
        }
#endif

        // Inspector 값대로 상자·글자·이름표 위치와 크기를 맞춘다
        public void ApplyLayout()
        {
            if (panel == null) panel = transform.Find("Panel") as RectTransform;
            if (panel == null) return;
            panel.anchorMin = panel.anchorMax = screenAnchor;
            panel.pivot = new Vector2(0.5f, screenAnchor.y >= 0.5f ? 1f : 0f);
            panel.anchoredPosition = new Vector2(offset.x, screenAnchor.y >= 0.5f ? -offset.y : offset.y);
            panel.sizeDelta = targetSize = boxSize;
            if (bodyText != null)
            {
                var rt = bodyText.rectTransform;
                rt.offsetMin = new Vector2(bodyPadding.x, bodyPadding.y);
                rt.offsetMax = new Vector2(-bodyPadding.x, -bodyPadding.z);
                bodyText.enableAutoSizing = true;
                bodyText.fontSizeMax = bodyFontSize;
                bodyText.fontSizeMin = bodyFontSize * 0.7f;
                bodyText.fontSize = bodyFontSize;
            }
            if (nameText != null)
            {
                nameText.fontSize = nameFontSize;
                if (nameText.transform.parent is RectTransform tag && tag != panel)
                {
                    tag.sizeDelta = nameTagSize;
                    tag.anchoredPosition = new Vector2(nameTagInset, 0f);
                }
            }
            if (nextIcon != null && nextIcon.transform is RectTransform nrt)
            {
                nrt.sizeDelta = nextIconSize;
                nrt.anchoredPosition = new Vector2(-bodyPadding.x, bodyPadding.y + nextIconSize.y * 0.5f);
            }
        }

        public void Show(string speaker, string text)
        {
            if (nameText != null) nameText.text = speaker;
            if (text != current)
            {
                current = text;
                shownAt = Time.unscaledTime;
                if (bodyText != null)
                {
                    bodyText.text = text;
                    bodyText.maxVisibleCharacters = charsPerSecond > 0f ? 0 : 99999;
                }
                FitToText(text, group == null || group.alpha < 0.05f);
            }
            visible = true;
        }

        // 대사 길이에 맞는 상자 크기를 정한다. snap이면 바로(처음 나타날 때), 아니면 Update에서 부드럽게
        void FitToText(string text, bool snap)
        {
            if (panel == null || bodyText == null) return;
            if (!fitToText) { targetSize = boxSize; return; }
            float padX = bodyPadding.x * 2f + nextIconSize.x; // 오른쪽 아래 ▼ 자리까지
            float padY = bodyPadding.y + bodyPadding.z;
            float maxW = Mathf.Max(minWidth, boxSize.x);
            bool auto = bodyText.enableAutoSizing;
            bodyText.enableAutoSizing = false;
            bodyText.fontSize = bodyFontSize;
            Vector2 pref = bodyText.GetPreferredValues(text, maxW - padX, 0f);
            bodyText.enableAutoSizing = auto;
            float w = Mathf.Clamp(pref.x + padX, minWidth, maxW);
            float h = Mathf.Clamp(pref.y + padY, boxSize.y, Mathf.Max(boxSize.y, maxHeight));
            targetSize = new Vector2(w, h);
            if (snap || resizeSpeed <= 0f) panel.sizeDelta = targetSize;
        }

        public void Hide()
        {
            visible = false;
            current = null;
        }

        void Update()
        {
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / fadeTime);
            if (panel != null && resizeSpeed > 0f && panel.sizeDelta != targetSize)
                panel.sizeDelta = Vector2.Lerp(panel.sizeDelta, targetSize, 1f - Mathf.Exp(-resizeSpeed * Time.unscaledDeltaTime));
            bool done = true;
            if (visible && bodyText != null && charsPerSecond > 0f)
            {
                int n = Mathf.FloorToInt((Time.unscaledTime - shownAt) * charsPerSecond);
                bodyText.maxVisibleCharacters = n;
                done = n >= (current?.Length ?? 0);
            }
            if (nextIcon != null && nextIcon.activeSelf != (visible && done)) nextIcon.SetActive(visible && done);
        }
    }
}
