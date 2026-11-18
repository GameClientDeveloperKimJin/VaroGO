using UnityEngine;

namespace VaroGO.Client
{
    // P19 낙석. 이 오브젝트 위치가 "떨어지는 지점"이다. 뗏목이 이 지점(앞뒤 거리)에 도착하는 순간 돌이 뗏목 높이를 지나간다.
    // 떨어지기 전 지점에 붉은 마법진(그림자)이 커지며 경고한다. 돌 높이·경고는 남은 거리로 정한다 →
    // 뗏목이 빨라지면 돌도 그만큼 빨리 떨어지고 경고 시간은 짧아진다 (기본 속도일 때 warnTime초).
    // 자식: Rock(떨어지는 돌), Warning(경고 마법진)
    // 2026-11-18: 기본은 "앞에서 날아옴"(FromFront) — 던전으로 빨려 들어가는 비행이라 잔해가 앞쪽에서 뗏목을 향해 날아온다.
    //   FromAbove = 예전처럼 위에서 떨어짐.
    public class FallingRock : FlightHazard
    {
        public enum ApproachMode { FromAbove, FromFront }

        [Tooltip("잔해가 오는 방향. FromFront = 앞에서 뗏목 쪽으로 날아옴, FromAbove = 위에서 떨어짐")]
        [SerializeField] ApproachMode approach = ApproachMode.FromFront;
        [Tooltip("FromFront: 날아오는 속도 (m/초, 뗏목이 기본 속도일 때)")]
        [SerializeField, Min(1f)] float flySpeed = 14f;
        [Tooltip("FromFront: 날아오면서 내려오는 정도. 0.15 = 앞으로 10m 떨어져 있을 때 1.5m 위")]
        [SerializeField, Min(0f)] float flyDrop = 0.12f;
        [Tooltip("FromFront: 이 거리(m)보다 앞에 있으면 아직 안 보인다")]
        [SerializeField, Min(5f)] float appearDistance = 90f;
        [Tooltip("돌 반지름 (m). 판정 크기")]
        [SerializeField, Min(0.1f)] float radius = 1.5f;
        [Tooltip("떨어지는 속도 (m/초, 뗏목이 기본 속도일 때)")]
        [SerializeField, Min(1f)] float fallSpeed = 18f;
        [Tooltip("떨어지기 전 경고 마법진이 보이는 시간 (초, 뗏목이 기본 속도일 때. 빨라지면 짧아진다)")]
        [SerializeField, Min(0.2f)] float warnTime = 2f;
        [Tooltip("돌이 보이기 시작하는 높이 (지점 위 m)")]
        [SerializeField, Min(1f)] float appearHeight = 30f;
        [Tooltip("경고 표시를 크기 맥동·회전시킨다 (옛 원판 경고용). 마법진·\"!\" 경고는 찌그러지므로 끄고 셰이더(_WarnProgress)가 연출한다")]
        [SerializeField] bool scaleWarning = true;
        [SerializeField] Transform rock;
        [SerializeField] Transform warning;

        Vector3 rockBaseScale = Vector3.one, warningBaseScale = Vector3.one;
        float impactDistance; // 뗏목이 지점에 도착하는 거리
        float offsetY;       // 지금 돌 높이 (지점 기준)
        float offsetZ;       // 지금 돌이 지점보다 앞쪽에 있는 거리 (FromFront)
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
            CacheWarning(warning);
            impactDistance = LocalAnchor.z;
            Tick(false, 0f);
        }

        public override void Tick(bool flying, float distance)
        {
            // 기본 속도 기준 "도착까지 남은 시간" (거리로 환산하므로 속도가 바뀌어도 도착 순간에 정확히 맞는다)
            float untilImpact = Course != null ? Course.TimeToReach(impactDistance - distance) : 0f;
            bool shattered = shatterAt >= 0f;
            if (approach == ApproachMode.FromFront)
            {
                offsetZ = untilImpact * flySpeed;
                offsetY = Mathf.Max(0f, offsetZ) * flyDrop;
                visible = flying && offsetZ < appearDistance && offsetZ > -25f;
            }
            else
            {
                offsetZ = 0f;
                offsetY = untilImpact * fallSpeed;
                visible = flying && offsetY < appearHeight && offsetY > -40f;
            }
            if (rock != null)
            {
                bool show = visible && !shattered;
                if (rock.gameObject.activeSelf != show) rock.gameObject.SetActive(show);
                if (show)
                {
                    Vector3 fwd = Course != null ? Course.transform.forward : Vector3.forward;
                    rock.position = transform.position + transform.up * offsetY + fwd * offsetZ;
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
                    SetWarnProgress(k);
                    if (scaleWarning)
                    {
                        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, k));
                        float s = Mathf.Lerp(0.4f, 1f, k) * pulse;
                        warning.localScale = new Vector3(warningBaseScale.x * s, warningBaseScale.y, warningBaseScale.z * s);
                        warning.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
                    }
                }
            }
        }

        public override bool Overlaps(Vector3 raftCenter, Vector3 half)
        {
            if (!visible || HitThisAttempt) return false;
            Vector3 c = LocalAnchor + new Vector3(0f, offsetY, offsetZ);
            return SphereBox(c, radius, raftCenter, half);
        }

        public override void PlayHit()
        {
            shatterAt = Time.time;
            Vector3 at = rock != null ? rock.position : transform.position;
            if (!SpawnHitVfx(HitPoint(at, radius)))
                FlightFx.Burst(at, new Color(0.6f, 0.45f, 0.3f), radius * 2.5f);
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
