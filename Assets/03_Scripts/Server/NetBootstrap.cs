using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using VaroGO.Shared;

namespace VaroGO.Net
{
    // Lobby: 2명 접속, 대기실 팀·준비 (S2-01). PartnerLost: 코스 중 파트너 끊김 (S2-04).
    // Browsing: 로비 방 리스트 보는 중 (세션 로비 접속). 새 값은 맨 뒤에 추가
    public enum SessionState { Idle, Connecting, WaitingPartner, LoadingCourse, InGame, Lobby, PartnerLost, Browsing }

    /// <summary>로비 방 리스트 한 줄. 세션 속성에서 읽는다.</summary>
    public readonly struct RoomEntry
    {
        public readonly string Code;
        public readonly string Name;
        public readonly int Players;
        public readonly bool InGame;
        public readonly bool VersionMatch;

        public RoomEntry(string code, string name, int players, bool inGame, bool versionMatch)
        {
            Code = code; Name = name; Players = players; InGame = inGame; VersionMatch = versionMatch;
        }

        public bool Joinable => Players < 2 && !InGame && VersionMatch;
    }

    /// <summary>대기실 슬롯 하나(P1 = 방장, P2 = 참가자).</summary>
    public readonly struct RoomSlot
    {
        public readonly bool Present;
        public readonly bool IsLocal;
        public readonly bool IsHost;
        public readonly PlayerRole Team;
        public readonly bool Ready;

        public RoomSlot(bool present, bool isLocal, bool isHost, PlayerRole team, bool ready)
        {
            Present = present; IsLocal = isLocal; IsHost = isHost; Team = team; Ready = ready;
        }
    }

    /// <summary>
    /// TitleScene에 하나 둔다(DontDestroyOnLoad). 로비 방 리스트·방 만들기/참가·대기실·코스 로드·종료 후 복귀.
    /// 로비 기획서 v0.1: 타이틀 → LobbyScene(방 리스트) → 대기실(팀·준비) → 방장 시작 → 코스.
    /// GameServices 등록은 StartGame 직전에 한다(코스 씬 로드 전, 계약 §2).
    /// ISessionLobby(계약 §3.7)를 직접 구현한다. 로비 화면은 서버 스크립트 LobbyUI가 이 클래스를 직접 본다.
    /// </summary>
    public class NetBootstrap : MonoBehaviour, INetworkRunnerCallbacks, ISessionLobby, IStageLoader
    {
        public const string TitleScenePath = "Assets/01_Scenes/TitleScene.unity";
        public const string LobbyScenePath = "Assets/01_Scenes/LobbyScene.unity";
        const string DefaultCourseScenePath = "Assets/01_Scenes/11_SpiralCourse.unity";
        const int MaxPlayers = 2;
        const int CodeRetryCount = 3;
        public const int RoomNameMaxLength = 16;
        public const string DefaultRoomName = "같이 깨요";

        // 세션 속성 키 (Photon: 10개·500바이트·int/string/bool 한도). 생성할 때 모두 넣는다
        const string PropName = "nm";
        const string PropPrivate = "pv";
        const string PropVersion = "ver";
        const string PropState = "st"; // 0 대기 / 1 게임 중

        const float SpawnSpacing = 0.75f;

        [Tooltip("02_Prefabs/Net/Player_Net")]
        [SerializeField] NetworkObject playerPrefab;

        [Tooltip("02_Prefabs/Net/NetSession — 세션 공용 상태(팀·준비). 마스터가 스폰한다")]
        [SerializeField] NetworkObject sessionPrefab;

#if UNITY_EDITOR
        [Tooltip("접속 후 열 코스 씬. 여기에 씬을 끌어다 놓으면 아래 경로가 자동으로 채워진다. 빌드 세팅(Scenes In Build)에 들어 있어야 한다")]
        [SerializeField] UnityEditor.SceneAsset courseScene;
#endif
        [Tooltip("접속 후 열 코스 씬 경로. 비우면 11_SpiralCourse")]
        [SerializeField] string courseScenePath = DefaultCourseScenePath;

        string CourseScenePath => string.IsNullOrEmpty(courseScenePath) ? DefaultCourseScenePath : courseScenePath;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (courseScene != null)
                courseScenePath = UnityEditor.AssetDatabase.GetAssetPath(courseScene);
            else if (!string.IsNullOrEmpty(courseScenePath))
                courseScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(courseScenePath);
        }
#endif

        public static NetBootstrap Instance { get; private set; }

        public SessionState State { get; private set; }
        public string SessionCode { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public bool IsFailure { get; private set; }
        public NetworkRunner Runner { get; private set; }

        // ---- 로비 화면용 (서버 LobbyUI가 읽는다) ----

        /// <summary>세션 로비에 붙어 방 목록을 받는 중인지.</summary>
        public bool ListReady { get; private set; }
        /// <summary>공개방 목록(비밀방·가득 찬 방은 Photon이 빼고 보낸다).</summary>
        public IReadOnlyList<RoomEntry> RoomList => rooms;
        public event Action RoomListChanged;
        /// <summary>코드 참가·방 만들기 실패 사유. 팝업 안 ErrorText용. 다음 시도 때 지운다.</summary>
        public string JoinError { get; private set; } = string.Empty;
        /// <summary>내가 이 방의 방장인지(만든 사람, 또는 재접속 시간 초과로 남은 사람). 대기실 해산 판정용.</summary>
        public bool IsRoomHost { get; private set; }
        public string RoomName => ReadProp(PropName, DefaultRoomName);
        public bool IsPrivate => ReadPropBool(PropPrivate);

        NetRoleProvider role;
        NetGameClock clock;
        string failMessage;
        bool joining;
        bool courseRequested;
        bool userLeaving;
        bool exitToTitle;
        NetworkObject localPlayer;
        readonly List<RoomEntry> rooms = new List<RoomEntry>();
        int connectVersion;
        // StartGame을 기다리는 중. 실패하면 Fusion이 러너를 닫으며 OnShutdown이 먼저 불리는데, 정리는 StartSession이 한다
        bool startingGame;
        // 앱 종료(에디터 Play 정지 포함) 중. 이때 러너가 닫혀도 복귀·재연결하지 않는다
        // (안 막으면 정지 도중 새 Photon 연결 오브젝트가 생겨 에디터 씬에 남고, 씬 저장 때 같이 저장된다)
        bool quitting;

        void OnApplicationQuit() => quitting = true;

        // 씬이 바뀌어도 보여 줄 알림 (UI가 꺼내 간다)
        string pendingPopupTitle, pendingPopupBody, pendingToast;

        // Changed 이벤트용: 지난 프레임에 UI가 보던 값
        (SessionPhase, string, bool, bool, bool, bool) lastSnapshot;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // S2-03: 클라 UI는 GameServices.Session만 본다. 코스 씬을 바로 실행하면 이 오브젝트가 없어 OfflineSession이 쓰인다
            GameServices.RegisterSystems(this, null, null, null);
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
            Application.quitting += OnApplicationQuit; // 러너의 종료 처리보다 먼저 올 수 있게 이벤트로도 받는다
#if UNITY_EDITOR
            // 에디터 Play 정지: Fusion이 ExitingPlayMode에서 러너를 먼저 닫으므로 그 신호도 받는다
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

#if UNITY_EDITOR
        void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode) quitting = true;
        }
#endif

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            Application.quitting -= OnApplicationQuit;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
        }

        // LobbyScene에 들어오면(타이틀 "게임 시작", 방 종료 후 복귀) 방 목록 연결을 시작한다
        void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == LobbyScenePath && State == SessionState.Idle) EnterLobbyList();
        }

        // ---- UI가 부르는 진입점 ----

        /// <summary>ISessionLobby (클라 BootMenuUI 호환): 공개방·기본 이름.</summary>
        public void CreateRoom() => CreateRoom(DefaultRoomName, false);

        /// <summary>방 만들기 (로비 기획서 p7). 이름이 비면 기본 이름.</summary>
        public void CreateRoom(string roomName, bool isPrivate)
        {
            if (!CanStartSession) return;
            roomName = (roomName ?? string.Empty).Trim();
            if (roomName.Length == 0) roomName = DefaultRoomName;
            if (roomName.Length > RoomNameMaxLength) roomName = roomName.Substring(0, RoomNameMaxLength);
            _ = StartSessionSafe(GameMode.Shared, create: true, code: null, roomName, isPrivate);
        }

        /// <summary>코드로 참가. 공개방 클릭·(추후) 스팀 초대 수락도 이 함수 하나로 들어온다 (p12).</summary>
        public void JoinRoom(string input)
        {
            if (!CanStartSession) return;
            if (!VaroGO.Net.SessionCode.TryNormalize(input, out var code))
            {
                Fail("코드는 6자리예요 (0 O 1 I L 제외)");
                return;
            }
            _ = StartSessionSafe(GameMode.Shared, create: false, code, null, false);
        }

        /// <summary>방 리스트에서 공개방 참가. 세션 이름 = 방 코드.</summary>
        public void JoinPublic(string sessionName) => JoinRoom(sessionName);

        public void StartSolo()
        {
            if (!CanStartSession) return;
            _ = StartSessionSafe(GameMode.Single, create: true, code: null, null, false);
        }

        bool CanStartSession => State == SessionState.Idle || (State == SessionState.Browsing && ListReady);

        /// <summary>방 나가기 (대기실 나가기, 게임 중 나가기). 방 리스트로 돌아간다.</summary>
        public void Leave()
        {
            userLeaving = true;
            exitToTitle = false;
            if (Runner != null) _ = Runner.Shutdown();
            else ReturnToLobbyList();
        }

        /// <summary>방 리스트 "뒤로": 세션 로비 연결을 끊고 타이틀로.</summary>
        public void ExitToTitle()
        {
            userLeaving = true;
            exitToTitle = true;
            connectVersion++; // 진행 중인 목록 연결은 버린다
            if (Runner != null) _ = Runner.Shutdown();
            else GoToTitle();
        }

        /// <summary>방 리스트 연결(세션 로비). 이미 연결돼 있으면 아무것도 하지 않는다. 새로고침 버튼도 이걸 부른다.</summary>
        public void EnterLobbyList()
        {
            if (quitting) return;
            if (State == SessionState.Browsing) return;
            if (State != SessionState.Idle) return;
            _ = ConnectListSafe();
        }

        /// <summary>방 목록 다시 그리기 요청(새로고침 버튼). 목록 연결이 없으면 다시 연결한다.</summary>
        public void RefreshList()
        {
            if (State == SessionState.Browsing && ListReady) RoomListChanged?.Invoke();
            else EnterLobbyList();
        }

        /// <summary>대기실 팀 선택 (레드 = A, 블루 = B).</summary>
        public void RequestTeam(PlayerRole team)
        {
            if (State != SessionState.Lobby && State != SessionState.WaitingPartner) return;
            NetSession.Instance?.RequestTeam(team);
        }

        // ---- 알림 (씬 로드 후 UI가 꺼낸다) ----

        public bool TakePopup(out string title, out string body)
        {
            title = pendingPopupTitle;
            body = pendingPopupBody;
            pendingPopupTitle = pendingPopupBody = null;
            return body != null;
        }

        public string TakeToast()
        {
            var t = pendingToast;
            pendingToast = null;
            return t;
        }

        void Popup(string body, string title = "알림")
        {
            pendingPopupTitle = title;
            pendingPopupBody = body;
        }

        /// <summary>지금 보여 줄 로딩 문구. null이면 로딩 화면을 끈다.</summary>
        public string LoadingText
        {
            get
            {
                switch (State)
                {
                    case SessionState.Browsing: return ListReady ? null : "서버 연결 중...";
                    case SessionState.Connecting: return joining ? "방에 들어가는 중..." : "방을 만드는 중...";
                    case SessionState.LoadingCourse: return string.IsNullOrEmpty(Status) ? "로딩 중..." : Status;
                    default: return null;
                }
            }
        }

        // ---- 대기실 상태 (UI) ----

        /// <summary>P1 = 방장 슬롯, P2 = 참가자 슬롯.</summary>
        public RoomSlot GetSlot(int index)
        {
            var runner = Runner;
            var session = NetSession.Instance;
            if (runner == null || !runner.IsRunning) return default;

            var players = runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            PlayerRef host = session != null ? session.HostPlayer : PlayerRef.None;
            if (host == PlayerRef.None || !players.Contains(host))
                host = IsRoomHost ? runner.LocalPlayer : players.FirstOrDefault(p => p != runner.LocalPlayer);

            PlayerRef target = index == 0 ? host : players.FirstOrDefault(p => p != host);
            if (target == PlayerRef.None || !players.Contains(target)) return default;
            var team = session != null ? session.RoleOf(target) : PlayerRole.None;
            bool ready = session != null && session.IsReady(target);
            return new RoomSlot(true, target == runner.LocalPlayer, index == 0, team, ready);
        }

        public PlayerRole LocalTeam => NetSession.Instance != null && Runner != null ? NetSession.Instance.RoleOf(Runner.LocalPlayer) : PlayerRole.None;
        public bool LocalReady => NetSession.Instance != null && Runner != null && NetSession.Instance.IsReady(Runner.LocalPlayer);
        public int PlayerCount => Runner != null && Runner.IsRunning ? Runner.ActivePlayers.Count() : 0;

        // ---- ISessionLobby (S2-03, 로비 기획서로 의미 변경: 계약 v0.10 §3.7) ----

        public SessionPhase Phase
        {
            get
            {
                switch (State)
                {
                    case SessionState.Connecting: return SessionPhase.Connecting;
                    case SessionState.WaitingPartner: return SessionPhase.WaitingPartner;
                    case SessionState.Lobby: return SessionPhase.Lobby;
                    case SessionState.LoadingCourse: return SessionPhase.LoadingCourse;
                    case SessionState.InGame: return SessionPhase.InGame;
                    case SessionState.PartnerLost: return SessionPhase.PartnerLost;
                    default: return SessionPhase.Idle; // Idle, Browsing
                }
            }
        }

        public bool IsHost => Runner != null && Runner.IsRunning && (Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient);
        public string Code => SessionCode;
        public bool PartnerConnected => Runner != null && Runner.IsRunning && Runner.ActivePlayers.Count() >= MaxPlayers;
        /// <summary>상대가 준비했는지.</summary>
        public bool PartnerReady
        {
            get
            {
                var session = NetSession.Instance;
                if (session == null || Runner == null || !Runner.IsRunning) return false;
                var partner = Runner.ActivePlayers.FirstOrDefault(p => p != Runner.LocalPlayer);
                return partner != PlayerRef.None && session.IsReady(partner);
            }
        }
        // S2-04: 끊긴 뒤 경과 초. NetSession(상태 권한 = 남은 사람)이 멈춘 틱부터 센다
        public float PartnerLostSeconds => State == SessionState.PartnerLost && NetSession.Instance != null ? NetSession.Instance.LostSeconds : 0f;
        public event Action Changed;

        /// <summary>내 준비. 두 사람 모두 쓴다 (팀을 골라야 하고, 2명이어야 함).</summary>
        public void SetReady(bool ready)
        {
            if (State != SessionState.Lobby) return;
            NetSession.Instance?.RequestReady(ready);
        }

        /// <summary>방장만, 둘 다 팀 선택 + 준비일 때만 코스를 로드한다.</summary>
        public bool CanStartGame => State == SessionState.Lobby && IsHost && NetSession.Instance != null && NetSession.Instance.BothReady;

        public void StartGame()
        {
            if (!CanStartGame) return;
            if (!Runner.IsSceneAuthority)
            {
                Debug.LogWarning("[Net] StartGame: 씬 권한이 없어 코스를 로드할 수 없습니다");
                return;
            }
            LoadCourse();
        }

        void Update()
        {
            var session = NetSession.Instance;

            // S2-04·05: 파트너가 다시 들어와 NetSession이 시계를 재개하면 게임으로 돌아간다
            if (State == SessionState.PartnerLost && session != null && PartnerConnected && !session.Paused)
            {
                Debug.Log("[Net] 파트너 재접속 → InGame");
                State = SessionState.InGame;
                Status = string.Empty;
            }
            // 코스 로드 중에 끊겨 OnSceneLoadDone이 InGame으로 덮은 경우 등: NetSession이 멈춰 있으면 대기 화면.
            // 파트너가 이미 접속해 있으면(내가 재접속한 쪽, 재개 직전) 대기 화면을 띄우지 않는다
            else if (State == SessionState.InGame && session != null && session.Paused && !PartnerConnected)
            {
                State = SessionState.PartnerLost;
                Status = "파트너 재접속 대기 중...";
            }

            var snapshot = (Phase, Status, IsFailure, IsHost, PartnerConnected, PartnerReady);
            if (!snapshot.Equals(lastSnapshot))
            {
                lastSnapshot = snapshot;
                Changed?.Invoke();
            }
        }

        // ---- 방 리스트 (세션 로비) ----

        async Task ConnectListSafe()
        {
            try
            {
                await ConnectList();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (State != SessionState.Browsing) return; // 그동안 뒤로 가기 등으로 이미 정리됨
                var runner = Runner;
                try { await CleanupRunner(runner); }
                catch (Exception cleanupError) { Debug.LogException(cleanupError); }
                State = SessionState.Idle;
                ListReady = false;
                Popup("서버에 연결할 수 없어요");
            }
        }

        /// <summary>새 러너로 세션 로비에 접속한다. 성공하면 State = Browsing, ListReady = true.</summary>
        async Task<bool> ConnectList()
        {
            if (!ServerConfigLoader.TryLoad(out var config, out var configError))
            {
                Popup(configError);
                return false;
            }

            int version = ++connectVersion;
            State = SessionState.Browsing;
            ListReady = false;
            rooms.Clear();
            RoomListChanged?.Invoke();

            var runner = CreateRunner();
            var appSettings = ServerConfigLoader.BuildAppSettings(config);
            // 같은 러너로 이어서 StartGame 하면 StartGame의 앱 설정은 무시된다 → 여기서 넘긴다 (Photon 매치메이킹 문서)
            var result = await runner.JoinSessionLobby(SessionLobby.Shared, customAppSettings: appSettings);
            if (version != connectVersion || Runner != runner)
            {
                // 그동안 뒤로/다른 연결: 이 러너는 버린다
                if (Runner != runner && runner != null && runner.IsRunning) _ = runner.Shutdown();
                return false;
            }

            if (!result.Ok)
            {
                Debug.LogWarning($"[Net] 세션 로비 접속 실패: {result.ShutdownReason} {result.ErrorMessage}");
                await CleanupRunner(runner);
                State = SessionState.Idle;
                Popup(DescribeFailure(result.ShutdownReason, result.ErrorMessage));
                return false;
            }

            Debug.Log($"[Net] 세션 로비 접속 region='{appSettings.FixedRegion}'");
            ListReady = true;
            return true;
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner != Runner) return;
            rooms.Clear();
            string myVersion = Application.version;
            foreach (var s in sessionList)
            {
                if (s == null || !s.IsValid || !s.IsVisible) continue;
                var props = s.Properties;
                if (props != null && props.TryGetValue(PropPrivate, out var pv) && pv.Isbool && (bool)pv) continue;
                string name = props != null && props.TryGetValue(PropName, out var nm) && nm.IsString ? (string)nm : s.Name;
                bool inGame = props != null && props.TryGetValue(PropState, out var st) && st.IsInt && (int)st == 1;
                bool versionMatch = props != null && props.TryGetValue(PropVersion, out var ver) && ver.IsString && (string)ver == myVersion;
                rooms.Add(new RoomEntry(s.Name, name, s.PlayerCount, inGame, versionMatch));
            }
            RoomListChanged?.Invoke();
        }

        // ---- 세션 시작 ----

        // fire-and-forget 호출이라 예외가 그대로 버려진다. 여기서 받아 로그를 남기고 Idle로 되돌린다
        async Task StartSessionSafe(GameMode mode, bool create, string code, string roomName, bool isPrivate)
        {
            try
            {
                await StartSession(mode, create, code, roomName, isPrivate);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var runner = Runner;
                try { await CleanupRunner(runner); }
                catch (Exception cleanupError) { Debug.LogException(cleanupError); }
                Fail($"접속 오류: {e.GetType().Name} {e.Message}");
            }
        }

        async Task StartSession(GameMode mode, bool create, string code, string roomName, bool isPrivate)
        {
            if (!CanStartSession) return;

            if (!ServerConfigLoader.TryLoad(out var config, out var configError))
            {
                Fail(configError);
                return;
            }

            IsFailure = false;
            JoinError = string.Empty;
            failMessage = null;
            joining = !create;
            courseRequested = false;
            userLeaving = false;
            exitToTitle = mode == GameMode.Single; // Solo 테스트는 끝나면 타이틀로
            bool generateCode = create && mode == GameMode.Shared;

            int attempts = generateCode ? CodeRetryCount : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                // Shared: 세션 로비에 붙은 러너로 StartGame. 실패하면 러너가 닫히므로 시도마다 다시 붙는다
                if (mode == GameMode.Shared && (Runner == null || !ListReady))
                {
                    if (Runner != null) await CleanupRunner(Runner);
                    if (!await ConnectList())
                    {
                        Fail(pendingPopupBody ?? "서버에 연결할 수 없어요");
                        pendingPopupBody = null;
                        return;
                    }
                }
                else if (mode == GameMode.Single)
                {
                    if (Runner != null) await CleanupRunner(Runner);
                    CreateRunner();
                }

                State = SessionState.Connecting;
                Status = create ? "방을 만드는 중..." : "방에 들어가는 중...";
                ListReady = false;

                var runner = Runner;
                SessionCode = generateCode ? VaroGO.Net.SessionCode.Generate() : (code ?? string.Empty);
                IsRoomHost = create;

                // 공유 시계·서비스·보이스는 방에 들어갈 때만 (목록 러너에는 붙이지 않는다)
                RegisterServices();
                clock.Bind(runner);
                if (mode != GameMode.Single) NetVoiceChat.Attach(runner, config);

                Dictionary<string, SessionProperty> props = null;
                if (generateCode)
                {
                    props = new Dictionary<string, SessionProperty>
                    {
                        [PropName] = roomName ?? DefaultRoomName,
                        [PropPrivate] = isPrivate,
                        [PropVersion] = Application.version,
                        [PropState] = 0,
                    };
                }

                var appSettings = ServerConfigLoader.BuildAppSettings(config);
                Debug.Log($"[Net] StartGame 호출: mode={mode} session={(mode == GameMode.Single ? "-" : SessionCode)} create={create} private={isPrivate} name='{roomName}'");
                startingGame = true;
                var startTask = runner.StartGame(new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = mode == GameMode.Single ? null : SessionCode,
                    PlayerCount = MaxPlayers,
                    IsVisible = !isPrivate,
                    IsOpen = true,
                    EnableClientSessionCreation = create,
                    SessionProperties = props,
                    CustomPhotonAppSettings = appSettings,
                    SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                });

                if (await Task.WhenAny(startTask, Task.Delay(15000)) != startTask)
                    Debug.LogWarning("[Net] StartGame 응답 15초 지연 (Photon 응답 없음)");
                StartGameResult result;
                try { result = await startTask; }
                finally { startingGame = false; }
                Debug.Log($"[Net] StartGame 결과: ok={result.Ok} reason={result.ShutdownReason} {result.ErrorMessage}");

                if (result.Ok)
                {
                    OnStarted();
                    return;
                }

                // 코드가 겹치면 새 코드로 다시 시도
                bool duplicate = result.ShutdownReason == ShutdownReason.GameIdAlreadyExists;
                bool retry = generateCode && duplicate && attempt < attempts - 1;
                await CleanupRunner(runner);
                if (!retry)
                {
                    Fail(failMessage ?? DescribeFailure(result.ShutdownReason, result.ErrorMessage));
                    return;
                }
            }
        }

        void OnStarted()
        {
            // 역할은 NetPlayer가 스폰될 때 NetSession 팀 칸에서 받는다. 그 전까지는 None → Common만 보임
            if (Runner.GameMode == GameMode.Single)
            {
                LoadCourse();
                return;
            }

            // Shared 모드는 없는 코드로 들어가도 방이 새로 만들어질 수 있다(확인 필요).
            // 참가인데 혼자뿐이면 없는 코드로 본다.
            if (joining && Runner.ActivePlayers.Count() < 2)
            {
                failMessage = "존재하지 않는 방 코드예요";
                _ = Runner.Shutdown();
                return;
            }

            // 버전 확인 (p13). 코드로 들어온 비밀방은 목록에서 못 거르므로 여기서 막는다
            if (joining && ReadProp(PropVersion, null) != Application.version)
            {
                Debug.LogWarning($"[Net] 버전 다름: 방 {ReadProp(PropVersion, "?")} / 나 {Application.version}");
                failMessage = "게임 버전이 달라요";
                _ = Runner.Shutdown();
                return;
            }

            SpawnSessionObject();

            if (Runner.ActivePlayers.Count() >= MaxPlayers) EnterLobby();
            else EnterWaitingPartner();
        }

        void EnterWaitingPartner()
        {
            State = SessionState.WaitingPartner;
            Status = "파트너 대기 중...";
        }

        // 2명이 모이면 대기실에서 팀·준비·방장 시작을 기다린다
        void EnterLobby()
        {
            State = SessionState.Lobby;
            Status = string.Empty;
        }

        // 마스터만 스폰. 상태 권한은 항상 현재 마스터, 씬 전환에도 유지 (NetworkSpawnFlags 문서 확인함)
        void SpawnSessionObject()
        {
            if (Runner.GameMode != GameMode.Shared || !Runner.IsSharedModeMasterClient) return;
            if (NetSession.Instance != null) return;
            if (sessionPrefab == null)
            {
                Debug.LogError("[Net] sessionPrefab(NetSession)이 NetBootstrap에 연결되지 않았습니다");
                return;
            }
            Runner.Spawn(sessionPrefab, flags: NetworkSpawnFlags.DontDestroyOnLoad | NetworkSpawnFlags.SharedModeStateAuthMasterClient);
        }

        void LoadCourse()
        {
            if (courseRequested) return;
            int index = SceneUtility.GetBuildIndexByScenePath(CourseScenePath);
            if (index < 0)
            {
                failMessage = $"빌드 세팅에 코스 씬이 없습니다: {CourseScenePath}";
                _ = Runner.Shutdown();
                return;
            }
            courseRequested = true;
            State = SessionState.LoadingCourse;
            Status = "코스 로딩 중...";
            NetSession.Instance?.MarkInCourse(true); // S2-04: 이때부터 혼자 남으면 일시정지
            SetRoomState(1);
            Runner.LoadScene(SceneRef.FromIndex(index));
        }

        // 방 리스트의 "대기 중/게임 중". 마스터만 바꿀 수 있고, 생성 때 넣은 키만 갱신된다
        void SetRoomState(int value)
        {
            if (Runner == null || Runner.GameMode != GameMode.Shared || !Runner.IsSharedModeMasterClient) return;
            var info = Runner.SessionInfo;
            if (info == null || !info.IsValid) return;
            info.UpdateCustomProperties(new Dictionary<string, SessionProperty> { [PropState] = value });
        }

        string ReadProp(string key, string fallback)
        {
            var info = Runner != null ? Runner.SessionInfo : null;
            if (info == null || !info.IsValid || info.Properties == null) return fallback;
            return info.Properties.TryGetValue(key, out var p) && p.IsString ? (string)p : fallback;
        }

        bool ReadPropBool(string key)
        {
            var info = Runner != null ? Runner.SessionInfo : null;
            if (info == null || !info.IsValid || info.Properties == null) return false;
            return info.Properties.TryGetValue(key, out var p) && p.Isbool && (bool)p;
        }

        // ---- 서비스 / 러너 ----

        void RegisterServices()
        {
            role = new NetRoleProvider();
            clock = new NetGameClock();
            // 구역 리셋(S2-07)·밀기(S2-09)·핑(C-1101-3). Register보다 먼저 채워 Registered 구독자가 바로 쓸 수 있게 한다
            GameServices.RegisterSystems(null, null, new NetZoneReset(), new NetPlayerInteraction());
            GameServices.RegisterPing(new NetPingSystem());
            GameServices.Register(role, clock, isOnline: true);
            GameServices.RegisterStageLoader(this);
        }

        NetworkRunner CreateRunner()
        {
            var go = new GameObject("NetworkRunner");
            DontDestroyOnLoad(go);
            Runner = go.AddComponent<NetworkRunner>();
            go.AddComponent<NetworkSceneManagerDefault>();
            Runner.ProvideInput = false;
            Runner.AddCallbacks(this);
            return Runner;
        }

        async Task CleanupRunner(NetworkRunner runner)
        {
            if (Runner == runner) Runner = null;
            ListReady = false;
            clock?.Unbind();
            if (runner != null) await runner.Shutdown();
        }

        // ---- 종료 / 복귀 ----

        // 방 만들기·참가 실패. 방 리스트에 남아 오류를 보여 주고 목록을 다시 연결한다
        void Fail(string message)
        {
            State = SessionState.Idle;
            ListReady = false;
            ResetServices();
            IsFailure = true;
            Status = message;
            JoinError = message;
            Debug.LogWarning($"[Net] 접속 실패: {message}");
            if (SceneManager.GetActiveScene().path == LobbyScenePath) EnterLobbyList();
        }

        void ResetServices()
        {
            // OfflineBootstrap은 Role이 비어 있어야 새로 등록한다
            GameServices.Register(null, null, isOnline: false);
            // 핑은 null로 해제 가능(클라 OfflinePingSystem으로 돌아감). Interaction은 null 해제가 없어 남지만,
            // 로컬 플레이어가 없으면 아무것도 안 하고, 오프라인 코스 씬에선 OfflineBootstrap이 덮어쓴다
            GameServices.RegisterPing(null);
            if (GameServices.StageLoader == (IStageLoader)this) GameServices.RegisterStageLoader(null);
            role = null;
            clock = null;
        }

        void ResetSessionFields()
        {
            State = SessionState.Idle;
            ListReady = false;
            courseRequested = false;
            localPlayer = null;
            SessionCode = string.Empty;
            IsRoomHost = false;
            rooms.Clear();
            ResetServices();
        }

        // 방 종료(나가기·해산·끊김) → LobbyScene 방 리스트. 이미 LobbyScene이면 목록만 다시 연결
        void ReturnToLobbyList()
        {
            ResetSessionFields();
            if (SceneManager.GetActiveScene().path == LobbyScenePath)
            {
                EnterLobbyList();
                return;
            }
            int index = SceneUtility.GetBuildIndexByScenePath(LobbyScenePath);
            if (index < 0)
            {
                Debug.LogError($"[Net] 빌드 세팅에 로비 씬이 없습니다: {LobbyScenePath}");
                GoToTitle();
                return;
            }
            SceneManager.LoadScene(index); // 로드 후 OnUnitySceneLoaded가 목록을 연결한다
        }

        void GoToTitle()
        {
            ResetSessionFields();
            int index = SceneUtility.GetBuildIndexByScenePath(TitleScenePath);
            if (index < 0)
            {
                Debug.LogError($"[Net] 빌드 세팅에 타이틀 씬이 없습니다: {TitleScenePath}");
                return;
            }
            if (SceneManager.GetActiveScene().buildIndex != index) SceneManager.LoadScene(index);
        }

        static string DescribeFailure(ShutdownReason reason, string detail)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound: return "존재하지 않는 방 코드예요";
                case ShutdownReason.GameIsFull: return "방이 가득 찼어요";
                case ShutdownReason.GameIdAlreadyExists: return "이미 사용 중인 코드예요";
                case ShutdownReason.GameClosed: return "닫힌 방이에요";
                case ShutdownReason.InvalidRegion: return "리전 설정이 올바르지 않습니다";
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed: return "App ID가 올바르지 않습니다";
                case ShutdownReason.MaxCcuReached: return "서버 동시 접속 한도에 도달했습니다";
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout:
                case ShutdownReason.PhotonCloudTimeout: return "연결이 끊겼어요";
                default: return $"연결이 끊겼어요 ({reason} {detail})".Trim();
            }
        }

        static bool IsMenuScene(string path) => path == TitleScenePath || path == LobbyScenePath;

        // ---- INetworkRunnerCallbacks ----

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner != Runner) return;
            if (player != runner.LocalPlayer) Debug.Log($"[Net] 플레이어 들어옴: {player} (state={State})");
            if (State == SessionState.WaitingPartner && runner.ActivePlayers.Count() >= MaxPlayers)
                EnterLobby();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner != Runner) return;
            Debug.Log($"[Net] 플레이어 나감: {player} (state={State}, roomHost={IsRoomHost})");
            if (State == SessionState.Lobby || State == SessionState.WaitingPartner)
            {
                // 대기실에서 방장이 나가면 방 해산 (p13). 2인 고정이라 내가 방장이 아니면 나간 사람이 방장
                if (!IsRoomHost)
                {
                    failMessage = "방장이 방을 나갔어요";
                    _ = Runner.Shutdown();
                    return;
                }
                // 참가자가 나감 → 슬롯 비움, 준비 해제(NetSession 상태 권한), 토스트
                pendingToast = "상대가 나갔어요";
                EnterWaitingPartner();
            }
            // S2-04: 코스 중 끊김 → 대기 화면. 누가 나가든(마스터가 나가도) 남은 PC에서 같은 흐름.
            // 시계 정지·역할 칸 비우기·골 정리는 NetSession 상태 권한이 한다
            else if (State == SessionState.InGame || State == SessionState.LoadingCourse)
            {
                State = SessionState.PartnerLost;
                Status = "파트너 재접속 대기 중...";
            }
        }

        /// <summary>
        /// S2-05: 재접속 대기 60초가 지나면 NetSession(상태 권한)이 부른다.
        /// 방은 유지하고 남은 사람만 LobbyScene 대기실로 돌아간다. 같은 코드로 들어오면 대기실부터 다시.
        /// </summary>
        internal void ReturnToLobbyAfterTimeout()
        {
            if (Runner == null) return;
            if (!Runner.IsSceneAuthority)
            {
                Debug.LogWarning("[Net] 재접속 시간 초과: 씬 권한이 없어 로비로 돌아갈 수 없습니다");
                return;
            }
            int index = SceneUtility.GetBuildIndexByScenePath(LobbyScenePath);
            if (index < 0)
            {
                Debug.LogError($"[Net] 빌드 세팅에 로비 씬이 없습니다: {LobbyScenePath}");
                return;
            }
            if (localPlayer != null && localPlayer.IsValid) Runner.Despawn(localPlayer);
            localPlayer = null;
            courseRequested = false;
            IsRoomHost = true; // 남은 사람이 방장
            NetSession.Instance?.SetHostToSelf();
            SetRoomState(0);
            State = SessionState.LoadingCourse;
            Status = "로비로 돌아가는 중...";
            Runner.LoadScene(SceneRef.FromIndex(index));
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != Runner) return;
            if (IsMenuScene(SceneManager.GetActiveScene().path))
            {
                // S2-05: 재접속 시간 초과로 돌아온 경우. 방은 그대로, 대기실부터 (플레이어 스폰 없음)
                if (State == SessionState.LoadingCourse && !courseRequested)
                {
                    localPlayer = null;
                    if (Runner.ActivePlayers.Count() >= MaxPlayers) EnterLobby();
                    else EnterWaitingPartner();
                }
                return;
            }
            // 참가자는 방장이 연 씬을 따라가고, 스테이지 이동(LoadStage)으로 다른 씬이 열릴 수도 있다.
            // courseScenePath와 비교하면 그런 씬에서 스폰을 건너뛰므로 메뉴 씬이 아니면 코스 씬으로 본다
            // 상태 있는 오브젝트(스위치·골)가 Net Variant가 아니면 PC마다 따로 돈다. 온라인 테스트 첫 화면에서 바로 보이게 알린다
            var missing = NetSyncCheck.FindMissing(SceneManager.GetActiveScene());
            if (missing.Count > 0)
                Debug.LogError($"[Net] 온라인 동기화 안 되는 오브젝트 {missing.Count}개: {string.Join(", ", missing)} — {NetSyncCheck.FixHint}");
            State = SessionState.InGame;
            SpawnLocalPlayer();
        }

        // ---- 플레이어 스폰 (S-05) ----

        /// <summary>NetPlayer가 정한 로컬 역할을 NetRoleProvider에 반영한다.</summary>
        public void SetLocalRole(PlayerRole value) => role?.SetRole(value);

        // 각자 자기 플레이어만 스폰한다(Shared). 코스 씬은 건드리지 않고, 씬에 배치된 오프라인 Player 위치를 스폰 기준으로 쓴다
        void SpawnLocalPlayer()
        {
            if (localPlayer != null) return;
            if (playerPrefab == null)
            {
                Debug.LogError("[Net] playerPrefab(Player_Net)이 NetBootstrap에 연결되지 않았습니다");
                return;
            }

            var anchor = FindScenePlayerAnchor();
            Vector3 pos = Vector3.up;
            Quaternion rot = Quaternion.identity;
            if (anchor != null) anchor.GetPositionAndRotation(out pos, out rot);
            else Debug.LogWarning("[Net] 씬의 Player 위치를 찾지 못해 (0,1,0)에 스폰합니다");

            // A와 B가 겹치지 않게 좌우로 벌린다. 역할 칸(팀)이 있으면 그걸, 없으면 마스터 기준
            var myRole = NetSession.Instance != null ? NetSession.Instance.RoleOf(Runner.LocalPlayer) : PlayerRole.None;
            bool isA = myRole != PlayerRole.None ? myRole == PlayerRole.A_Red
                     : Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient;
            pos += rot * Vector3.right * (isA ? -SpawnSpacing : SpawnSpacing);

            localPlayer = Runner.Spawn(playerPrefab, pos, rot, Runner.LocalPlayer);
        }

        // 오프라인용 Player는 OfflineBootstrap이 비활성화해 두었다. 클라 타입을 직접 쓰지 않고 IPlayerSimulation으로 찾는다
        static Transform FindScenePlayerAnchor()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var sim in root.GetComponentsInChildren<IPlayerSimulation>(true))
                {
                    var behaviour = sim as MonoBehaviour;
                    if (behaviour != null && behaviour.GetComponent<NetworkObject>() == null)
                        return behaviour.transform;
                }
            }
            return null;
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (runner != Runner) return; // StartGame 실패로 직접 정리 중인 러너는 무시
            if (startingGame) return;     // StartGame 실패: StartSession이 정리·재시도·오류 표시를 한다
            Runner = null;
            if (quitting) return;
            clock?.Unbind();

            var prevState = State;
            bool wasConnecting = prevState == SessionState.Connecting;
            bool wasBrowsing = prevState == SessionState.Browsing;
            string message = failMessage;
            if (message == null && !userLeaving && shutdownReason != ShutdownReason.Ok)
                message = DescribeFailure(shutdownReason, null);
            if (message == null && !userLeaving && !wasBrowsing && prevState != SessionState.Idle)
                message = "연결이 끊겼어요";
            bool toTitle = exitToTitle;
            failMessage = null;
            userLeaving = false;
            exitToTitle = false;
            Debug.Log($"[Net] 러너 종료: reason={shutdownReason} state={prevState} → {(toTitle ? "타이틀" : "방 리스트")} msg={message}");

            if (toTitle)
            {
                GoToTitle();
                return;
            }

            if (message != null)
            {
                if (wasConnecting)
                {
                    // 참가 직후 실패(없는 방·버전 다름): 코드 참가 팝업 안 오류 문구
                    IsFailure = true;
                    Status = message;
                    JoinError = message;
                }
                else Popup(message);
            }
            ReturnToLobbyList();
        }

        // 스테이지 이동 (예: 스테이지 1 클리어 → PlayScene_Stage2). 씬 권한(방장)만 실제로 바꾸고 참가자는 따라간다 (C-1103-1)
        public void LoadStage(string scenePath)
        {
            if (Runner == null || !Runner.IsRunning || State != SessionState.InGame) return;
            if (!Runner.IsSceneAuthority) return;
            int index = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (index < 0)
            {
                Debug.LogWarning($"[Net] 빌드 세팅에 없는 씬이라 이동할 수 없습니다: {scenePath}");
                return;
            }
            Status = "다음 스테이지 로딩 중...";
            Runner.LoadScene(SceneRef.FromIndex(index));
        }

        // 씬이 바뀌면 내 플레이어를 정리하고, 로드가 끝나면 새 씬의 Player 위치에 다시 스폰한다 (C-1103-1)
        public void OnSceneLoadStart(NetworkRunner runner)
        {
            if (runner != Runner) return;
            if (localPlayer != null && localPlayer.IsValid) Runner.Despawn(localPlayer);
            localPlayer = null;
            // 참가자: 방장이 시작하면 씬 로드가 따라온다.
            // S2-05: 재접속한 PC는 이미 코스 중인 방에 들어오므로 대기실/대기 상태에서 씬 로드가 시작된다.
            // (재접속 시간 초과 후 대기실 씬을 다시 받는 경우는 OnSceneLoadDone에서 메뉴 씬으로 처리)
            if (State != SessionState.Lobby && State != SessionState.WaitingPartner) return;
            State = SessionState.LoadingCourse;
            Status = "코스 로딩 중...";
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
