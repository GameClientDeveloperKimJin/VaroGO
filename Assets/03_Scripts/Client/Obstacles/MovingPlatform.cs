using UnityEngine;

namespace VaroGO.Client
{
    // 사람이 타는 발판. 시작 위치와 시작 위치 + travel 사이를 부드럽게 오가고, 양 끝에서 잠깐 멈춘다.
    // 위에 선 플레이어는 PlayerController가 GetVelocityAt으로 함께 옮긴다.
    public class MovingPlatform : ClockDrivenObstacle
    {
        [Tooltip("도착 지점까지의 이동량 (오브젝트 로컬)")]
        [SerializeField] Vector3 travel = new Vector3(0f, 0f, 6f);
        [Tooltip("한쪽 끝에서 다른 쪽 끝까지 가는 시간 (초)")]
        [SerializeField] float moveTime = 3f;
        [Tooltip("양 끝에서 멈춰 있는 시간 (초)")]
        [SerializeField] float pauseTime = 1f;

        protected override void Evaluate(double t, out Vector3 position, out Quaternion rotation)
        {
            float leg = moveTime + pauseTime;
            float p = (float)Repeat(t, leg * 2f);
            float amount = p < leg
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / moveTime))
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - leg) / moveTime));
            position = originPos + originRot * (travel * amount);
            rotation = originRot;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 o = Application.isPlaying ? originPos : transform.position;
            Quaternion r = Application.isPlaying ? originRot : transform.rotation;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(o, o + r * travel);
            Gizmos.DrawWireCube(o + r * travel, Vector3.one * 0.3f);
        }
    }
}
