using System;
using UnityEngine;

namespace VaroGO.Shared
{
    public enum PlayerRole { None = 0, A_Red = 1, B_Blue = 2 }

    // 오브젝트를 누가 볼 수 있는가. 충돌은 항상 공통.
    public enum MagicType { Common = 0, Red = 1, Blue = 2 }

    public enum PlayerEvent { Died, Respawned, KnockbackLight, KnockbackHeavy, Landed, HitInvisible }

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
    // 구현체 공통 직렬화 필드: PlayerRole allowedRole (None = 둘 다 가능),
    //                         float autoOffSeconds (0 = 계속 유지), bool startOn
    public interface ISwitchState
    {
        bool IsOn { get; }
        event Action<bool> Changed;
        void RequestSet(bool on, PlayerRole requester); // 권한 검사는 구현체가 한다
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
        event Action<PlayerEvent> EventRaised; // 원격 이펙트·사운드 재생용
    }

    // 전역 서비스 등록소. 오프라인/온라인 부트스트랩이 Register를 호출한다.
    public static class GameServices
    {
        public static IRoleProvider Role { get; private set; }
        public static IGameClock Clock { get; private set; }
        public static bool IsOnline { get; private set; }
        public static event Action Registered;

        public static void Register(IRoleProvider role, IGameClock clock, bool isOnline)
        {
            Role = role;
            Clock = clock;
            IsOnline = isOnline;
            Registered?.Invoke();
        }
    }
}
