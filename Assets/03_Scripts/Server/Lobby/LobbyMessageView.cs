using TMPro;
using UnityEngine;
using static VaroGO.Net.Lobby.LobbyUiUtil;

namespace VaroGO.Net.Lobby
{
    /// <summary>
    /// LobbyScene의 UI_Message(클라 UI 세팅, C-1104-1)에 붙는 서버 연결 스크립트.
    /// 알림 팝업(방장 해산·연결 끊김), 토스트(상대가 나갔어요), 로딩 화면(서버 연결 중·방 들어가는 중)을 켠다.
    /// 오브젝트 이름은 클라 UI 그대로 쓴다: MessagePopup/Window/{Header/TitleText, MessageText}, Toast/ToastText, LoadingOverlay/LoadingText.
    /// </summary>
    public class LobbyMessageView : MonoBehaviour
    {
        const float ToastSeconds = 2.5f;

        GameObject popup, toast, loading;
        TMP_Text popupTitle, popupBody, toastText, loadingText;
        float toastHideAt;

        void Awake()
        {
            var root = transform;
            popup = FindGo(root, "MessagePopup");
            popupTitle = Find<TMP_Text>(root, "MessagePopup/Window/Header/TitleText");
            popupBody = Find<TMP_Text>(root, "MessagePopup/Window/MessageText");
            toast = FindGo(root, "Toast");
            toastText = Find<TMP_Text>(root, "Toast/ToastText");
            loading = FindGo(root, "LoadingOverlay");
            loadingText = Find<TMP_Text>(root, "LoadingOverlay/LoadingText");
        }

        public void ShowPopup(string body, string title = "알림")
        {
            SetText(popupTitle, title);
            SetText(popupBody, body);
            SetActive(popup, true);
        }

        public void ShowToast(string message)
        {
            SetText(toastText, message);
            SetActive(toast, true);
            toastHideAt = Time.unscaledTime + ToastSeconds;
        }

        void Update()
        {
            var net = NetBootstrap.Instance;
            if (net == null) return;

            if (net.TakePopup(out var title, out var body)) ShowPopup(body, title);
            var t = net.TakeToast();
            if (t != null) ShowToast(t);
            if (toast != null && toast.activeSelf && Time.unscaledTime >= toastHideAt) toast.SetActive(false);

            var text = net.LoadingText;
            SetActive(loading, text != null);
            if (text != null) SetText(loadingText, text);
        }
    }
}
