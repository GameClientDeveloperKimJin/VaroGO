using UnityEngine;
using UnityEngine.UI;
using VaroGO.Net;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 00_Boot 타이틀 메뉴(uGUI). 메인(게임 시작/옵션/종료) → 시작(방 만들기/코드 참가) → 세션(코드 표시/대기/취소).
    /// 세션 동작은 NetBootstrap.Instance에 맡긴다. NetBootstrap은 DontDestroyOnLoad라 씬 참조 대신 Instance를 쓴다.
    /// </summary>
    public class BootMenuUI : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject startPanel;
        [SerializeField] GameObject sessionPanel;

        [Header("메인")]
        [SerializeField] Button startButton;
        [SerializeField] Button optionButton;
        [SerializeField] Button quitButton;

        [Header("시작")]
        [SerializeField] Button createButton;
        [SerializeField] InputField codeInput;
        [SerializeField] Button joinButton;
        [SerializeField] Button backButton;

        [Header("세션")]
        [SerializeField] Text sessionCodeText;
        [SerializeField] Button copyButton;
        [SerializeField] Button cancelButton;

        [Header("공용")]
        [Tooltip("접속 상태/실패 메시지를 보여줄 글자")]
        [SerializeField] Text statusText;
        [SerializeField] Color statusColor = new(0.75f, 0.78f, 0.9f);
        [SerializeField] Color failureColor = new(1f, 0.5f, 0.5f);

        bool showStart;
        string notice;

        void Awake()
        {
            startButton.onClick.AddListener(() => { showStart = true; notice = null; });
            optionButton.onClick.AddListener(() => notice = "옵션은 준비 중입니다");
            quitButton.onClick.AddListener(Quit);

            createButton.onClick.AddListener(() => Bootstrap?.CreateRoom());
            joinButton.onClick.AddListener(Join);
            backButton.onClick.AddListener(() => { showStart = false; notice = null; });
            codeInput.characterLimit = Net.SessionCode.Length;
            codeInput.onValueChanged.AddListener(v => codeInput.SetTextWithoutNotify(v.ToUpperInvariant()));
            codeInput.onSubmit.AddListener(_ => Join());

            copyButton.onClick.AddListener(() =>
            {
                if (Bootstrap != null) GUIUtility.systemCopyBuffer = Bootstrap.SessionCode;
            });
            cancelButton.onClick.AddListener(() => Bootstrap?.Leave());
        }

        static NetBootstrap Bootstrap => NetBootstrap.Instance;

        void Join() => Bootstrap?.JoinRoom(codeInput.text);

        void Update()
        {
            var bootstrap = Bootstrap;
            var state = bootstrap != null ? bootstrap.State : SessionState.Idle;
            var idle = state == SessionState.Idle;
            var inGame = state == SessionState.InGame;

            mainPanel.SetActive(idle && !showStart);
            startPanel.SetActive(idle && showStart);
            sessionPanel.SetActive(!idle && !inGame);

            if (sessionPanel.activeSelf)
            {
                var code = bootstrap.SessionCode;
                sessionCodeText.text = string.IsNullOrEmpty(code) ? "······" : code;
                copyButton.gameObject.SetActive(!string.IsNullOrEmpty(code));
            }

            var message = bootstrap != null && !string.IsNullOrEmpty(bootstrap.Status) ? bootstrap.Status : notice;
            statusText.gameObject.SetActive(!inGame && !string.IsNullOrEmpty(message));
            statusText.text = message;
            statusText.color = bootstrap != null && bootstrap.IsFailure && message == bootstrap.Status ? failureColor : statusColor;
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
