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

        [Header("다시 시작할 때 화면 페이드")]
        [Tooltip("뗏목이 부서져 출발점으로 돌아갈 때, 카메라가 날아가는 대신 화면을 검게 닫았다가 출발점에서 연다")]
        [SerializeField] bool respawnFade = true;
        [Tooltip("검게 닫히는 시간 (초). 다시 시작 직전에 닫히기 시작한다")]
        [SerializeField, Min(0.05f)] float respawnFadeOut = 0.5f;
        [Tooltip("검은 채로 기다리는 시간 (초). 이 동안 카메라가 출발점으로 옮겨진다")]
        [SerializeField, Min(0f)] float respawnHold = 0.25f;
        [Tooltip("다시 밝아지는 시간 (초)")]
        [SerializeField, Min(0.05f)] float respawnFadeIn = 0.6f;

        CinemachineCamera vcam;
        float shakeUntil = -1f;
        bool active;
        int lastAttempt = -1;
        bool fadingOut;
        float snapUntil = -1f;

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
            // 다시 시작: Failed 끝무렵 화면을 닫고, 새 도전(Attempt 바뀜)이 되면 검은 동안 출발점에 바로 붙였다가 연다
            if (respawnFade)
            {
                if (phase == FlightPhase.Failed && !fadingOut && course.PhaseTime >= course.FailRestartDelay - respawnFadeOut)
                {
                    fadingOut = true;
                    ScreenFade.FadeOut(respawnFadeOut, false);
                }
                int attempt = course.Attempt;
                if (lastAttempt >= 0 && attempt != lastAttempt && fadingOut)
                {
                    ScreenFade.Set(1f);
                    snapUntil = Time.unscaledTime + respawnHold;
                }
                lastAttempt = attempt;
                if (snapUntil > 0f)
                {
                    if (active) transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                    vcam.PreviousStateIsValid = false;
                    if (Time.unscaledTime >= snapUntil)
                    {
                        snapUntil = -1f;
                        fadingOut = false;
                        ScreenFade.FadeIn(respawnFadeIn);
                    }
                    return;
                }
                // 페이드 도중 다시 시작 없이 다른 단계로 넘어가면(나가기 등) 화면을 연다
                if (fadingOut && phase != FlightPhase.Failed)
                {
                    fadingOut = false;
                    ScreenFade.FadeIn(respawnFadeIn);
                }
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
