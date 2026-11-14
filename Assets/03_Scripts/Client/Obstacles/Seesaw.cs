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
        bool fired;          // 판이 발사 뒤 자세인가
        double firedAt;
        float angle;

        PlayerController local;
        CharacterController localCc;
        bool wasAirborne;
        float airPeakY;
        bool soloSelfPending;

        readonly Dictionary<PlayerController, CharacterController> ccCache = new Dictionary<PlayerController, CharacterController>();

        Vector3 LaunchVelocity
        {
            get
            {
                Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
                return f * launchForwardSpeed + Vector3.up * launchUpSpeed;
            }
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

            if (jumpZone == null || localCc == null || local.IsDead) return;
            if (airPeakY - y < minDropHeight) return;
            if (!jumpZone.bounds.Intersects(localCc.bounds)) return;
            // 켜진 동안 다시 누르면 타이머만 늘어나고 신호가 가지 않는다 → 꺼져 있을 때만 요청
            if (state.IsOn) return;
            // 혼자 테스트: 날아갈 사람이 없으면 밟은 사람이 대신 날아간다 (같은 자리에 착지하도록 속도를 보정)
            soloSelfPending = offlineSoloSelfLaunch && !GameServices.IsOnline && !AnyoneInLaunchZone();
            state.RequestSet(true, SwitchStates.LocalRole);
        }

        bool AnyoneInLaunchZone()
        {
            if (launchZone == null) return false;
            Bounds zone = launchZone.bounds;
            foreach (var pc in PlayerController.All)
            {
                if (pc == null) continue;
                var cc = Cc(pc);
                if (cc != null && zone.Intersects(cc.bounds)) return true;
            }
            return false;
        }

        // 혼자 테스트용 발사: 밟은 쪽 끝에서 출발해도 날아가는 쪽에서 쏜 것과 같은 곳에 떨어지게 한다
        void LaunchSolo()
        {
            if (local == null || launchZone == null) return;
            Vector3 v = LaunchVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;
            if (speed < 0.01f) { local.Launch(v); return; }
            Vector3 dir = flat / speed;
            float g = Mathf.Max(0.1f, previewGravity);
            float rise = transform.position.y + previewLandingHeight - local.transform.position.y;
            float disc = v.y * v.y - 2f * g * rise;
            float airTime = disc > 0f ? (v.y + Mathf.Sqrt(disc)) / g : 2f * v.y / g;
            float extra = Vector3.Dot(launchZone.bounds.center - local.transform.position, dir);
            if (airTime > 0.1f) speed += extra / airTime;
            local.Launch(dir * speed + Vector3.up * v.y);
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

            fired = true;
            firedAt = Time.timeAsDouble;
            PlayFireFx();
            if (soloSelfPending) { soloSelfPending = false; LaunchSolo(); }
            else LaunchPlayers();
        }

        // 판이 튕기는 순간: 밟은 쪽 끝에 쿵 소리와 먼지 (모든 PC)
        void PlayFireFx()
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

        void LaunchPlayers()
        {
            if (launchZone == null) return;
            Bounds zone = launchZone.bounds;
            Vector3 v = LaunchVelocity;
            foreach (var pc in PlayerController.All)
            {
                if (pc == null || !pc.Simulated) continue;
                var cc = Cc(pc);
                if (cc == null || !zone.Intersects(cc.bounds)) continue;
                pc.Launch(v);
            }
        }

        void UpdatePose()
        {
            // 꺼졌고 판 위에 아무도 없으면 원래 자세로 (사람 발밑에서 판이 솟지 않게)
            if (fired && !state.IsOn && !AnyoneOnPlank()) fired = false;

            // 충돌 판은 날아간 사람이 충분히 뜬 뒤에 바꾼다
            bool firedPose = fired && Time.timeAsDouble - firedAt >= ColliderSwapDelay;
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
