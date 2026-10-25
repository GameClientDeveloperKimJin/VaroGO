using UnityEngine;

namespace VaroGO.Client
{
    [CreateAssetMenu(menuName = "VaroGO/PlayerTuning")]
    public class PlayerTuning : ScriptableObject
    {
        [Header("이동")]
        [Tooltip("최고 이동 속도 (m/s)")]
        public float moveSpeed = 6f;
        [Tooltip("최고 속도까지 가속·감속하는 정도 (m/s²). 클수록 반응이 즉각적이다")]
        public float acceleration = 40f;
        [Tooltip("이동 방향으로 몸을 돌리는 속도 (초당 각도)")]
        public float turnSpeed = 720f;

        [Header("점프")]
        [Tooltip("점프 최고 높이 (m)")]
        public float jumpHeight = 1.6f;
        [Tooltip("중력 가속도 (m/s², 음수). 클수록 빨리 떨어진다")]
        public float gravity = -25f;
        [Tooltip("발판에서 떨어진 직후에도 점프를 허용하는 시간 (초)")]
        public float coyoteTime = 0.1f;
        [Tooltip("착지 직전에 누른 점프를 기억해 두는 시간 (초)")]
        public float jumpBuffer = 0.1f;

        [Header("카메라 (C-04)")]
        [Tooltip("마우스 1픽셀 이동당 카메라 회전 각도")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("오른쪽 스틱을 끝까지 기울였을 때 카메라 회전 속도 (초당 각도)")]
        public float stickLookSpeed = 180f;
        [Tooltip("켜면 마우스·스틱 위아래 방향을 반대로 한다")]
        public bool invertY = false;
        [Tooltip("켜면 벽이 캐릭터를 가릴 때 카메라를 벽 앞으로 당긴다. 기획 미정")]
        public bool cameraAvoidObstacles = true;

        [Header("넉백 (C-09)")]
        [Tooltip("약한 장애물에 밀릴 때 속도 (m/s)")]
        public float lightPushSpeed = 3f;
        [Tooltip("강한 넉백의 수평 속도 (m/s)")]
        public float heavyKnockbackSpeed = 14f;
        [Tooltip("강한 넉백의 위로 튀는 속도 (m/s)")]
        public float heavyKnockbackUp = 6f;
        [Tooltip("넉백 중 공중 조작 비율 (0~1). 매우 작게 둔다")]
        [Range(0f, 1f)]
        public float airControlDuringKnockback = 0.08f;
        [Tooltip("장애물 속도가 이 값(m/s)을 넘으면 강한 넉백")]
        public float heavySpeedThreshold = 5f;
    }
}
