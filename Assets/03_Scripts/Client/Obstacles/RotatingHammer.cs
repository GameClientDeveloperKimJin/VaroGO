using UnityEngine;

namespace VaroGO.Client
{
    // 축 회전 또는 진자. 이 오브젝트의 위치가 회전 중심이므로 해머 모양은 자식으로 둔다.
    public class RotatingHammer : ClockDrivenObstacle
    {
        public enum Mode { Spin, Pendulum }

        [SerializeField] Mode mode = Mode.Pendulum;
        [Tooltip("회전축 (오브젝트 로컬 축)")]
        [SerializeField] Vector3 axis = Vector3.forward;
        [Tooltip("Spin: 한 바퀴 시간 / Pendulum: 한 번 왕복하는 시간 (초)")]
        [SerializeField] float period = 2.5f;
        [Tooltip("Pendulum 전용. 가운데에서 한쪽으로 기우는 최대 각도")]
        [SerializeField] float maxAngle = 70f;

        protected override void Evaluate(double t, out Vector3 position, out Quaternion rotation)
        {
            float phase = (float)(Repeat(t, period) / period);
            float angle = mode == Mode.Spin
                ? phase * 360f
                : Mathf.Sin(phase * 2f * Mathf.PI) * maxAngle;
            position = originPos;
            rotation = originRot * Quaternion.AngleAxis(angle, axis.normalized);
        }
    }
}
