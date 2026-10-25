using System;
using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IPlayerSimulation
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField] PlayerInputReader input;
        [SerializeField] GameObject cameraRig;      // SetLocal(false)이면 비활성화
        [SerializeField] Transform cameraTransform; // 이동 기준. 비어 있으면 Camera.main
        [SerializeField] Animator animator;
        [Tooltip("넉백을 일으키는 이동 장애물을 찾을 레이어 (C-09)")]
        [SerializeField] LayerMask obstacleMask = ~0;

        // 파라미터 이름을 바꾸면 서버 담당에게 알린다 (NetworkMecanimAnimator)
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int JumpHash = Animator.StringToHash("Jump");
        static readonly int AirborneHash = Animator.StringToHash("Airborne");

        CharacterController cc;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float simTime;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressedTime = float.NegativeInfinity;
        bool grounded;
        bool jumping;

        // C-07: 안 보이는 마법과 마지막으로 닿은 시간. 계속 닿아 있으면 갱신만 하고 다시 터뜨리지 않는다.
        const float InvisibleHitCooldown = 0.2f;
        const float LandingNormalY = 0.7f;
        readonly Dictionary<Collider, MagicVisibility> magicCache = new Dictionary<Collider, MagicVisibility>();
        readonly Dictionary<Collider, float> lastInvisibleContact = new Dictionary<Collider, float>();

        // C-08: 밟고 있는 이동 장애물. 다음 틱에 그 속도만큼 함께 옮긴다.
        readonly Dictionary<Collider, ClockDrivenObstacle> obstacleCache = new Dictionary<Collider, ClockDrivenObstacle>();
        ClockDrivenObstacle groundObstacle;

        // C-09: 이동 장애물에 맞았는지는 매 틱 몸 주변을 겹침 검사한다.
        // 장애물이 다가와서 부딪히는 경우는 OnControllerColliderHit가 불리지 않기 때문이다.
        const float HitProbePadding = 0.05f;
        const float MinObstacleSpeed = 0.2f;      // 이보다 느리면 정지 장애물로 보고 막기만 한다
        const float ObstacleHitCooldown = 0.3f;   // 같은 장애물에 다시 맞기까지
        const float KnockbackMinAirTime = 0.1f;
        readonly Collider[] hitBuffer = new Collider[8];
        readonly Dictionary<ClockDrivenObstacle, float> lastObstacleHit = new Dictionary<ClockDrivenObstacle, float>();
        Vector3 pushVelocity;
        Vector3 knockbackHorizontal;
        float knockbackStartTime;
        bool knockedBack;

        // C-10: 죽은 동안에는 시뮬레이션을 멈춘다. 부활은 PlayerRespawn이 Respawn()으로 한다.
        bool dead;

        public bool IsLocal { get; private set; }
        public bool IsDead => dead;
        public bool IsGrounded => grounded;
        public bool IsKnockedBack => knockedBack;
        public PlayerTuning Tuning => tuning;

        public event Action<Vector3> Teleported;
        public event Action<PlayerEvent> EventRaised;
        // 로컬 전용 (네트워크로 보내지 않는다). 위치, 법선, 착지 여부
        public event Action<Vector3, Vector3, bool> InvisibleHit;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            SetLocal(false);
        }

        public void SetLocal(bool isLocal)
        {
            IsLocal = isLocal;
            if (input != null) input.enabled = isLocal;
            if (cameraRig != null) cameraRig.SetActive(isLocal);
            if (!isLocal)
            {
                horizontalVelocity = Vector3.zero;
                verticalVelocity = 0f;
            }
        }

        public void Simulate(float deltaTime)
        {
            if (!IsLocal || deltaTime <= 0f) return;
            simTime += deltaTime;
            if (dead) return;

            // 강한 넉백 중에는 점프를 받지 않는다 (버퍼에도 남기지 않음)
            if (input.ConsumeJump() && !knockedBack) lastJumpPressedTime = simTime;

            UpdateHorizontal(deltaTime);
            UpdateVertical(deltaTime);

            Vector3 motion = (horizontalVelocity + pushVelocity + Vector3.up * verticalVelocity) * deltaTime;
            if (grounded && groundObstacle != null) motion += groundObstacle.GetVelocityAt(transform.position) * deltaTime;
            groundObstacle = null; // cc.Move 안의 OnControllerColliderHit가 다시 채운다
            cc.Move(motion);
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, tuning.lightPushSpeed / Mathf.Max(0.01f, tuning.lightPushDuration) * deltaTime);

            bool wasGrounded = grounded;
            grounded = cc.isGrounded;
            // 넉백 직후에는 장애물에 눌려 바닥에 닿아도 착지로 보지 않는다 (Landed가 바로 나면 연출이 끊긴다)
            if (grounded && knockedBack && simTime - knockbackStartTime < KnockbackMinAirTime) grounded = false;
            if (grounded)
            {
                lastGroundedTime = simTime;
                if (verticalVelocity < 0f) verticalVelocity = -2f; // 경사·계단에서 바닥에 붙어 있게
                jumping = false;
                knockedBack = false;
                if (!wasGrounded) Raise(PlayerEvent.Landed);
            }
            else if (verticalVelocity > 0f && (cc.collisionFlags & CollisionFlags.Above) != 0)
            {
                verticalVelocity = 0f; // 천장에 머리를 박으면 바로 낙하
            }

            CheckObstacleHits();
            UpdateFacing(deltaTime);
            UpdateAnimator();
        }

        void UpdateHorizontal(float dt)
        {
            if (knockedBack)
            {
                // 날아가는 속도는 유지하고, 입력은 아주 조금만 더한다 → 방향을 뒤집을 수 없다
                Vector3 steer = knockbackHorizontal + GetMoveDirection() * (tuning.moveSpeed * tuning.airControlDuringKnockback);
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, steer, tuning.acceleration * dt);
                return;
            }
            Vector3 target = GetMoveDirection() * tuning.moveSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, tuning.acceleration * dt);
        }

        void UpdateVertical(float dt)
        {
            bool buffered = simTime - lastJumpPressedTime <= tuning.jumpBuffer;
            bool coyote = simTime - lastGroundedTime <= tuning.coyoteTime;
            if (buffered && coyote && !jumping && !knockedBack)
            {
                verticalVelocity = Mathf.Sqrt(2f * tuning.jumpHeight * -tuning.gravity);
                lastJumpPressedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                jumping = true;
                if (animator != null) animator.SetTrigger(JumpHash);
            }
            verticalVelocity += tuning.gravity * dt;
        }

        Vector3 GetMoveDirection()
        {
            Vector2 move = input.Move;
            if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

            Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(cam.up, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return forward * move.y + right * move.x;
        }

        void UpdateFacing(float dt)
        {
            Vector3 dir = horizontalVelocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, tuning.turnSpeed * dt);
        }

        void UpdateAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            Vector3 flat = horizontalVelocity;
            flat.y = 0f;
            animator.SetFloat(SpeedHash, flat.magnitude);
            animator.SetBool(GroundedHash, grounded);
            animator.SetBool(AirborneHash, !grounded);
        }

        // 부활 등 순간이동. CharacterController를 끄고 옮긴 뒤 다시 켠다.
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            jumping = false;
            groundObstacle = null;
            knockedBack = false;
            pushVelocity = Vector3.zero;
            Teleported?.Invoke(position);
        }

        public void Die()
        {
            if (dead) return;
            dead = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            knockedBack = false;
            pushVelocity = Vector3.zero;
            Raise(PlayerEvent.Died);
        }

        // Teleported → Respawned 순서로 정확히 한 번씩 발생한다
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            Teleport(position, rotation);
            grounded = false;
            dead = false;
            Raise(PlayerEvent.Respawned);
        }

        void CheckObstacleHits()
        {
            Vector3 center = transform.position + cc.center;
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * half, center - Vector3.up * half,
                cc.radius + cc.skinWidth + HitProbePadding, hitBuffer, obstacleMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = hitBuffer[i];
                if (col == cc) continue;
                ClockDrivenObstacle obstacle = FindObstacle(col);
                if (obstacle == null || obstacle == groundObstacle) continue; // 타고 있는 발판은 넉백하지 않는다
                if (lastObstacleHit.TryGetValue(obstacle, out float last) && simTime - last < ObstacleHitCooldown) continue;

                Vector3 point = col.ClosestPoint(center);
                Vector3 v = obstacle.GetVelocityAt(point);
                Vector3 away = center - point;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = new Vector3(v.x, 0f, v.z); // 몸 안쪽까지 들어온 경우
                // 정지했거나 멀어지는 장애물은 이동만 막는다
                if (v.sqrMagnitude < MinObstacleSpeed * MinObstacleSpeed || Vector3.Dot(v, away) <= 0f) continue;

                lastObstacleHit[obstacle] = simTime;
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                Vector3 dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : away.normalized;
                if (v.magnitude >= tuning.heavySpeedThreshold) StartHeavyKnockback(dir);
                else
                {
                    pushVelocity = dir * tuning.lightPushSpeed; // 조작은 그대로
                    Raise(PlayerEvent.KnockbackLight);
                }
                return; // 한 틱에 하나만
            }
        }

        void StartHeavyKnockback(Vector3 dir)
        {
            knockbackHorizontal = dir * tuning.heavyKnockbackSpeed;
            horizontalVelocity = knockbackHorizontal;
            verticalVelocity = tuning.heavyKnockbackUp;
            pushVelocity = Vector3.zero;
            knockedBack = true;
            knockbackStartTime = simTime;
            grounded = false; // 다음 착지에서 Landed가 나도록
            jumping = false;
            lastGroundedTime = float.NegativeInfinity;
            lastJumpPressedTime = float.NegativeInfinity;
            Raise(PlayerEvent.KnockbackHeavy);
        }

        ClockDrivenObstacle FindObstacle(Collider col)
        {
            if (!obstacleCache.TryGetValue(col, out ClockDrivenObstacle obstacle))
            {
                obstacle = col.GetComponentInParent<ClockDrivenObstacle>();
                obstacleCache[col] = obstacle;
            }
            return obstacle;
        }

        // cc.Move 안에서 불린다
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsLocal) return;
            Collider col = hit.collider;
            bool landing = hit.normal.y >= LandingNormalY;

            if (landing)
            {
                ClockDrivenObstacle obstacle = FindObstacle(col);
                if (obstacle != null) groundObstacle = obstacle;
            }

            if (!magicCache.TryGetValue(col, out MagicVisibility magic))
            {
                magic = col.GetComponentInParent<MagicVisibility>();
                magicCache[col] = magic;
            }
            if (magic == null || magic.IsVisibleToLocal) return;

            bool fresh = !lastInvisibleContact.TryGetValue(col, out float last) || simTime - last > InvisibleHitCooldown;
            lastInvisibleContact[col] = simTime;
            if (!fresh) return;

            InvisibleHit?.Invoke(hit.point, hit.normal, landing);
            Raise(PlayerEvent.HitInvisible);
        }

        void Raise(PlayerEvent e) => EventRaised?.Invoke(e);
    }
}
