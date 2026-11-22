using UnityEngine;
using UnityEngine.UI;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// TitleScene(예전 00_Boot) 타이틀 메인 메뉴(uGUI): 게임 시작 / 옵션 / 종료.
    /// - 게임 시작 → LobbyScene 전환은 서버 TitleStartLink, 닉네임 확인은 TitleNicknameGate가 맡는다(S-1107-2, C-1109-1).
    /// - 옵션 버튼은 인스펙터 onClick으로 OptionPopup을 연다(코드 리스너 없음).
    /// - 이 스크립트는 종료만 처리한다. 예전 방 만들기/코드 참가/대기 패널은 LobbyScene으로 옮겨져 지웠다(2026-11-22).
    /// </summary>
    public class BootMenuUI : MonoBehaviour
    {
        [SerializeField] Button quitButton;

        void Awake()
        {
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
