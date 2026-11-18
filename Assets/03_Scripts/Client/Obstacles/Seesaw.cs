using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 통나무 시소. 한 명이 올라간 쪽 끝(jumpZone)에 뛰어내리면 반대쪽 끝(launchZone)에 서 있던 사람이 날아간다.
    // 신호는 같은 오브젝트의 스위치 상태(LocalSwitchState, 온라인은 NetSwitchState)로 두 PC에 전달한다 (계약 §3.3).
    //   밟은 사람 PC: 로컬 플레이어가 뛰어내린 것을 보고 RequestSet(true) (R4)
    //   모든 PC: 켜진 것을 보고 자기 PC에서 시뮬레이션하는 플레이어만 날린다 (R6, BranchTrap과 같은 방식)
    // 날아가는 거리는 launchForwardSpeed × 체공 시간. 선택하면 예상 포물선이 Scene 뷰에 보인다.
    // 스위치의 Auto Off Seconds = 시소가 다시 쓸 수 있게 되기까지의 시간 (1.5 권장. 0이면 한 번만 동작)
    public class Seesaw : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("상태 오브젝트 (LocalSwitchState가 붙은 GameObject). 비우면 자기 자신")]
        [SerializeField] GameObject stateObject;
        [Tooltip("뛰어내려 밟는 쪽 범위 (트리거 콜라이더)")]
        [SerializeField] Collider jumpZone;
        [Tooltip("날아가는 사람이 서는 쪽 범위 (트리거 콜라이더). 좁을수록 착지점이 일정하다")]
        [SerializeField] Collider launchZone;
        [Header("양방향 (둘 다 넣으면 양쪽으로 날릴 수 있다. 비우면 한쪽으로만)")]
        [Tooltip("앞쪽(+Z) 끝의 뛰어내려 밟는 범위. 판이 뒤집혀 앞쪽 끝이 올라와 있을 때 쓴다")]
        [SerializeField] Collider jumpZoneFront;
        [Tooltip("뒤쪽(-Z) 끝의 날아가는 사람이 서는 범위. 앞쪽 끝을 밟으면 여기 선 사람이 뒤로 날아간다")]
        [SerializeField] Collider launchZoneBack;
        [Tooltip("뒤로 날릴 때 수평 속도 (m/s). 0보다 작으면 앞으로 날릴 때와 같다")]
        [SerializeField] float launchBackForwardSpeed = -1f;
        [Tooltip("뒤로 날릴 때 위로 속도 (m/s). 0보다 작으면 앞으로 날릴 때와 같다")]
        [SerializeField] float launchBackUpSpeed = -1f;

        [Header("모델·충돌")]
        [Tooltip("기울어지는 판 모델. 로컬 X축으로 돈다 (+Z 끝 = 날아가는 쪽)")]
        [SerializeField] Transform plankVisual;
        [Tooltip("평소 자세의 충돌 판 (날아가는 쪽이 내려가 있음)")]
        [SerializeField] GameObject restCollider;
        [Tooltip("발사 뒤 자세의 충돌 판 (밟은 쪽이 내려가 있음)")]
        [SerializeField] GameObject firedCollider;

        [Header("발사")]
        [Tooltip("앞으로 날리는 속도 (m/s). 방향 = 이 오브젝트의 앞(+Z)")]
        [SerializeField] float launchForwardSpeed = 12f;
        [Tooltip("위로 날리는 속도 (m/s). 15 = 약 4.5 m 높이, 체공 약 1.1초")]
        [SerializeField] float launchUpSpeed = 15f;
        [Tooltip("떨어지는 동안 방향키로 더할 수 있는 수평 속도 (m/s). 클수록 착지점을 많이 옮길 수 있다. 0 = 조종 불가. 올라가는 동안은 조종되지 않는다")]
        [SerializeField, Min(0f)] float fallSteerSpeed = 6f;
        [Tooltip("방향키를 눌렀을 때 위 속도에 이르는 빠르기 (m/s²). 작을수록 둔하게 움직인다")]
        [SerializeField, Min(0.1f)] float fallSteerAccel = 20f;
        [Tooltip("이 높이(m) 이상에서 떨어져 밟아야 발동한다. 0.8 = 제자리 점프로도 발동, 걸어 올라오면 발동 안 함")]
        [SerializeField] float minDropHeight = 0.8f;
        [Tooltip("밟은 뒤 날아가기까지 지연 (초)")]
        [SerializeField] float launchDelay = 0.05f;
        [Tooltip("오프라인 혼자 테스트용: 날아가는 쪽 끝에 아무도 없으면, 높은 쪽 끝을 밟은 사람이 대신 날아간다 (온라인에는 영향 없음)")]
        [SerializeField] bool offlineSoloSelfLaunch = true;

        [Header("연출")]
        [Tooltip("판이 튕길 때 소리. 비우면 임시음(쿵)")]
        [SerializeField] AudioClip fireClip;
        [Range(0f, 1f), Tooltip("튕길 때 소리 크기. 0이면 소리 없음")]
        [SerializeField] float fireVolume = 0.8f;
        [Tooltip("판이 튕길 때 밟은 쪽 끝에 띄울 이펙트 (비워도 됨)")]
        [SerializeField] GameObject fireVfx;
        [SerializeField] float fireVfxScale = 1.5f;
        [Tooltip("판이 기울어지는 각도 (도)")]
        [SerializeField] float tiltAngle = 10f;
        [Tooltip("튕겨 올라가는 데 걸리는 시간 (초)")]
        [SerializeField] float flipTime = 0.12f;
        [Tooltip("원래대로 돌아가는 데 걸리는 시간 (초)")]
        [SerializeField] float returnTime = 0.6f;
        [Tooltip("포물선 미리보기에 쓰는 중력. PlayerTuning의 Gravity와 같게 (25)")]
        [SerializeField] float previewGravity = 25f;
        [Tooltip("포물선 미리보기의 착지 높이 (이 오브젝트 기준, m)")]
        [SerializeField] float previewLandingHeight = 1f;

        const float FireWindow = 0.5f;      // 켜진 뒤 이 시간 안에 본 PC만 날린다 (늦게 들어온 PC는 날리지 않음)
        const float ColliderSwapDelay = 0.15f;

        ISwitchState state;
        double handledOnTime = double.NegativeInfinity;
        bool fired;          // (한쪽 방향) 판이 발사 뒤 자세인가
        bool flipped;        // (양방향) 판이 뒤집혀 있는가: false = 앞쪽(+Z) 끝이 내려감, true = 뒤쪽(-Z) 끝이 내려감
        bool soloBack;       // 혼자 테스트 발사가 뒤쪽 방향인가
        double firedAt;
        float angle;

        PlayerController local;
        CharacterController localCc;
        bool wasAirborne;
        float airPeakY;
        bool soloSelfPending;

        readonly Dictionary<PlayerController, CharacterController> ccCache = new Dictionary<PlayerController, CharacterController>();

        // 양방향: 밟는 범위·서는 범위가 양 끝에 모두 있다. 밟힌 끝은 내려간 채로 남고, 다음엔 반대쪽 끝을 밟아 반대로 날린다.
        // 판 자세는 각 PC가 발사 신호를 받을 때마다 뒤집어 맞춘다 (처음부터 같이 있던 PC끼리는 같다.
        // 중간에 들어온 PC는 처음 자세로 시작하므로, 온라인 버전을 만들 때 자세를 따로 동기화해야 한다)
        bool TwoWay => jumpZoneFront != null && launchZoneBack != null;
        Collider ActiveJumpZone => TwoWay && flipped ? jumpZoneFront : jumpZone;
        Collider ActiveLaunchZone => TwoWay && flipped ? launchZoneBack : launchZone;

        Vector3 LaunchVelocity => VelocityFor(false);

        // back = 뒤쪽(-Z)으로 날리는 속도
        Vector3 VelocityFor(bool back)
        {
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            if (!back) return f * launchForwardSpeed + Vector3.up * launchUpSpeed;
            float fwd = launchBackForwardSpeed >= 0f ? launchBackForwardSpeed : launchForwardSpeed;
            float up = launchBackUpSpeed >= 0f ? launchBackUpSpeed : launchUpSpeed;
            return -f * fwd + Vector3.up * up;
        }

        // 범위 모양 그대로 판정한다 (bounds는 시소를 비스듬히 돌려 놓으면 모서리가 부풀어 범위 밖에서도 걸린다)
        static bool Touches(Collider zone, CharacterController cc)
        {
            if (zone == null || cc == null) return false;
            Vector3 center = cc.bounds.center;
            Vector3 d = center - zone.ClosestPoint(center); // 안에 있으면 0
            float halfHeight = Mathf.Max(cc.height * 0.5f, cc.radius);
            return new Vector2(d.x, d.z).magnitude <= cc.radius && Mathf.Abs(d.y) <= halfHeight;
        }

        void OnEnable()
        {
            GameServices.Registered += ResolveState;
            ResolveState();
            angle = tiltAngle;
            ApplyPose(false);
            ApplyAngle();
        }

        void OnDisable()
        {
            GameServices.Registered -= ResolveState;
            state = null;
        }

        // 온라인/오프라인이 바뀌면 상태 구현을 다시 고른다
        void ResolveState()
        {
            state = SwitchStates.Resolve(stateObject != null ? stateObject : gameObject, null, this);
            // 이미 켜져 있던 것은 지나간 발사로 본다
            if (state is ISwitchDetails d && state.IsOn) handledOnTime = d.LastOnTime;
        }

        void Update()
        {
            if (state == null) return;
            WatchLocalJump();
            WatchFire();
            UpdatePose();
        }

        // 로컬 플레이어가 높은 데서 jumpZone에 내려앉았는가
        void WatchLocalJump()
        {
            if (local == null || !local.IsLocal)
            {
                local = null;
                foreach (var p in PlayerController.All)
                    if (p != null && p.IsLocal) { local = p; break; }
                localCc = local != null ? Cc(local) : null;
                wasAirborne = false;
                if (local == null) return;
            }

            float y = local.transform.position.y;
            if (!local.IsGrounded)
            {
                airPeakY = wasAirborne ? Mathf.Max(airPeakY, y) : y;
                wasAirborne = true;
                return;
            }
            if (!wasAirborne) return;
            wasAirborne = false;

            Collider stomp = ActiveJumpZone; // 지금 올라와 있는 끝
            if (stomp == null || localCc == null || local.IsDead) return;
            if (airPeakY - y < minDropHeight) return;
            if (!Touches(stomp, localCc)) return;
            // 켜진 동안 다시 누르면 타이머만 늘어나고 신호가 가지 않는다 → 꺼져 있을 때만 요청
            if (state.IsOn) return;
            // 혼자 테스트: 날아갈 사람이 없으면 밟은 사람이 대신 날아간다 (같은 자리에 착지하도록 속도를 보정)
            soloSelfPending = offlineSoloSelfLaunch && !GameServices.IsOnline && !AnyoneIn(ActiveLaunchZone);
            soloBack = TwoWay && flipped;
            state.RequestSet(true, SwitchStates.LocalRole);
        }

        bool AnyoneIn(Collider zone)
        {
            if (zone == null) return false;
            foreach (var pc in PlayerController.All)
            {
                if (pc == null) continue;
                if (Touches(zone, Cc(pc))) return true;
            }
            return false;
        }

        // 혼자 테스트용 발사: 밟은 쪽 끝에서 출발해도 날아가는 쪽에서 쏜 것과 같은 곳에 떨어지게 한다
        void LaunchSolo(bool back)
        {
            Collider launchZone = back ? launchZoneBack : this.launchZone;
            if (local == null || launchZone == null) return;
            Vector3 v = VelocityFor(back);
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;
            if (speed < 0.01f) { local.Launch(v, fallSteerSpeed, fallSteerAccel); return; }
            Vector3 dir = flat / speed;
            float g = Mathf.Max(0.1f, previewGravity);
            float rise = transform.position.y + previewLandingHeight - local.transform.position.y;
            float disc = v.y * v.y - 2f * g * rise;
            float airTime = disc > 0f ? (v.y + Mathf.Sqrt(disc)) / g : 2f * v.y / g;
            float extra = Vector3.Dot(launchZone.bounds.center - local.transform.position, dir);
            if (airTime > 0.1f) speed += extra / airTime;
            local.Launch(dir * speed + Vector3.up * v.y, fallSteerSpeed, fallSteerAccel);
        }

        // 스위치가 새로 켜졌으면 (LastOnTime이 바뀜) 내 PC의 플레이어를 날린다
        void WatchFire()
        {
            if (!state.IsOn || !(state is ISwitchDetails details)) return;
            double onTime = details.LastOnTime;
            if (onTime == handledOnTime) return;
            double since = SwitchStates.Now - onTime;
            if (since < launchDelay) return;
            handledOnTime = onTime;
            if (since > launchDelay + FireWindow) return;

            // 양방향: 이번 발사 방향 = 지금 내려가 있던 끝 쪽. 발사 뒤 판은 뒤집힌 채 남는다
            bool back = TwoWay && flipped;
            Collider stomped = ActiveJumpZone;
            if (TwoWay) flipped = !flipped; else fired = true;
            firedAt = Time.timeAsDouble;
            PlayFireFx(stomped);
            if (soloSelfPending) { soloSelfPending = false; LaunchSolo(soloBack); }
            else LaunchPlayers(back);
        }

        // 판이 튕기는 순간: 밟은 쪽 끝에 쿵 소리와 먼지 (모든 PC)
        void PlayFireFx(Collider jumpZone)
        {
            Vector3 pos = transform.position;
            if (jumpZone != null) { Bounds b = jumpZone.bounds; pos = new Vector3(b.center.x, b.min.y + 0.3f, b.center.z); }
            if (fireVolume > 0f) AudioSource.PlayClipAtPoint(fireClip != null ? fireClip : TempSfx.Thud, pos, fireVolume);
            if (fireVfx != null)
            {
                var fx = Instantiate(fireVfx, pos, Quaternion.identity);
                fx.transform.localScale *= fireVfxScale;
            }
        }

        void LaunchPlayers(bool back)
        {
            Collider zone = back ? launchZoneBack : launchZone;
            if (zone == null) return;
            Vector3 v = VelocityFor(back);
            foreach (var pc in PlayerController.All)
            {
                if (pc == null || !pc.Simulated) continue;
                if (!Touches(zone, Cc(pc))) continue;
                pc.Launch(v, fallSteerSpeed, fallSteerAccel);
            }
        }

        void UpdatePose()
        {
            bool swapped = Time.timeAsDouble - firedAt >= ColliderSwapDelay; // 충돌 판은 날아간 사람이 충분히 뜬 뒤에 바꾼다
            if (TwoWay)
            {
                // 양방향: 밟힌 끝이 내려간 채로 남는다 (저절로 돌아오지 않음)
                ApplyPose(swapped ? flipped : !flipped);
                float to = flipped ? -tiltAngle : tiltAngle;
                angle = Mathf.MoveTowards(angle, to, 2f * Mathf.Abs(tiltAngle) / Mathf.Max(0.01f, flipTime) * Time.deltaTime);
                ApplyAngle();
                return;
            }

            // 꺼졌고 판 위에 아무도 없으면 원래 자세로 (사람 발밑에서 판이 솟지 않게)
            if (fired && !state.IsOn && !AnyoneOnPlank()) fired = false;

            bool firedPose = fired && swapped;
            ApplyPose(firedPose);

            float target = fired ? -tiltAngle : tiltAngle;
            float duration = Mathf.Max(0.01f, fired ? flipTime : returnTime);
            angle = Mathf.MoveTowards(angle, target, 2f * Mathf.Abs(tiltAngle) / duration * Time.deltaTime);
            ApplyAngle();
        }

        void ApplyPose(bool firedPose)
        {
            if (restCollider != null && restCollider.activeSelf == firedPose) restCollider.SetActive(!firedPose);
            if (firedCollider != null && firedCollider.activeSelf != firedPose) firedCollider.SetActive(firedPose);
        }

        void ApplyAngle()
        {
            if (plankVisual != null) plankVisual.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        bool AnyoneOnPlank()
        {
            if (jumpZone == null && launchZone == null) return false;
            Bounds b = jumpZone != null ? jumpZone.bounds : launchZone.bounds;
            if (launchZone != null) b.Encapsulate(launchZone.bounds);
            // 두 끝 사이(판 가운데)에 서 있는 사람도 잡아야 해서 양 끝을 감싼 상자로 본다
            foreach (var pc in PlayerController.All)
            {
                if (pc == null) continue;
                var cc = Cc(pc);
                if (cc != null && b.Intersects(cc.bounds)) return true;
            }
            return false;
        }

        CharacterController Cc(PlayerController pc)
        {
            if (!ccCache.TryGetValue(pc, out var cc) || cc == null)
            {
                cc = pc.GetComponent<CharacterController>();
                ccCache[pc] = cc;
            }
            return cc;
        }

        // 혼자 테스트: 날아가는 쪽에 서서 컴포넌트 메뉴 → Test Fire (플레이 중)
        [ContextMenu("Test Fire")]
        public void TestFire()
        {
            if (!Application.isPlaying || state == null || state.IsOn) return;
            state.RequestSet(true, SwitchStates.LocalRole);
        }

        void OnDrawGizmosSelected()
        {
            if (launchZone == null) return;
            Bounds b = launchZone.bounds;
            Vector3 start = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 v = LaunchVelocity;
            float g = Mathf.Max(0.1f, previewGravity);
            float landY = transform.position.y + previewLandingHeight;

            Gizmos.color = new Color(1f, 0.8f, 0.1f, 1f);
            Vector3 prev = start;
            const float step = 0.05f;
            for (float t = step; t < 5f; t += step)
            {
                Vector3 p = start + new Vector3(v.x, 0f, v.z) * t + Vector3.up * (v.y * t - 0.5f * g * t * t);
                Gizmos.DrawLine(prev, p);
                prev = p;
                if (v.y - g * t < 0f && p.y <= landY) break;
            }
            Gizmos.DrawWireSphere(prev, 0.5f);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.DrawCube(b.center, b.size);
            if (jumpZone != null)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.3f);
                Gizmos.DrawCube(jumpZone.bounds.center, jumpZone.bounds.size);
            }
        }
    }
}
