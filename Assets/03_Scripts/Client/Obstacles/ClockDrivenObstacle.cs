using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-08: 위치·회전을 GameServices.Clock.Now의 순수 함수로만 계산한다. 상태를 누적하지 않으므로
    // 두 PC의 시계가 같으면 같은 자리에 있고, 시계가 튀어도 그 시간에 맞는 자리로 바로 간다.
    // 시작 위치·회전(origin)은 씬에 놓인 그대로를 기준으로 쓴다.
    [RequireComponent(typeof(Rigidbody))]
    public abstract class ClockDrivenObstacle : MonoBehaviour
    {
        [Tooltip("시간 어긋남 (초). 같은 장애물을 여러 개 놓을 때 타이밍을 다르게 한다")]
        [SerializeField] protected float phaseOffset;

        const double VelocitySampleDelta = 0.005;

        protected Vector3 originPos;
        protected Quaternion originRot;
        Rigidbody rb;

        protected virtual void Awake()
        {
            originPos = transform.position;
            originRot = transform.rotation;
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public double LocalTime => (GameServices.Clock != null ? GameServices.Clock.Now : Time.timeAsDouble) + phaseOffset;

        // t: phaseOffset이 더해진 시간
        protected abstract void Evaluate(double t, out Vector3 position, out Quaternion rotation);

        protected virtual void FixedUpdate()
        {
            Evaluate(LocalTime, out Vector3 pos, out Quaternion rot);
            rb.MovePosition(pos);
            rb.MoveRotation(rot);
        }

        // 넉백(C-09)·발판 태우기용. 포즈 함수의 앞뒤 시간을 비교하므로 상태 없이 어느 시점이든 구할 수 있다.
        public Vector3 GetVelocityAt(Vector3 worldPoint)
        {
            double t = LocalTime;
            Evaluate(t, out Vector3 pos, out Quaternion rot);
            Vector3 local = Quaternion.Inverse(rot) * (worldPoint - pos);
            Evaluate(t - VelocitySampleDelta, out Vector3 p0, out Quaternion r0);
            Evaluate(t + VelocitySampleDelta, out Vector3 p1, out Quaternion r1);
            return ((p1 + r1 * local) - (p0 + r0 * local)) / (float)(2.0 * VelocitySampleDelta);
        }

        // 음수 시간에서도 0 ~ period 사이 값
        protected static double Repeat(double t, double period) => t - System.Math.Floor(t / period) * period;
    }
}
