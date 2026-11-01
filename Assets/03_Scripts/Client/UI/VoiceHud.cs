using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VaroGO.Client.Voice;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 인게임 보이스 HUD (설계서 02). 화면 위쪽 양 끝에만 놓는다.
    /// - 우측 상단: 내 마이크 켜짐/꺼짐. 꺼짐은 빨갛게 강조
    /// - 좌측 상단: 상대 말하는 중/조용함. 말하는 동안만 초록 강조 (거리와 무관). 온라인 보이스 연결 때만 보임
    /// 일시정지 메뉴보다 위에 그린다(같은 캔버스에서 메뉴보다 뒤 순서에 둔다).
    /// 씬에 미리 놓은 PersistentUI 프리팹 안에 있다. 배치·크기·글꼴은 프리팹에서, 색은 아래 값에서 바꾼다.
    /// </summary>
    public class VoiceHud : MonoBehaviour
    {
        [Header("내 마이크 (우측 상단)")]
        [SerializeField] GameObject micPill;
        [SerializeField] Image micBackground;
        [SerializeField] TMP_Text micLabel;
        [Tooltip("꺼짐일 때만 켜지는 테두리")]
        [SerializeField] Outline micBorder;

        [Header("파트너 (좌측 상단)")]
        [SerializeField] GameObject partnerPill;
        [SerializeField] Image partnerBackground;
        [SerializeField] TMP_Text partnerLabel;
        [Tooltip("말하는 중일 때만 켜지는 테두리")]
        [SerializeField] Outline partnerBorder;

        [Header("문구")]
        [SerializeField] string micOnText = "내 마이크 · 켜짐";
        [SerializeField] string micOffText = "내 마이크 · 꺼짐";
        [SerializeField] string speakingText = "파트너 · 말하는 중";
        [SerializeField] string quietText = "파트너 · 조용함";

        [Header("색")]
        [SerializeField] Color micOnBg = new(0.12f, 0.14f, 0.2f, 0.85f);
        [SerializeField] Color micOnFg = Color.white;
        [SerializeField] Color micOffBg = new(1f, 1f, 1f, 0.9f);
        [SerializeField] Color micOffFg = new(0.75f, 0.2f, 0.18f);
        [SerializeField] Color speakingBg = new(1f, 1f, 1f, 0.92f);
        [SerializeField] Color speakingFg = new(0.2f, 0.55f, 0.3f);
        [SerializeField] Color quietBg = new(1f, 1f, 1f, 0.6f);
        [SerializeField] Color quietFg = new(0.35f, 0.38f, 0.42f);

        void Update()
        {
            var voice = HasLocalPlayer() ? VoiceManager.Current : null;

            SetActive(micPill, voice != null);
            SetActive(partnerPill, voice != null && voice.IsConnected);
            if (voice == null) return;

            bool micOn = voice.MicEnabled;
            Apply(micBackground, micLabel, micOn ? micOnText : micOffText,
                  micOn ? micOnBg : micOffBg, micOn ? micOnFg : micOffFg);
            if (micBorder != null) { micBorder.enabled = !micOn; micBorder.effectColor = micOffFg; }

            if (!voice.IsConnected) return;
            bool speaking = voice.PartnerSpeaking;
            Apply(partnerBackground, partnerLabel, speaking ? speakingText : quietText,
                  speaking ? speakingBg : quietBg, speaking ? speakingFg : quietFg);
            if (partnerBorder != null) { partnerBorder.enabled = speaking; partnerBorder.effectColor = speakingFg; }
        }

        static void Apply(Image bg, TMP_Text label, string text, Color bgColor, Color fgColor)
        {
            if (bg != null) bg.color = bgColor;
            if (label == null) return;
            if (label.text != text) label.text = text;
            label.color = fgColor;
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        static bool HasLocalPlayer()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].IsLocal) return true;
            return false;
        }
    }
}
