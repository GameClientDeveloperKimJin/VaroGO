using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VaroGO.Shared;
using static VaroGO.Net.Lobby.LobbyUiUtil;

namespace VaroGO.Net.Lobby
{
    /// <summary>
    /// LobbyScene의 UI_Lobby(클라 UI 세팅, C-1104-1)에 붙는 서버 연결 스크립트 (로비 기획서 v0.1 p6~p11).
    /// 클라가 만든 오브젝트를 이름으로 찾아 버튼·글자를 NetBootstrap에 연결한다. 배치·이름은 바꾸지 않는다.
    /// 팝업 켜기/끄기(SetActive)처럼 클라가 인스펙터에 넣어 둔 연결은 그대로 두고 그 위에 추가만 한다.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        static readonly Color DisabledText = new Color(0.55f, 0.55f, 0.55f);

        // 방 리스트 (p6)
        GameObject roomListPanel;
        Button backButton, refreshButton;
        Transform listContent;
        GameObject itemTemplate, emptyText;
        readonly List<GameObject> items = new List<GameObject>();

        // 방 만들기 (p7)
        GameObject createPopup;
        TMP_InputField roomNameInput;
        Toggle privateToggle;
        Button createButton;

        // 코드로 참가 (p8)
        GameObject joinPopup;
        TMP_InputField codeInput;
        readonly TMP_Text[] codeSlots = new TMP_Text[SessionCode.Length];
        Button pasteButton, joinButton;
        GameObject errorObject;
        TMP_Text errorText;

        // 대기실 (p9~p11)
        GameObject roomPanel;
        TMP_Text roomNameText, tagText, codeText;
        Button copyButton, steamButton, leaveButton, redButton, blueButton, readyButton, startButton;
        TMP_Text redLabel, blueLabel, readyLabel;

        LobbyMessageView messages;
        string shownJoinError = string.Empty;
        bool listDirty = true;
        NetBootstrap subscribed;

        void Awake()
        {
            var root = transform;

            roomListPanel = FindGo(root, "RoomListPanel");
            backButton = Find<Button>(root, "RoomListPanel/TopBar/BackButton");
            refreshButton = Find<Button>(root, "RoomListPanel/TopBar/RefreshButton");
            listContent = Find<Transform>(root, "RoomListPanel/RoomList/Viewport/Content");
            itemTemplate = FindGo(root, "RoomListPanel/RoomList/Viewport/Content/RoomItem_Template");
            emptyText = FindGo(root, "RoomListPanel/RoomList/EmptyText");

            createPopup = FindGo(root, "CreateRoomPopup");
            roomNameInput = Find<TMP_InputField>(root, "CreateRoomPopup/Window/Rows/RoomNameRow/RoomNameInput");
            privateToggle = Find<Toggle>(root, "CreateRoomPopup/Window/Rows/VisibilityRow/VisibilityToggles/PrivateToggle");
            createButton = Find<Button>(root, "CreateRoomPopup/Window/Footer/CreateButton");

            joinPopup = FindGo(root, "JoinCodePopup");
            codeInput = Find<TMP_InputField>(root, "JoinCodePopup/Window/CodeInput");
            for (int i = 0; i < codeSlots.Length; i++)
                codeSlots[i] = Find<TMP_Text>(root, $"JoinCodePopup/Window/CodeSlots/CodeSlot_{i + 1}/SlotText");
            pasteButton = Find<Button>(root, "JoinCodePopup/Window/PasteButton");
            joinButton = Find<Button>(root, "JoinCodePopup/Window/Footer/JoinButton");
            errorObject = FindGo(root, "JoinCodePopup/Window/ErrorText");
            errorText = errorObject != null ? errorObject.GetComponent<TMP_Text>() : null;

            roomPanel = FindGo(root, "RoomPanel");
            roomNameText = Find<TMP_Text>(root, "RoomPanel/RoomInfo/RoomNameText");
            tagText = Find<TMP_Text>(root, "RoomPanel/RoomInfo/VisibilityTag/TagText");
            codeText = Find<TMP_Text>(root, "RoomPanel/CodeBar/CodeText");
            copyButton = Find<Button>(root, "RoomPanel/CodeBar/CopyCodeButton");
            steamButton = Find<Button>(root, "RoomPanel/CodeBar/SteamInviteButton");
            leaveButton = Find<Button>(root, "RoomPanel/LeaveButton");
            redButton = Find<Button>(root, "RoomPanel/TeamReadyPanel/RedTeamButton");
            blueButton = Find<Button>(root, "RoomPanel/TeamReadyPanel/BlueTeamButton");
            readyButton = Find<Button>(root, "RoomPanel/TeamReadyPanel/ReadyButton");
            startButton = Find<Button>(root, "RoomPanel/StartGameButton");
            redLabel = Label(redButton);
            blueLabel = Label(blueButton);
            readyLabel = Label(readyButton);

            messages = FindFirstObjectByType<LobbyMessageView>();

            Wire();
        }

        void Wire()
        {
            backButton?.onClick.AddListener(() => NetBootstrap.Instance?.ExitToTitle());
            refreshButton?.onClick.AddListener(() => NetBootstrap.Instance?.RefreshList());

            if (roomNameInput != null) roomNameInput.characterLimit = NetBootstrap.RoomNameMaxLength;
            createButton?.onClick.AddListener(OnCreate);

            if (codeInput != null)
            {
                codeInput.characterLimit = SessionCode.Length;
                codeInput.onValueChanged.AddListener(OnCodeChanged);
                codeInput.onSubmit.AddListener(_ => OnJoin());
            }
            pasteButton?.onClick.AddListener(OnPaste);
            joinButton?.onClick.AddListener(OnJoin);

            copyButton?.onClick.AddListener(OnCopyCode);
            leaveButton?.onClick.AddListener(() => NetBootstrap.Instance?.Leave());
            redButton?.onClick.AddListener(() => NetBootstrap.Instance?.RequestTeam(PlayerRole.A_Red));
            blueButton?.onClick.AddListener(() => NetBootstrap.Instance?.RequestTeam(PlayerRole.B_Blue));
            readyButton?.onClick.AddListener(() =>
            {
                var net = NetBootstrap.Instance;
                net?.SetReady(!net.LocalReady);
            });
            startButton?.onClick.AddListener(() => NetBootstrap.Instance?.StartGame());
            if (steamButton != null) steamButton.interactable = false; // 스팀 연동 후 활성 (p12)
        }

        void Start()
        {
            var net = NetBootstrap.Instance;
            if (net == null)
            {
                // LobbyScene을 바로 실행한 경우: NetBootstrap은 TitleScene에 있다
                Debug.LogWarning("[Lobby] NetBootstrap 없음 → TitleScene부터 시작합니다");
                SceneManager.LoadScene(NetBootstrap.TitleScenePath);
                return;
            }
            subscribed = net;
            net.RoomListChanged += MarkListDirty;
            OnCodeChanged(codeInput != null ? codeInput.text : string.Empty);
            if (net.State == SessionState.Idle) net.EnterLobbyList();
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.RoomListChanged -= MarkListDirty;
        }

        void MarkListDirty() => listDirty = true;

        void Update()
        {
            var net = NetBootstrap.Instance;
            if (net == null) return;

            bool inRoom = net.State == SessionState.WaitingPartner || net.State == SessionState.Lobby;
            SetActive(roomListPanel, !inRoom);
            SetActive(roomPanel, inRoom);
            if (inRoom)
            {
                SetActive(createPopup, false);
                SetActive(joinPopup, false);
                DrawRoom(net);
            }
            else if (listDirty)
            {
                listDirty = false;
                DrawList(net);
            }

            // 방 만들기·참가 실패 사유 (p8 ③, p13)
            if (net.JoinError != shownJoinError)
            {
                shownJoinError = net.JoinError;
                if (!string.IsNullOrEmpty(shownJoinError))
                {
                    if (joinPopup != null && joinPopup.activeInHierarchy)
                    {
                        SetText(errorText, shownJoinError);
                        SetActive(errorObject, true);
                    }
                    else messages?.ShowPopup(shownJoinError);
                }
            }
        }

        // ---- 방 리스트 ----

        void DrawList(NetBootstrap net)
        {
            foreach (var go in items) Destroy(go);
            items.Clear();
            if (itemTemplate == null || listContent == null) return;

            foreach (var room in net.RoomList)
            {
                var go = Instantiate(itemTemplate, listContent);
                go.name = $"RoomItem_{room.Code}";
                go.SetActive(true);
                var t = go.transform;
                SetText(Find<TMP_Text>(t, "RoomNameText"), room.Name);
                SetText(Find<TMP_Text>(t, "PlayerCountText"), $"{room.Players} / 2");
                string state = room.InGame ? "게임 중" : !room.VersionMatch ? "버전 다름" : room.Players >= 2 ? "가득 참" : "대기 중";
                SetText(Find<TMP_Text>(t, "StateText"), state);

                var join = Find<Button>(t, "JoinButton");
                if (join != null)
                {
                    join.interactable = room.Joinable;
                    SetText(Label(join), room.Joinable ? "참가" : "불가");
                    string code = room.Code;
                    join.onClick.AddListener(() => NetBootstrap.Instance?.JoinPublic(code));
                }
                if (!room.Joinable)
                    foreach (var text in go.GetComponentsInChildren<TMP_Text>(true)) text.color = DisabledText;
                items.Add(go);
            }
            SetActive(emptyText, items.Count == 0);
        }

        // ---- 방 만들기 ----

        void OnCreate()
        {
            var net = NetBootstrap.Instance;
            if (net == null) return;
            net.CreateRoom(roomNameInput != null ? roomNameInput.text : string.Empty, privateToggle != null && privateToggle.isOn);
        }

        // ---- 코드로 참가 ----

        // 대문자·허용 문자만, 최대 6자 (0 O 1 I L 제외)
        static string CleanCode(string value)
        {
            var sb = new StringBuilder(SessionCode.Length);
            foreach (char c in (value ?? string.Empty).ToUpperInvariant())
                if (sb.Length < SessionCode.Length && SessionCode.TryNormalize(new string(c, SessionCode.Length), out _)) sb.Append(c);
            return sb.ToString();
        }

        void OnCodeChanged(string value)
        {
            string clean = CleanCode(value);
            if (codeInput != null && codeInput.text != clean)
            {
                codeInput.SetTextWithoutNotify(clean);
                codeInput.caretPosition = clean.Length;
            }
            for (int i = 0; i < codeSlots.Length; i++)
                SetText(codeSlots[i], i < clean.Length ? clean[i].ToString() : string.Empty);
            if (joinButton != null) joinButton.interactable = SessionCode.TryNormalize(clean, out _);
            SetActive(errorObject, false);
        }

        void OnPaste()
        {
            if (codeInput == null) return;
            codeInput.text = CleanCode(GUIUtility.systemCopyBuffer); // 메신저에서 받은 코드를 한 번에 채움 (p8 ②)
        }

        void OnJoin()
        {
            var net = NetBootstrap.Instance;
            if (net == null || codeInput == null) return;
            if (!SessionCode.TryNormalize(codeInput.text, out var code)) return;
            SetActive(errorObject, false);
            net.JoinRoom(code);
        }

        // ---- 대기실 ----

        void OnCopyCode()
        {
            var net = NetBootstrap.Instance;
            if (net == null || string.IsNullOrEmpty(net.SessionCode)) return;
            GUIUtility.systemCopyBuffer = net.SessionCode;
            messages?.ShowToast("코드를 복사했어요");
        }

        void DrawRoom(NetBootstrap net)
        {
            SetText(roomNameText, net.RoomName);
            SetText(tagText, net.IsPrivate ? "비밀" : "공개");
            SetText(codeText, net.SessionCode);

            var me = net.LocalTeam;
            bool ready = net.LocalReady;
            PlayerRole partnerTeam = PlayerRole.None;
            for (int i = 0; i < 2; i++)
            {
                var slot = net.GetSlot(i);
                if (slot.Present && !slot.IsLocal) partnerTeam = slot.Team;
            }

            DrawTeamButton(redButton, redLabel, "레드", PlayerRole.A_Red, me, partnerTeam, ready);
            DrawTeamButton(blueButton, blueLabel, "블루", PlayerRole.B_Blue, me, partnerTeam, ready);

            if (readyButton != null) readyButton.interactable = net.PlayerCount >= 2 && me != PlayerRole.None;
            SetText(readyLabel, ready ? "준비 취소" : "준비");

            if (startButton != null)
            {
                SetActive(startButton.gameObject, net.IsRoomHost); // 방장 전용, P2 화면엔 없음 (p10 ⑤)
                startButton.interactable = net.CanStartGame;
            }
        }

        static void DrawTeamButton(Button button, TMP_Text label, string name, PlayerRole team, PlayerRole me, PlayerRole partner, bool ready)
        {
            if (button == null) return;
            bool locked = partner == team;
            button.interactable = !locked && !ready && me != team;
            SetText(label, locked ? $"{name} · 잠김" : me == team ? $"{name} (선택)" : name);
        }
    }
}
