using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-10 / C2-05: 트리거 영역. 로컬 플레이어가 지나가면 ICheckpointState에 내 역할 도착을 알린다.
    // 두 역할이 모두 도착하면 둘의 공용 부활 지점이 된다 (오프라인은 혼자 도착 = 활성).
    // 표시: 대기 / 한 명 도착(반 점등) / 활성
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("코스 순서. 0, 1, 2… 로 늘려가며 준다. 더 큰 값이 활성되면 작은 값은 다시 저장되지 않는다")]
        [SerializeField] int order;
        [Tooltip("부활 위치·방향. 비어 있으면 이 오브젝트 위치")]
        [SerializeField] Transform spawnPoint;
        [Tooltip("상태 색을 바꿀 Renderer. 비어 있으면 자식에서 찾는다")]
        [SerializeField] Renderer indicator;
        [Tooltip("아무도 안 밟았을 때 색")]
        [SerializeField] Color idleColor = new Color(0.45f, 0.45f, 0.45f);
        [Tooltip("한 명만 밟았을 때 색 (반 점등)")]
        [SerializeField] Color halfColor = new Color(0.9f, 0.75f, 0.2f);
        [Tooltip("둘 다 밟아 저장됐을 때 색")]
        [SerializeField] Color activeColor = new Color(0.3f, 1f, 0.4f);

        static readonly List<Checkpoint> all = new List<Checkpoint>();

        ICheckpointState subscribed;
        MaterialPropertyBlock block;

        public int Order => order;
        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

        // order 순으로 정렬된 목록
        public static IReadOnlyList<Checkpoint> All => all;

        // Order가 같은 체크포인트가 여러 개면 먼저 찾은 것
        public static Checkpoint Find(int order)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i].order == order) return all[i];
            return null;
        }

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void Awake()
        {
            if (indicator == null) indicator = GetComponentInChildren<Renderer>();
        }

        void OnEnable()
        {
            all.Add(this);
            all.Sort((a, b) => a.order.CompareTo(b.order));
            Subscribe();
        }

        void OnDisable()
        {
            all.Remove(this);
            Unsubscribe();
        }

        // 씬마다 상태 구현이 새로 등록되므로(오프라인 → 온라인 덮어쓰기) 바뀌면 다시 구독한다
        void Update()
        {
            if (subscribed != GameServices.Checkpoints) Subscribe();
        }

        void Subscribe()
        {
            Unsubscribe();
            subscribed = GameServices.Checkpoints;
            if (subscribed != null) subscribed.Changed += Refresh;
            Refresh();
        }

        void Unsubscribe()
        {
            if (subscribed != null) subscribed.Changed -= Refresh;
            subscribed = null;
        }

        void OnTriggerEnter(Collider other)
        {
            var respawn = other.GetComponent<PlayerRespawn>();
            if (respawn != null) respawn.ReachCheckpoint(this);
        }

        void Refresh()
        {
            if (indicator == null) return;
            var state = subscribed;
            Color color = idleColor;
            if (state != null)
            {
                if (order <= state.ActiveOrder && (state.IsReached(order, PlayerRole.A_Red) || state.IsReached(order, PlayerRole.B_Blue)))
                    color = activeColor;
                else if (state.IsReached(order, PlayerRole.A_Red) || state.IsReached(order, PlayerRole.B_Blue))
                    color = halfColor;
            }
            if (block == null) block = new MaterialPropertyBlock();
            indicator.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            indicator.SetPropertyBlock(block);
        }

        void OnDrawGizmos()
        {
            var p = SpawnPoint;
            Gizmos.color = new Color(0.3f, 1f, 0.4f);
            Gizmos.DrawWireSphere(p.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawLine(p.position + Vector3.up * 0.9f, p.position + Vector3.up * 0.9f + p.forward);
        }
    }
}
