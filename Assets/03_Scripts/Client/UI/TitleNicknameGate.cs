using UnityEngine;
using UnityEngine.UI;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// TitleScene "게임 시작"에서 저장된 닉네임이 없으면 닉네임 팝업을 연다.
    /// 확인 → 저장 후 "게임 시작"을 다시 눌러 로비로 이동 / 취소 → 타이틀에 그대로 남는다.
    /// 로비 이동 자체는 서버 TitleStartLink가 맡고, 닉네임이 없으면 이동하지 않는다(C-1109-1 요청).
    /// </summary>
    public class TitleNicknameGate : MonoBehaviour
    {
        [SerializeField] Button startButton;
        [SerializeField] NicknamePopup popup;

        void Awake() => startButton.onClick.AddListener(OnStart);

        void OnStart()
        {
            if (PlayerNickname.HasNickname || popup.IsOpen) return;
            popup.Open(onConfirm: _ => startButton.onClick.Invoke());
        }
    }
}
