using System;
using UnityEngine;

namespace VaroGO.Client
{
    // P19 천둥 번개. 이 오브젝트 위치가 "번개가 떨어지는 지점"이다 (코스 기준 x = 좌우, z = 거리).
    // 뗏목(비행기)이 이 지점 앞뒤 거리에 도착하는 순간 하늘에서 세로 기둥으로 내려친다.
    // 그 전에 warnTime초 동안 지점에 경고 표시가 깜빡인다 → 좌우로 피한다.
    // 맞으면 내구도와 상관없이 출발 위치로 돌아간다 (FlightCourse.SendBackToStart).
    // 시간은 남은 거리로 정하므로 모든 PC에서 같은 순간에 친다. 비행 한 번에 1~2개 배치를 권장.
    // 자식: Bolt(번개 줄기, 세로로 긴 모양), Warning(경고 표시), Flash(선택, Light)
    public class LightningStrike : FlightHazard
    {
        [Tooltip("번개 기둥 반지름 (m). 판정 크기")]
        [SerializeField, Min(0.3f)] float radius = 1.5f;
        [Tooltip("내려치기 전 경고가 보이는 시간 (초, 뗏목이 기본 속도일 때. 빨라지면 짧아진다)")]
        [SerializeField, Min(0.3f)] float warnTime = 1.8f;
        [Tooltip("번개가 남아 있는 시간 (초). 이 동안 닿으면 맞는다")]
        [SerializeField, Min(0.05f)] float boltTime = 0.35f;
        [Tooltip("번쩍임 Light 최대 밝기")]
        [SerializeField, Min(0f)] float flashIntensity = 8f;
        [SerializeField] Transform bolt;
        [SerializeField] Transform warning;
        [SerializeField] Light flash;

        // 모든 PC. 번개가 내려친 순간 (화면 번쩍임·천둥 소리용). 인자 = 내려친 위치
        public static event Action<Vector3> Struck;

        float impactDistance;
        Vector3 warningBaseScale = Vector3.one;
        bool striking;
        bool struckThisAttempt;

        public override bool SendsBackToStart => true;

        public override void Bind(FlightCourse course)
        {
            base.Bind(course);
            if (bolt == null) bolt = transform.Find("Bolt");
            if (warning == null) warning = transform.Find("Warning");
            if (flash == null) flash = GetComponentInChildren<Light>(true);
            if (warning != null) warningBaseScale = warning.localScale;
            impactDistance = LocalAnchor.z;
            Tick(false, 0f);
        }

        public override void Tick(bool flying, float distance)
        {
            float untilImpact = Course != null ? Course.TimeToReach(impactDistance - distance) : 999f;
            bool warn = flying && untilImpact < warnTime && untilImpact > 0f;
            striking = flying && untilImpact <= 0f && untilImpact > -boltTime;

            if (striking && !struckThisAttempt)
            {
                struckThisAttempt = true;
                Struck?.Invoke(transform.position);
                FlightFx.Burst(transform.position, new Color(0.75f, 0.85f, 1f), radius * 3f, 0.5f);
                var cam = Camera.main;
                AudioSource.PlayClipAtPoint(TempSfx.Thud, cam != null ? cam.transform.position : transform.position, 1f);
            }

            if (warning != null)
            {
                if (warning.gameObject.activeSelf != warn) warning.gameObject.SetActive(warn);
                if (warn)
                {
                    float k = 1f - Mathf.Clamp01(untilImpact / warnTime);
                    float blink = Mathf.Repeat(Time.time * Mathf.Lerp(3f, 14f, k), 1f) < 0.6f ? 1f : 0.75f;
                    float s = Mathf.Lerp(0.5f, 1f, k) * blink;
                    warning.localScale = new Vector3(warningBaseScale.x * s, warningBaseScale.y, warningBaseScale.z * s);
                }
            }
            if (bolt != null)
            {
                if (bolt.gameObject.activeSelf != striking) bolt.gameObject.SetActive(striking);
                if (striking)
                {
                    // 지글지글 떨리는 느낌
                    bolt.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-3f, 3f));
                }
            }
            if (flash != null)
            {
                float f = striking ? flashIntensity * (1f + untilImpact / boltTime) : 0f;
                flash.enabled = f > 0.01f;
                flash.intensity = f;
            }
        }

        public override bool Overlaps(Vector3 raftCenter, Vector3 half)
        {
            if (!striking || HitThisAttempt) return false;
            Vector3 c = LocalAnchor;
            // 세로 기둥: 높이는 상관없이 좌우·앞뒤만 본다
            return Mathf.Abs(raftCenter.x - c.x) < radius + half.x && Mathf.Abs(raftCenter.z - c.z) < radius + half.z;
        }

        public override void PlayHit()
        {
            FlightFx.Burst(transform.position, new Color(1f, 1f, 0.6f), radius * 4f, 0.7f);
        }

        public override void ResetAttempt()
        {
            base.ResetAttempt();
            struckThisAttempt = false;
            striking = false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.95f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 40f);
        }
    }
}
