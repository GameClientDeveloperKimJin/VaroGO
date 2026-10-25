using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // ISwitchDetails 인터페이스는 계약 v0.2에서 VaroGO.Shared(Contracts.cs)로 옮겼다. 여기는 클라 헬퍼만 남긴다.
    public static class SwitchStates
    {
        // 계약 §3.3: 상태는 GameObject로 참조하고 GetComponent로 얻는다 (Net Variant에서 구현체가 바뀌어도 유지).
        // legacy: 예전 MonoBehaviour 참조. 아직 다시 저장되지 않은 프리팹·씬을 위해 그 GameObject로 대신 찾는다
        // 계약 §3.3·§3.4·§6: Net Variant에는 로컬 구현 옆에 Net 구현이 추가된다.
        // 여러 개면 온라인은 TLocal이 아닌 것, 오프라인은 TLocal을 고른다. 하나뿐이면 그대로 쓴다
        public static TState Pick<TState, TLocal>(GameObject target, MonoBehaviour legacy, Object context)
            where TState : class where TLocal : TState
        {
            if (target == null && legacy != null) target = legacy.gameObject;
            if (target == null) return null;
            var states = target.GetComponents<TState>();
            if (states.Length == 0)
            {
                Debug.LogWarning($"[State] {target.name}에 {typeof(TState).Name}이 없습니다", context);
                return null;
            }
            if (states.Length == 1) return states[0];
            foreach (var s in states)
                if ((s is TLocal) != GameServices.IsOnline) return s;
            return states[0];
        }

        public static ISwitchState Resolve(GameObject target, MonoBehaviour legacy, Object context)
            => Pick<ISwitchState, LocalSwitchState>(target, legacy, context);

        public static IGoalState ResolveGoal(GameObject target, MonoBehaviour legacy, Object context)
            => Pick<IGoalState, LocalGoalState>(target, legacy, context);

        public static bool CanUse(ISwitchState state, PlayerRole role)
        {
            if (!(state is ISwitchDetails details)) return true;
            return details.AllowedRole == PlayerRole.None || details.AllowedRole == role;
        }

        public static PlayerRole LocalRole => GameServices.Role != null ? GameServices.Role.LocalRole : PlayerRole.None;

        public static double Now => GameServices.Clock != null ? GameServices.Clock.Now : Time.timeAsDouble;
    }
}
