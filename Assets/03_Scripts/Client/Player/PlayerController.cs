using System;
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

        public bool IsLocal { get; private set; }
        public bool IsGrounded => grounded;
        public PlayerTuning Tuning => tuning;

        public event Action<Vector3> Teleported;
        public event Action<PlayerEvent> EventRaised;

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

            if (input.ConsumeJump()) lastJumpPressedTime = simTime;

            UpdateHorizontal(deltaTime);
            UpdateVertical(deltaTime);

            Vector3 motion = (horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime;
            cc.Move(motion);

            bool wasGrounded = grounded;
            grounded = cc.isGrounded;
            if (grounded)
            {
                lastGroundedTime = simTime;
                if (verticalVelocity < 0f) verticalVelocity = -2f; // 경사·계단에서 바닥에 붙어 있게
                jumping = false;
                if (!wasGrounded) Raise(PlayerEvent.Landed);
            }
            else if (verticalVelocity > 0f && (cc.collisionFlags & CollisionFlags.Above) != 0)
            {
                verticalVelocity = 0f; // 천장에 머리를 박으면 바로 낙하
            }

            UpdateFacing(deltaTime);
            UpdateAnimator();
        }

        void UpdateHorizontal(float dt)
        {
            Vector3 target = GetMoveDirection() * tuning.moveSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, tuning.acceleration * dt);
        }

        void UpdateVertical(float dt)
        {
            bool buffered = simTime - lastJumpPressedTime <= tuning.jumpBuffer;
            bool coyote = simTime - lastGroundedTime <= tuning.coyoteTime;
            if (buffered && coyote && !jumping)
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
            Teleported?.Invoke(position);
        }

        void Raise(PlayerEvent e) => EventRaised?.Invoke(e);
    }
}
