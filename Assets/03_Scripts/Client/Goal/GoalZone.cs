using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-12: 로컬 플레이어가 들어오거나 나가면 IGoalState에 알린다. 트리거 Collider와 함께 쓴다.
    // 원격 플레이어는 각자 자기 PC에서 알린다.
    [RequireComponent(typeof(Collider))]
    public class GoalZone : MonoBehaviour
    {
        [Tooltip("IGoalState를 구현한 컴포넌트 (LocalGoalState 등)")]
        [SerializeField] MonoBehaviour stateSource;

        IGoalState state;
        PlayerRole reportedRole;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void Awake()
        {
            state = stateSource as IGoalState;
            if (state == null && stateSource != null)
                Debug.LogWarning($"[Goal] {stateSource.name}의 {stateSource.GetType().Name}은 IGoalState가 아닙니다", this);
        }

        void OnTriggerEnter(Collider other)
        {
            if (state == null || !IsLocalPlayer(other)) return;
            // 들어올 때의 역할을 기억해 둔다. 안에서 역할이 바뀌어도 나갈 때 같은 역할로 알린다
            reportedRole = SwitchStates.LocalRole;
            state.ReportInGoal(reportedRole, true);
        }

        void OnTriggerExit(Collider other)
        {
            if (state == null || !IsLocalPlayer(other) || reportedRole == PlayerRole.None) return;
            state.ReportInGoal(reportedRole, false);
            reportedRole = PlayerRole.None;
        }

        static bool IsLocalPlayer(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            return player != null && player.IsLocal;
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
