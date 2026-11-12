using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 밟기 이펙트: 플레이어가 이 오브젝트에 착지하거나(Landed) 튕겨 오를 때(Jumped, 점프 버섯) 밟은 자리에 이펙트를 띄운다.
    // 플레이어 이벤트(로컬·원격 모두 PlayerController.Raise)를 기준으로 각 PC에서 따로 만든다 → 네트워크 오브젝트 불필요, 상대 화면에도 보인다.
    // 투명 마법(MagicVisibility)은 이 PC에서 보일 때만 띄운다 → 안 보이는 발판의 위치를 드러내지 않는다.
    public class StepFx : MonoBehaviour
    {
        [Tooltip("밟은 자리에 띄울 이펙트 프리팹 (1회 재생 후 스스로 삭제되는 것)")]
        [SerializeField] GameObject vfxPrefab;
        [Tooltip("이펙트 크기 배율")]
        [SerializeField, Min(0.05f)] float vfxScale = 1f;
        [Tooltip("켜면 오브젝트 가로 크기에 맞춰 이펙트를 키운다 (큰 버섯 등). 기준 너비에서 1배, 2배 크기면 약 1.4배")]
        [SerializeField] bool scaleWithSize;
        [Tooltip("scaleWithSize 기준 너비 (m). 이 너비일 때 vfxScale 그대로")]
        [SerializeField, Min(0.1f)] float referenceWidth = 1f;
        [Tooltip("착지할 때 띄운다 (발판)")]
        [SerializeField] bool onLand = true;
        [Tooltip("이 오브젝트에서 튕겨 오를 때 띄운다 (점프 버섯)")]
        [SerializeField] bool onJump;
        [Tooltip("같은 오브젝트에서 연달아 터지지 않게 막는 시간 (초)")]
        [SerializeField] float cooldown = 0.2f;

        MagicVisibility visibility;
        Collider[] colliders;
        float lastPlayed = float.NegativeInfinity;

        static readonly RaycastHit[] hits = new RaycastHit[8];

        void Awake()
        {
            visibility = GetComponent<MagicVisibility>();
            colliders = GetComponentsInChildren<Collider>(true);
        }

        // PlayerController.Raise에서 부른다 (로컬·원격 플레이어 모두)
        public static void Notify(PlayerController player, PlayerEvent e)
        {
            if (player == null || (e != PlayerEvent.Landed && e != PlayerEvent.Jumped)) return;
            // 발밑 오브젝트 찾기. 원격 플레이어는 네트워크 위치가 조금 늦을 수 있어 넉넉히 쏜다
            Vector3 origin = player.transform.position + Vector3.up * 0.6f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 2.5f, ~0, QueryTriggerInteraction.Ignore);
            StepFx best = null;
            float bestDist = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider is CharacterController || h.distance >= bestDist) continue;
                bestDist = h.distance;
                best = h.collider.GetComponentInParent<StepFx>();
                point = h.point;
            }
            if (best == null) return;
            if (e == PlayerEvent.Landed ? !best.onLand : !best.onJump) return;
            best.Play(point);
        }

        public void Play(Vector3 point)
        {
            if (vfxPrefab == null || !isActiveAndEnabled) return;
            if (Time.time - lastPlayed < cooldown) return;
            if (visibility != null && !MagicVisibility.DebugShowAll && !visibility.IsVisibleToLocal) return;
            lastPlayed = Time.time;
            var fx = Instantiate(vfxPrefab, point, Quaternion.identity);
            fx.transform.localScale = vfxPrefab.transform.localScale * (vfxScale * SizeFactor());
        }

        float SizeFactor()
        {
            if (!scaleWithSize) return 1f;
            float width = 0f;
            foreach (var c in colliders)
                if (c != null && !c.isTrigger) width = Mathf.Max(width, Mathf.Max(c.bounds.size.x, c.bounds.size.z));
            if (width <= 0f) return 1f;
            return Mathf.Clamp(Mathf.Sqrt(width / referenceWidth), 0.6f, 2f);
        }
    }
}
