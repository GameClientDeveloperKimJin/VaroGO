using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18: 밀어서 옮기는 돌·바위·통나무. 플레이어가 밀기 키(F)를 누른 채 앞으로 걸으면 미는 방향을 알린다.
    // 권한 PC(오프라인 = 나, 온라인 = 방장)만 움직이고, 위치는 NetworkTransform이 맞춘다.
    // 큰 바위: requiredPushers = 2 → 두 명이 같은 방향으로 동시에 밀 때만 움직이고, 한 명이라도 손을 떼면 멈춘다
    [RequireComponent(typeof(Rigidbody))]
    public class Pushable : MonoBehaviour
    {
        static readonly List<Pushable> all = new List<Pushable>();
        public static IReadOnlyList<Pushable> All => all;

        [Tooltip("움직이려면 동시에 밀어야 하는 인원. 작은 돌 = 1, 큰 바위 = 2")]
        [SerializeField, Range(1, 2)] int requiredPushers = 1;
        [Tooltip("밀릴 때 이동 속도 (m/s)")]
        [SerializeField] float moveSpeed = 1.6f;
        [Tooltip("두 명이 밀 때 '같은 방향'으로 보는 최대 각도 차이 (도)")]
        [SerializeField] float sameDirectionAngle = 35f;
        [Tooltip("미는 신호가 이 시간(초) 동안 안 오면 손을 뗀 것으로 본다")]
        [SerializeField] float holdTimeout = 0.3f;
        [Tooltip("켜면 앞·뒤·좌·우 4방향으로만 밀린다 (상자·바위 퍼즐용). 끄면 미는 방향 그대로")]
        [SerializeField] bool snapToAxes = true;
        [Tooltip("오프라인 테스트에서 2인 바위도 혼자 밀 수 있게 한다 (온라인에는 영향 없음)")]
        [SerializeField] bool offlineSoloCounts = true;
        [Tooltip("받침이 없을 때 떨어지는 중력 가속도")]
        [SerializeField] float fallGravity = 20f;
        [SerializeField] float maxFallSpeed = 20f;
        [Tooltip("이 거리보다 아래로 떨어지면 처음 자리로 돌아온다 (m)")]
        [SerializeField] float resetFallDistance = 30f;
        [Tooltip("상태 오브젝트. 비우면 자기 자신")]
        [SerializeField] GameObject stateObject;

        struct PushInput { public Vector3 dir; public float time; }
        readonly Dictionary<PlayerRole, PushInput> pushes = new Dictionary<PlayerRole, PushInput>();

        Rigidbody rb;
        Collider[] solids;
        readonly RaycastHit[] castHits = new RaycastHit[16];
        IPushableState state;
        bool subscribed;
        float fallSpeed;
        Vector3 startPos;
        Quaternion startRot;

        public int RequiredPushers => requiredPushers;
        public bool Moving { get; private set; }
        // 지금 밀고 있는 인원 (안내 표시용, 권한 PC 기준)
        public int ActivePushers { get; private set; }

        IPushableState State
        {
            get
            {
                if (state == null)
                {
                    state = SwitchStates.Pick<IPushableState, LocalPushableState>(stateObject != null ? stateObject : gameObject, null, this);
                    if (state != null && !subscribed)
                    {
                        state.PushReported += OnPushReported;
                        subscribed = true;
                    }
                }
                return state;
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var list = new List<Collider>();
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && c.attachedRigidbody == rb) list.Add(c);
            solids = list.ToArray();
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
            if (state != null && subscribed) state.PushReported -= OnPushReported;
            state = null;
            subscribed = false;
        }

        // 온라인/오프라인이 바뀌면 상태 구현을 다시 고른다
        void ResetState()
        {
            if (state != null && subscribed) state.PushReported -= OnPushReported;
            state = null;
            subscribed = false;
            pushes.Clear();
        }

        // 미는 로컬 PC에서 호출 (R4). dir = 0이면 손 뗌
        public void Report(PlayerRole role, Vector3 dir)
        {
            var s = State;
            if (s != null) s.ReportPush(role, dir);
        }

        void OnPushReported(PlayerRole role, Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) { pushes.Remove(role); return; }
            dir.y = 0f;
            pushes[role] = new PushInput { dir = dir.normalized, time = Time.time };
        }

        // 플레이어 정면을 미는 방향으로 바꾼다
        public Vector3 SnapDirection(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return Vector3.zero;
            forward.Normalize();
            if (!snapToAxes) return forward;
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            float df = Vector3.Dot(forward, f), dr = Vector3.Dot(forward, r);
            return Mathf.Abs(df) >= Mathf.Abs(dr) ? f * Mathf.Sign(df) : r * Mathf.Sign(dr);
        }

        void FixedUpdate()
        {
            var s = State;
            bool authority = s != null && s.HasAuthority;
            // 권한이 없으면 위치는 NetworkTransform이 정한다. 보간이 서로 싸우지 않게 끈다
            var interp = authority ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            if (rb.interpolation != interp) rb.interpolation = interp;
            if (!authority) { Moving = false; return; }

            float dt = Time.fixedDeltaTime;
            Vector3 dir = PushDirection();
            Vector3 pos = rb.position;
            Vector3 step = Vector3.zero;

            if (dir != Vector3.zero)
            {
                float dist = moveSpeed * dt;
                if (Free(dir, dist + 0.02f, true)) step += dir * dist;
            }
            Moving = step != Vector3.zero;

            // 받침이 없으면 떨어진다 (절벽 아래로 밀어 다리 만들기)
            float down = (fallSpeed + fallGravity * dt) * dt;
            if (Free(Vector3.down, Mathf.Max(down, 0.02f) + 0.02f, false))
            {
                fallSpeed = Mathf.Min(maxFallSpeed, fallSpeed + fallGravity * dt);
                step += Vector3.down * fallSpeed * dt;
            }
            else fallSpeed = 0f;

            if (step != Vector3.zero) rb.MovePosition(pos + step);
            if (startPos.y - (pos + step).y > resetFallDistance)
            {
                fallSpeed = 0f;
                rb.position = startPos;
                rb.rotation = startRot;
                transform.SetPositionAndRotation(startPos, startRot);
            }
        }

        Vector3 PushDirection()
        {
            float now = Time.time;
            Vector3 sum = Vector3.zero;
            Vector3 first = Vector3.zero;
            int count = 0;
            bool aligned = true;
            foreach (var kv in pushes)
            {
                if (now - kv.Value.time > holdTimeout) continue;
                if (count == 0) first = kv.Value.dir;
                else if (Vector3.Angle(first, kv.Value.dir) > sameDirectionAngle) aligned = false;
                sum += kv.Value.dir;
                count++;
            }
            ActivePushers = count;
            int need = requiredPushers;
            if (!GameServices.IsOnline && offlineSoloCounts) need = 1;
            if (count < need || !aligned || sum.sqrMagnitude < 0.0001f) return Vector3.zero;
            return sum.normalized;
        }

        // dir 방향으로 dist만큼 갈 수 있는가. 바닥(위를 보는 면)은 옆으로 갈 때 막지 않는다
        // SweepTest는 이미 맞닿은 면(거리 0)을 못 찾아 바닥에 놓인 채 빠져 버린다. 조금 줄인 상자를 중심에서 쏜다
        bool Free(Vector3 dir, float dist, bool sideways)
        {
            const float margin = 0.05f;
            if (solids == null || solids.Length == 0) return true;
            Bounds b = solids[0].bounds;
            for (int i = 1; i < solids.Length; i++) b.Encapsulate(solids[i].bounds);
            Vector3 half = Vector3.Max(b.extents - Vector3.one * margin, Vector3.one * 0.01f);
            int count = Physics.BoxCastNonAlloc(b.center, half, dir, castHits, Quaternion.identity, dist + margin, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var h = castHits[i];
                if (h.collider == null || h.collider.attachedRigidbody == rb) continue;
                if (sideways && h.normal.y > 0.5f) continue;
                if (!sideways && h.collider is CharacterController) continue; // 떨어질 때 사람은 받침으로 치지 않는다
                return false;
            }
            return true;
        }
    }
}
