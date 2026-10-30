using System;
using System.Linq;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// (임시, 클라 작성 C-1030-3) S2-03 전까지 기존 NetBootstrap을 ISessionLobby로 보여준다.
    /// 지금 흐름(파트너 접속 = 바로 코스 로드)을 그대로 따르므로 Lobby 단계·Ready는 없다.
    /// S2-03에서 NetBootstrap이 ISessionLobby를 직접 구현·등록하면 이 파일은 지운다.
    /// </summary>
    public class NetSessionLobbyBridge : ISessionLobby
    {
        // 코스 씬 OfflineBootstrap(Awake)이 오프라인 세션으로 덮어쓸 수 있게 씬 로드 전에 등록한다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameServices.RegisterSystems(new NetSessionLobbyBridge(), null, null, null);
        }

        static NetBootstrap Boot => NetBootstrap.Instance;

        public SessionPhase Phase
        {
            get
            {
                var boot = Boot;
                if (boot == null) return SessionPhase.Idle;
                switch (boot.State)
                {
                    case SessionState.Connecting: return SessionPhase.Connecting;
                    case SessionState.WaitingPartner: return SessionPhase.WaitingPartner;
                    case SessionState.LoadingCourse: return SessionPhase.LoadingCourse;
                    case SessionState.InGame: return SessionPhase.InGame;
                    default: return SessionPhase.Idle;
                }
            }
        }

        public bool IsHost => Boot != null && Boot.Runner != null && Boot.Runner.IsSharedModeMasterClient;
        public string Code => Boot != null ? Boot.SessionCode : string.Empty;
        public string Status => Boot != null ? Boot.Status : string.Empty;
        public bool IsFailure => Boot != null && Boot.IsFailure;
        public bool PartnerConnected => Boot != null && Boot.Runner != null && Boot.Runner.ActivePlayers.Count() >= 2;
        public bool PartnerReady => false;
        public float PartnerLostSeconds => 0f;

        // NetBootstrap에 변경 이벤트가 없다. UI는 매 프레임 다시 그린다
        public event Action Changed { add { } remove { } }

        public void CreateRoom() => Boot?.CreateRoom();
        public void JoinRoom(string code) => Boot?.JoinRoom(code);
        public void Leave() => Boot?.Leave();
        public void SetReady(bool ready) { }
        public void StartGame() { }
    }
}
