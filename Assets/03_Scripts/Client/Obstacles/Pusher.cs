using UnityEngine;

namespace VaroGO.Client
{
    // 일정 주기로 빠르게 튀어나왔다가 천천히 들어가는 벽. 튀어나올 때 맞으면 날아간다.
    public class Pusher : ClockDrivenObstacle
    {
        [Tooltip("튀어나오는 방향 (오브젝트 로컬 축)")]
        [SerializeField] Vector3 axis = Vector3.forward;
        [Tooltip("튀어나오는 거리 (m)")]
        [SerializeField] float distance = 2f;
        [Tooltip("한 사이클 시간 (초)")]
        [SerializeField] float period = 3f;
        [Tooltip("튀어나오는 데 걸리는 시간 (초). 짧을수록 세게 친다")]
        [SerializeField] float extendTime = 0.15f;
        [Tooltip("튀어나온 채로 버티는 시간 (초)")]
        [SerializeField] float holdTime = 0.5f;
        [Tooltip("들어가는 데 걸리는 시간 (초)")]
        [SerializeField] float retractTime = 1f;

        protected override void Evaluate(double t, out Vector3 position, out Quaternion rotation)
        {
            float p = (float)Repeat(t, period);
            float amount;
            if (p < extendTime) amount = p / extendTime;
            else if (p < extendTime + holdTime) amount = 1f;
            else if (p < extendTime + holdTime + retractTime) amount = 1f - Mathf.SmoothStep(0f, 1f, (p - extendTime - holdTime) / retractTime);
            else amount = 0f;
            position = originPos + originRot * axis.normalized * (amount * distance);
            rotation = originRot;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 o = Application.isPlaying ? originPos : transform.position;
            Quaternion r = Application.isPlaying ? originRot : transform.rotation;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(o, o + r * axis.normalized * distance);
        }
    }
}
