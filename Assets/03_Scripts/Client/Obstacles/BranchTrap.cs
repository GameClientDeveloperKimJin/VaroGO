using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18: 협곡의 거대 나뭇가지. 공용 시계(GameClock)로 주기마다 떨어진다 → 두 PC에서 같은 타이밍.
    // 떨어지기 전 전조(omens)가 보인다. 전조에 MagicVisibility(Red/Blue)를 붙이면 역할마다 다른 전조를 본다.
    // 내려친 순간 hitZone 안에 있던 플레이어는 묶인다 → 파트너가 옆에서 A/D를 번갈아 눌러 구한다.
    // 맞았는지는 맞은 사람의 PC에서만 판정한다 (R6)
    public class BranchTrap : MonoBehaviour
    {
        [Header("타이밍 (초)")]
        [Tooltip("한 번 떨어지고 다시 올라올 때까지 전체 주기")]
        [SerializeField] float period = 6f;
        [Tooltip("시간 어긋남. 여러 개를 놓을 때 타이밍을 다르게 한다")]
        [SerializeField] float phaseOffset;
        [Tooltip("떨어지기 전 전조가 보이는 시간")]
        [SerializeField] float warnTime = 1.5f;
        [Tooltip("내려치는 데 걸리는 시간")]
        [SerializeField] float dropTime = 0.25f;
        [Tooltip("내려친 채 머무는 시간")]
        [SerializeField] float downTime = 1f;
        [Tooltip("다시 올라가는 데 걸리는 시간")]
        [SerializeField] float raiseTime = 1f;

        [Header("연결")]
        [Tooltip("움직일 나뭇가지 모델 (비우면 움직이지 않음)")]
        [SerializeField] Transform branch;
        [Tooltip("내려칠 때 위치 이동 (로컬, m)")]
        [SerializeField] Vector3 dropOffset = new Vector3(0f, -3f, 0f);
        [Tooltip("내려칠 때 회전 (로컬, 도)")]
        [SerializeField] Vector3 downLocalEuler;
        [Tooltip("떨어지기 전 켜지는 전조 오브젝트 (빨강/파랑은 MagicVisibility로 역할마다 다르게)")]
        [SerializeField] GameObject[] omens;
        [Tooltip("맞는 범위. 트리거 콜라이더 권장")]
        [SerializeField] Collider hitZone;
        [Tooltip("내려친 직후 이 시간(초) 동안 범위 안에 들어와도 묶인다")]
        [SerializeField] float hitWindow = 0.2f;

        Vector3 upPos;
        Quaternion upRot;
        int lastHitCycle = int.MinValue;

        void Awake()
        {
            if (branch != null)
            {
                upPos = branch.localPosition;
                upRot = branch.localRotation;
            }
        }

        float Cycle => Mathf.Max(0.1f, period);
        double LocalTime => SwitchStates.Now + phaseOffset;

        void Update()
        {
            double t = LocalTime;
            int cycle = (int)System.Math.Floor(t / Cycle);
            float p = (float)(t - cycle * (double)Cycle); // 0 ~ period
            float dropStart = warnTime;
            float downStart = dropStart + dropTime;
            float raiseStart = downStart + downTime;
            float raiseEnd = raiseStart + raiseTime;

            float k; // 0 = 위, 1 = 내려침
            if (p < dropStart) k = 0f;
            else if (p < downStart) k = (p - dropStart) / Mathf.Max(0.01f, dropTime);
            else if (p < raiseStart) k = 1f;
            else if (p < raiseEnd) k = 1f - (p - raiseStart) / Mathf.Max(0.01f, raiseTime);
            else k = 0f;
            k = Mathf.Clamp01(k);

            if (branch != null)
            {
                float e = p < downStart ? k * k : k; // 떨어질 때 가속
                branch.localPosition = upPos + dropOffset * e;
                branch.localRotation = Quaternion.Slerp(upRot, upRot * Quaternion.Euler(downLocalEuler), e);
            }

            bool warn = p < dropStart;
            if (omens != null)
                foreach (var o in omens)
                    if (o != null && o.activeSelf != warn) o.SetActive(warn);

            if (p >= downStart && p < downStart + hitWindow && lastHitCycle != cycle)
            {
                if (HitPlayers()) lastHitCycle = cycle;
            }
            else if (p >= downStart + hitWindow && lastHitCycle != cycle) lastHitCycle = cycle;
        }

        // 내 PC에서 시뮬레이션하는 플레이어(로컬 + 오프라인 더미)만 판정
        bool HitPlayers()
        {
            if (hitZone == null) return false;
            Bounds zone = hitZone.bounds;
            bool any = false;
            foreach (var pc in PlayerController.All)
            {
                if (pc == null || !pc.Simulated) continue;
                var cc = pc.GetComponent<CharacterController>();
                if (cc == null || !zone.Intersects(cc.bounds)) continue;
                if (pc.Bind()) any = true;
            }
            return any;
        }

        void OnDrawGizmosSelected()
        {
            if (hitZone == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.35f);
            var b = hitZone.bounds;
            Gizmos.DrawCube(b.center, b.size);
        }
    }
}
