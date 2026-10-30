using System;
using UnityEngine;

namespace VaroGO.Shared
{
    public enum PlayerRole { None = 0, A_Red = 1, B_Blue = 2 }

    // 오브젝트를 누가 볼 수 있는가. 충돌은 항상 공통.
    public enum MagicType { Common = 0, Red = 1, Blue = 2 }

    // 새 값은 맨 뒤에만 추가한다 (계약명세 R7)
    public enum PlayerEvent { Died, Respawned, KnockbackLight, KnockbackHeavy, Landed, HitInvisible, Jumped, Pushed }

    // 내 PC의 역할. 오프라인: 디버그 키로 전환 / 온라인: 서버가 배정
    public interface IRoleProvider
    {
        PlayerRole LocalRole { get; }
        event Action<PlayerRole> LocalRoleChanged;
    }

    // 두 PC에서 같은 값이 나와야 하는 시간(초). 이동 장애물은 이 값만 사용한다.
    public interface IGameClock
    {
        double Now { get; }
    }

    // 스위치·발판·레버의 on/off 상태. 문과 시간제한 발판은 Changed를 구독한다.
    // 설정값(allowedRole, autoOffSeconds, startOn)은 LocalSwitchState 한 곳에만 둔다.
    // 온라인 NetSwitchState는 같은 GameObject의 LocalSwitchState에서 읽는다 (계약명세 §3.3)
    public interface ISwitchState
    {
        bool IsOn { get; }
        event Action<bool> Changed;
        void RequestSet(bool on, PlayerRole requester); // 권한 검사는 구현체가 한다
    }

    // ISwitchState 구현체가 함께 구현하는 연출용 정보. 없으면 클라는 권한 표시·남은 시간 깜빡임을 생략한다.
    public interface ISwitchDetails
    {
        PlayerRole AllowedRole { get; }   // None = 둘 다 가능
        float AutoOffSeconds { get; }     // 0 = 계속 유지
        double LastOnTime { get; }        // 마지막으로 켜진 GameClock 시각
    }

    // 골 지점. 두 역할이 모두 들어오면 ChapterCleared가 발생한다.
    public interface IGoalState
    {
        bool IsCleared { get; }
        event Action ChapterCleared;
        void ReportInGoal(PlayerRole role, bool inside);
    }

    // 플레이어 캐릭터(클라 구현) ↔ 네트워크 래퍼(서버 구현) 사이의 계약
    public interface IPlayerSimulation
    {
        void SetLocal(bool isLocal);       // false: 입력·카메라·시뮬레이션 끔(원격 플레이어)
        void Simulate(float deltaTime);    // 로컬 플레이어에게만 호출된다
        event Action<Vector3> Teleported;  // 부활 등 순간이동. 네트워크 보간을 끊는 데 사용
        event Action<PlayerEvent> EventRaised; // 로컬: 자기 시뮬레이션에서 / 원격: PlayRemoteEvent로 발생
        void PlayRemoteEvent(PlayerEvent e);   // 원격 플레이어에게만 의미 있음. 로컬이면 무시된다
    }

    // 공용 체크포인트 (계약 v0.4 P12). 두 역할이 모두 도착한 체크포인트가 둘의 부활 지점이 된다.
    // 오프라인: LocalCheckpointState(혼자 도착 = 활성) / 온라인: 코스 씬의 NetCheckpointState
    public interface ICheckpointState
    {
        int ActiveOrder { get; }                           // 두 역할 모두 도착한 가장 높은 Order (-1 = 시작점)
        bool IsReached(int order, PlayerRole role);
        event Action Changed;
        void ReportReached(int order, PlayerRole role);    // 로컬 플레이어만 호출 (R4)
        bool CanFastTravel(int order);                     // (임시) 활성된 지점인지
    }

    // 세션 진행 단계 (계약 v0.4 P11). 오프라인은 바로 InGame
    public enum SessionPhase { Offline, Idle, Connecting, WaitingPartner, Lobby, LoadingCourse, InGame, PartnerLost }

    // 방 만들기·참가·준비·시작 (P11, C-1028-3 B안). 클라 UI는 이 인터페이스만 본다 (Net 직접 참조 금지)
    public interface ISessionLobby
    {
        SessionPhase Phase { get; }
        bool IsHost { get; }
        string Code { get; }
        string Status { get; }
        bool IsFailure { get; }
        bool PartnerConnected { get; }
        bool PartnerReady { get; }
        float PartnerLostSeconds { get; }  // 끊긴 뒤 경과 시간
        event Action Changed;
        void CreateRoom();
        void JoinRoom(string code);
        void Leave();
        void SetReady(bool ready);         // 게스트만
        void StartGame();                  // 호스트만, PartnerReady일 때만 동작
    }

    // 구역 리셋 (P13). 상태 권한이 그 구역 스위치를 StartOn으로 되돌린다
    public interface IZoneReset
    {
        void RequestReset(int zone, PlayerRole requester);
        event Action<int> ZoneReset;       // 모든 PC에서 1회
    }

    // 플레이어끼리 밀기 (P14). 넉백 판정·적용은 밀린 PC의 클라 코드 (R6)
    public interface IPlayerInteraction
    {
        void RequestPush(PlayerRole target, Vector3 dir);   // 미는 PC에서 호출
        event Action<PlayerRole, Vector3> Pushed;           // 밀린 PC에서만 (from, dir)
    }

    // 전역 서비스 등록소. 오프라인/온라인 부트스트랩이 Register를 호출한다.
    public static class GameServices
    {
        public static IRoleProvider Role { get; private set; }
        public static IGameClock Clock { get; private set; }
        public static bool IsOnline { get; private set; }
        public static ISessionLobby Session { get; private set; }
        public static ICheckpointState Checkpoints { get; private set; }
        public static IZoneReset ZoneReset { get; private set; }
        public static IPlayerInteraction Interaction { get; private set; }
        public static event Action Registered;

        public static void Register(IRoleProvider role, IGameClock clock, bool isOnline)
        {
            Role = role;
            Clock = clock;
            IsOnline = isOnline;
            Registered?.Invoke();
        }

        // 게임 시스템 등록 (P15). null 인자는 기존 값을 유지한다
        // (온라인: 세션은 00_Boot에서, 체크포인트 등은 코스 씬에서 따로 등록할 수 있다)
        public static void RegisterSystems(ISessionLobby session, ICheckpointState checkpoints, IZoneReset zoneReset, IPlayerInteraction interaction)
        {
            if (session != null) Session = session;
            if (checkpoints != null) Checkpoints = checkpoints;
            if (zoneReset != null) ZoneReset = zoneReset;
            if (interaction != null) Interaction = interaction;
        }

        // 체크포인트만 교체. 코스 씬마다 OfflineBootstrap이 먼저, 온라인 NetCheckpointState가 Spawned에서 덮어쓴다
        public static void RegisterCheckpoints(ICheckpointState checkpoints)
        {
            Checkpoints = checkpoints;
        }
    }
}
