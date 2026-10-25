using VaroGO.Shared;

namespace VaroGO.Client
{
    // ISwitchState 구현체가 추가로 알려주는 정보 (선택). 서버의 NetSwitchState도 구현하면 연출이 똑같이 동작한다.
    // 없으면: 권한 검사 없이 누를 수 있다고 보고, 시간제한 발판은 깜빡이지 않는다.
    public interface ISwitchDetails
    {
        PlayerRole AllowedRole { get; }   // None = 둘 다 가능
        float AutoOffSeconds { get; }     // 0 = 계속 유지
        double LastOnTime { get; }        // 마지막으로 켜진 GameClock 시각
    }

    public static class SwitchStates
    {
        // Inspector의 stateSource(MonoBehaviour)를 인터페이스로 바꾼다
        public static ISwitchState Resolve(UnityEngine.MonoBehaviour source, UnityEngine.Object context)
        {
            if (source == null) return null;
            if (source is ISwitchState state) return state;
            UnityEngine.Debug.LogWarning($"[Switch] {source.name}의 {source.GetType().Name}은 ISwitchState가 아닙니다", context);
            return null;
        }

        public static bool CanUse(ISwitchState state, PlayerRole role)
        {
            if (!(state is ISwitchDetails details)) return true;
            return details.AllowedRole == PlayerRole.None || details.AllowedRole == role;
        }

        public static PlayerRole LocalRole => GameServices.Role != null ? GameServices.Role.LocalRole : PlayerRole.None;

        public static double Now => GameServices.Clock != null ? GameServices.Clock.Now : UnityEngine.Time.timeAsDouble;
    }
}
