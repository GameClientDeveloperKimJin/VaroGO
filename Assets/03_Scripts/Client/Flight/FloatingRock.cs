using UnityEngine;

namespace VaroGO.Client
{
    // P19 떠 있는 바위(나무 잔해). 앞쪽에서 뗏목 쪽으로 천천히 날아온다 (빨려 들어가는 느낌). approachSpeed = 0이면 제자리.
    // 이 오브젝트 위치 = 뗏목과 만나는 지점. 다가오는 거리는 남은 거리로 정해서 모든 PC에서 같다.
    // 크기는 radius로 정하고, 자식 Visual은 그 크기에 맞춰 늘린다
    public class FloatingRock : FlightHazard
    {
        [Tooltip("바위 반지름 (m). 판정 크기")]
        [SerializeField, Min(0.1f)] float radius = 2f;
        [Tooltip("위아래로 흔들리는 폭 (m). 보이는 모습만, 판정은 고정")]
        [SerializeField, Min(0f)] float bobAmount = 0.3f;
        [Tooltip("뗏목 쪽으로 날아오는 속도 (m/초, 뗏목이 기본 속도일 때). 0 = 제자리에 떠 있음")]
        [SerializeField, Min(0f)] float approachSpeed = 6f;
        [Tooltip("이 거리(m)보다 멀리 있으면 아직 안 보인다 (날아오기 시작하는 곳)")]
        [SerializeField, Min(5f)] float appearDistance = 80f;
        [Tooltip("날아오며 빙글 도는 속도 (도/초)")]
        [SerializeField] float tumbleSpeed = 40f;
        [Tooltip("만나기 전 경고 표시가 보이는 시간 (초, 뗏목이 기본 속도일 때. 빨라지면 짧아진다)")]
        [SerializeField, Min(0.2f)] float warnTime = 2f;
        [SerializeField] Transform visual;
        [Tooltip("만나는 지점의 경고 표시 (셰이더 _WarnProgress로 연출). 비우면 경고 없음")]
        [SerializeField] Transform warning;

        Vector3 visualBase;
        float phaseOffset;
        float flashUntil;
        float impactDistance;
        float approachOffset; // 만나는 지점보다 앞쪽으로 떨어져 있는 거리 (m)

        public float Radius => radius;

        public override void Bind(FlightCourse course)
        {
            base.Bind(course);
            if (visual == null) visual = transform.Find("Visual");
            if (visual != null) visualBase = visual.localPosition;
            if (warning == null) warning = transform.Find("Warning");
            CacheWarning(warning);
            phaseOffset = (transform.position.x * 0.37f + transform.position.z * 0.11f) % 6.28f;
            impactDistance = LocalAnchor.z;
        }

        public override void Tick(bool flying, float distance)
        {
            // 비행 중에만 다가온다. 비행 전·실패 뒤에는 처음 자리(만나는 지점 + 출발 거리 기준 오프셋)
            float untilImpact = Course != null ? Course.TimeToReach(impactDistance - distance) : 0f;
            approachOffset = approachSpeed > 0f ? Mathf.Max(-20f, untilImpact * approachSpeed) : 0f;
            if (warning != null)
            {
                // 경고: warnTime 전부터 지나갈 때까지 (이미 부딪혔으면 끔)
                bool warn = flying && untilImpact < warnTime && untilImpact > -0.3f && !HitThisAttempt;
                if (warning.gameObject.activeSelf != warn) warning.gameObject.SetActive(warn);
                if (warn) SetWarnProgress(1f - Mathf.Clamp01(untilImpact / warnTime));
            }
            if (visual == null) return;
            bool show = approachOffset < appearDistance;
            if (visual.gameObject.activeSelf != show) visual.gameObject.SetActive(show);
            Vector3 fwd = Course != null ? Course.transform.forward : Vector3.forward;
            visual.position = transform.TransformPoint(visualBase) + fwd * approachOffset
                              + Vector3.up * (Mathf.Sin(Time.time * 0.8f + phaseOffset) * bobAmount);
            if (approachSpeed > 0f) visual.Rotate(new Vector3(0.6f, 1f, 0.3f) * (tumbleSpeed * Time.deltaTime), Space.Self);
        }

        public override bool Overlaps(Vector3 raftCenter, Vector3 half)
        {
            if (HitThisAttempt) return false;
            if (approachOffset >= appearDistance) return false;
            return SphereBox(LocalAnchor + Vector3.forward * approachOffset, radius, raftCenter, half);
        }

        public override void PlayHit()
        {
            Vector3 at = visual != null ? visual.position : transform.position; // 날아온 지금 자리
            if (!SpawnHitVfx(HitPoint(at, radius)))
                FlightFx.Burst(at, new Color(0.6f, 0.45f, 0.3f), radius * 1.5f); // 나무 잔해 파편 색
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
