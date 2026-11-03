using Unity.Cinemachine;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P19 비행 카메라. 소환 연출(옆에서 조립 장면)과 비행 중(뗏목 뒤 위)에만 켜지고, 나머지는 평소 플레이어 카메라.
    // 같은 오브젝트의 CinemachineCamera를 켜고 끄며, 위치는 여기서 직접 정한다. 맞으면 흔들린다.
    [RequireComponent(typeof(CinemachineCamera))]
    public class FlightCamera : MonoBehaviour
    {
        [SerializeField] FlightCourse course;
        [Tooltip("비행 중 뗏목 기준 카메라 위치 (위, 뒤)")]
        [SerializeField] Vector3 followOffset = new Vector3(0f, 5f, -11f);
        [Tooltip("뗏목보다 얼마나 앞을 볼지 (m)")]
        [SerializeField, Min(0f)] float lookAhead = 10f;
        [Tooltip("소환 연출 때 뗏목 기준 카메라 위치")]
        [SerializeField] Vector3 summonOffset = new Vector3(7f, 4f, -7f);
        [Tooltip("따라가는 부드러움 (클수록 바로 붙음)")]
        [SerializeField, Min(0.5f)] float follow = 6f;
        [Tooltip("맞았을 때 흔들림 세기 (m)")]
        [SerializeField, Min(0f)] float shakeAmount = 0.5f;
        [SerializeField, Min(0f)] float shakeTime = 0.4f;

        CinemachineCamera vcam;
        float shakeUntil = -1f;
        bool active;

        void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
            if (course == null) course = FindAnyObjectByType<FlightCourse>();
            vcam.Priority = 50;
            vcam.enabled = false;
        }

        void OnEnable() { if (course != null) course.Hit += OnHit; }
        void OnDisable() { if (course != null) course.Hit -= OnHit; }

        void OnHit(int left) => shakeUntil = Time.time + shakeTime;

        void LateUpdate()
        {
            if (course == null || course.State == null || course.Raft == null) return;
            var phase = course.Phase;
            bool summonView = phase == FlightPhase.Summoning && course.PhaseTime > 1.2f && course.PhaseTime < course.SummonDuration - 0.3f;
            bool want = course.ShouldUseFlightCam || summonView;
            var raft = course.Raft.transform;
            var rot = course.transform.rotation;

            Vector3 pos, look;
            if (summonView)
            {
                pos = raft.position + rot * summonOffset;
                look = raft.position + Vector3.up * 0.5f;
            }
            else
            {
                pos = raft.position + rot * followOffset;
                look = raft.position + rot * new Vector3(0f, 1f, lookAhead);
            }

            if (want != active)
            {
                active = want;
                vcam.enabled = want;
                if (want) transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            }
            if (!active) return;

            float k = 1f - Mathf.Exp(-follow * Time.deltaTime);
            var p = Vector3.Lerp(transform.position, pos, k);
            var r = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - p), k);
            if (Time.time < shakeUntil)
            {
                float a = shakeAmount * (shakeUntil - Time.time) / Mathf.Max(0.01f, shakeTime);
                p += new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
            }
            transform.SetPositionAndRotation(p, r);
        }
    }
}
