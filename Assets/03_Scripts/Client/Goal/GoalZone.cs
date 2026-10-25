using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-12: 로컬 플레이어가 들어오거나 나가면 IGoalState에 알린다. 트리거 Collider와 함께 쓴다.
    // 원격 플레이어는 각자 자기 PC에서 알린다.
    [RequireComponent(typeof(Collider))]
    public class GoalZone : MonoBehaviour
    {
        [Tooltip("연결할 골 상태 오브젝트 (골 상태 컴포넌트가 붙은 GameObject)")]
        [SerializeField] GameObject stateObject;
        [SerializeField, HideInInspector] MonoBehaviour stateSource; // 예전 참조. OnValidate에서 stateObject로 옮긴다

        IGoalState state;
        PlayerRole reportedRole;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void Awake()
        {
            state = SwitchStates.ResolveGoal(stateObject, stateSource, this);
        }

        void OnValidate()
        {
            if (stateObject == null && stateSource != null) stateObject = stateSource.gameObject;
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
