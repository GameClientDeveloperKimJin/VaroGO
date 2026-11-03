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
    // Lobby: 2명 접속, 게스트 Ready·호스트 Start 대기 (S2-01). PartnerLost: 코스 중 파트너 끊김 (S2-04). 새 값은 맨 뒤에 추가
    public enum SessionState { Idle, Connecting, WaitingPartner, LoadingCourse, InGame, Lobby, PartnerLost }

    /// <summary>
    /// 00_Boot 씬에 하나 둔다. 세션 시작/참가/Solo, 코스 씬 로드, 종료 후 Boot 복귀(S-03, S-04).
    /// GameServices 등록은 StartGame 호출 전에 한다. 코스 씬 로드 전이면 되고(계약 §2),
    /// 접속 시점에 씬이 먼저 로드되는 경우를 막기 위해서다.
    /// ISessionLobby(계약 §3.7)를 직접 구현해 클라 BootMenuUI에 보여준다 (S2-03).
    /// 흐름: 방 만들기/참가 → WaitingPartner → (2명) Lobby → 게스트 Ready → 호스트 Start → 코스 로드
    /// </summary>
    public class NetBootstrap : MonoBehaviour, INetworkRunnerCallbacks, ISessionLobby, IStageLoader
    {
        const string BootScenePath = "Assets/01_Scenes/00_Boot.unity";
        const string DefaultCourseScenePath = "Assets/01_Scenes/11_SpiralCourse.unity";
        const int MaxPlayers = 2;
        const int CodeRetryCount = 3;

        const float SpawnSpacing = 0.75f;

        [Tooltip("02_Prefabs/Net/Player_Net")]
        [SerializeField] NetworkObject playerPrefab;

        [Tooltip("02_Prefabs/Net/NetSession — 세션 공용 상태(게스트 Ready). 마스터가 스폰한다")]
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

        NetRoleProvider role;
        NetGameClock clock;
        string failMessage;
        bool joining;
        bool courseRequested;
        NetworkObject localPlayer;

        // 게스트가 원하는 Ready 값. NetSession이 늦게 도착해도 맞춰질 때까지 다시 보낸다
        bool wantReady;
        float nextReadySendTime;
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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- UI가 부르는 진입점 ----

        public void CreateRoom() => _ = StartSessionSafe(GameMode.Shared, create: true, code: null);

        public void JoinRoom(string input)
        {
            if (State != SessionState.Idle) return;
            if (!VaroGO.Net.SessionCode.TryNormalize(input, out var code))
            {
                Fail("코드는 6자리입니다 (0 O 1 I L 제외)");
                return;
            }
            _ = StartSessionSafe(GameMode.Shared, create: false, code);
        }

        public void StartSolo() => _ = StartSessionSafe(GameMode.Single, create: true, code: null);

        public void Leave()
        {
            if (Runner != null) _ = Runner.Shutdown();
            else ReturnToBoot();
        }

        // ---- ISessionLobby (S2-03) ----

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
                    default: return SessionPhase.Idle;
                }
            }
        }

        public bool IsHost => Runner != null && (Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient);
        public string Code => SessionCode;
        public bool PartnerConnected => Runner != null && Runner.ActivePlayers.Count() >= MaxPlayers;
        // 호스트가 보는 "게스트 준비됨". 게스트 쪽에서는 의미 없음(false)
        public bool PartnerReady => IsHost && NetSession.Instance != null && NetSession.Instance.GuestReady;
        // S2-04: 끊긴 뒤 경과 초. NetSession(상태 권한 = 남은 사람)이 멈춘 틱부터 센다
        public float PartnerLostSeconds => State == SessionState.PartnerLost && NetSession.Instance != null ? NetSession.Instance.LostSeconds : 0f;
        public event Action Changed;

        /// <summary>게스트만. Lobby 단계에서만 의미가 있다.</summary>
        public void SetReady(bool ready)
        {
            if (State != SessionState.Lobby || IsHost) return;
            wantReady = ready;
            nextReadySendTime = 0f;
        }

        /// <summary>호스트만, 게스트가 Ready일 때만 코스를 로드한다 (S2-01).</summary>
        public void StartGame()
        {
            if (State != SessionState.Lobby || !IsHost || !PartnerReady) return;
            if (!Runner.IsSceneAuthority)
            {
                Debug.LogWarning("[Net] StartGame: 씬 권한이 없어 코스를 로드할 수 없습니다");
                return;
            }
            LoadCourse();
        }

        void Update()
        {
            // 게스트 Ready를 NetSession 값과 맞춘다 (NetSession이 아직 안 왔거나 RPC가 늦으면 0.5초마다 다시 보냄)
            var session = NetSession.Instance;
            if (State == SessionState.Lobby && !IsHost && session != null
                && session.GuestReady != wantReady && Time.unscaledTime >= nextReadySendTime)
            {
                session.RequestReady(wantReady);
                nextReadySendTime = Time.unscaledTime + 0.5f;
            }

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

            var snapshot = (Phase, Status, IsFailure, IsHost, PartnerConnected, PartnerReady);
            if (!snapshot.Equals(lastSnapshot))
            {
                lastSnapshot = snapshot;
                Changed?.Invoke();
            }
        }

        // ---- 세션 시작 ----

        // fire-and-forget 호출이라 예외가 그대로 버려진다. 여기서 받아 로그를 남기고 Idle로 되돌린다
        async Task StartSessionSafe(GameMode mode, bool create, string code)
        {
            try
            {
                await StartSession(mode, create, code);
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

        async Task StartSession(GameMode mode, bool create, string code)
        {
            if (State != SessionState.Idle) return;

            if (!ServerConfigLoader.TryLoad(out var config, out var configError))
            {
                Fail(configError);
                return;
            }

            State = SessionState.Connecting;
            IsFailure = false;
            Status = "연결 중...";
            failMessage = null;
            joining = !create;
            courseRequested = false;
            bool generateCode = create && mode == GameMode.Shared;

            RegisterServices();

            int attempts = generateCode ? CodeRetryCount : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                SessionCode = generateCode ? VaroGO.Net.SessionCode.Generate() : (code ?? string.Empty);
                CreateRunner(config, mode);

                var runner = Runner;
                var appSettings = ServerConfigLoader.BuildAppSettings(config);
                Debug.Log($"[Net] StartGame 호출: mode={mode} session={(mode == GameMode.Single ? "-" : SessionCode)} create={create} region='{appSettings.FixedRegion}' appId={(string.IsNullOrEmpty(appSettings.AppIdFusion) ? "없음" : "있음")} config={ServerConfigLoader.FilePath}");
                var startTask = runner.StartGame(new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = mode == GameMode.Single ? null : SessionCode,
                    PlayerCount = MaxPlayers,
                    IsVisible = false,
                    IsOpen = true,
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
            // 역할은 NetPlayer가 스폰될 때 [Networked]로 정해진다 (S-06). 그 전까지는 None → Common만 보임
            if (Runner.GameMode == GameMode.Single)
            {
                LoadCourse();
                return;
            }

            // Shared 모드는 없는 코드로 들어가도 방이 새로 만들어질 수 있다(확인 필요).
            // 참가인데 혼자뿐이면 없는 코드로 본다.
            if (joining && Runner.ActivePlayers.Count() < 2)
            {
                failMessage = "방을 찾을 수 없습니다. 코드를 확인하세요";
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
            wantReady = false;
        }

        // S2-01: 2명이 모이면 바로 코스로 가지 않고 Lobby에서 게스트 Ready·호스트 Start를 기다린다
        void EnterLobby()
        {
            State = SessionState.Lobby;
            Status = string.Empty;
            wantReady = false;
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
            State = SessionState.Idle;
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
            State = SessionState.Idle;
            courseRequested = false;
            localPlayer = null;
            SessionCode = string.Empty;
            wantReady = false;
            ResetServices();
            int index = SceneUtility.GetBuildIndexByScenePath(BootScenePath);
            if (index >= 0 && SceneManager.GetActiveScene().buildIndex != index) SceneManager.LoadScene(index);
        }

        static string DescribeFailure(ShutdownReason reason, string detail)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound: return "방을 찾을 수 없습니다. 코드를 확인하세요";
                case ShutdownReason.GameIsFull: return "방이 가득 찼습니다";
                case ShutdownReason.GameIdAlreadyExists: return "이미 사용 중인 코드입니다";
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
            // S2-02: 로비에서 파트너가 나가면 대기로 되돌리고 Ready를 내린다. 남은 사람이 호스트가 된다(마스터 승계)
            if (State == SessionState.Lobby)
            {
                NetSession.Instance?.ClearReady();
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
        /// 방은 유지하고 남은 사람만 00_Boot "파트너 대기"로 돌아간다. 같은 코드로 들어오면 로비부터 다시.
        /// </summary>
        internal void ReturnToLobbyAfterTimeout()
        {
            if (Runner == null) return;
            if (!Runner.IsSceneAuthority)
            {
                Debug.LogWarning("[Net] 재접속 시간 초과: 씬 권한이 없어 로비로 돌아갈 수 없습니다");
                return;
            }
            int index = SceneUtility.GetBuildIndexByScenePath(BootScenePath);
            if (index < 0)
            {
                Debug.LogError($"[Net] 빌드 세팅에 Boot 씬이 없습니다: {BootScenePath}");
                return;
            }
            if (localPlayer != null && localPlayer.IsValid) Runner.Despawn(localPlayer);
            localPlayer = null;
            courseRequested = false;
            State = SessionState.LoadingCourse;
            Status = "로비로 돌아가는 중...";
            Runner.LoadScene(SceneRef.FromIndex(index));
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != Runner) return;
            if (SceneManager.GetActiveScene().path == BootScenePath)
            {
                // S2-05: 재접속 시간 초과로 돌아온 경우. 방은 그대로, 파트너 대기부터
                if (State == SessionState.LoadingCourse && !courseRequested)
                {
                    localPlayer = null;
                    if (Runner.ActivePlayers.Count() >= MaxPlayers) EnterLobby();
                    else EnterWaitingPartner();
                }
                return;
            }
            // 참가자는 방장이 연 씬을 따라가고, 스테이지 이동(LoadStage)으로 다른 씬이 열릴 수도 있다.
            // courseScenePath와 비교하면 그런 씬에서 스폰을 건너뛰므로 Boot가 아닌 씬이면 코스 씬으로 본다
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

            // A와 B가 겹치지 않게 좌우로 벌린다. 역할 칸(S2-05)이 있으면 그걸, 없으면 마스터 기준
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
            // 게스트: 호스트가 Start하면 씬 로드가 따라온다.
            // S2-05: 재접속한 PC는 이미 코스 중인 방에 들어오므로 로비/대기 상태에서 씬 로드가 시작된다
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
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
