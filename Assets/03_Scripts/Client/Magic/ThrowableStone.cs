using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18: 줍고 던지는 돌멩이 (활 대신). E로 줍고 E로 카메라 방향으로 던진다.
    // 숨겨진 발판(RevealablePlatform)에 맞으면 그 발판이 잠시 보인다.
    // 돌이 박히는 발판(StoneStickPlatform)에 맞으면 발판이 계속 보이고 돌은 그 자리에 붙는다 (다시 못 주움).
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
        float radius = 0.25f;
        bool stuck; // 권한 PC: 지금 발판에 붙어 물리를 멈춘 상태
        float stuckAt = float.NegativeInfinity;

        public PlayerRole Holder => State != null ? State.Holder : PlayerRole.None;
        // 드러난 "돌이 박히는 발판"에 붙어 있는가 (모든 PC에서 같은 답)
        public bool Stuck => Holder == PlayerRole.None && StoneStickPlatform.AnyHolds(transform.position, radius);

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
            if (colliders.Length > 0)
            {
                var e = colliders[0].bounds.extents;
                radius = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            }
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
                if (s == null || s.Holder != PlayerRole.None || s.Stuck) continue;
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
            bool dynamic = authority && !held && !stuck;
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
            if (State != null && State.HasAuthority)
            {
                // 붙음 상태를 규칙과 맞춘다: 방장이 바뀌었거나(늦게 권한 받음) 구역 리셋으로 발판이 숨으면 풀린다
                // 붙인 직후 잠깐은 발판 켜짐이 상태에 반영되기를 기다린다
                bool should = Stuck || (stuck && Time.time - stuckAt < 0.5f);
                if (should != stuck) { stuck = should; if (stuck) stuckAt = Time.time; ApplyPhysics(); }
            }
            if (stuck) return;
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
            if (Holder != PlayerRole.None || State == null || !State.HasAuthority || stuck) return;
            var platform = other.GetComponentInParent<RevealablePlatform>();
            if (platform != null) platform.Reveal(lastHolder);
            var sticky = StoneStickPlatform.Of(other);
            if (sticky != null) StickTo(sticky, other);
        }

        // 권한 PC: 맞은 자리에 붙인다. 물리를 멈추고 위치는 NetworkTransform으로 다른 PC에 전달된다
        void StickTo(StoneStickPlatform sticky, Collider hit)
        {
            Vector3 p = sticky.SnapPoint(hit, transform.position, radius);
            rb.linearVelocity = rb.angularVelocity = Vector3.zero;
            stuck = true;
            stuckAt = Time.time;
            ApplyPhysics();
            rb.position = p;
            transform.position = p;
        }
    }
}
