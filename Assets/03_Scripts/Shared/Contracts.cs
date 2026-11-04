using System;
using UnityEngine;

namespace VaroGO.Shared
{
    public enum PlayerRole { None = 0, A_Red = 1, B_Blue = 2 }

    // 오브젝트를 누가 볼 수 있는가. 충돌은 항상 공통.
    public enum MagicType { Common = 0, Red = 1, Blue = 2 }

    // 새 값은 맨 뒤에만 추가한다 (계약명세 R7)
    public enum PlayerEvent { Died, Respawned, KnockbackLight, KnockbackHeavy, Landed, HitInvisible, Jumped, Pushed,
        LedgeGrab, LedgeClimb, Bound, Freed, PickedUp, Thrown }  // P18

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
        void RequestRescueShake(PlayerRole target);         // P18: 묶인 파트너 옆에서 A/D를 바꿔 누를 때마다 구하는 PC에서 호출
        event Action<PlayerRole> RescueShaken;              // P18: 묶인 PC에서만 (from). 횟수 판정·풀림은 묶인 PC의 클라 코드
    }

    // 미는 오브젝트 (P18). 몇 명이 어느 방향으로 미는지 모아 이동 판정·적용은 권한 PC(온라인 = 방장)의 클라 코드.
    // 위치는 구현체가 동기화한다 (온라인: NetworkTransform)
    public interface IPushableState
    {
        bool HasAuthority { get; }                          // 이 PC가 오브젝트를 움직이는가
        void ReportPush(PlayerRole role, Vector3 dir);      // 미는 로컬 PC가 주기적으로 호출 (R4). Vector3.zero = 손 뗌
        event Action<PlayerRole, Vector3> PushReported;     // 권한 PC에서만
    }

    // 줍고 던지는 물건 (P18). 물리(날아감·충돌)는 권한 PC의 클라 코드. 위치는 구현체가 동기화한다
    public interface ICarryState
    {
        bool HasAuthority { get; }                          // 이 PC가 물리를 돌리는가
        PlayerRole Holder { get; }                          // 들고 있는 역할. None = 바닥에 있음
        event Action HolderChanged;                         // 모든 PC
        void RequestPickUp(PlayerRole role);                // 줍는 로컬 PC (R4). 이미 누가 들고 있으면 무시
        void RequestThrow(PlayerRole role, Vector3 origin, Vector3 velocity); // 든 사람의 PC (R4). velocity = 0이면 내려놓기
        event Action<Vector3, Vector3> Thrown;              // 권한 PC에서만 (origin, velocity). Holder가 None이 된 뒤
    }

    // 인게임 보이스 (P16). 클라 UI(ESC 메뉴·HUD·음성 설정)는 이 인터페이스만 본다.
    // 온라인: 서버가 Photon Voice 2로 구현해 RegisterVoice / 오프라인·미등록: 클라 OfflineVoiceChat이 대신한다
    public interface IVoiceChat
    {
        bool IsConnected { get; }                // 음성 채널에 들어가 있는지
        bool MicEnabled { get; }                 // 내 목소리를 상대에게 보내는지
        void SetMicEnabled(bool on);             // 즉시 전송 중단·재개
        string[] MicDevices { get; }             // 고를 수 있는 입력 장치 이름
        string MicDevice { get; }                // "" = 기본 장치
        void SetMicDevice(string device);
        float InputLevel { get; }                // 내 마이크 입력 크기 0~1 (꺼져 있어도 측정)
        bool PartnerSpeaking { get; }            // 상대 음성이 들어오는 중
        AudioSource PartnerAudio { get; }        // 상대 목소리가 나오는 AudioSource (없으면 null). 클라가 3D·볼륨을 적용
    }

    // 핑 종류 (P17). 새 값은 맨 뒤에만 추가한다 (R7)
    public enum PingType { Step = 0, Danger = 1 }  // Step = 여기 밟아! / Danger = 위험·멈춰!

    // 핑 (P17). 위치 판정·연출은 클라. 구현체는 받은 값을 그대로 모든 PC에 전달만 한다
    // 온라인: 서버 구현을 RegisterPing / 오프라인·미등록: 클라 OfflinePingSystem(내 화면에만)
    public interface IPingSystem
    {
        void RequestPing(PingType type, Vector3 position, Vector3 normal, PlayerRole sender); // 핑을 찍은 로컬 PC만 호출 (R4)
        event Action<PingType, Vector3, Vector3, PlayerRole> PingReceived;                   // 보낸 PC 포함 모든 PC에서 1회
    }

    // 공중 비행 보너스 단계 (P19). 새 값은 맨 뒤에만 추가한다 (R7)
    public enum FlightPhase { Idle = 0, Summoning = 1, Boarding = 2, Countdown = 3, Flying = 4, Failed = 5, Landing = 6, Arrived = 7 }

    // 공중 비행 보너스 (P19). 이동·충돌·내구도·성공 판정은 권한 PC(온라인 = 방장)의 클라 코드(FlightCourse)가 한다.
    // 구현체는 값 보관·동기화만 한다. 탑승·조작 입력·소환 요청은 각 로컬 PC가 보고한다 (R4)
    public interface IFlightState
    {
        bool HasAuthority { get; }                  // 이 PC가 비행 판정을 돌리는가
        FlightPhase Phase { get; }
        double PhaseStartTime { get; }              // 현재 단계가 시작된 GameClock 시각
        int Durability { get; }
        int Attempt { get; }                        // 0 = 첫 도전. 실패 후 다시 시작할 때마다 +1
        float Lateral { get; }                      // 뗏목 좌우 위치 (m, 코스 중심 기준 + = 오른쪽)
        float LateralVelocity { get; }
        event Action Changed;                       // Phase·Durability·Attempt가 바뀌면 모든 PC에서 1회
        bool IsBoarded(PlayerRole role);
        bool IsPressing(PlayerRole role);           // A_Red = A(왼쪽) 누름, B_Blue = D(오른쪽) 누름
        void RequestSummon(PlayerRole requester);   // 수정구를 만진 로컬 PC
        void ReportBoarded(PlayerRole role, bool boarded);   // 로컬 PC (R4)
        void ReportPressing(PlayerRole role, bool pressing); // 로컬 PC (R4). 바뀔 때만 호출
        event Action SummonRequested;               // 권한 PC에서만
        void SetPhase(FlightPhase phase, double startTime, int durability, int attempt); // 권한 PC만
        void SetLateral(float lateral, float velocity);                                   // 권한 PC만
        // 가속 구간 (P19 속도 증가): 마지막으로 속도가 기본값으로 돌아간 시각(출발·피격)과 그때 간 거리.
        // 모든 PC가 이 두 값으로 같은 거리·속도를 계산한다. 단계 시작보다 이전 값이면 무시(= 출발점, 거리 0)
        double SegmentStartTime { get; }
        float SegmentStartDistance { get; }
        void SetSegment(double startTime, float distance);                                // 권한 PC만
    }

    // 스테이지(씬) 전환 (P19). 온라인: 서버(NetBootstrap)가 등록, 씬 권한 PC만 실제로 불러온다.
    // 미등록(오프라인)이면 클라 StageLoading이 직접 불러온다
    public interface IStageLoader
    {
        void LoadStage(string scenePath);           // Build Settings에 있는 씬 경로
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

        // 인게임 보이스 (P16). 온라인 보이스가 연결되면 서버가 호출한다. null = 해제(클라 기본 구현으로 돌아감)
        public static IVoiceChat Voice { get; private set; }
        public static void RegisterVoice(IVoiceChat voice)
        {
            Voice = voice;
        }

        // 핑 (P17). 온라인 핑이 준비되면 서버가 호출한다. null = 해제(클라 기본 구현으로 돌아감)
        public static IPingSystem Ping { get; private set; }
        public static void RegisterPing(IPingSystem ping)
        {
            Ping = ping;
        }

        // 체크포인트만 교체. 코스 씬마다 OfflineBootstrap이 먼저, 온라인 NetCheckpointState가 Spawned에서 덮어쓴다
        public static void RegisterCheckpoints(ICheckpointState checkpoints)
        {
            Checkpoints = checkpoints;
        }

        // 스테이지 전환 (P19). 온라인 접속 시 서버가 호출한다. null = 해제(오프라인 전환)
        public static IStageLoader StageLoader { get; private set; }
        public static void RegisterStageLoader(IStageLoader loader)
        {
            StageLoader = loader;
        }
    }
}
