using UnityEngine;

namespace VaroGO.Client
{
    // P19 낙석. 이 오브젝트 위치가 "떨어지는 지점"이다. 뗏목이 이 지점(앞뒤 거리)에 도착하는 순간 돌이 뗏목 높이를 지나간다.
    // 떨어지기 전 지점에 붉은 마법진(그림자)이 커지며 경고한다. 돌 높이·경고는 남은 거리로 정한다 →
    // 뗏목이 빨라지면 돌도 그만큼 빨리 떨어지고 경고 시간은 짧아진다 (기본 속도일 때 warnTime초).
    // 자식: Rock(떨어지는 돌), Warning(경고 마법진)
    public class FallingRock : FlightHazard
    {
        [Tooltip("돌 반지름 (m). 판정 크기")]
        [SerializeField, Min(0.1f)] float radius = 1.5f;
        [Tooltip("떨어지는 속도 (m/초, 뗏목이 기본 속도일 때)")]
        [SerializeField, Min(1f)] float fallSpeed = 18f;
        [Tooltip("떨어지기 전 경고 마법진이 보이는 시간 (초, 뗏목이 기본 속도일 때. 빨라지면 짧아진다)")]
        [SerializeField, Min(0.2f)] float warnTime = 2f;
        [Tooltip("돌이 보이기 시작하는 높이 (지점 위 m)")]
        [SerializeField, Min(1f)] float appearHeight = 30f;
        [SerializeField] Transform rock;
        [SerializeField] Transform warning;

        Vector3 rockBaseScale = Vector3.one, warningBaseScale = Vector3.one;
        float impactDistance; // 뗏목이 지점에 도착하는 거리
        float offsetY;       // 지금 돌 높이 (지점 기준)
        bool visible;
        float shatterAt = -1f;

        public float Radius => radius;

        public override void Bind(FlightCourse course)
        {
            base.Bind(course);
            if (rock == null) rock = transform.Find("Rock");
            if (warning == null) warning = transform.Find("Warning");
            if (rock != null) rockBaseScale = rock.localScale;
            if (warning != null) warningBaseScale = warning.localScale;
            impactDistance = LocalAnchor.z;
            Tick(false, 0f);
        }

        public override void Tick(bool flying, float distance)
        {
            // 기본 속도 기준 "도착까지 남은 시간" (거리로 환산하므로 속도가 바뀌어도 도착 순간에 정확히 맞는다)
            float untilImpact = Course != null ? Course.TimeToReach(impactDistance - distance) : 0f;
            offsetY = untilImpact * fallSpeed;
            bool shattered = shatterAt >= 0f;
            visible = flying && offsetY < appearHeight && offsetY > -40f;
            if (rock != null)
            {
                bool show = visible && !shattered;
                if (rock.gameObject.activeSelf != show) rock.gameObject.SetActive(show);
                if (show)
                {
                    rock.position = transform.position + transform.up * offsetY;
                    rock.Rotate(new Vector3(90f, 40f, 0f) * Time.deltaTime, Space.Self);
                }
            }
            if (warning != null)
            {
                // 경고: warnTime 전부터 지나갈 때까지. 가까워질수록 커지고 빨리 깜빡인다
                bool warn = flying && untilImpact < warnTime && untilImpact > -0.3f && !shattered;
                if (warning.gameObject.activeSelf != warn) warning.gameObject.SetActive(warn);
                if (warn)
                {
                    float k = 1f - Mathf.Clamp01(untilImpact / warnTime);
                    float pulse = 1f + 0.12f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, k));
                    float s = Mathf.Lerp(0.4f, 1f, k) * pulse;
                    warning.localScale = new Vector3(warningBaseScale.x * s, warningBaseScale.y, warningBaseScale.z * s);
                    warning.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
                }
            }
        }

        public override bool Overlaps(Vector3 raftCenter, Vector3 half)
        {
            if (!visible || HitThisAttempt) return false;
            Vector3 c = LocalAnchor + Vector3.up * offsetY;
            return SphereBox(c, radius, raftCenter, half);
        }

        public override void PlayHit()
        {
            shatterAt = Time.time;
            if (rock != null) FlightFx.Burst(rock.position, new Color(0.6f, 0.45f, 0.3f), radius * 2.5f);
        }

        public override void ResetAttempt()
        {
            base.ResetAttempt();
            shatterAt = -1f;
            if (rock != null) rock.localScale = rockBaseScale;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 10f);
        }
    }
}
