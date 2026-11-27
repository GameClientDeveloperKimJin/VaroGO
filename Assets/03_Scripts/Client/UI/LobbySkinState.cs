using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VaroGO.Net;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    // UI_Lobby 루트에 붙는다. 서버 LobbyUI가 정한 상태(글자·활성)를 읽어 아트 그림만 바꾼다 (2026-11-28 로비 ② 아트).
    // - 코드 6칸: 빈칸 / 입력됨 / 오류(ErrorText가 켜졌을 때)
    // - 팀 버튼: 기본 / 내가 고름 / 상대가 고름 / 둘 다
    // - 준비 버튼: 준비(초록) ↔ 준비 취소(주황) 그림 세트
    // - 공개 태그: 색 + 비공개 자물쇠
    // 동작(클릭·네트워크)은 건드리지 않는다.
    public class LobbySkinState : MonoBehaviour
    {
        [System.Serializable]
        public class SpriteSet
        {
            public Sprite normal, highlighted, pressed, disabled;
            public void ApplyTo(Button b)
            {
                if (b == null) return;
                if (b.image != null && b.image.sprite != normal) b.image.sprite = normal;
                var s = b.spriteState;
                if (s.highlightedSprite == highlighted && s.pressedSprite == pressed) return;
                b.spriteState = new SpriteState { highlightedSprite = highlighted, selectedSprite = highlighted, pressedSprite = pressed, disabledSprite = disabled };
            }
        }

        [Header("코드로 참가")]
        [SerializeField] Image[] codeSlots = new Image[6];
        [SerializeField] TMP_Text[] codeSlotTexts = new TMP_Text[6];
        [SerializeField] GameObject errorObject;
        [SerializeField] Sprite slotEmpty, slotFilled, slotError;

        [Header("팀 버튼 (0 기본, 1 내가, 2 상대, 3 둘 다)")]
        [SerializeField] Image redButton;
        [SerializeField] Sprite[] redSprites = new Sprite[4];
        [SerializeField] Image blueButton;
        [SerializeField] Sprite[] blueSprites = new Sprite[4];

        [Header("준비 버튼")]
        [SerializeField] Button readyButton;
        [SerializeField] SpriteSet readySet;
        [SerializeField] SpriteSet cancelSet;

        [Header("공개 태그")]
        [SerializeField] Image visibilityTag;
        [SerializeField] GameObject lockIcon;
        [SerializeField] Color privateColor = new(0.95f, 0.8f, 0.45f);
        [SerializeField] Color publicColor = new(0.55f, 0.95f, 0.45f);

        void LateUpdate()
        {
            DrawCodeSlots();
            var net = NetBootstrap.Instance;
            if (net == null) return;
            bool inRoom = net.State == SessionState.WaitingPartner || net.State == SessionState.Lobby;
            if (!inRoom) return;

            var me = net.LocalTeam;
            PlayerRole partner = PlayerRole.None;
            for (int i = 0; i < 2; i++)
            {
                var slot = net.GetSlot(i);
                if (slot.Present && !slot.IsLocal) partner = slot.Team;
            }
            SetTeam(redButton, redSprites, PlayerRole.A_Red, me, partner);
            SetTeam(blueButton, blueSprites, PlayerRole.B_Blue, me, partner);

            (net.LocalReady ? cancelSet : readySet)?.ApplyTo(readyButton);

            if (visibilityTag != null) visibilityTag.color = net.IsPrivate ? privateColor : publicColor;
            if (lockIcon != null && lockIcon.activeSelf != net.IsPrivate) lockIcon.SetActive(net.IsPrivate);
        }

        void DrawCodeSlots()
        {
            if (codeSlots == null) return;
            bool error = errorObject != null && errorObject.activeInHierarchy;
            for (int i = 0; i < codeSlots.Length; i++)
            {
                if (codeSlots[i] == null) continue;
                string t = i < codeSlotTexts.Length && codeSlotTexts[i] != null ? codeSlotTexts[i].text : string.Empty;
                bool filled = !string.IsNullOrEmpty(t) && t != "|";
                var s = error ? slotError : filled ? slotFilled : slotEmpty;
                if (s != null && codeSlots[i].sprite != s) codeSlots[i].sprite = s;
            }
        }

        static void SetTeam(Image img, Sprite[] set, PlayerRole team, PlayerRole me, PlayerRole partner)
        {
            if (img == null || set == null || set.Length < 4) return;
            bool mine = me == team, other = partner == team;
            var s = mine && other ? set[3] : mine ? set[1] : other ? set[2] : set[0];
            if (s != null && img.sprite != s) img.sprite = s;
        }
    }
}
