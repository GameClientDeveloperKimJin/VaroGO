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
    // Lobby: 2명이 01_Lobby에서 역할 선택·준비 중 (S2-11). PartnerLost: 코스 중 파트너 끊김 (S2-04).
    // RoomList: 00_Boot 대기실에서 방 목록을 받는 중 (S2-11a, Phase로는 Idle). 새 값은 맨 뒤에 추가 (R7)
    public enum SessionState { Idle, Connecting, WaitingPartner, LoadingCourse, InGame, Lobby, PartnerLost, RoomList }

    /// <summary>대기실 목록 한 줄 (S2-11a).</summary>
    public readonly struct RoomEntry
    {
        public readonly string Number;   // = SessionName
        public readonly string Name;     // 방 속성 name (없으면 번호)
        public readonly int PlayerCount;
        public readonly bool InGame;     // 방 속성 ingame == 1
        public readonly bool IsOpen;

        public RoomEntry(string number, string name, int playerCount, bool inGame, bool isOpen)
        {
            Number = number;
            Name = name;
            PlayerCount = playerCount;
            InGame = inGame;
            IsOpen = isOpen;
        }

        public bool IsFull => PlayerCount >= NetBootstrap.MaxPlayers;
        /// <summary>입장: 게임중 아님 + 자리 있음. 재접속: 게임중 + 자리 있음 (S2-05).</summary>
        public bool CanJoin => IsOpen && !IsFull;
    }

    /// <summary>
    /// 00_Boot 씬에 하나 둔다(DontDestroyOnLoad). 대기실 방 목록, 방 만들기/입장/Solo, 로비·코스 씬 로드, 종료 후 Boot 복귀.
    /// GameServices 등록은 StartGame 호출 전에 한다. 코스 씬 로드 전이면 되고(계약 §2),
    /// 접속 시점에 씬이 먼저 로드되는 경우를 막기 위해서다.
    /// ISessionLobby(계약 §3.7)를 직접 구현한다 (S2-03).
    /// 흐름 (S2-11): 00_Boot 대기실(방 목록) → 방 만들기/입장 → 01_Lobby 역할 선택·준비 → 둘 다 준비 → 11_SpiralCourse
    /// </summary>
    public class NetBootstrap : MonoBehaviour, INetworkRunnerCallbacks, ISessionLobby, IStageLoader
    {
        const string BootScenePath = "Assets/01_Scenes/00_Boot.unity";
        const string LobbyScenePath = "Assets/01_Scenes/01_Lobby.unity";
        const string DefaultCourseScenePath = "Assets/01_Scenes/11_SpiralCourse.unity";
        public const int MaxPlayers = 2;
        const int RoomNumberRetryCount = 5;
        const int FirstRoomNumber = 1000;   // 임시 번호 규칙 (S2-11 §0.3)
        public const int MaxRoomNameLength = 20;
        const string PropName = "name";
        const string PropInGame = "ingame";
        const float RoomListRetrySeconds = 5f;

        const float SpawnSpacing = 0.75f;

        [Tooltip("02_Prefabs/Net/Player_Net")]
        [SerializeField] NetworkObject playerPrefab;

        [Tooltip("02_Prefabs/Net/NetSession — 세션 공용 상태(역할 칸·준비). 마스터가 스폰한다")]
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
        /// <summary>방 번호 (= Photon 세션 이름).</summary>
        public string SessionCode { get; private set; } = string.Empty;
        public string RoomName { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public bool IsFailure { get; private set; }
        public NetworkRunner Runner { get; private set; }

        /// <summary>대기실 방 목록 (번호 순).</summary>
        public IReadOnlyList<RoomEntry> Rooms => rooms;

        NetRoleProvider role;
        NetGameClock clock;
        string failMessage;
        bool joining;
        bool courseRequested;
        NetworkObject localPlayer;

        // 대기실 전용 러너 (S2-11a). 방에 들어갈 때 종료하고 세션 러너를 새로 만든다 (러너는 종료 후 재사용 불가)
        NetworkRunner listRunner;
        bool listReady;
        float nextListTryTime;
        readonly List<RoomEntry> rooms = new List<RoomEntry>();

        // 로컬이 원하는 역할·준비. NetSession이 늦게 오거나 RPC가 늦으면 0.5초마다 다시 보낸다 (S2-11d)
        PlayerRole wantRole;
        bool wantReady;
        float nextRoleSendTime;
        float nextReadySendTime;
        // Changed 이벤트용: 지난 프레임에 UI가 보던 값
        (SessionPhase, string, bool, bool, bool, bool, PlayerRole, int) lastSnapshot;

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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- UI가 부르는 진입점 ----

        /// <summary>ISessionLobby. 이름 없이 만들면 이름 = 방 번호.</summary>
        public void CreateRoom() => CreateRoom(null);

        public void CreateRoom(string roomName)
        {
            if (!CanStartSession) return;
            _ = StartSessionSafe(GameMode.Shared, create: true, code: null, roomName);
        }

        /// <summary>방 번호로 입장 (게임중 방이면 재접속).</summary>
        public void JoinRoom(string number)
        {
            if (!CanStartSession) return;
            number = number?.Trim() ?? string.Empty;
            if (number.Length == 0)
            {
                Status = "방 번호가 비어 있습니다";
                IsFailure = true;
                return;
            }
            _ = StartSessionSafe(GameMode.Shared, create: false, number, null);
        }

        public void StartSolo()
        {
            if (!CanStartSession) return;
            _ = StartSessionSafe(GameMode.Single, create: true, code: null, null);
        }

        public void Leave()
        {
            if (Runner != null) _ = Runner.Shutdown();
            else if (State != SessionState.Idle && State != SessionState.RoomList) ReturnToBoot();
        }

        bool CanStartSession => State == SessionState.Idle || State == SessionState.RoomList;

        // ---- ISessionLobby (S2-03, S2-11 §3.1) ----

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
                    default: return SessionPhase.Idle; // Idle, RoomList
                }
            }
        }

        public bool IsHost => Runner != null && (Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient);
        public string Code => SessionCode;
        public bool PartnerConnected => Runner != null && Runner.ActivePlayers.Count() >= MaxPlayers;
        /// <summary>S2-11: 상대가 가진 역할 칸이 준비했는지 (양쪽에서 의미 있음).</summary>
        public bool PartnerReady => PartnerRole != PlayerRole.None && NetSession.Instance.IsReady(PartnerRole);
        // S2-04: 끊긴 뒤 경과 초. NetSession(상태 권한 = 남은 사람)이 멈춘 틱부터 센다
        public float PartnerLostSeconds => State == SessionState.PartnerLost && NetSession.Instance != null ? NetSession.Instance.LostSeconds : 0f;
        public event Action Changed;

        /// <summary>내 역할 칸 (S2-11d). 아직 없으면 None.</summary>
        public PlayerRole LocalSlot => Runner != null && NetSession.Instance != null ? NetSession.Instance.RoleOf(Runner.LocalPlayer) : PlayerRole.None;

        /// <summary>상대가 가진 역할 칸. 상대가 아직 고르지 않았으면 None.</summary>
        public PlayerRole PartnerRole
        {
            get
            {
                var session = NetSession.Instance;
                if (Runner == null || session == null) return PlayerRole.None;
                foreach (var r in new[] { PlayerRole.A_Red, PlayerRole.B_Blue })
                {
                    var owner = session.OwnerOf(r);
                    if (owner != PlayerRef.None && owner != Runner.LocalPlayer) return r;
                }
                return PlayerRole.None;
            }
        }

        /// <summary>내 칸의 준비 상태.</summary>
        public bool LocalReady => LocalSlot != PlayerRole.None && NetSession.Instance.IsReady(LocalSlot);

        /// <summary>S2-11d: 역할 칸 선택 (None = 취소). 역할을 바꾸면 준비가 풀린다. 로비 단계에서만.</summary>
        public void SelectRole(PlayerRole value)
        {
            if (State != SessionState.Lobby && State != SessionState.WaitingPartner) return;
            if (value == wantRole && value == LocalSlot) return;
            wantRole = value;
            wantReady = false;
            nextRoleSendTime = 0f;
        }

        /// <summary>S2-11: 두 사람 모두. 역할 칸을 가진 경우에만 의미가 있다.</summary>
        public void SetReady(bool ready)
        {
            if (State != SessionState.Lobby && State != SessionState.WaitingPartner) return;
            if (ready && LocalSlot == PlayerRole.None) return;
            wantReady = ready;
            nextReadySendTime = 0f;
        }

        /// <summary>S2-11e: 둘 다 준비하면 자동으로 시작하므로 아무것도 하지 않는다.</summary>
        public void StartGame()
        {
            Debug.Log("[Net] StartGame 무시: 두 사람이 모두 준비하면 자동으로 시작합니다 (S2-11)");
        }

        void Update()
        {
            UpdateRoomList();

            var session = NetSession.Instance;
            bool inRoomLobby = State == SessionState.Lobby || State == SessionState.WaitingPartner;
            if (inRoomLobby && session != null && Runner != null)
                SyncRoleAndReady(session);

            // S2-04·05: 파트너가 다시 들어와 NetSession이 시계를 재개하면 게임으로 돌아간다
            if (State == SessionState.PartnerLost && session != null && PartnerConnected && !session.Paused)
            {
                Debug.Log("[Net] 파트너 재접속 → InGame");
                State = SessionState.InGame;
                Status = string.Empty;
            }
            // 코스 로드 중에 끊겨 OnSceneLoadDone이 InGame으로 덮은 경우 등: NetSession이 멈춰 있으면 대기 화면
            else if (State == SessionState.InGame && session != null && session.Paused)
            {
                State = SessionState.PartnerLost;
                Status = "파트너 재접속 대기 중...";
            }

            int readyBits = session == null ? 0 : (session.IsReady(PlayerRole.A_Red) ? 1 : 0) | (session.IsReady(PlayerRole.B_Blue) ? 2 : 0);
            var snapshot = (Phase, Status, IsFailure, IsHost, PartnerConnected, PartnerReady, LocalSlot, readyBits);
            if (!snapshot.Equals(lastSnapshot))
            {
                lastSnapshot = snapshot;
                Changed?.Invoke();
            }
        }

        // 원하는 역할·준비를 NetSession 실제 값과 맞춘다 (S2-11 §3.1)
        void SyncRoleAndReady(NetSession session)
        {
            var actual = session.RoleOf(Runner.LocalPlayer);
            if (wantRole != actual)
            {
                // 원하는 칸을 이미 상대가 가졌으면(거절됨) 요청을 접는다. 계속 보내면 거절 로그만 쌓인다
                var owner = session.OwnerOf(wantRole);
                if (wantRole != PlayerRole.None && owner != PlayerRef.None && owner != Runner.LocalPlayer)
                {
                    wantRole = actual;
                    wantReady = false;
                }
                else if (Time.unscaledTime >= nextRoleSendTime)
                {
                    session.RequestRole(wantRole);
                    nextRoleSendTime = Time.unscaledTime + 0.5f;
                }
                return; // 역할이 맞춰진 뒤에 준비를 보낸다
            }

            if (actual == PlayerRole.None) wantReady = false;
            if (actual != PlayerRole.None && session.IsReady(actual) != wantReady && Time.unscaledTime >= nextReadySendTime)
            {
                session.RequestReady(wantReady);
                nextReadySendTime = Time.unscaledTime + 0.5f;
            }
        }

        // ---- 대기실 방 목록 (S2-11a) ----

        void UpdateRoomList()
        {
            if (!CanStartSession) return;
            if (listRunner == null)
            {
                if (Time.unscaledTime >= nextListTryTime) _ = EnterRoomListSafe();
            }
            else if (listReady && State == SessionState.Idle)
            {
                State = SessionState.RoomList;
            }
        }

        async Task EnterRoomListSafe()
        {
            try
            {
                await EnterRoomList();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var runner = listRunner;
                listRunner = null;
                listReady = false;
                nextListTryTime = Time.unscaledTime + RoomListRetrySeconds;
                if (runner != null)
                {
                    try { await runner.Shutdown(); }
                    catch (Exception cleanupError) { Debug.LogException(cleanupError); }
                }
            }
        }

        async Task EnterRoomList()
        {
            if (listRunner != null) return;
            nextListTryTime = Time.unscaledTime + RoomListRetrySeconds;
            if (!ServerConfigLoader.TryLoad(out var config, out var configError))
            {
                if (!IsFailure)
                {
                    Status = configError;
                    IsFailure = true;
                }
                return;
            }

            var go = new GameObject("NetworkRunner_RoomList");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            runner.AddCallbacks(this);
            listRunner = runner;
            listReady = false;
            rooms.Clear();

            Debug.Log("[Net] 대기실 접속 (JoinSessionLobby Shared)");
            var result = await runner.JoinSessionLobby(SessionLobby.Shared, customAppSettings: ServerConfigLoader.BuildAppSettings(config));

            if (listRunner != runner) return; // 기다리는 동안 방에 들어가며 닫혔다 (CloseRoomList가 Shutdown함)
            if (!result.Ok)
            {
                Debug.LogWarning($"[Net] 대기실 접속 실패: {result.ShutdownReason} {result.ErrorMessage}");
                listRunner = null;
                nextListTryTime = Time.unscaledTime + RoomListRetrySeconds;
                await runner.Shutdown();
                if (CanStartSession)
                {
                    Status = $"대기실 접속 실패: {DescribeFailure(result.ShutdownReason, result.ErrorMessage)}";
                    IsFailure = true;
                }
                return;
            }

            listReady = true;
            if (State == SessionState.Idle) State = SessionState.RoomList;
            Debug.Log("[Net] 대기실 접속 완료");
        }

        async Task CloseRoomList()
        {
            var runner = listRunner;
            listRunner = null;
            listReady = false;
            rooms.Clear();
            if (runner != null) await runner.Shutdown();
        }

        // 임시 번호 규칙: 목록에 보이는 숫자 번호 중 가장 큰 값 + 1, 없으면 1000 (S2-11 §0.3). 방식을 바꾸면 이 함수만 고친다
        int NextRoomNumber()
        {
            int max = FirstRoomNumber - 1;
            foreach (var room in rooms)
                if (int.TryParse(room.Number, out var n) && n > max) max = n;
            return max + 1;
        }

        static RoomEntry ToEntry(SessionInfo info)
        {
            string name = info.Name;
            bool inGame = false;
            var props = info.Properties;
            if (props != null)
            {
                if (props.TryGetValue(PropName, out var p) && p.IsString && !string.IsNullOrEmpty((string)p)) name = (string)p;
                if (props.TryGetValue(PropInGame, out var g) && g.IsInt) inGame = (int)g == 1;
            }
            return new RoomEntry(info.Name, name, info.PlayerCount, inGame, info.IsOpen);
        }

        static int CompareRooms(RoomEntry a, RoomEntry b)
        {
            bool na = int.TryParse(a.Number, out var ia), nb = int.TryParse(b.Number, out var ib);
            if (na && nb) return ia.CompareTo(ib);
            if (na != nb) return na ? -1 : 1;
            return string.CompareOrdinal(a.Number, b.Number);
        }

        // ---- 세션 시작 ----

        // fire-and-forget 호출이라 예외가 그대로 버려진다. 여기서 받아 로그를 남기고 Idle로 되돌린다
        async Task StartSessionSafe(GameMode mode, bool create, string code, string roomName)
        {
            try
            {
                await StartSession(mode, create, code, roomName);
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

        async Task StartSession(GameMode mode, bool create, string code, string roomName)
        {
            if (!CanStartSession) return;

            if (!ServerConfigLoader.TryLoad(out var config, out var configError))
            {
                Fail(configError);
                return;
            }

            bool generateNumber = create && mode == GameMode.Shared;
            // 번호는 대기실 목록 기준. 러너를 닫기 전에 정한다
            int firstNumber = generateNumber ? NextRoomNumber() : 0;
            string customName = string.IsNullOrWhiteSpace(roomName) ? null : roomName.Trim();
            if (customName != null && customName.Length > MaxRoomNameLength) customName = customName.Substring(0, MaxRoomNameLength);

            State = SessionState.Connecting;
            IsFailure = false;
            Status = "연결 중...";
            failMessage = null;
            joining = !create;
            courseRequested = false;
            wantRole = PlayerRole.None;
            wantReady = false;
            RoomName = string.Empty;

            await CloseRoomList();
            RegisterServices();

            int attempts = generateNumber ? RoomNumberRetryCount : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                SessionCode = generateNumber ? (firstNumber + attempt).ToString() : (code ?? string.Empty);
                string nameForRoom = customName ?? SessionCode;
                CreateRunner(config, mode);

                Dictionary<string, SessionProperty> props = null;
                if (generateNumber)
                {
                    props = new Dictionary<string, SessionProperty> { { PropName, nameForRoom }, { PropInGame, 0 } };
                    Debug.Log($"[Net] 방 생성 번호={SessionCode} 이름={nameForRoom}");
                }

                var runner = Runner;
                var appSettings = ServerConfigLoader.BuildAppSettings(config);
                Debug.Log($"[Net] StartGame 호출: mode={mode} session={(mode == GameMode.Single ? "-" : SessionCode)} create={create} region='{appSettings.FixedRegion}' appId={(string.IsNullOrEmpty(appSettings.AppIdFusion) ? "없음" : "있음")} config={ServerConfigLoader.FilePath}");
                var startTask = runner.StartGame(new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = mode == GameMode.Single ? null : SessionCode,
                    PlayerCount = MaxPlayers,
                    IsVisible = mode == GameMode.Shared, // S2-11a: 대기실 목록에 보이게
                    IsOpen = true,
                    SessionProperties = props,
                    EnableClientSessionCreation = create,
                    CustomPhotonAppSettings = appSettings,
                    SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                });

                if (await Task.WhenAny(startTask, Task.Delay(15000)) != startTask)
                    Debug.LogWarning("[Net] StartGame 응답 15초 지연 (Photon 응답 없음)");
                var result = await startTask;
                Debug.Log($"[Net] StartGame 결과: ok={result.Ok} reason={result.ShutdownReason} {result.ErrorMessage}");

                if (result.Ok)
                {
                    OnStarted();
                    return;
                }

                // 번호가 겹치면 +1 해서 다시 시도 (S2-11b)
                bool duplicate = result.ShutdownReason == ShutdownReason.GameIdAlreadyExists;
                bool retry = generateNumber && duplicate && attempt < attempts - 1;
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
            // 역할은 NetPlayer가 스폰될 때 [Networked]로 정해진다 (S-06). 그 전까지는 None → Common만 보임
            if (Runner.GameMode == GameMode.Single)
            {
                LoadCourse();
                return;
            }

            // 참가인데 혼자뿐이면 없는 방으로 본다 (목록이 늦게 갱신돼 이미 사라진 방 등)
            if (joining && Runner.ActivePlayers.Count() < 2)
            {
                failMessage = "방을 찾을 수 없습니다. 목록을 다시 확인하세요";
                _ = Runner.Shutdown();
                return;
            }

            RoomName = ReadRoomName();
            SpawnSessionObject();

            // S2-11f: 게임중인 방에 들어왔으면 재접속. 마스터가 이미 코스에 있으니 Fusion이 코스 씬을 따라 연다 (S2-05)
            if (ReadInGame())
            {
                Debug.Log($"[Net] 게임중 방 {SessionCode} 참가 → 재접속");
                State = SessionState.LoadingCourse;
                Status = "코스 로딩 중... (재접속)";
                return;
            }

            if (Runner.ActivePlayers.Count() >= MaxPlayers) EnterLobby();
            else EnterWaitingPartner();

            // S2-11b: 방을 만든 마스터가 01_Lobby를 연다. 들어온 사람은 Fusion이 같은 씬을 따라 연다
            if (Runner.IsSharedModeMasterClient && SceneManager.GetActiveScene().path != LobbyScenePath)
                LoadLobbyScene();
        }

        string ReadRoomName()
        {
            var info = Runner.SessionInfo;
            if (info && info.Properties != null && info.Properties.TryGetValue(PropName, out var p) && p.IsString && !string.IsNullOrEmpty((string)p))
                return (string)p;
            return SessionCode;
        }

        bool ReadInGame()
        {
            var info = Runner.SessionInfo;
            return info && info.Properties != null && info.Properties.TryGetValue(PropInGame, out var p) && p.IsInt && (int)p == 1;
        }

        // 방 속성 ingame 갱신 (S2-11e·f). name도 같이 보낸다 (교체인지 병합인지 확인 전이라 둘 다 유지)
        void SetRoomInGame(bool value)
        {
            if (Runner == null || Runner.GameMode != GameMode.Shared) return;
            var info = Runner.SessionInfo;
            if (!info) return;
            info.UpdateCustomProperties(new Dictionary<string, SessionProperty>
            {
                { PropName, string.IsNullOrEmpty(RoomName) ? SessionCode : RoomName },
                { PropInGame, value ? 1 : 0 },
            });
            Debug.Log($"[Net] 방 속성 ingame={(value ? 1 : 0)} (master={Runner.IsSharedModeMasterClient})");
        }

        void EnterWaitingPartner()
        {
            State = SessionState.WaitingPartner;
            Status = "파트너 대기 중...";
        }

        // S2-11: 2명이 모이면 01_Lobby에서 역할 선택·준비를 기다린다
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

        void LoadLobbyScene()
        {
            if (!Runner.IsSceneAuthority)
            {
                Debug.LogWarning("[Net] 로비 씬 로드: 씬 권한이 없습니다");
                return;
            }
            int index = SceneUtility.GetBuildIndexByScenePath(LobbyScenePath);
            if (index < 0)
            {
                failMessage = $"빌드 세팅에 로비 씬이 없습니다: {LobbyScenePath}";
                _ = Runner.Shutdown();
                return;
            }
            Runner.LoadScene(SceneRef.FromIndex(index));
        }

        /// <summary>S2-11e: NetSession(상태 권한)이 두 사람 모두 준비했을 때 1회 부른다.</summary>
        internal void StartCourseFromRoom()
        {
            if (Runner == null || (State != SessionState.Lobby && State != SessionState.WaitingPartner)) return;
            if (!Runner.IsSceneAuthority)
            {
                Debug.LogWarning("[Net] 둘 다 준비: 씬 권한이 없어 코스를 로드할 수 없습니다");
                return;
            }
            Debug.Log("[Net] 둘 다 준비 → 코스");
            LoadCourse();
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
            SetRoomInGame(true);
            Runner.LoadScene(SceneRef.FromIndex(index));
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

        void CreateRunner(ServerConfigData config, GameMode mode)
        {
            var go = new GameObject("NetworkRunner");
            DontDestroyOnLoad(go);
            Runner = go.AddComponent<NetworkRunner>();
            go.AddComponent<NetworkSceneManagerDefault>();
            Runner.ProvideInput = false;
            Runner.AddCallbacks(this);
            clock.Bind(Runner);
            // 인게임 보이스 (Solo는 상대가 없으니 붙이지 않음). Start 전에 붙여야 러너가 콜백을 자동 등록한다
            if (mode != GameMode.Single) NetVoiceChat.Attach(Runner, config);
        }

        async Task CleanupRunner(NetworkRunner runner)
        {
            if (Runner == runner) Runner = null;
            clock?.Unbind();
            if (runner != null) await runner.Shutdown();
        }

        // ---- 종료 / 복귀 ----

        void Fail(string message)
        {
            State = SessionState.Idle; // Update가 대기실에 다시 접속한다
            ResetServices();
            IsFailure = true;
            Status = message;
            Debug.LogWarning($"[Net] 접속 실패: {message}");
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

        void ReturnToBoot()
        {
            State = SessionState.Idle; // Update가 대기실에 다시 접속한다 (S2-11a)
            Status = string.Empty;
            IsFailure = false;
            courseRequested = false;
            localPlayer = null;
            SessionCode = string.Empty;
            RoomName = string.Empty;
            wantRole = PlayerRole.None;
            wantReady = false;
            nextListTryTime = 0f;
            ResetServices();
            int index = SceneUtility.GetBuildIndexByScenePath(BootScenePath);
            if (index >= 0 && SceneManager.GetActiveScene().buildIndex != index) SceneManager.LoadScene(index);
        }

        static string DescribeFailure(ShutdownReason reason, string detail)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound: return "방을 찾을 수 없습니다. 목록을 다시 확인하세요";
                case ShutdownReason.GameIsFull: return "방이 가득 찼습니다";
                case ShutdownReason.GameIdAlreadyExists: return "이미 사용 중인 방 번호입니다";
                case ShutdownReason.GameClosed: return "닫힌 방입니다";
                case ShutdownReason.InvalidRegion: return "리전 설정이 올바르지 않습니다";
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed: return "App ID가 올바르지 않습니다";
                case ShutdownReason.MaxCcuReached: return "서버 동시 접속 한도에 도달했습니다";
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout:
                case ShutdownReason.PhotonCloudTimeout: return "네트워크 연결 시간이 초과되었습니다";
                default: return $"접속 실패: {reason} {detail}".Trim();
            }
        }

        // ---- INetworkRunnerCallbacks ----

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner != Runner) return;
            if (State == SessionState.WaitingPartner && runner.ActivePlayers.Count() >= MaxPlayers)
                EnterLobby();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner != Runner) return;
            Debug.Log($"[Net] 플레이어 나감: {player} (state={State})");
            // S2-02·S2-11d: 로비에서 파트너가 나가면 대기로. 나간 사람의 칸·준비는 NetSession이 비운다. 남은 사람이 방장(마스터 승계)
            if (State == SessionState.Lobby)
            {
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
        /// S2-05·S2-11f: 재접속 대기 60초가 지나면 NetSession(상태 권한)이 부른다.
        /// 방은 유지하고 남은 사람은 01_Lobby로 돌아간다. 방 속성 ingame=0 → 대기실 목록에 [입장]으로 보인다.
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
            wantReady = false;
            State = SessionState.LoadingCourse;
            Status = "로비로 돌아가는 중...";
            SetRoomInGame(false);
            Runner.LoadScene(SceneRef.FromIndex(index));
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != Runner) return;
            var path = SceneManager.GetActiveScene().path;
            if (path == LobbyScenePath)
            {
                // S2-11f: 재접속 시간 초과로 돌아온 경우. 방은 그대로, 역할 선택부터
                if (State == SessionState.LoadingCourse && !courseRequested)
                {
                    localPlayer = null;
                    // 코스에서 쓰던 칸을 그대로 둔다 (재접속으로 자동 배정된 칸이면 wantRole이 None이라 지워질 수 있음)
                    wantRole = LocalSlot;
                    wantReady = false;
                    if (Runner.ActivePlayers.Count() >= MaxPlayers) EnterLobby();
                    else EnterWaitingPartner();
                }
                return;
            }
            // 참가자는 방장이 연 씬을 따라가고, 스테이지 이동(LoadStage)으로 다른 씬이 열릴 수도 있다.
            // courseScenePath와 비교하면 그런 씬에서 스폰을 건너뛰므로 Boot·Lobby가 아닌 씬이면 코스 씬으로 본다 (Lobby는 위에서 처리)
            if (path == BootScenePath) return;
            // 상태 있는 오브젝트(스위치·골)가 Net Variant가 아니면 PC마다 따로 돈다. 온라인 테스트 첫 화면에서 바로 보이게 알린다
            var missing = NetSyncCheck.FindMissing(SceneManager.GetActiveScene());
            if (missing.Count > 0)
                Debug.LogError($"[Net] 온라인 동기화 안 되는 오브젝트 {missing.Count}개: {string.Join(", ", missing)} — {NetSyncCheck.FixHint}");
            State = SessionState.InGame;
            Status = string.Empty;
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

            // A와 B가 겹치지 않게 좌우로 벌린다. 역할 칸(S2-05·S2-11d)이 있으면 그걸, 없으면 마스터 기준
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
            // 대기실 러너가 스스로 끊겼으면 잠시 뒤 다시 접속한다
            if (runner == listRunner)
            {
                Debug.LogWarning($"[Net] 대기실 연결 종료: {shutdownReason}");
                listRunner = null;
                listReady = false;
                rooms.Clear();
                nextListTryTime = Time.unscaledTime + RoomListRetrySeconds;
                if (State == SessionState.RoomList) State = SessionState.Idle;
                return;
            }
            if (runner != Runner) return; // StartGame 실패로 직접 정리 중인 러너는 무시
            Runner = null;
            clock?.Unbind();

            bool wasInGame = State == SessionState.InGame;
            string message = failMessage;
            if (message == null && shutdownReason != ShutdownReason.Ok)
                message = DescribeFailure(shutdownReason, null);
            if (message == null && wasInGame) message = "연결이 종료되었습니다";

            ReturnToBoot();
            if (message != null)
            {
                IsFailure = true;
                Status = message;
            }
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
            // 게스트: 둘 다 준비하면 마스터가 코스를 열고, 씬 로드가 따라온다.
            // 01_Lobby에 있다가 시작되는 로드만 코스로 본다 (입장 직후 01_Lobby를 따라 여는 로드와 구분).
            // 재접속(S2-05)은 OnStarted에서 이미 LoadingCourse로 바꿔 둔다

            if (State != SessionState.Lobby && State != SessionState.WaitingPartner) return;
            if (SceneManager.GetActiveScene().path != LobbyScenePath) return;
            State = SessionState.LoadingCourse;
            Status = "코스 로딩 중...";
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner != listRunner) return;
            rooms.Clear();
            foreach (var info in sessionList)
                if (info) rooms.Add(ToEntry(info));
            rooms.Sort(CompareRooms);
            Debug.Log($"[Net] 방 목록 {rooms.Count}개" + (rooms.Count > 0 ? ": " + string.Join(", ", rooms.Select(r => $"{r.Number}({r.PlayerCount}/{MaxPlayers}{(r.InGame ? " 게임중" : "")})")) : ""));
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
