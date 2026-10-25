using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-11: 상태가 on이면 열린다. Collider가 달린 오브젝트 자체를 옮기므로 충돌도 같이 움직인다.
    public class Door : MonoBehaviour
    {
        [Tooltip("연결할 스위치 오브젝트 (상태 컴포넌트가 붙은 GameObject)")]
        [SerializeField] GameObject stateObject;
        [SerializeField, HideInInspector] MonoBehaviour stateSource; // 예전 참조. OnValidate에서 stateObject로 옮긴다
        [Tooltip("열렸을 때의 이동량 (부모 기준 로컬 좌표)")]
        [SerializeField] Vector3 openOffset = new Vector3(0f, 3f, 0f);
        [Tooltip("다 열리거나 닫히는 데 걸리는 시간 (초)")]
        [SerializeField] float moveTime = 0.6f;
        [Tooltip("켜면 반대로: on일 때 닫힌다")]
        [SerializeField] bool invert;

        ISwitchState state;
        Vector3 closedPos;
        float openAmount;
        bool targetOpen;

        void Awake()
        {
            state = SwitchStates.Resolve(stateObject, stateSource, this);
            closedPos = transform.localPosition;
        }

        void OnValidate()
        {
            if (stateObject == null && stateSource != null) stateObject = stateSource.gameObject;
        }

        void OnEnable()
        {
            if (state == null) return;
            state.Changed += OnChanged;
            targetOpen = state.IsOn != invert;
            openAmount = targetOpen ? 1f : 0f; // 시작 상태는 바로 맞춘다
            Apply();
        }

        void OnDisable()
        {
            if (state != null) state.Changed -= OnChanged;
        }

        void OnChanged(bool on) => targetOpen = on != invert;

        void Update()
        {
            float target = targetOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target)) return;
            openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime / Mathf.Max(0.01f, moveTime));
            Apply();
        }

        void Apply()
        {
            transform.localPosition = closedPos + openOffset * Mathf.SmoothStep(0f, 1f, openAmount);
        }

        // 열린 위치를 노란 박스로 표시
        void OnDrawGizmosSelected()
        {
            var parent = transform.parent;
            Vector3 closed = Application.isPlaying ? closedPos : transform.localPosition;
            Gizmos.color = Color.yellow;
            Gizmos.matrix = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
            Gizmos.DrawWireCube(closed + openOffset, transform.localScale);
        }
    }
}
