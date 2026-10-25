using UnityEngine;

namespace VaroGO.Client
{
    // 왕복 이동. 느리게 두면 밀어내기만 한다 (C-09 가벼운 밀림).
    public class SlidingObstacle : ClockDrivenObstacle
    {
        [Tooltip("이동 방향 (오브젝트 로컬 축)")]
        [SerializeField] Vector3 axis = Vector3.right;
        [Tooltip("시작 위치에서 한쪽으로 가는 거리 (m)")]
        [SerializeField] float amplitude = 2f;
        [Tooltip("왕복 한 번에 걸리는 시간 (초)")]
        [SerializeField] float period = 4f;

        protected override void Evaluate(double t, out Vector3 position, out Quaternion rotation)
        {
            float s = Mathf.Sin((float)(Repeat(t, period) / period * 2.0 * Mathf.PI));
            position = originPos + originRot * axis.normalized * (s * amplitude);
            rotation = originRot;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 o = Application.isPlaying ? originPos : transform.position;
            Quaternion r = Application.isPlaying ? originRot : transform.rotation;
            Vector3 d = r * axis.normalized * amplitude;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(o - d, o + d);
        }
    }
}
