using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 착지 이펙트: 플레이어가 착지(PlayerEvent.Landed)하면 발밑 바닥 종류에 맞는 이펙트를 발 위치에 띄운다.
    // 이벤트는 로컬·원격 모두 PlayerController.EventRaised로 오므로 각 PC가 따로 재생한다 (네트워크 오브젝트 불필요).
    // 높은 곳에서 떨어졌거나 강한 넉백 뒤 착지면 Heavy 이펙트.
    // 바닥 종류: LandSurface 컴포넌트 → 마법 발판 색(MagicVisibility) → 지형(Terrain) 레이어 이름 → 머티리얼·오브젝트 이름 → 기본 흙먼지
    public class PlayerLandFx : MonoBehaviour
    {
        [SerializeField] PlayerController player;

        [Header("이펙트 프리팹 (1회 재생 후 스스로 사라지는 것)")]
        [Tooltip("기본 흙먼지. 바닥 종류를 모를 때")]
        [SerializeField] GameObject dust;
        [SerializeField] GameObject grass;
        [SerializeField] GameObject stone;
        [SerializeField] GameObject wood;
        [SerializeField] GameObject magicRed;
        [SerializeField] GameObject magicBlue;
        [Tooltip("높은 곳에서 떨어졌을 때 (큰 먼지 + 충격파 + 파편)")]
        [SerializeField] GameObject heavy;

        [Header("조절")]
        [Tooltip("이펙트 크기 배율 (프리팹 루트 스케일에 곱한다)")]
        [SerializeField, Min(0.05f)] float scale = 1f;
        [Tooltip("이 높이(m) 이상 떨어져 착지하면 Heavy 이펙트. 제자리 점프 높이는 약 1.6m")]
        [SerializeField] float heavyFallHeight = 4f;
        [Tooltip("켜면 강한 넉백(장애물·밀기)으로 날아간 뒤 착지도 Heavy")]
        [SerializeField] bool heavyAfterKnockback = true;
        [Tooltip("Heavy일 때 바닥 종류 이펙트도 같이 띄운다")]
        [SerializeField] bool heavyAlsoSurface = true;
        [Tooltip("연달아 터지지 않게 막는 시간 (초)")]
        [SerializeField] float cooldown = 0.15f;

        static readonly RaycastHit[] hits = new RaycastHit[8];
        float peakY;
        bool flying;
        float lastPlayed = float.NegativeInfinity;

        /// <summary>이 컴포넌트가 켜져 있으면 PlayerReactionFx는 예전 착지 먼지를 뿌리지 않는다</summary>
        public bool Active => isActiveAndEnabled;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            peakY = transform.position.y;
        }

        void OnEnable()
        {
            if (player != null) player.EventRaised += OnPlayerEvent;
        }

        void OnDisable()
        {
            if (player != null) player.EventRaised -= OnPlayerEvent;
        }

        // 원격 플레이어도 동기화된 위치로 높이를 잰다
        void LateUpdate()
        {
            if (player == null) return;
            float y = player.transform.position.y;
            if (y > peakY) peakY = y;
        }

        void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case PlayerEvent.KnockbackHeavy:
                case PlayerEvent.Pushed:
                    flying = true;
                    break;
                case PlayerEvent.Respawned:
                case PlayerEvent.Died:
                    flying = false;
                    peakY = player.transform.position.y;
                    break;
                case PlayerEvent.Landed:
                    float fall = peakY - player.transform.position.y;
                    bool isHeavy = fall >= heavyFallHeight || (heavyAfterKnockback && flying);
                    flying = false;
                    peakY = player.transform.position.y;
                    PlayLanding(isHeavy);
                    break;
            }
        }

        void PlayLanding(bool isHeavy)
        {
            if (Time.time - lastPlayed < cooldown) return;
            if (!FindGround(out RaycastHit hit)) return;
            // 이 PC에서 안 보이는 마법 발판이면 띄우지 않는다 (발판 위치가 드러나지 않게)
            var magic = hit.collider.GetComponentInParent<MagicVisibility>();
            if (magic != null && !MagicVisibility.DebugShowAll && !magic.IsVisibleToLocal) return;
            lastPlayed = Time.time;

            var surface = Prefab(Detect(hit, magic));
            if (isHeavy && heavy != null)
            {
                Spawn(heavy, hit.point);
                if (!heavyAlsoSurface || surface == dust) return;
            }
            Spawn(surface, hit.point);
        }

        bool FindGround(out RaycastHit best)
        {
            best = default;
            // 원격 플레이어는 네트워크 위치가 조금 늦을 수 있어 넉넉히 쏜다
            Vector3 origin = player.transform.position + Vector3.up * 0.6f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 2.5f, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider is CharacterController || h.distance >= bestDist) continue;
                bestDist = h.distance;
                best = h;
            }
            return bestDist < float.PositiveInfinity;
        }

        static LandSurfaceType Detect(RaycastHit hit, MagicVisibility magic)
        {
            var col = hit.collider;
            var explicitSurface = col.GetComponentInParent<LandSurface>();
            if (explicitSurface != null && explicitSurface.type != LandSurfaceType.Auto) return explicitSurface.type;
            if (magic != null)
            {
                if (magic.Type == MagicType.Red) return LandSurfaceType.MagicRed;
                if (magic.Type == MagicType.Blue) return LandSurfaceType.MagicBlue;
            }
            if (col is TerrainCollider tc && tc.terrainData != null)
            {
                var t = FromName(DominantTerrainLayer(tc, hit.point));
                if (t != LandSurfaceType.Auto) return t;
            }
            var r = col.GetComponentInParent<Renderer>();
            if (r == null) r = col.GetComponentInChildren<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                var t = FromName(r.sharedMaterial.name);
                if (t != LandSurfaceType.Auto) return t;
            }
            var byObject = FromName(col.name);
            return byObject != LandSurfaceType.Auto ? byObject : LandSurfaceType.Dust;
        }

        static string DominantTerrainLayer(TerrainCollider tc, Vector3 point)
        {
            var data = tc.terrainData;
            var layers = data.terrainLayers;
            if (layers == null || layers.Length == 0) return null;
            Vector3 local = point - tc.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            var map = data.GetAlphamaps(x, z, 1, 1);
            int best = 0;
            for (int i = 1; i < map.GetLength(2); i++) if (map[0, 0, i] > map[0, 0, best]) best = i;
            var layer = best < layers.Length ? layers[best] : null;
            if (layer == null) return null;
            return layer.name + " " + (layer.diffuseTexture != null ? layer.diffuseTexture.name : "");
        }

        // 이름에 들어 있는 낱말로 바닥 종류를 추측한다
        static LandSurfaceType FromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return LandSurfaceType.Auto;
            string n = name.ToLowerInvariant();
            if (Has(n, "grass", "moss", "leaf", "lawn", "잔디", "풀")) return LandSurfaceType.Grass;
            if (Has(n, "wood", "plank", "log", "bridge", "crate", "barrel", "나무", "목재")) return LandSurfaceType.Wood;
            if (Has(n, "stone", "rock", "cliff", "brick", "pebble", "boulder", "돌", "바위")) return LandSurfaceType.Stone;
            return LandSurfaceType.Auto;
        }

        static bool Has(string n, params string[] words)
        {
            foreach (var w in words) if (n.Contains(w)) return true;
            return false;
        }

        GameObject Prefab(LandSurfaceType t)
        {
            GameObject p = null;
            switch (t)
            {
                case LandSurfaceType.Grass: p = grass; break;
                case LandSurfaceType.Stone: p = stone; break;
                case LandSurfaceType.Wood: p = wood; break;
                case LandSurfaceType.MagicRed: p = magicRed; break;
                case LandSurfaceType.MagicBlue: p = magicBlue; break;
            }
            return p != null ? p : dust;
        }

        void Spawn(GameObject prefab, Vector3 point)
        {
            if (prefab == null) return;
            var fx = Instantiate(prefab, point, Quaternion.identity);
            fx.transform.localScale = prefab.transform.localScale * scale;
        }
    }
}
