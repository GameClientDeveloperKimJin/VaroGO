using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client
{
    // P18 변형: 돌이 박히는 발판. 숨겨진 발판(RevealablePlatform)과 같이 붙인다.
    // 돌멩이가 맞으면 발판이 드러나 계속 보이고(LocalSwitchState의 autoOffSeconds = 0), 돌은 맞은 자리에 붙어 멈춘다.
    // 붙은 돌은 다시 주울 수 없다. "드러난 발판에 닿아 있는 돌 = 붙은 돌" 규칙이라 따로 동기화할 값이 없다
    // (발판 켜짐은 NetSwitchState, 돌 위치는 NetworkTransform이 맞춘다 → 늦게 들어온 PC도 같은 결과).
    // 구역 리셋으로 발판이 다시 숨으면 돌도 풀려 떨어진다.
    [RequireComponent(typeof(RevealablePlatform))]
    public class StoneStickPlatform : MonoBehaviour
    {
        static readonly List<StoneStickPlatform> all = new List<StoneStickPlatform>();

        [Tooltip("돌 표면이 발판에서 이 거리(m) 안이면 붙은 것으로 본다")]
        [SerializeField, Min(0.01f)] float stickTolerance = 0.15f;

        RevealablePlatform platform;
        Collider[] colliders;
        Collider[] solids; // 밟는 면 (숨어 있는 동안은 RevealablePlatform이 트리거로 바꿔 두므로 시작 때 기억한다)

        public bool Revealed => platform != null && platform.Revealed;

        void Awake()
        {
            platform = GetComponent<RevealablePlatform>();
            var list = new List<Collider>();
            var solidList = new List<Collider>();
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                list.Add(c);
                if (!c.isTrigger) solidList.Add(c);
            }
            colliders = list.ToArray();
            solids = solidList.Count > 0 ? solidList.ToArray() : colliders;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        // 이 콜라이더가 돌이 박히는 발판의 것인가
        public static StoneStickPlatform Of(Collider c) => c != null ? c.GetComponentInParent<StoneStickPlatform>() : null;

        // 드러난 발판 중 하나에 닿아 있는가 (모든 PC에서 같은 답)
        public static bool AnyHolds(Vector3 center, float radius)
        {
            foreach (var p in all)
                if (p != null && p.Holds(center, radius)) return true;
            return false;
        }

        public bool Holds(Vector3 center, float radius)
        {
            if (!Revealed) return false;
            float limit = radius + stickTolerance;
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled) continue;
                if ((c.ClosestPoint(center) - center).sqrMagnitude <= limit * limit) return true;
            }
            return false;
        }

        // 맞은 순간 돌을 놓을 자리: 발판 표면 바깥에 돌 반지름만큼 (이미 표면에 있으면 그대로)
        public Vector3 SnapPoint(Collider hit, Vector3 center, float radius)
        {
            if (hit == null) return center;
            // 감지용 트리거(겉 상자)에 먼저 닿으면 실제 밟는 면(트리거 아닌 콜라이더)에 붙인다 → 떠 보이지 않게
            if (System.Array.IndexOf(solids, hit) < 0)
            {
                float best = float.PositiveInfinity;
                foreach (var c in solids)
                {
                    if (c == null || !c.enabled) continue;
                    float d2 = (c.ClosestPoint(center) - center).sqrMagnitude;
                    if (d2 < best) { best = d2; hit = c; }
                }
            }
            Vector3 closest = hit.ClosestPoint(center);
            Vector3 d = center - closest;
            if (d.sqrMagnitude < 0.000001f) return center; // 안에 들어가 있음 (트리거로 통과 중) → 그 자리
            return closest + d.normalized * Mathf.Min(radius, d.magnitude);
        }
    }
}
