using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client
{
    // C-10: 트리거 영역. 로컬 플레이어가 지나가면 그 플레이어의 부활 지점이 된다 (파트너와 따로 기록).
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("F6(다음 체크포인트)에서 쓰는 순서. 작은 값부터")]
        [SerializeField] int order;
        [Tooltip("부활 위치·방향. 비어 있으면 이 오브젝트 위치")]
        [SerializeField] Transform spawnPoint;

        static readonly List<Checkpoint> all = new List<Checkpoint>();

        public int Order => order;
        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

        // order 순으로 정렬된 목록
        public static IReadOnlyList<Checkpoint> All => all;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnEnable()
        {
            all.Add(this);
            all.Sort((a, b) => a.order.CompareTo(b.order));
        }

        void OnDisable()
        {
            all.Remove(this);
        }

        void OnTriggerEnter(Collider other)
        {
            var respawn = other.GetComponent<PlayerRespawn>();
            if (respawn != null) respawn.ReachCheckpoint(this);
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
