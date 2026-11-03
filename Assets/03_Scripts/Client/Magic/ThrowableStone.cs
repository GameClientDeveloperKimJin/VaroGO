using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18: 줍고 던지는 돌멩이 (활 대신). E로 줍고 E로 카메라 방향으로 던진다.
    // 숨겨진 발판(RevealablePlatform)에 맞으면 그 발판이 잠시 보인다.
    // 날아가는 물리는 권한 PC(오프라인 = 나, 온라인 = 방장)만 돌리고, 위치는 NetworkTransform이 맞춘다
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableStone : MonoBehaviour
    {
        static readonly List<ThrowableStone> all = new List<ThrowableStone>();

        [Tooltip("이 거리보다 아래로 떨어지면 처음 자리로 돌아온다 (m)")]
        [SerializeField] float resetFallDistance = 25f;
        [Tooltip("상태 오브젝트. 비우면 자기 자신")]
        [SerializeField] GameObject stateObject;

        Rigidbody rb;
        Collider[] colliders;
        ICarryState state;
        bool subscribed;
        PlayerRole lastHolder;
        Vector3 startPos;
        Quaternion startRot;

        public PlayerRole Holder => State != null ? State.Holder : PlayerRole.None;

        ICarryState State
        {
            get
            {
                if (state == null)
                {
                    state = SwitchStates.Pick<ICarryState, LocalCarryState>(stateObject != null ? stateObject : gameObject, null, this);
                    if (state != null && !subscribed)
                    {
                        state.HolderChanged += OnHolderChanged;
                        state.Thrown += OnThrown;
                        subscribed = true;
                        OnHolderChanged();
                    }
                }
                return state;
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            startPos = transform.position;
            startRot = transform.rotation;
        }

        void OnEnable()
        {
            all.Add(this);
            GameServices.Registered += ResetState;
        }

        void OnDisable()
        {
            all.Remove(this);
            GameServices.Registered -= ResetState;
            Unsubscribe();
        }

        void ResetState()
        {
            Unsubscribe();
            ApplyPhysics();
        }

        void Unsubscribe()
        {
            if (state != null && subscribed)
            {
                state.HolderChanged -= OnHolderChanged;
                state.Thrown -= OnThrown;
            }
            state = null;
            subscribed = false;
        }

        // 바닥에 있고 누구도 들고 있지 않은 가장 가까운 돌멩이
        public static ThrowableStone FindNearest(Vector3 position, float range)
        {
            ThrowableStone best = null;
            float bestSqr = range * range;
            foreach (var s in all)
            {
                if (s == null || s.Holder != PlayerRole.None) continue;
                float d = (s.transform.position - position).sqrMagnitude;
                if (d > bestSqr) continue;
                best = s;
                bestSqr = d;
            }
            return best;
        }

        public void RequestPickUp(PlayerRole role) => State?.RequestPickUp(role);
        public void RequestThrow(PlayerRole role, Vector3 origin, Vector3 velocity) => State?.RequestThrow(role, origin, velocity);

        void OnHolderChanged()
        {
            var h = Holder;
            if (h != PlayerRole.None) lastHolder = h;
            ApplyPhysics();
        }

        // 권한 PC에서 바닥에 있을 때만 물리. 들고 있는 동안은 충돌 끔
        void ApplyPhysics()
        {
            if (rb == null) return;
            bool held = Holder != PlayerRole.None;
            bool authority = State != null && State.HasAuthority;
            bool dynamic = authority && !held;
            if (rb.isKinematic == dynamic)
            {
                if (!rb.isKinematic) rb.linearVelocity = rb.angularVelocity = Vector3.zero;
                rb.isKinematic = !dynamic;
            }
            rb.interpolation = authority ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            foreach (var c in colliders)
                if (c != null) c.enabled = !held;
        }

        void OnThrown(Vector3 origin, Vector3 velocity)
        {
            if (State == null || !State.HasAuthority) return;
            ApplyPhysics();
            rb.position = origin;
            transform.position = origin;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 4f;
        }

        void LateUpdate()
        {
            var h = Holder;
            if (h != PlayerRole.None)
            {
                var holder = PlayerController.FindByRole(h);
                if (holder != null)
                {
                    transform.SetPositionAndRotation(holder.CarryPoint, holder.transform.rotation);
                    if (rb.isKinematic) rb.position = transform.position;
                }
                return;
            }
            if (State != null && State.HasAuthority && startPos.y - transform.position.y > resetFallDistance)
            {
                rb.linearVelocity = rb.angularVelocity = Vector3.zero;
                rb.position = startPos;
                rb.rotation = startRot;
                transform.SetPositionAndRotation(startPos, startRot);
            }
        }

        void OnCollisionEnter(Collision collision) => TryReveal(collision.collider);

        // 숨겨진 발판이 통과형(트리거)일 때
        void OnTriggerEnter(Collider other) => TryReveal(other);

        void TryReveal(Collider other)
        {
            if (Holder != PlayerRole.None || State == null || !State.HasAuthority) return;
            var platform = other.GetComponentInParent<RevealablePlatform>();
            if (platform != null) platform.Reveal(lastHolder);
        }
    }
}
