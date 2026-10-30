using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 00_Boot 타이틀 메뉴(uGUI). 메인(게임 시작/옵션/종료) → 시작(방 만들기/코드 참가) → 세션(코드 표시/대기/준비/시작/취소).
    /// C2-02: 세션 동작은 GameServices.Session(ISessionLobby)에 맡긴다. 서버(Net) 코드를 직접 참조하지 않는다.
    /// 호스트는 파트너가 준비하면 시작 버튼, 게스트는 준비 버튼을 쓴다.
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
        [Tooltip("방 코드 글자 수 (서버 SessionCode.Length와 같게)")]
        [SerializeField] int codeLength = 6;

        [Header("세션")]
        [SerializeField] Text sessionCodeText;
        [SerializeField] Button copyButton;
        [SerializeField] Button cancelButton;
        [Tooltip("파트너 접속·준비 상태 글자 (Text 또는 TextMeshPro)")]
        [SerializeField] Graphic partnerText;
        [Tooltip("게스트용 준비/준비 취소 버튼")]
        [SerializeField] Button readyButton;
        [Tooltip("호스트용 시작 버튼. 파트너가 준비해야 눌린다")]
        [SerializeField] Button startGameButton;

        [Header("공용")]
        [Tooltip("접속 상태/실패 메시지를 보여줄 글자")]
        [SerializeField] Text statusText;
        [SerializeField] Color statusColor = new(0.75f, 0.78f, 0.9f);
        [SerializeField] Color failureColor = new(1f, 0.5f, 0.5f);

        [Header("테스트")]
        [Tooltip("켜면 서버 없이 가짜 로비로 준비/시작 흐름을 확인한다 (에디터·개발 빌드만)")]
        [SerializeField] bool fakeLobby;

        bool showStart;
        bool localReady;
        string notice;
        FakeSessionLobby fake;

        static ISessionLobby Session => GameServices.Session;

        void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (fakeLobby)
            {
                fake = new FakeSessionLobby();
                GameServices.RegisterSystems(fake, null, null, null);
            }
#endif
            startButton.onClick.AddListener(() => { showStart = true; notice = null; });
            optionButton.onClick.AddListener(() => notice = "옵션은 준비 중입니다");
            quitButton.onClick.AddListener(Quit);

            createButton.onClick.AddListener(() => Session?.CreateRoom());
            joinButton.onClick.AddListener(Join);
            backButton.onClick.AddListener(() => { showStart = false; notice = null; });
            codeInput.characterLimit = codeLength;
            codeInput.onValueChanged.AddListener(v => codeInput.SetTextWithoutNotify(v.ToUpperInvariant()));
            codeInput.onSubmit.AddListener(_ => Join());

            copyButton.onClick.AddListener(() =>
            {
                if (Session != null) GUIUtility.systemCopyBuffer = Session.Code;
            });
            cancelButton.onClick.AddListener(() => Session?.Leave());
            if (readyButton != null) readyButton.onClick.AddListener(ToggleReady);
            if (startGameButton != null) startGameButton.onClick.AddListener(() => Session?.StartGame());
        }

        void Join() => Session?.JoinRoom(codeInput.text);

        void ToggleReady()
        {
            var session = Session;
            if (session == null) return;
            localReady = !localReady;
            session.SetReady(localReady);
        }

        // 세션 구현에 변경 이벤트가 없을 수 있어(임시 브리지) 매 프레임 다시 그린다
        void Update()
        {
            fake?.Tick(Time.deltaTime);

            var session = Session;
            var phase = session != null ? session.Phase : SessionPhase.Idle;
            var idle = phase == SessionPhase.Idle;
            // PartnerLost는 코스 안 오버레이(C2-03)가 맡는다
            var inGame = phase == SessionPhase.InGame || phase == SessionPhase.Offline || phase == SessionPhase.PartnerLost;
            var lobby = phase == SessionPhase.Lobby;
            if (!lobby) localReady = false;

            mainPanel.SetActive(idle && !showStart);
            startPanel.SetActive(idle && showStart);
            sessionPanel.SetActive(!idle && !inGame);

            if (sessionPanel.activeSelf)
            {
                var code = session.Code;
                sessionCodeText.text = string.IsNullOrEmpty(code) ? "······" : code;
                copyButton.gameObject.SetActive(!string.IsNullOrEmpty(code));
                DrawLobby(session, lobby);
            }

            var message = session != null && !string.IsNullOrEmpty(session.Status) ? session.Status : notice;
            statusText.gameObject.SetActive(!inGame && !string.IsNullOrEmpty(message));
            statusText.text = message;
            statusText.color = session != null && session.IsFailure && message == session.Status ? failureColor : statusColor;
        }

        void DrawLobby(ISessionLobby session, bool lobby)
        {
            SetText(partnerText, PartnerLabel(session, lobby));
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(lobby && !session.IsHost);
                Graphic label = readyButton.GetComponentInChildren<TMP_Text>();
                if (label == null) label = readyButton.GetComponentInChildren<Text>();
                SetText(label, localReady ? "준비 취소" : "준비");
            }
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(lobby && session.IsHost);
                startGameButton.interactable = session.PartnerReady;
            }
        }

        // 아트 적용된 씬은 TextMeshPro, 빌더 기본 UI는 legacy Text
        static void SetText(Graphic graphic, string text)
        {
            if (graphic is TMP_Text tmp) tmp.text = text;
            else if (graphic is Text legacy) legacy.text = text;
        }

        string PartnerLabel(ISessionLobby session, bool lobby)
        {
            switch (session.Phase)
            {
                case SessionPhase.Connecting: return "연결 중…";
                case SessionPhase.WaitingPartner: return "파트너를 기다리는 중";
                case SessionPhase.LoadingCourse: return "코스를 불러오는 중";
            }
            if (!lobby) return "";
            if (session.IsHost) return session.PartnerReady ? "파트너 준비 완료" : "파트너 준비 대기 중";
            return localReady ? "호스트의 시작을 기다리는 중" : "준비를 눌러 주세요";
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
