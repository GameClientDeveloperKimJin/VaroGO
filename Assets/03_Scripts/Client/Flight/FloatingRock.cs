using UnityEngine;

namespace VaroGO.Client
{
    // P19 떠 있는 바위. 제자리에 떠 있고(보이는 부분만 살짝 흔들림) 뗏목이 닿으면 피해.
    // 크기는 radius로 정하고, 자식 Visual은 그 크기에 맞춰 늘린다
    public class FloatingRock : FlightHazard
    {
        [Tooltip("바위 반지름 (m). 판정 크기")]
        [SerializeField, Min(0.1f)] float radius = 2f;
        [Tooltip("위아래로 흔들리는 폭 (m). 보이는 모습만, 판정은 고정")]
        [SerializeField, Min(0f)] float bobAmount = 0.3f;
        [SerializeField] Transform visual;

        Vector3 visualBase;
        float phaseOffset;
        float flashUntil;

        public float Radius => radius;

        public override void Bind(FlightCourse course)
        {
            base.Bind(course);
            if (visual == null) visual = transform.Find("Visual");
            if (visual != null) visualBase = visual.localPosition;
            phaseOffset = (transform.position.x * 0.37f + transform.position.z * 0.11f) % 6.28f;
        }

        public override void Tick(bool flying, float distance)
        {
            if (visual == null) return;
            visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * 0.8f + phaseOffset) * bobAmount);
        }

        public override bool Overlaps(Vector3 raftCenter, Vector3 half)
        {
            if (HitThisAttempt) return false;
            return SphereBox(LocalAnchor, radius, raftCenter, half);
        }

        public override void PlayHit()
        {
            FlightFx.Burst(transform.position, new Color(0.7f, 0.7f, 0.75f), radius * 1.5f);
        }

        void OnValidate()
        {
            if (visual == null) visual = transform.Find("Visual");
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
