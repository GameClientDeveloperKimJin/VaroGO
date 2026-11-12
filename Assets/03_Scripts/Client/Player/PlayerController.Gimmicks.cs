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
        static readonly int PullUpHash = Animator.StringToHash("PullUp");
        static readonly int ThrowHash = Animator.StringToHash("Throw");                 // 손을 떠나는 순간 (로컬 + 원격 Thrown 이벤트)
        static readonly int ThrowChargingHash = Animator.StringToHash("ThrowCharging"); // 파워 충전 중 (로컬만)

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

        // 붙잡기 점프 (끝자락에서 손 표시 → 일반 점프 → 손이 닿으면 매달림)
        bool assistReady;
        Vector3 assistNormal, assistWall;
        RaycastHit assistTop;
        float assistReadyAt = float.NegativeInfinity;
        bool assistArmed;
        float armedAt;
        Vector3 armedNormal, armedWall;
        RaycastHit armedTop;
        LedgeGrabHint assistHint;

        // 물건 밀기
        Pushable pushTarget;
        Pushable grabbedPushable; // E로 잡은 물건. 잡은 상태에서만 밀린다
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
        public Pushable PushTargetDebug => pushTarget; // 밀기 진단 표시용
        public bool InSap => saps.Count > 0;
        public int RescueCount => rescueCount;
        // 든 돌멩이가 따라갈 위치. 캐릭터 모델이 있으면 오른손(애니메이션을 따라 움직임), 없으면 carryOffset(머리 위)
        const string CarryHandBone = "hand.R";
        Transform carryHand;
        public Vector3 CarryPoint => tuning.carryInHand && carryHand != null && carryHand.gameObject.activeInHierarchy
            ? carryHand.position + transform.rotation * tuning.carryHandOffset
            : transform.position + transform.rotation * tuning.carryOffset;

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
            if (!IsLocal || DevFlying) return; // 비행 치트 중에는 절벽을 잡지 않는다
            float r = cc.radius, h = cc.height;
            Vector3 center = transform.position + cc.center;
            float feetY = center.y - h * 0.5f;
            if (grounded)
            {
                lastGroundFeet = new Vector3(center.x, feetY, center.z);
                hasGroundFeet = true;
                if (assistArmed && simTime - armedAt > 0.1f) DisarmLedgeAssist(); // 붙잡기 점프 실패 후 착지
                return;
            }
            if (bound || carried != null || knockedBack) DisarmLedgeAssist();
            if (TryAssistGrab(center, feetY, r, h)) return; // 붙잡기 점프는 올라가는 중에도 잡는다
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
            if (ledgeRescueShakes >= Mathf.Max(1, tuning.ledgeRescuePresses)) { StartClimb(true); return; }
            if (tuning.ledgeSelfClimb || DevCheats.SelfClimb) // 개발 치트: 혼자 올라가기
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
            rescuePressedAt = Time.time;
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

        // rescued: 파트너가 구해 줘서 올라가는가 (혼자 올라가기면 false)
        void StartClimb(bool rescued = false)
        {
            hanging = false;
            climbing = true;
            climbT = 0f;
            climbStart = transform.position;
            cc.enabled = false;
            SetAnimTrigger(ClimbHash);
            Raise(PlayerEvent.LedgeClimb);
            if (rescued) Partner?.PlayPullUp(this, true);
        }

        // ---------- 파트너 끌어올리기 연출 ----------
        // 새 네트워크 이벤트 없이, 각 PC가 매달린 사람의 올라가기 시작(로컬 StartClimb / 원격 LedgeClimb)을 보고 구출한 쪽 PullUp을 재생한다.
        // 구출한 사람이 내 로컬이면: 턱 옆으로 짧게 붙고 벽 바깥을 바라본 채 잠깐 멈춘다 (위치·방향은 transform 동기화로 상대 화면에도 보인다)
        const float PullUpLock = 0.5f;      // 이 시간(초) 동안 제자리 (애니 0.6초, 파트너 Climb 0.45초)
        const float PullUpSnapTime = 0.1f;  // 끌어올리는 자리로 붙는 시간
        const float PullUpInward = 0.5f;    // 턱 모서리에서 안쪽으로 (m)
        const float PullUpSide = 0.6f;      // 파트너 정면에서 옆으로 (올라온 파트너와 몸이 겹치지 않게)
        float rescuePressedAt = float.NegativeInfinity;
        float pullUpUntil = float.NegativeInfinity;
        float pullSnapUntil = float.NegativeInfinity;
        Vector3 pullSnapTo;

        void PlayPullUp(PlayerController hanger, bool rescued)
        {
            if (hanger == null || dead || bound || hanging || climbing || seat != null) return;
            float range = tuning.ledgeRescueRange + 1f;
            if ((hanger.transform.position - transform.position).sqrMagnitude > range * range) return;
            if (IsLocal)
            {
                if (Time.time - rescuePressedAt > 1.5f) return; // 내가 E로 구한 게 아니면 (혼자 올라가기 등)
                StartPullUpPose(hanger);
            }
            else if (!rescued) return;
            SetAnimTrigger(PullUpHash);
        }

        void StartPullUpPose(PlayerController hanger)
        {
            float r = cc.radius, h = cc.height;
            // 매달린 사람은 벽을 바라본다 → 벽 바깥(허공) 방향 n = 그 사람 뒤쪽
            Vector3 n = -Vector3.ProjectOnPlane(hanger.transform.forward, Vector3.up);
            if (n.sqrMagnitude < 0.0001f) return;
            n.Normalize();
            Vector3 hangFeet = hanger.transform.position + cc.center - Vector3.up * (h * 0.5f);
            Vector3 edge = hangFeet - n * (r + 0.05f); // 벽면 = 턱 모서리 (Hang()의 hangFeet 기준)
            edge.y = hangFeet.y + h * 0.95f;

            pullUpUntil = simTime + PullUpLock;
            horizontalVelocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(n);
            facingLocked = true;

            // 턱 안쪽, 파트너 옆 중 내가 있던 쪽. 바닥이 있고 비어 있을 때만 붙는다
            Vector3 side = Vector3.Cross(Vector3.up, n);
            if (Vector3.Dot(transform.position - edge, side) < 0f) side = -side;
            // 옆 간격은 몸 두 개(반지름 2배)보다 넓게. 좁으면 올라온 파트너와 몸이 겹친다
            Vector3 targetFeet = edge - n * PullUpInward + side * Mathf.Max(PullUpSide, r * 2f + 0.05f);
            pullSnapUntil = float.NegativeInfinity;
            if (!Physics.Raycast(targetFeet + Vector3.up * 1f, Vector3.down, out RaycastHit ground, 1.5f, tuning.ledgeMask, QueryTriggerInteraction.Ignore) ||
                ground.normal.y < 0.7f || ground.collider is CharacterController)
                return;
            targetFeet.y = ground.point.y + 0.02f;
            if (Blocked(targetFeet, r, h)) return;
            pullSnapTo = targetFeet - (cc.center - Vector3.up * h * 0.5f);
            pullSnapUntil = simTime + PullUpSnapTime;
        }

        // 끌어올리는 동안 제자리. true면 이번 틱 일반 이동을 하지 않는다
        bool UpdatePullUp(float dt)
        {
            if (simTime >= pullUpUntil) return false;
            horizontalVelocity = Vector3.zero;
            Vector3 motion = Vector3.down * 2f * dt;
            if (pullSnapUntil > float.NegativeInfinity)
            {
                float left = pullSnapUntil - (simTime - dt);
                Vector3 delta = pullSnapTo - transform.position;
                delta.y = 0f;
                motion += left > dt ? delta * (dt / left) : delta;
                if (simTime >= pullSnapUntil) pullSnapUntil = float.NegativeInfinity;
            }
            cc.Move(motion);
            grounded = cc.isGrounded;
            if (grounded) lastGroundedTime = simTime;
            input.ConsumeJump(); // 끌어올리는 중 누른 점프는 버린다
            return true;
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
            assistReady = false;
            DisarmLedgeAssist();
            pullUpUntil = pullSnapUntil = float.NegativeInfinity;
        }

        // ---------- 붙잡기 점프 ----------
        // 점프 자체는 일반 점프 그대로. 발판 끝자락에서 "여기서 점프하면 손이 저 끝에 닿는다"면 손 표시를 띄우고,
        // 그 상태에서 점프하면 공중에서 손이 그 끝 높이에 왔을 때 붙잡는다 (발은 못 올라서는 거리)

        // 매 틱 (땅): 앞쪽 건너편 끝을 찾아 손 표시. 공중: 점프할 때 고른 끝을 계속 표시
        void UpdateLedgeAssist()
        {
            if (assistArmed)
            {
                assistReady = false;
                ShowAssistHint(armedTop.point);
                return;
            }
            bool usable = IsLocal && tuning.ledgeAssist && !bound && carried == null && !knockedBack && !dead && simTime >= ledgeRegrabAt;
            if (usable && grounded && FindAssistLedge()) assistReadyAt = simTime;
            // 끝에서 발이 막 떨어진 직후(코요테 시간)에 점프해도 잡히게, 마지막으로 찾은 끝을 잠깐 유지한다
            assistReady = usable && simTime - assistReadyAt <= (grounded ? 0f : tuning.coyoteTime);
            if (assistReady) ShowAssistHint(assistTop.point);
        }

        void ShowAssistHint(Vector3 ledgeTop)
        {
            var hint = AssistHint();
            if (hint != null) hint.Show(ledgeTop + Vector3.up * tuning.ledgeAssistHintHeight);
        }

        LedgeGrabHint AssistHint()
        {
            if (assistHint == null && tuning.ledgeAssistHint != null)
            {
                var go = Instantiate(tuning.ledgeAssistHint);
                go.name = "LedgeGrabHint (" + name + ")";
                assistHint = go.GetComponent<LedgeGrabHint>();
                if (assistHint == null) assistHint = go.AddComponent<LedgeGrabHint>();
            }
            return assistHint;
        }

        // 일반 점프가 시작된 틱에 불린다. 손 표시가 떠 있었으면 그 끝을 붙잡을 대상으로 기억한다
        void ArmLedgeAssist()
        {
            if (!assistReady) return;
            assistReady = false;
            assistArmed = true;
            armedAt = simTime;
            armedNormal = assistNormal;
            armedWall = assistWall;
            armedTop = assistTop;
        }

        void DisarmLedgeAssist()
        {
            assistArmed = false;
            if (assistHint != null) assistHint.Hide();
        }

        // 공중: 기억한 끝 앞에 몸이 닿았고 손이 끝 높이 근처면 매달린다. 손이 끝보다 많이 내려가면 놓친 것
        bool TryAssistGrab(Vector3 center, float feetY, float r, float h)
        {
            if (!assistArmed) return false;
            if (grounded)
            {
                if (simTime - armedAt > 0.1f) DisarmLedgeAssist(); // 착지했으면 끝
                return false;
            }
            float handY = feetY + h * 0.95f;
            float topY = armedTop.point.y;
            if (handY < topY - tuning.ledgeGrabBelow - 0.3f) { DisarmLedgeAssist(); return false; }
            if (handY > topY + tuning.ledgeGrabAbove || handY < topY - tuning.ledgeGrabBelow) return false;
            float dist = Vector3.Dot(new Vector3(center.x - armedWall.x, 0f, center.z - armedWall.z), armedNormal);
            if (dist > r + tuning.ledgeReach + Mathf.Max(0f, tuning.ledgeAssistExtraReach) || dist < 0f) return false;

            // 끝이 아직 있는지 다시 확인한다 (숨겨진 발판 등)
            var col = armedTop.collider;
            RaycastHit top = default;
            bool there = col != null && col.enabled && !col.isTrigger &&
                Physics.Raycast(armedTop.point + Vector3.up * 0.5f, Vector3.down, out top, 1f, tuning.ledgeMask, QueryTriggerInteraction.Ignore) &&
                top.collider == col;
            if (!there) { DisarmLedgeAssist(); return false; }
            Vector3 wallAtMe = armedWall + Vector3.ProjectOnPlane(center - armedWall, armedNormal);
            wallAtMe.y = armedWall.y;
            DisarmLedgeAssist();
            return Hang(armedNormal, wallAtMe, top, r, h);
        }

        // 발판 끝자락에서, 걷는(바라보는) 방향으로 여러 높이로 쏴서 아래가 깊게 빈 건너편 벽의 윗끝을 찾는다. 가장 가까운 것
        bool FindAssistLedge()
        {
            float r = cc.radius, h = cc.height;
            Vector3 center = transform.position + cc.center;
            float feetY = center.y - h * 0.5f;
            Vector3 fwd = GetMoveDirection();
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) return false;
            fwd.Normalize();
            if (!AtEdge(center, fwd, feetY, r)) return false;

            float above = Mathf.Max(0f, tuning.ledgeAssistAbove), below = Mathf.Max(0f, tuning.ledgeAssistBelow);
            float lowY = feetY - below - 0.3f, highY = feetY + above - 0.1f;
            float range = r + Mathf.Max(0f, tuning.ledgeAssistRange);
            float best = float.PositiveInfinity;
            const int Rays = 6;
            for (int i = 0; i < Rays; i++)
            {
                Vector3 origin = new Vector3(center.x, Mathf.Lerp(highY, lowY, i / (Rays - 1f)), center.z);
                if (!Physics.Raycast(origin, fwd, out RaycastHit wall, range, tuning.ledgeMask, QueryTriggerInteraction.Ignore)) continue;
                if (wall.distance >= best || wall.collider is CharacterController || Mathf.Abs(wall.normal.y) > 0.3f) continue;
                Vector3 n = new Vector3(wall.normal.x, 0f, wall.normal.z);
                if (n.sqrMagnitude < 0.0001f) continue;
                n.Normalize();
                if (Vector3.Dot(n, -fwd) < 0.5f) continue; // 비스듬한 벽은 제외

                Vector3 probe = wall.point - n * 0.15f;
                probe.y = feetY + above + 0.3f;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, above + below + 0.6f, tuning.ledgeMask, QueryTriggerInteraction.Ignore)) continue;
                if (top.normal.y < 0.7f || top.collider is CharacterController) continue;
                if (top.point.y > feetY + above || top.point.y < feetY - below || top.point.y < wall.point.y - 0.05f) continue;
                if (top.collider.GetComponentInParent<Pushable>() != null) continue;

                // 벽과 나 사이가 깊게 비어 있어야 한다 (계단·턱·평지 벽 제외)
                if (!GapBeforeWall(wall.point, n, wall.distance, top.point.y, r)) continue;
                // 일반 점프로 발이 올라설 수 있으면 손 표시를 띄우지 않는다
                if (tuning.ledgeAssistOnlyUnreachable && NormalJumpReaches(feetY, top.point.y, wall.distance + r + 0.1f)) continue;
                // 일반 점프로 손조차 안 닿으면 띄우지 않는다
                if (!JumpHandsReach(feetY, top.point.y, wall.distance, r, h)) continue;
                // 올라선 자리에 몸이 들어갈 공간
                Vector3 standFeet = new Vector3(top.point.x, top.point.y + 0.02f, top.point.z) - n * (r + 0.1f);
                if (Blocked(standFeet, r, h)) continue;

                best = wall.distance;
                assistNormal = n;
                assistWall = wall.point;
                assistTop = top;
            }
            return best < float.PositiveInfinity;
        }

        // 내 발판이 몸 앞 ledgeAssistEdgeDistance 안에서 끝나는가 (끝자락에 서 있나)
        bool AtEdge(Vector3 center, Vector3 fwd, float feetY, float r)
        {
            Vector3 p = center + fwd * (r + Mathf.Max(0.05f, tuning.ledgeAssistEdgeDistance));
            p.y = feetY + 0.3f + r * 0.5f;
            return !Physics.SphereCast(p, r * 0.5f, Vector3.down, out RaycastHit hit, 0.3f + 1f, tuning.ledgeMask, QueryTriggerInteraction.Ignore) ||
                hit.collider is CharacterController;
        }

        // 벽 바로 앞(내 쪽) 몇 군데를 내려다봐서, 끝 높이 아래 ledgeAssistMinDrop 안에 바닥이 있으면 틈이 아니다
        bool GapBeforeWall(Vector3 wallPoint, Vector3 n, float wallDist, float topY, float r)
        {
            float startY = Mathf.Min(topY, transform.position.y + cc.center.y - cc.height * 0.5f) - 0.05f;
            float maxOut = wallDist - r - 0.2f; // 내 발밑까지는 보지 않는다
            int samples = 0;
            for (int i = 0; i < 3; i++)
            {
                float d = r + 0.1f + i * 0.5f;
                if (d > maxOut) break;
                Vector3 p = wallPoint + n * d;
                p.y = startY + r;
                samples++;
                if (Physics.SphereCast(p, r * 0.9f, Vector3.down, out RaycastHit hit, r + Mathf.Max(0f, tuning.ledgeAssistMinDrop), tuning.ledgeMask, QueryTriggerInteraction.Ignore) &&
                    !(hit.collider is CharacterController))
                    return false;
            }
            return samples > 0;
        }

        float JumpStartSpeed => Mathf.Sqrt(2f * tuning.jumpHeight * SapJumpMultiplier * Mathf.Max(0.0001f, -tuning.gravity));

        // 일반 점프(최고 속도로 달려서)로 targetY 높이에 내려앉기 전까지 dist만큼 갈 수 있나 = 발로 올라설 수 있나
        bool NormalJumpReaches(float feetY, float targetY, float dist)
        {
            float g = -tuning.gravity;
            if (g <= 0.0001f) return false;
            float v = JumpStartSpeed;
            float disc = v * v - 2f * g * (targetY - feetY);
            if (disc < 0f) return false; // 그 높이까지 못 올라간다
            float t = (v + Mathf.Sqrt(disc)) / g;
            return tuning.moveSpeed * SapSpeedMultiplier * t >= dist;
        }

        // 일반 점프(최고 속도)로 손이 닿는 곳(벽 - ExtraReach)에 왔을 때, 손이 끝 높이(ledgeGrabBelow 여유)까지 와 있나.
        // 손이 끝보다 높으면 벽을 타고 내려오다 끝에서 잡히므로 괜찮다
        bool JumpHandsReach(float feetY, float topY, float wallDist, float r, float h)
        {
            float g = -tuning.gravity;
            if (g <= 0.0001f) return false;
            float v = JumpStartSpeed;
            // 붙잡기 점프는 벽에서 ledgeAssistExtraReach만큼 떨어진 곳에서도 손을 뻗어 잡는다
            float t = Mathf.Max(0f, wallDist - r - Mathf.Max(0f, tuning.ledgeAssistExtraReach)) / Mathf.Max(0.01f, tuning.moveSpeed * SapSpeedMultiplier);
            float handAtWall = feetY + v * t - 0.5f * g * t * t + h * 0.95f;
            float handPeak = feetY + v * v / (2f * g) + h * 0.95f;
            return handPeak >= topY - tuning.ledgeGrabBelow && handAtWall >= topY - tuning.ledgeGrabBelow;
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

        // 잡기(E)로 바위를 잡은 상태에서 바위 쪽으로 걸어 들어가면 미는 방향을 주기적으로 알린다. 움직이는 판정은 권한 PC의 Pushable.
        // 잡은 동안은 몸이 안 돌아간다. 걷는 방향이 바위에서 벗어나면 미는 신호만 멈추고 잡기는 유지한다.
        // 다시 E, 점프·낙하, 바위에서 멀어짐, 묶임·넉백·들기 → 놓는다
        void UpdateObjectPush(float dt)
        {
            if (grabbedPushable != null && !CanHoldPushable(grabbedPushable)) ReleaseObjectPush();
            if (grabbedPushable == null)
            {
                if (pushTarget != null) ReleaseObjectPush();
                return;
            }
            pushTarget = grabbedPushable;
            Vector3 dir = Vector3.zero;
            Vector3 move = GetMoveDirection();
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (move.sqrMagnitude > 0.01f && Vector3.Dot(move.normalized, fwd) > 0.5f)
                dir = pushTarget.SnapDirection(fwd);
            if (dir != lastPushDir || (dir != Vector3.zero && simTime - lastPushReport >= PushReportInterval))
            {
                pushTarget.Report(Role, dir);
                lastPushDir = dir;
                lastPushReport = simTime;
            }
        }

        bool CanHoldPushable(Pushable p)
        {
            if (p == null || !p.isActiveAndEnabled) return false;
            if (!IsLocal || dead || bound || hanging || carried != null || knockedBack) return false;
            if (simTime - lastGroundedTime > 0.25f) return false; // 점프·낙하 (잠깐 뜨는 건 봐준다)
            Vector3 center = transform.position + cc.center;
            return p.SurfaceDistance(center) <= cc.radius + tuning.objectPushReach + 0.35f;
        }

        // E: 앞에 밀 수 있는 물건이 있으면 잡는다. 잡은 상태면 놓는다
        bool TryGrabPushable()
        {
            if (grabbedPushable != null) { ReleaseObjectPush(); return true; }
            if (!grounded || bound || hanging || carried != null || knockedBack) return false;
            var p = FindPushable();
            if (p == null) return false;
            grabbedPushable = p;
            pushTarget = p;
            // 바위의 면을 똑바로 보게 돌린다 (미는 방향이 4방향으로 맞춰지므로)
            Vector3 face = p.SnapDirection(Vector3.ProjectOnPlane(transform.forward, Vector3.up));
            if (face.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(face);
            return true;
        }

        void ReleaseObjectPush()
        {
            if (pushTarget != null && lastPushDir != Vector3.zero) pushTarget.Report(Role, Vector3.zero);
            pushTarget = null;
            grabbedPushable = null;
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
            if (TryGrabPushable()) return true;
            if (carried != null)
            {
                // 누르고 있는 동안 파워를 모으고, 떼면 던진다 (UpdateThrowCharge)
                throwCharging = true;
                throwChargeStart = simTime;
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

        // ---------- 던지기 파워 ----------
        bool throwCharging;
        float throwChargeStart;
        ThrowPowerGauge powerGauge;

        // 현재 파워 (0~1). 왕복이면 최대에서 다시 내려간다
        float ThrowPower
        {
            get
            {
                float x = (simTime - throwChargeStart) / Mathf.Max(0.05f, tuning.throwChargeTime);
                return tuning.throwChargePingPong ? Mathf.PingPong(x, 1f) : Mathf.Clamp01(x);
            }
        }

        // 매 틱: 충전 중이면 게이지 표시, 키를 떼면 던진다. 메뉴가 열리는 등 입력이 막히면 던지지 않고 취소
        void UpdateThrowCharge()
        {
            if (!throwCharging) return;
            if (carried == null || bound || hanging || climbing || dead || seat != null ||
                PlayerInputReader.Blocked || PlayerInputReader.MenuOpen || PlayerInputReader.PauseOpen)
            {
                CancelThrowCharge();
                return;
            }
            float power = ThrowPower;
            if (input.InteractHeld)
            {
                var gauge = PowerGauge();
                if (gauge != null) gauge.Show(power);
                return;
            }
            throwCharging = false;
            if (powerGauge != null) powerGauge.Hide();
            ThrowCarried(power);
        }

        void CancelThrowCharge()
        {
            throwCharging = false;
            if (powerGauge != null) powerGauge.Hide();
        }

        void ThrowCarried(float power)
        {
            Vector3 velocity = ThrowVelocity(power);
            carried.RequestThrow(Role, ReleasePoint(velocity), velocity);
            Vector3 face = new Vector3(velocity.x, 0f, velocity.z);
            if (face.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(face);
            carried = null;
            SetAnimTrigger(ThrowHash);
            Raise(PlayerEvent.Thrown);
        }

        ThrowPowerGauge PowerGauge()
        {
            if (powerGauge == null && IsLocal && tuning.throwPowerGauge != null)
            {
                var go = Instantiate(tuning.throwPowerGauge);
                go.name = "ThrowPowerGauge (" + name + ")";
                powerGauge = go.GetComponent<ThrowPowerGauge>();
                if (powerGauge == null) powerGauge = go.AddComponent<ThrowPowerGauge>();
            }
            return powerGauge;
        }

        // 포물선 던지기: 카메라가 보는 수평 방향으로, throwArcAngle만큼 올려서. 속도는 파워로 최소~최대 사이.
        // 돌은 Rigidbody 중력으로 날아가므로 궤적은 실제 물리 포물선이다 (가이드라인은 그리지 않는다)
        Vector3 ThrowVelocity(float power)
        {
            Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            Vector3 flat = Vector3.ProjectOnPlane(cam != null ? cam.forward : transform.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f && cam != null) flat = Vector3.ProjectOnPlane(cam.up, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            flat.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            // 카메라를 위로 올리면 높이, 내리면 낮게 던진다 (궤적 미리보기는 일부러 없음)
            float camPitch = cam != null ? Mathf.Asin(Mathf.Clamp(cam.forward.y, -1f, 1f)) * Mathf.Rad2Deg : tuning.throwAimNeutralPitch;
            float arc = tuning.throwArcAngle + (camPitch - tuning.throwAimNeutralPitch) * tuning.throwAimPitchInfluence;
            float lo = Mathf.Clamp(Mathf.Min(tuning.throwMinArcAngle, tuning.throwMaxArcAngle), -30f, 85f);
            float hi = Mathf.Clamp(Mathf.Max(tuning.throwMinArcAngle, tuning.throwMaxArcAngle), -30f, 85f);
            Vector3 dir = Quaternion.AngleAxis(-Mathf.Clamp(arc, lo, hi), right) * flat;
            float speed = Mathf.Lerp(Mathf.Min(tuning.throwMinSpeed, tuning.throwSpeed), tuning.throwSpeed, Mathf.Clamp01(power));
            return dir * speed;
        }

        void UpdateCarry()
        {
            if (carried == null) { carried = null; return; } // 파괴된 경우 정리
            // 다른 사람이 먼저 주웠으면 요청이 거절된다 (온라인은 응답이 늦게 오므로 잠시 기다린다)
            if (simTime - carryRequestTime > 1f && carried.Holder != Role) carried = null;
        }

        // 손에서 놓는 위치. 손은 몸(CharacterController) 안쪽일 수 있어서, 그대로 놓으면 돌이 몸에 끼어 튕겨 나간다 →
        // 던지는 방향(없으면 정면)으로 몸 바깥까지 밀어낸다
        Vector3 ReleasePoint(Vector3 velocity)
        {
            Vector3 p = CarryPoint;
            Vector3 dir = new Vector3(velocity.x, 0f, velocity.z);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (dir.sqrMagnitude < 0.0001f) return p;
            dir.Normalize();
            Vector3 center = transform.position + cc.center;
            float ahead = Vector3.Dot(new Vector3(p.x - center.x, 0f, p.z - center.z), dir);
            float need = cc.radius + ReleaseClearance;
            if (ahead < need) p += dir * (need - ahead);
            return p;
        }

        const float ReleaseClearance = 0.35f; // 몸 바깥 여유 (돌 반지름 0.25 + 여유)

        void DropCarried()
        {
            CancelThrowCharge();
            if (carried == null) { carried = null; return; }
            carried.RequestThrow(Role, ReleasePoint(Vector3.zero), Vector3.zero);
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
            SetAnimTrigger(JumpHash);
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
                    Partner?.PlayPullUp(this, false); // 내 로컬이 E로 구했으면 끌어올리기 연출
                    break;
                case PlayerEvent.Landed:
                case PlayerEvent.Jumped:
                case PlayerEvent.Pushed:
                case PlayerEvent.KnockbackHeavy:
                    remoteHanging = false;
                    break;
                case PlayerEvent.PickedUp: remoteCarrying = true; break;
                case PlayerEvent.Thrown:
                    remoteCarrying = false;
                    SetAnimTrigger(ThrowHash);
                    break;
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
            SetAnimBool(ThrowChargingHash, throwCharging);
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

        void SetAnimFloat(int hash, float value)
        {
            if (HasAnimParam(hash, AnimatorControllerParameterType.Float)) animator.SetFloat(hash, value);
        }

        void SetAnimTrigger(int hash)
        {
            if (HasAnimParam(hash, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(hash);
        }

        // 원격 캐릭터는 시뮬레이션하지 않으므로, 동기화된 위치가 움직인 만큼으로 달리기 애니 Speed를 정한다
        Vector3 remoteLastPos;
        bool remoteHasLast;
        float remoteSpeed;

        void UpdateRemoteSpeed()
        {
            Vector3 pos = transform.position;
            float dt = Time.deltaTime;
            if (remoteHasLast && dt > 0f)
            {
                Vector3 d = pos - remoteLastPos;
                d.y = 0f;
                float v = d.magnitude / dt;
                if (v > tuning.moveSpeed * 4f) v = 0f; // 순간이동·부활은 달리기로 치지 않는다
                remoteSpeed = Mathf.Lerp(remoteSpeed, v, 1f - Mathf.Exp(-15f * dt)); // 네트워크 보간 떨림 완화
            }
            remoteLastPos = pos;
            remoteHasLast = true;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            SetAnimFloat(SpeedHash, remoteSpeed);
            // 원격은 발밑을 짧게 내려다봐서 땅에 있는지 본다 (CharacterController 안에서 시작하므로 자기 몸은 안 맞는다)
            Vector3 feet = transform.position + cc.center - Vector3.up * (cc.height * 0.5f - cc.radius);
            bool onGround = Physics.SphereCast(feet, cc.radius * 0.9f, Vector3.down, out RaycastHit _, cc.radius * 0.1f + 0.15f,
                ~0, QueryTriggerInteraction.Ignore);
            SetGroundAnim(onGround, Time.time);
        }

        // Airborne은 잠깐(AirborneDelay) 이상 공중일 때만 켠다. 계단·비탈에서 땅 판정이 한 틱 꺼져도 낙하 애니가 깜빡이지 않게
        const float AirborneDelay = 0.1f;
        float animGroundAt = float.NegativeInfinity;

        void SetGroundAnim(bool onGround, float now)
        {
            if (onGround) animGroundAt = now;
            SetAnimBool(GroundedHash, onGround);
            SetAnimBool(AirborneHash, !onGround && now - animGroundAt >= AirborneDelay);
        }

        // ---------- 개발 키 · 안내 ----------

        void Update()
        {
            if (!Simulated)
            {
                UpdateGimmickAnimator(); // 원격: 수액 발버둥 등은 각 PC에서 직접 판정한다
                UpdateRemoteSpeed();
            }
            else remoteHasLast = false;
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
            else if (grabbedPushable != null)
                text = grabbedPushable.RequiredPushers >= 2 && !grabbedPushable.VisiblyMoving
                    ? "무거워요! 파트너도 잡고 같은 방향으로 같이 밀어요 · E 놓기"
                    : "앞으로 걸어 밀기 · E 놓기";
            else if (Event.current.type == EventType.Repaint && grounded && FindPushable() != null) text = "E 잡기";
            else if (Event.current.type == EventType.Repaint && ThrowableStone.FindNearest(transform.position, tuning.pickUpRange) != null) text = "E 줍기";
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
