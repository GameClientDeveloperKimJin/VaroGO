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
    public enum SessionState { Idle, Connecting, WaitingPartner, LoadingCourse, InGame }

    /// <summary>
    /// 00_Boot 씬에 하나 둔다. 세션 시작/참가/Solo, 코스 씬 로드, 종료 후 Boot 복귀(S-03, S-04).
    /// GameServices 등록은 StartGame 호출 전에 한다. 코스 씬 로드 전이면 되고(계약 §2),
    /// 접속 시점에 씬이 먼저 로드되는 경우를 막기 위해서다.
    /// </summary>
    public class NetBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        const string BootScenePath = "Assets/01_Scenes/00_Boot.unity";
        const string CourseScenePath = "Assets/01_Scenes/11_SpiralCourse.unity";
        const int MaxPlayers = 2;
        const int CodeRetryCount = 3;

        const float SpawnSpacing = 0.75f;

        [Tooltip("02_Prefabs/Net/Player_Net")]
        [SerializeField] NetworkObject playerPrefab;

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

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- UI가 부르는 진입점 ----

        public void CreateRoom() => _ = StartSession(GameMode.Shared, create: true, code: null);

        public void JoinRoom(string input)
        {
            if (State != SessionState.Idle) return;
            if (!VaroGO.Net.SessionCode.TryNormalize(input, out var code))
            {
                Fail("코드는 6자리입니다 (0 O 1 I L 제외)");
                return;
            }
            _ = StartSession(GameMode.Shared, create: false, code);
        }

        public void StartSolo() => _ = StartSession(GameMode.Single, create: true, code: null);

        public void Leave()
        {
            if (Runner != null) _ = Runner.Shutdown();
            else ReturnToBoot();
        }

        // ---- 세션 시작 ----

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
                CreateRunner();

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

            if (Runner.ActivePlayers.Count() >= MaxPlayers) OnPartnerReady();
            else
            {
                State = SessionState.WaitingPartner;
                Status = "파트너 대기 중...";
            }
        }

        void OnPartnerReady()
        {
            Status = "코스 로딩 중...";
            if (Runner.IsSceneAuthority) LoadCourse();
            else State = SessionState.LoadingCourse; // Master가 씬을 로드하면 따라간다 (확인 필요)
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
            Runner.LoadScene(SceneRef.FromIndex(index));
        }

        // ---- 서비스 / 러너 ----

        void RegisterServices()
        {
            role = new NetRoleProvider();
            clock = new NetGameClock();
            GameServices.Register(role, clock, isOnline: true);
        }

        void CreateRunner()
        {
            var go = new GameObject("NetworkRunner");
            DontDestroyOnLoad(go);
            Runner = go.AddComponent<NetworkRunner>();
            go.AddComponent<NetworkSceneManagerDefault>();
            Runner.ProvideInput = false;
            Runner.AddCallbacks(this);
            clock.Bind(Runner);
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
            role = null;
            clock = null;
        }

        void ReturnToBoot()
        {
            State = SessionState.Idle;
            courseRequested = false;
            localPlayer = null;
            SessionCode = string.Empty;
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
                OnPartnerReady();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            // 연결 끊김 상세 처리는 S-12. 지금은 로그만
            if (runner == Runner) Debug.Log($"[Net] 플레이어 나감: {player}");
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != Runner) return;
            if (SceneManager.GetActiveScene().path != CourseScenePath) return;
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

            // A와 B가 겹치지 않게 좌우로 벌린다
            bool isA = Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient;
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

        public void OnSceneLoadStart(NetworkRunner runner) { }
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
