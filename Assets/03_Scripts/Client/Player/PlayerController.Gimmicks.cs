using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18 상호작용·기믹: 물건 밀기, 절벽 붙잡기, 돌멩이 줍기·던지기, 묶임·구출(A/D), 수액 감속, 점프 버섯.
    // 판정은 모두 해당 플레이어의 PC에서 한다 (R4·R6). 원격 플레이어는 PlayerEvent로 연출만 따라 한다
    public partial class PlayerController
    {
        // 선택 Animator 파라미터. 컨트롤러에 없으면 건드리지 않는다 (이름을 바꾸면 서버 담당에게 알린다)
        static readonly int HangingHash = Animator.StringToHash("Hanging");
        static readonly int ClimbHash = Animator.StringToHash("Climb");
        static readonly int BoundHash = Animator.StringToHash("Bound");
        static readonly int StruggleHash = Animator.StringToHash("Struggle");
        static readonly int CarryingHash = Animator.StringToHash("Carrying");
        static readonly int PushingHash = Animator.StringToHash("Pushing");

        RuntimeAnimatorController animParamsFor;
        readonly Dictionary<int, AnimatorControllerParameterType> animParams = new Dictionary<int, AnimatorControllerParameterType>();

        // 묶임·구출
        bool bound;
        bool remoteBound;
        int rescueCount;
        int pendingShakes;
        float boundFor;
        float bothBoundFor;
        float immuneUntil = float.NegativeInfinity;
        bool rescuing;
        int lastShakeSign;
        int rescueSent;

        // 절벽 붙잡기
        bool hanging;
        bool climbing;
        bool remoteHanging;
        float ledgeRegrabAt = float.NegativeInfinity;
        Vector3 ledgeNormal;
        Vector3 ledgeProbe;
        Collider ledgeCollider;
        Vector3 climbStart, climbEnd;
        float climbT;
        readonly Collider[] ledgeBuffer = new Collider[8];
        Vector3 lastGroundFeet;
        bool hasGroundFeet;
        float hangFor;
        int ledgeRescueShakes;
        bool ledgeRescuer;   // 매달린 파트너 옆이라 E로 끌어올릴 수 있음
        int ledgeRescueSent;

        // 물건 밀기
        Pushable pushTarget;
        Vector3 lastPushDir;
        float lastPushReport = float.NegativeInfinity;
        readonly RaycastHit[] pushHits = new RaycastHit[8];
        const float PushReportInterval = 0.1f;

        // 줍기·던지기
        ThrowableStone carried;
        float carryRequestTime;
        bool remoteCarrying;

        // 수액
        readonly List<SapZone> saps = new List<SapZone>();

        // 점프 버섯
        JumpMushroom pendingMushroom;
        readonly Dictionary<Collider, JumpMushroom> mushroomCache = new Dictionary<Collider, JumpMushroom>();

        // ---------- 공개 정보 ----------

        // 이 캐릭터의 역할. 로컬 = 내 역할, 원격(오프라인 더미 포함) = 상대 역할
        public PlayerRole Role
        {
            get
            {
                var local = SwitchStates.LocalRole;
                return IsLocal ? local : Other(local);
            }
        }

        public static PlayerRole Other(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.A_Red: return PlayerRole.B_Blue;
                case PlayerRole.B_Blue: return PlayerRole.A_Red;
                default: return PlayerRole.None;
            }
        }

        public static PlayerController FindByRole(PlayerRole role)
        {
            if (role == PlayerRole.None) return null;
            foreach (var p in all)
                if (p.Role == role) return p;
            return null;
        }

        public PlayerController Partner
        {
            get
            {
                foreach (var p in all)
                    if (p != this) return p;
                return null;
            }
        }

        public bool IsBound => bound || remoteBound;
        public bool IsHanging => hanging || climbing || remoteHanging;
        public bool IsCarrying => carried != null || remoteCarrying;
        public bool IsPushingObject => pushTarget != null && lastPushDir != Vector3.zero;
        public bool InSap => saps.Count > 0;
        public int RescueCount => rescueCount;
        // 든 돌멩이가 따라갈 위치 (머리 위)
        public Vector3 CarryPoint => transform.position + transform.rotation * tuning.carryOffset;

        float SapSpeedMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = saps.Count - 1; i >= 0; i--)
                {
                    if (saps[i] == null) { saps.RemoveAt(i); continue; }
                    m = Mathf.Min(m, saps[i].SpeedMultiplier);
                }
                return m;
            }
        }

        float SapJumpMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = saps.Count - 1; i >= 0; i--)
                {
                    if (saps[i] == null) { saps.RemoveAt(i); continue; }
                    m = Mathf.Min(m, saps[i].JumpMultiplier);
                }
                return m;
            }
        }

        // ---------- 묶임·구출 ----------

        // 거대 나뭇가지 등에 맞았을 때. 판정하는 PC(맞은 사람)에서만 효과가 있다
        public bool Bind()
        {
            if (!Simulated || dead || bound || simTime < immuneUntil) return false;
            bound = true;
            rescueCount = 0;
            pendingShakes = 0;
            boundFor = 0f;
            horizontalVelocity = Vector3.zero;
            pushVelocity = Vector3.zero;
            if (verticalVelocity > 0f) verticalVelocity = 0f;
            lastJumpPressedTime = float.NegativeInfinity;
            ResetLedge();
            ReleaseObjectPush();
            DropCarried();
            Raise(PlayerEvent.Bound);
            return true;
        }

        void Free()
        {
            if (!bound) return;
            bound = false;
            rescueCount = 0;
            pendingShakes = 0;
            immuneUntil = simTime + tuning.rescueImmunity;
            Raise(PlayerEvent.Freed);
        }

        // 네트워크 콜백. 다음 틱에 반영한다
        void OnRescueShaken(PlayerRole from)
        {
            // 온라인: 이 PC의 로컬이 묶인 사람 / 오프라인: 보낸 역할이 아닌 쪽(더미)
            bool mine = GameServices.IsOnline ? IsLocal : Role != from;
            if (!mine) return;
            if (bound) pendingShakes++;
            else if (hanging) ledgeRescueShakes++; // 매달림 구출도 같은 요청을 쓴다 (계약 §3.9)
        }

        void UpdateBound(float dt)
        {
            if (bound)
            {
                boundFor += dt;
                if (pendingShakes > 0)
                {
                    rescueCount += pendingShakes;
                    pendingShakes = 0;
                }
                bool autoFree = tuning.boundAutoFreeSeconds > 0f && boundFor >= tuning.boundAutoFreeSeconds;
                if (rescueCount >= Mathf.Max(1, tuning.rescuePresses) || autoFree) Free();
            }
            else pendingShakes = 0;

            // 둘 다 묶이거나 매달리면(서로 구해 줄 수 없음) 잠시 뒤 체크포인트에서 부활 (각 PC가 자기 플레이어를 Die)
            if (!IsLocal) return;
            var partner = Partner;
            bool stuck = bound || hanging;
            if (stuck && partner != null && (partner.IsBound || partner.IsHanging))
            {
                bothBoundFor += dt;
                if (bothBoundFor >= tuning.bothBoundRespawnDelay)
                {
                    bothBoundFor = 0f;
                    if (!GameServices.IsOnline && partner.bound) partner.Free(); // 오프라인 더미는 죽지 않으므로 풀어 준다
                    Die();
                }
            }
            else bothBoundFor = 0f;
        }

        // 묶인 파트너 옆에서 A/D를 번갈아 누르면 한 번 바뀔 때마다 1회 보낸다
        void UpdateRescuer()
        {
            rescuing = false;
            if (!IsLocal || bound || dead) { lastShakeSign = 0; return; }
            var partner = Partner;
            if (partner == null || !partner.IsBound ||
                (partner.transform.position - transform.position).sqrMagnitude > tuning.rescueRange * tuning.rescueRange)
            {
                lastShakeSign = 0;
                rescueSent = 0;
                return;
            }
            rescuing = true;
            float x = input.Move.x;
            int sign = x > 0.5f ? 1 : x < -0.5f ? -1 : 0;
            if (sign == 0 || sign == lastShakeSign) return;
            lastShakeSign = sign;
            var service = GameServices.Interaction;
            if (service == null) return;
            service.RequestRescueShake(partner.Role);
            rescueSent++;
        }

        // 묶인 동안 이동 없음, 구출 중에는 좌우 입력을 흔들기에만 쓴다
        Vector2 GimmickMoveInput()
        {
            if (bound || input == null) return Vector2.zero;
            Vector2 move = input.Move;
            if (rescuing) move.x = 0f;
            return move;
        }

        // ---------- 절벽 붙잡기 ----------

        void TryLedgeGrab()
        {
            if (!IsLocal) return;
            float r = cc.radius, h = cc.height;
            Vector3 center = transform.position + cc.center;
            float feetY = center.y - h * 0.5f;
            if (grounded)
            {
                lastGroundFeet = new Vector3(center.x, feetY, center.z);
                hasGroundFeet = true;
                return;
            }
            if (!tuning.ledgeGrabEnabled || verticalVelocity > 0f) return;
            if (bound || carried != null || simTime < ledgeRegrabAt) return;
            if (tuning.ledgeFallGrab && TryFallGrab(center, feetY, r, h)) return;
            if (tuning.ledgeJumpGrab && !knockedBack) TryJumpGrab(center, feetY, r, h);
        }

        // 걷다가·밀려서 절벽 끝을 벗어나 떨어지면, 방금 밟던 끝을 붙잡는다
        bool TryFallGrab(Vector3 center, float feetY, float r, float h)
        {
            if (!hasGroundFeet) return false;
            float groundY = lastGroundFeet.y;
            // 방금 밟던 높이보다 조금 내려갔을 때 잡는다 (너무 많이 떨어졌으면 포기).
            // 프레임이 끊기면 한 번에 많이 떨어지므로 여유를 둔다 (판정은 밟던 높이 기준이라 상관없다)
            float drop = groundY - feetY;
            if (drop < 0.15f) return false;
            if (drop > h * 2f) { hasGroundFeet = false; return false; }

            Vector3 back = lastGroundFeet - new Vector3(center.x, groundY, center.z);
            back.y = 0f;
            float flatDist = back.magnitude;
            back =flatDist > 0.01f ? back / flatDist : -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (back.sqrMagnitude < 0.0001f) return false;

            // 절벽 벽면: 밟던 높이 조금 아래에서 밟던 쪽으로 쏜다
            Vector3 rayOrigin = new Vector3(center.x, groundY - 0.1f, center.z);
            if (!Physics.Raycast(rayOrigin, back, out RaycastHit wall, flatDist + r + 0.5f, tuning.ledgeMask, QueryTriggerInteraction.Ignore)) return false;
            if (wall.collider is CharacterController || Mathf.Abs(wall.normal.y) > 0.3f) return false;
            // 끝(벽면)에서 너무 멀리 날아갔으면 잡지 않는다
            if (wall.distance > tuning.ledgeFallReach + r) { hasGroundFeet = false; return false; }
            Vector3 n = new Vector3(wall.normal.x, 0f, wall.normal.z);
            if (n.sqrMagnitude < 0.0001f) return false;
            n.Normalize();

            // 끝 바깥이 깊게 비어 있어야 절벽. 계단·턱처럼 조금 아래에 바닥이 이어지면 매달리지 않는다
            if (!DeepDropBelow(wall.point, n, groundY, r)) return false;

            Vector3 probe = wall.point - n * 0.15f;
            probe.y = groundY + 0.5f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, 1.2f, tuning.ledgeMask, QueryTriggerInteraction.Ignore)) return false;
            if (top.normal.y < 0.7f || top.collider is CharacterController || Mathf.Abs(top.point.y - groundY) > 0.5f) return false;
            if (top.collider.GetComponentInParent<Pushable>() != null) return false;

            hasGroundFeet = false;
            return Hang(n, wall.point, top, r, h);
        }

        // 끝(wallPoint)에서 바깥(n)으로 몇 군데 내려다봐서, 밟던 높이 아래 ledgeMinDrop 안에 바닥이 하나라도 있으면 절벽이 아니다
        bool DeepDropBelow(Vector3 wallPoint, Vector3 n, float groundY, float r)
        {
            float startY = groundY - 0.05f;
            float depth = tuning.ledgeMinDrop;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = wallPoint + n * (r + 0.1f + i * 0.6f);
                p.y = startY + r;
                if (Physics.SphereCast(p, r * 0.9f, Vector3.down, out RaycastHit hit, r + depth, tuning.ledgeMask, QueryTriggerInteraction.Ignore) &&
                    !(hit.collider is CharacterController))
                    return false;
            }
            return true;
        }

        // 점프해서 앞쪽 벽 끝을 붙잡는다 (PlayerTuning.ledgeJumpGrab)
        void TryJumpGrab(Vector3 center, float feetY, float r, float h)
        {
            Vector3 fwd = GetMoveDirection();
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) return;
            fwd.Normalize();

            float handY = feetY + h * 0.95f;
            float rayY = Mathf.Max(feetY + h * 0.3f, handY - tuning.ledgeGrabBelow + 0.05f);
            Vector3 rayOrigin = new Vector3(center.x, rayY, center.z);

            if (!Physics.Raycast(rayOrigin, fwd, out RaycastHit wall, r + tuning.ledgeReach, tuning.ledgeMask, QueryTriggerInteraction.Ignore)) return;
            if (wall.collider is CharacterController || Mathf.Abs(wall.normal.y) > 0.3f) return;
            Vector3 n = new Vector3(wall.normal.x, 0f, wall.normal.z);
            if (n.sqrMagnitude < 0.0001f) return;
            n.Normalize();

            Vector3 probe = wall.point - n * 0.15f;
            probe.y = handY + tuning.ledgeGrabAbove;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, tuning.ledgeGrabAbove + tuning.ledgeGrabBelow, tuning.ledgeMask, QueryTriggerInteraction.Ignore)) return;
            if (top.normal.y < 0.7f || top.collider is CharacterController || top.point.y <= rayY) return;
            if (top.collider.GetComponentInParent<Pushable>() != null) return; // 굴러가는 바위는 붙잡지 않는다
            Hang(n, wall.point, top, r, h);
        }

        // wallPoint: 벽면, top: 끝 윗면, n: 벽에서 바깥(허공) 방향
        bool Hang(Vector3 n, Vector3 wallPoint, RaycastHit top, float r, float h)
        {
            // 올라선 자리에 몸이 들어갈 공간이 있는가 (파트너가 서 있는 건 괜찮다)
            Vector3 standFeet = new Vector3(top.point.x, top.point.y + 0.02f, top.point.z) - n * (r + 0.1f);
            if (Blocked(standFeet, r, h)) return false;

            ledgeNormal = n;
            ledgeProbe = new Vector3(top.point.x, top.point.y + 0.5f, top.point.z);
            ledgeCollider = top.collider;
            climbEnd = standFeet - (cc.center - Vector3.up * h * 0.5f); // 발 위치 → transform 위치

            Vector3 hangFeet = wallPoint + n * (r + 0.05f);
            hangFeet.y = top.point.y - h * 0.95f;
            Vector3 hangPos = hangFeet - (cc.center - Vector3.up * h * 0.5f);
            cc.enabled = false;
            transform.SetPositionAndRotation(hangPos, Quaternion.LookRotation(-n));
            cc.enabled = true;

            hanging = true;
            hangFor = 0f;
            ledgeRescueShakes = 0;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            pushVelocity = Vector3.zero;
            jumping = false;
            knockedBack = false;
            lastJumpPressedTime = float.NegativeInfinity;
            ReleaseObjectPush();
            Raise(PlayerEvent.LedgeGrab);
            return true;
        }

        bool Blocked(Vector3 feet, float r, float h)
        {
            Vector3 a = feet + Vector3.up * (r + 0.05f);
            Vector3 b = feet + Vector3.up * Mathf.Max(r + 0.05f, h - r);
            int count = Physics.OverlapCapsuleNonAlloc(a, b, r * 0.95f, ledgeBuffer, tuning.ledgeMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (ledgeBuffer[i] != cc && !(ledgeBuffer[i] is CharacterController)) return true;
            return false;
        }

        // 매달린 동안 움직일 수 없다. 파트너가 E로 끌어올려 줄 때까지 기다린다
        void UpdateHang(float dt)
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            hangFor += dt;
            bool jump = input.ConsumeJump();
            if (ledgeRescueShakes >= Mathf.Max(1, tuning.ledgeRescuePresses)) { StartClimb(); return; }
            if (tuning.ledgeSelfClimb)
            {
                Vector3 move = GetMoveDirection();
                float toward = Vector3.Dot(move, -ledgeNormal);
                if (jump || toward > 0.5f) { StartClimb(); return; }
                if (toward < -0.5f) { ReleaseLedge(); return; }
            }
            if (tuning.ledgeHangMaxSeconds > 0f && hangFor >= tuning.ledgeHangMaxSeconds) { ReleaseLedge(); return; }
            // 붙잡은 곳이 사라졌으면(숨겨진 발판 등) 떨어진다
            if (ledgeCollider == null || !ledgeCollider.enabled || ledgeCollider.isTrigger ||
                !Physics.Raycast(ledgeProbe, Vector3.down, out RaycastHit hit, 1f, tuning.ledgeMask, QueryTriggerInteraction.Ignore) ||
                hit.collider != ledgeCollider)
                ReleaseLedge();
        }

        // 매달린 파트너 옆에서 E. 처리했으면 줍기·레버에는 넘기지 않는다
        bool TryLedgeRescue()
        {
            UpdateLedgeRescuer();
            if (!ledgeRescuer) return false;
            var service = GameServices.Interaction;
            if (service == null) return false;
            service.RequestRescueShake(Partner.Role);
            ledgeRescueSent++;
            return true;
        }

        void UpdateLedgeRescuer()
        {
            var partner = Partner;
            ledgeRescuer = IsLocal && !bound && !hanging && !climbing && !dead && partner != null && partner.HangingForRescue &&
                (partner.transform.position - transform.position).sqrMagnitude <= tuning.ledgeRescueRange * tuning.ledgeRescueRange;
            if (!ledgeRescuer) ledgeRescueSent = 0;
        }

        // 구해 줄 수 있는 매달림 (올라가는 중은 제외)
        bool HangingForRescue => hanging || remoteHanging;

        void ReleaseLedge()
        {
            hanging = false;
            ledgeRegrabAt = simTime + tuning.ledgeRegrabCooldown;
            grounded = false;
            lastGroundedTime = float.NegativeInfinity;
        }

        void StartClimb()
        {
            hanging = false;
            climbing = true;
            climbT = 0f;
            climbStart = transform.position;
            cc.enabled = false;
            SetAnimTrigger(ClimbHash);
            Raise(PlayerEvent.LedgeClimb);
        }

        void UpdateClimb(float dt)
        {
            climbT += dt / Mathf.Max(0.05f, tuning.ledgeClimbTime);
            Vector3 mid = new Vector3(climbStart.x, climbEnd.y, climbStart.z);
            const float upPart = 0.6f;
            transform.position = climbT < upPart
                ? Vector3.Lerp(climbStart, mid, climbT / upPart)
                : Vector3.Lerp(mid, climbEnd, Mathf.Clamp01((climbT - upPart) / (1f - upPart)));
            if (climbT < 1f) return;
            climbing = false;
            cc.enabled = true;
            grounded = true;
            lastGroundedTime = simTime;
            verticalVelocity = -2f;
            ledgeRegrabAt = simTime + tuning.ledgeRegrabCooldown;
        }

        void ResetLedge()
        {
            if ((hanging || climbing) && cc != null) cc.enabled = true;
            hanging = false;
            climbing = false;
        }

        // ---------- 물건 밀기 ----------

        Pushable FindPushable()
        {
            Vector3 center = transform.position + cc.center;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            int count = Physics.SphereCastNonAlloc(center, cc.radius * 0.5f, fwd, pushHits, cc.radius + tuning.objectPushReach, ~0, QueryTriggerInteraction.Ignore);
            Pushable best = null;
            float bestDist = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (pushHits[i].collider == cc) continue;
                var p = pushHits[i].collider.GetComponentInParent<Pushable>();
                if (p == null || pushHits[i].distance >= bestDist) continue;
                best = p;
                bestDist = pushHits[i].distance;
            }
            return best;
        }

        // 밀기 키를 누른 채 바위 쪽으로 걸으면 미는 방향을 주기적으로 알린다. 움직이는 판정은 권한 PC의 Pushable
        void UpdateObjectPush(float dt)
        {
            Pushable target = null;
            Vector3 dir = Vector3.zero;
            if (IsLocal && input.PushHeld && grounded && !bound && carried == null && !knockedBack)
            {
                target = FindPushable();
                if (target != null)
                {
                    Vector3 move = GetMoveDirection();
                    Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                    if (move.sqrMagnitude > 0.01f && Vector3.Dot(move.normalized, fwd) > 0.5f) dir = target.SnapDirection(fwd);
                }
            }
            if (target != pushTarget)
            {
                ReleaseObjectPush();
                pushTarget = target;
            }
            if (pushTarget == null) return;
            if (dir != lastPushDir || (dir != Vector3.zero && simTime - lastPushReport >= PushReportInterval))
            {
                pushTarget.Report(Role, dir);
                lastPushDir = dir;
                lastPushReport = simTime;
            }
        }

        void ReleaseObjectPush()
        {
            if (pushTarget != null && lastPushDir != Vector3.zero) pushTarget.Report(Role, Vector3.zero);
            pushTarget = null;
            lastPushDir = Vector3.zero;
            lastPushReport = float.NegativeInfinity;
        }

        Vector3 AdjustMoveForGimmicks(Vector3 dir, out float speed)
        {
            speed = tuning.moveSpeed * SapSpeedMultiplier;
            if (pushTarget != null) speed = Mathf.Min(speed, tuning.objectPushWalkSpeed);
            return dir;
        }

        // true면 이번 틱에는 몸을 돌리지 않는다
        bool FaceGimmickTarget(float dt) => bound || pushTarget != null;

        // ---------- 줍기·던지기 ----------

        // Interact 키. 들고 있으면 던지고, 없으면 가까운 돌멩이를 줍는다. 처리했으면 레버 등에는 넘기지 않는다
        bool TryCarryInteract()
        {
            if (!IsLocal) return false;
            if (TryLedgeRescue()) return true;
            if (carried != null)
            {
                Vector3 velocity = ThrowVelocity();
                carried.RequestThrow(Role, CarryPoint, velocity);
                Vector3 face = new Vector3(velocity.x, 0f, velocity.z);
                if (face.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(face);
                carried = null;
                Raise(PlayerEvent.Thrown);
                return true;
            }
            var stone = ThrowableStone.FindNearest(transform.position, tuning.pickUpRange);
            if (stone == null) return false;
            stone.RequestPickUp(Role);
            carried = stone;
            carryRequestTime = simTime;
            Raise(PlayerEvent.PickedUp);
            return true;
        }

        Vector3 ThrowVelocity()
        {
            Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            Vector3 fwd = cam != null ? cam.forward : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (right.sqrMagnitude < 0.0001f) right = transform.right;
            Vector3 dir = Quaternion.AngleAxis(-tuning.throwUpAngle, right.normalized) * fwd.normalized;
            return dir * tuning.throwSpeed;
        }

        void UpdateCarry()
        {
            if (carried == null) { carried = null; return; } // 파괴된 경우 정리
            // 다른 사람이 먼저 주웠으면 요청이 거절된다 (온라인은 응답이 늦게 오므로 잠시 기다린다)
            if (simTime - carryRequestTime > 1f && carried.Holder != Role) carried = null;
        }

        void DropCarried()
        {
            if (carried == null) { carried = null; return; }
            carried.RequestThrow(Role, CarryPoint, Vector3.zero);
            carried = null;
        }

        // ---------- 수액 ----------

        public void EnterSap(SapZone zone)
        {
            if (zone != null && !saps.Contains(zone)) saps.Add(zone);
        }

        public void ExitSap(SapZone zone) => saps.Remove(zone);

        // ---------- 점프 버섯 ----------

        void CheckMushroom(Collider col)
        {
            if (!mushroomCache.TryGetValue(col, out JumpMushroom m))
            {
                m = col.GetComponentInParent<JumpMushroom>();
                mushroomCache[col] = m;
            }
            if (m != null && m.isActiveAndEnabled) pendingMushroom = m;
        }

        void ApplyMushroomLaunch()
        {
            var m = pendingMushroom;
            pendingMushroom = null;
            if (m == null || bound) return;
            verticalVelocity = m.LaunchSpeed;
            grounded = false;
            jumping = true;
            lastGroundedTime = float.NegativeInfinity;
            lastJumpPressedTime = float.NegativeInfinity;
            if (animator != null) animator.SetTrigger(JumpHash);
            Raise(PlayerEvent.Jumped);
            m.PlayBounce();
        }

        // ---------- 원격 연출 ----------

        void ApplyRemoteGimmickEvent(PlayerEvent e)
        {
            switch (e)
            {
                case PlayerEvent.Bound: remoteBound = true; break;
                case PlayerEvent.Freed: remoteBound = false; break;
                case PlayerEvent.Died:
                case PlayerEvent.Respawned:
                    remoteBound = false;
                    remoteHanging = false;
                    remoteCarrying = false;
                    break;
                case PlayerEvent.LedgeGrab: remoteHanging = true; break;
                case PlayerEvent.LedgeClimb:
                    remoteHanging = false;
                    SetAnimTrigger(ClimbHash);
                    break;
                case PlayerEvent.Landed:
                case PlayerEvent.Jumped:
                case PlayerEvent.Pushed:
                case PlayerEvent.KnockbackHeavy:
                    remoteHanging = false;
                    break;
                case PlayerEvent.PickedUp: remoteCarrying = true; break;
                case PlayerEvent.Thrown: remoteCarrying = false; break;
            }
            UpdateGimmickAnimator();
        }

        void UpdateGimmickAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            SetAnimBool(HangingHash, IsHanging);
            SetAnimBool(BoundHash, IsBound);
            SetAnimBool(StruggleHash, InSap);
            SetAnimBool(CarryingHash, IsCarrying);
            SetAnimBool(PushingHash, IsPushingObject);
        }

        bool HasAnimParam(int hash, AnimatorControllerParameterType type)
        {
            if (animator == null || !animator.isActiveAndEnabled) return false;
            var controller = animator.runtimeAnimatorController;
            if (controller == null) return false;
            if (controller != animParamsFor)
            {
                animParamsFor = controller;
                animParams.Clear();
                foreach (var p in animator.parameters) animParams[p.nameHash] = p.type;
            }
            return animParams.TryGetValue(hash, out var t) && t == type;
        }

        void SetAnimBool(int hash, bool value)
        {
            if (HasAnimParam(hash, AnimatorControllerParameterType.Bool)) animator.SetBool(hash, value);
        }

        void SetAnimTrigger(int hash)
        {
            if (HasAnimParam(hash, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hash);
        }

        // ---------- 개발 키 · 안내 ----------

        void Update()
        {
            if (!Simulated) UpdateGimmickAnimator(); // 원격: 수액 발버둥 등은 각 PC에서 직접 판정한다
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsLocal && !dead && !PlayerInputReader.Blocked && !PlayerInputReader.MenuOpen && !PlayerInputReader.PauseOpen)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.f4Key.wasPressedThisFrame) DebugBindKey();
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // F4: 내가 묶였거나 매달렸으면 구출 1회 / 오프라인이면 더미 파트너를 묶기 / 그 외에는 나를 묶기
        void DebugBindKey()
        {
            if (bound) { pendingShakes++; return; }
            if (hanging) { ledgeRescueShakes++; return; }
            var partner = Partner;
            if (!GameServices.IsOnline && partner != null && !partner.bound && partner.Bind()) return;
            Bind();
        }
#endif

        GUIStyle promptStyle;

        void OnGUI()
        {
            if (!IsLocal || dead || PlayerInputReader.Blocked || PlayerInputReader.MenuOpen || PlayerInputReader.PauseOpen) return;
            string text = null;
            if (bound) text = $"묶였어요! 파트너가 옆에서 A/D를 번갈아 눌러 구해 줘야 해요  ({rescueCount}/{tuning.rescuePresses})";
            else if (rescuing) text = $"A/D 번갈아 눌러 구출!  ({rescueSent})";
            else if (hanging) text = tuning.ledgeSelfClimb ? "W/Space 올라가기 · S 놓기" : "매달렸어요! 파트너가 와서 E로 끌어올려 줘야 해요";
            else if (ledgeRescuer) text = $"E 파트너 끌어올리기  ({ledgeRescueSent}/{Mathf.Max(1, tuning.ledgeRescuePresses)})";
            else if (carried != null) text = "E 던지기";
            else if (Event.current.type == EventType.Repaint && ThrowableStone.FindNearest(transform.position, tuning.pickUpRange) != null) text = "E 줍기";
            else if (pushTarget != null) text = "F를 누른 채 앞으로 밀기";
            if (text == null) return;
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                promptStyle.normal.textColor = Color.white;
            }
            var size = promptStyle.CalcSize(new GUIContent(text));
            float w = size.x + 24f, h = size.y + 12f;
            GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, h), text, promptStyle);
        }
    }
}
