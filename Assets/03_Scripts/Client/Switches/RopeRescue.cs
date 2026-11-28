using System;
using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // Stage2 Z3 밧줄 구조. 먼저 올라간 사람이
    //   1) 상층에서 밧줄을 줍고(E) → 2) 구조 고리에 건다(E, 창살이 열리고 밧줄이 내려감)
    //   → 3) 아래 표식에 선 파트너를 끌어올린다(E, 3.5초) → 4) 끝나면 출구 문이 열린다.
    // 단계는 두 PC가 공유한다(IRopeRescueState). 끌려 올라가는 이동은 끌려가는 본인 PC가 자기 캐릭터를 좌석에 붙여 옮긴다
    // (위치는 플레이어 동기화로 전달). 오프라인 혼자 테스트에서는 파트너가 없으면 끌어올리기를 바로 끝낸다.
    public class RopeRescue : MonoBehaviour
    {
        [Tooltip("주울 밧줄 (주우면 숨김)")]
        [SerializeField] GameObject ropeCoil;
        [Tooltip("고리에 건 뒤 보이는 내려간 밧줄")]
        [SerializeField] GameObject ropeLine;
        [Tooltip("고리에 걸면 열리는 창살 (숨김)")]
        [SerializeField] GameObject rescueGate;
        [Tooltip("구조가 끝나면 열리는 출구 문 (숨김)")]
        [SerializeField] GameObject exitGate;
        [Tooltip("밧줄을 거는 고리 위치 (끌어올리기도 여기서)")]
        [SerializeField] Transform anchor;
        [Tooltip("파트너가 서서 기다리는 아래 표식")]
        [SerializeField] Transform rescueBottom;
        [Tooltip("끌어올린 파트너가 도착하는 위 위치")]
        [SerializeField] Transform rescueTop;
        [SerializeField, Min(0.5f)] float useRange = 2.5f;
        [SerializeField, Min(0.5f)] float waitRange = 2.5f;
        [SerializeField, Min(0.5f)] float pullSeconds = 3.5f;

        int localStage;
        int appliedStage = -1;

        IRopeRescueState net;
        bool netResolved;
        IRopeRescueState Net
        {
            get
            {
                if (!GameServices.IsOnline) return null;
                if (!netResolved)
                {
                    netResolved = true;
                    net = GetComponent<IRopeRescueState>();
                    if (net != null) { net.Changed += Apply; net.Pulled += OnPulled; }
                }
                return net != null && net.Ready ? net : null;
            }
        }
        bool Ready => !GameServices.IsOnline || Net != null;
        int Stage => Net != null ? Net.Stage : localStage;

        string prompt;

        // 끌어올리는 중 (내 캐릭터)
        PlayerController passenger;
        Transform carrier;
        Vector3 liftStart;
        float liftTime;

        void Start() => Apply();

        void OnDestroy()
        {
            if (net != null) { net.Changed -= Apply; net.Pulled -= OnPulled; }
            UnsubscribeAll();
            Release();
        }

        void Apply()
        {
            if (!Ready) return;
            int s = Stage;
            if (s == appliedStage) return;
            appliedStage = s;
            if (ropeCoil != null) ropeCoil.SetActive(s < 1);
            if (ropeLine != null) ropeLine.SetActive(s >= 2);
            if (rescueGate != null) rescueGate.SetActive(s < 2);
            if (exitGate != null) exitGate.SetActive(s < 3);
        }

        // 로컬 몸마다 상호작용을 구독한다 (온라인은 1명, 한 에디터 2인 테스트는 2명)
        readonly Dictionary<PlayerController, Action> handlers = new Dictionary<PlayerController, Action>();
        readonly List<PlayerController> stale = new List<PlayerController>();

        void SubscribeLocals()
        {
            stale.Clear();
            foreach (var kv in handlers)
                if (kv.Key == null || !kv.Key.IsLocal || !kv.Key.isActiveAndEnabled) stale.Add(kv.Key);
            foreach (var p in stale)
            {
                if (p != null) p.Interacted -= handlers[p];
                handlers.Remove(p);
            }
            foreach (var p in PlayerController.All)
            {
                if (p == null || !p.IsLocal || handlers.ContainsKey(p)) continue;
                var who = p;
                Action h = () => OnInteract(who);
                handlers[p] = h;
                p.Interacted += h;
            }
        }

        void UnsubscribeAll()
        {
            foreach (var kv in handlers) if (kv.Key != null) kv.Key.Interacted -= kv.Value;
            handlers.Clear();
        }

        static PlayerController Local()
        {
            foreach (var p in PlayerController.All) if (p != null && p.IsLocal) return p;
            return null;
        }

        bool Near(PlayerController p, Transform t, float range) =>
            p != null && t != null && Vector3.Distance(p.transform.position, t.position) <= range;

        bool Near(PlayerController p, GameObject g, float range) => g != null && Near(p, g.transform, range);

        void Update()
        {
            if (Ready && appliedStage != Stage) Apply();
            var local = Local();
            SubscribeLocals();
            prompt = null;
            if (local != null && Ready && passenger == null && !local.IsDead)
            {
                int s = Stage;
                if (s == 0 && Near(local, ropeCoil, useRange)) prompt = "E: 밧줄 줍기";
                else if (s == 1 && Near(local, anchor, useRange)) prompt = "E: 고리에 밧줄 걸기";
                else if (s == 2 && Near(local, anchor, useRange))
                    prompt = PartnerWaiting(local) || !HasPartner(local) ? "E: 동료 끌어올리기" : "동료가 아래 밧줄 표식에 서야 합니다";
            }
            UpdateLift();
        }

        static bool HasPartner(PlayerController local) => local.Partner != null;
        bool PartnerWaiting(PlayerController local) => Near(local.Partner, rescueBottom, waitRange);

        void OnInteract(PlayerController local)
        {
            if (local == null || !Ready || passenger != null) return;
            int s = Stage;
            if (s == 0 && Near(local, ropeCoil, useRange)) SetStage(1);
            else if (s == 1 && Near(local, anchor, useRange)) SetStage(2);
            else if (s == 2 && Near(local, anchor, useRange))
            {
                var partner = local.Partner;
                if (partner == null)
                {
                    if (!GameServices.IsOnline) SetStage(3); // 혼자 테스트: 끌어올릴 사람이 없으면 바로 끝
                    return;
                }
                if (!PartnerWaiting(local)) return;
                var n = Net;
                if (n != null) n.RequestPull(partner.Role);
                else if (!GameServices.IsOnline) StartLift(partner);
            }
        }

        void SetStage(int stage)
        {
            var n = Net;
            if (n != null) { n.RequestStage(stage); return; }
            if (GameServices.IsOnline) return;
            if (stage > localStage) localStage = stage;
            Apply();
        }

        // 온라인: 끌어올리기 시작. 끌려가는 본인 PC만 자기 캐릭터를 옮긴다
        void OnPulled()
        {
            if (net == null) return;
            PlayerController local = null;
            foreach (var p in PlayerController.All) if (p != null && p.IsLocal && p.Role == net.PullTarget) local = p;
            if (local == null) return;
            if (!Near(local, rescueBottom, waitRange + 1.5f)) return;
            StartLift(local);
        }

        void StartLift(PlayerController p)
        {
            if (rescueTop == null || passenger != null) return;
            passenger = p;
            liftStart = p.transform.position;
            liftTime = 0f;
            if (carrier == null)
            {
                carrier = new GameObject("RopeRescueCarrier").transform;
                carrier.SetParent(transform, false);
            }
            carrier.SetPositionAndRotation(liftStart, p.transform.rotation);
            p.SetSeat(carrier, false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Z3] 끌어올리기 시작 역할={p.Role} 위치={liftStart}");
#endif
        }

        void UpdateLift()
        {
            if (passenger == null) return;
            if (passenger.IsDead || passenger.Seat != carrier) { Release(); return; }
            liftTime += Time.deltaTime;
            float u = Mathf.Clamp01(liftTime / pullSeconds);
            var end = rescueTop.position;
            var top = new Vector3(liftStart.x, end.y + 0.3f, liftStart.z);
            // 먼저 벽 바깥으로 위까지 올린 뒤, 위 바닥 안쪽으로 들인다
            carrier.position = u < 0.8f ? Vector3.Lerp(liftStart, top, u / 0.8f) : Vector3.Lerp(top, end, (u - 0.8f) / 0.2f);
            if (u < 1f) return;
            var p = passenger;
            Release();
            p.Teleport(end, rescueTop.rotation);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Z3] 끌어올리기 끝 역할={p.Role} 위치={end}");
#endif
            SetStage(3);
        }

        void Release()
        {
            if (passenger != null && passenger.Seat == carrier) passenger.SetSeat(null);
            passenger = null;
        }

        void OnDisable() => Release();

        // 그레이박스용 임시 안내. UI가 들어오면 교체한다.
        void OnGUI()
        {
            if (string.IsNullOrEmpty(prompt)) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            GUI.Label(new Rect(0, Screen.height * 0.65f, Screen.width, 30), prompt, style);
        }
    }
}
