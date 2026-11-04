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
        [Tooltip("약하게 밀린 속도가 0으로 줄어드는 시간 (초)")]
        public float lightPushDuration = 0.25f;
        [Tooltip("강한 넉백의 수평 속도 (m/s)")]
        public float heavyKnockbackSpeed = 14f;
        [Tooltip("강한 넉백의 위로 튀는 속도 (m/s)")]
        public float heavyKnockbackUp = 6f;
        [Tooltip("넉백 중 공중 조작 비율 (0~1). 매우 작게 둔다")]
        [Range(0f, 1f)]
        public float airControlDuringKnockback = 0.08f;
        [Tooltip("장애물 속도가 이 값(m/s)을 넘으면 강한 넉백")]
        public float heavySpeedThreshold = 5f;

        [Header("밀기 (C2-09)")]
        [Tooltip("파트너를 밀 수 있는 거리 (m, 몸 중심 사이)")]
        public float pushRange = 1.6f;
        [Tooltip("앞쪽 몇 도 안에 있어야 밀리는지 (좌우 각각)")]
        [Range(10f, 180f)]
        public float pushAngle = 70f;
        [Tooltip("한 번 밀고 다시 밀 수 있기까지 (초)")]
        public float pushCooldown = 1f;
        [Tooltip("밀린 쪽이 뒤로 밀려나는 속도 (m/s). 클수록 멀리 밀린다")]
        public float pushForce = 5f;
        [Tooltip("밀릴 때 위로 살짝 뜨는 속도 (m/s). 0이면 땅에 붙어 미끄러진다")]
        public float pushUp = 3f;

        [Header("물건 밀기 (P18)")]
        [Tooltip("밀기 키를 누른 채 앞으로 걸으면, 몸 앞 이 거리(m) 안의 돌·바위·통나무를 민다")]
        public float objectPushReach = 0.6f;
        [Tooltip("물건을 미는 동안 걷는 속도 (m/s)")]
        public float objectPushWalkSpeed = 1.8f;

        [Header("절벽 붙잡기 (P18)")]
        [Tooltip("끄면 떨어질 때 절벽 끝을 잡지 않는다")]
        public bool ledgeGrabEnabled = true;
        [Tooltip("몸 앞 몇 m 안의 벽 끝을 잡을 수 있는지")]
        public float ledgeReach = 0.5f;
        [Tooltip("손 높이(머리 근처)보다 이만큼(m) 위에 있는 끝까지 잡는다")]
        public float ledgeGrabAbove = 0.35f;
        [Tooltip("손 높이보다 이만큼(m) 아래에 있는 끝까지 잡는다. 클수록 빨리 떨어질 때도 잘 잡힌다")]
        public float ledgeGrabBelow = 0.6f;
        [Tooltip("매달린 뒤 올라가는 데 걸리는 시간 (초)")]
        public float ledgeClimbTime = 0.45f;
        [Tooltip("손을 놓은 뒤 다시 잡을 수 있기까지 (초)")]
        public float ledgeRegrabCooldown = 0.5f;
        [Tooltip("잡을 수 있는 벽 레이어")]
        public LayerMask ledgeMask = ~0;
        [Tooltip("걷다가·밀려서 절벽 끝에서 떨어지면 방금 밟던 끝에 매달린다")]
        public bool ledgeFallGrab = true;
        [Tooltip("끝 바깥 아래가 이 깊이(m)까지 비어 있어야 '절벽'으로 본다. 계단·턱·낮은 단차에서는 매달리지 않는다")]
        public float ledgeMinDrop = 5f;
        [Tooltip("절벽 끝에서 이 거리(m) 안에서 떨어질 때만 잡는다")]
        public float ledgeFallReach = 1.2f;
        [Tooltip("켜면 점프해서 앞쪽 벽 끝도 잡는다")]
        public bool ledgeJumpGrab = false;
        [Tooltip("켜면 매달린 사람이 혼자 올라가고(W/Space) 놓을(S) 수 있다. 끄면 파트너가 구해 줄 때까지 기다린다")]
        public bool ledgeSelfClimb = false;
        [Tooltip("매달린 파트너를 끌어올리려면 상호작용 키(E)를 몇 번 눌러야 하는지")]
        public int ledgeRescuePresses = 1;
        [Tooltip("매달린 파트너와 이 거리(m) 안에 있어야 끌어올릴 수 있다")]
        public float ledgeRescueRange = 2.2f;
        [Tooltip("0보다 크면 이 시간(초) 동안 아무도 안 구하면 손을 놓고 떨어진다. 0 = 계속 매달려 있음")]
        public float ledgeHangMaxSeconds = 0f;

        [Header("붙잡기 점프 (P18)")]
        [Tooltip("켜면 발판 끝자락에서, 일반 점프로 발은 못 올라서지만 손은 닿는 건너편 끝에 손 표시가 뜬다. 그때 점프하면 공중에서 그 끝을 붙잡는다 (점프 자체는 일반 점프)")]
        public bool ledgeAssist = true;
        [Tooltip("내 발판 끝이 몸 앞 이 거리(m) 안에 있어야 '끝자락'으로 본다. 클수록 끝에서 조금 멀어도 손 표시가 뜬다")]
        public float ledgeAssistEdgeDistance = 0.6f;
        [Tooltip("건너편 벽을 찾는 최대 수평 거리 (m). 실제로 뜨는지는 점프로 손이 닿는지로 정해진다")]
        public float ledgeAssistRange = 7f;
        [Tooltip("내 발보다 이만큼(m) 위에 있는 끝까지 본다")]
        public float ledgeAssistAbove = 2.5f;
        [Tooltip("내 발보다 이만큼(m) 아래에 있는 끝까지 본다")]
        public float ledgeAssistBelow = 2f;
        [Tooltip("켜면 일반 점프로 발이 올라설 수 있는 곳에는 손 표시를 띄우지 않는다")]
        public bool ledgeAssistOnlyUnreachable = true;
        [Tooltip("붙잡기 점프 전용 손 닿는 거리 보너스 (m). 손 표시가 뜬 점프에서만, 벽에서 이만큼 더 멀리서도 손을 뻗어 붙잡는다. 클수록 먼 절벽도 잡힌다 (일반 점프는 그대로)")]
        public float ledgeAssistExtraReach = 1f;
        [Tooltip("붙잡기 점프용: 앞 절벽 아래가 이 깊이(m)까지 비어 있어야 손 표시가 뜬다. 얕은 구덩이도 잡게 하려면 줄인다 (걷다 떨어질 때 붙잡기는 ledgeMinDrop을 따로 쓴다)")]
        public float ledgeAssistMinDrop = 2.5f;
        [Tooltip("손 표시 프리팹 (LedgeGrabHint)")]
        public GameObject ledgeAssistHint;
        [Tooltip("손 표시를 절벽 끝보다 이만큼(m) 위에 띄운다")]
        public float ledgeAssistHintHeight = 0.5f;

        [Header("돌멩이 줍기·던지기 (P18)")]
        [Tooltip("상호작용 키로 주울 수 있는 거리 (m)")]
        public float pickUpRange = 1.6f;
        [Tooltip("던지는 속도 (m/s)")]
        public float throwSpeed = 13f;
        [Tooltip("카메라가 보는 방향보다 이만큼(도) 위로 던진다")]
        public float throwUpAngle = 10f;
        [Tooltip("든 돌의 위치 (캐릭터 기준. x 오른쪽, y 위, z 앞)")]
        public Vector3 carryOffset = new Vector3(0f, 1.9f, 0.1f);

        [Header("묶임·구출 (P18)")]
        [Tooltip("묶인 파트너를 풀어주려면 A/D를 몇 번 번갈아 눌러야 하는지")]
        public int rescuePresses = 10;
        [Tooltip("파트너와 이 거리(m) 안에 있어야 A/D로 풀어줄 수 있다")]
        public float rescueRange = 2.2f;
        [Tooltip("풀린 뒤 다시 묶이지 않는 시간 (초)")]
        public float rescueImmunity = 1.5f;
        [Tooltip("둘 다 묶이면 이 시간(초) 뒤 체크포인트에서 다시 시작한다")]
        public float bothBoundRespawnDelay = 2f;
        [Tooltip("0보다 크면 이 시간(초)이 지나면 혼자서도 풀린다. 0 = 파트너가 풀어줘야만 풀림")]
        public float boundAutoFreeSeconds = 0f;
    }
}
