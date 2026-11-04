using UnityEngine;
using UnityEngine.UI;
using VaroGO.Client.Voice;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 인게임 보이스 HUD (설계서 02). 화면 위쪽 양 끝에만 놓는다.
    /// - 우측 상단: 내 마이크 패널. 켜짐/꺼짐 아이콘으로 보여 준다
    /// - 좌측 상단: 파트너가 말하는 동안만 스피커 아이콘이 보인다 (거리와 무관). 온라인 보이스 연결 때만
    /// 씬에 미리 놓은 PersistentUI 프리팹 안에 있다. 배치·크기·그림은 프리팹에서 바꾼다.
    /// </summary>
    public class VoiceHud : MonoBehaviour
    {
        [Header("내 마이크 (우측 상단)")]
        [Tooltip("마이크 패널 전체. 보이스가 있을 때만 보인다")]
        [SerializeField] GameObject micPill;
        [Tooltip("켜짐/꺼짐 그림을 바꿔 끼울 아이콘")]
        [SerializeField] Image micIcon;
        [SerializeField] Sprite micOnSprite;
        [SerializeField] Sprite micOffSprite;

        [Header("파트너 (좌측 상단)")]
        [Tooltip("파트너가 말하는 동안만 켜지는 아이콘")]
        [SerializeField] GameObject partnerSpeaking;

        void Update()
        {
            var voice = HasLocalPlayer() ? VoiceManager.Current : null;

            SetActive(micPill, voice != null);
            SetActive(partnerSpeaking, voice != null && voice.IsConnected && voice.PartnerSpeaking);
            if (voice == null || micIcon == null) return;

            var sprite = voice.MicEnabled ? micOnSprite : micOffSprite;
            if (micIcon.sprite != sprite) micIcon.sprite = sprite;
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
