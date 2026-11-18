using UnityEngine;

namespace VaroGO.Client
{
    // 길을 막은 통나무 더미. 사람은 지나갈 수 없고, 큰 바위(Pushable)를 밀어 부딪치면 부서져 길이 열린다.
    // 바위 위치는 네트워크로 맞춰지므로 각 PC가 자기 화면에서 "바위가 닿았는가"를 본다 → 따로 동기화하지 않는다.
    // (바위가 지나간 뒤 들어온 PC는 더미가 남아 보일 수 있다. 온라인 버전을 만들 때 부서진 상태를 동기화한다)
    public class BoulderBarricade : MonoBehaviour
    {
        [Tooltip("이 인원 이상이 밀어야 하는 바위만 부술 수 있다. 2 = 큰 바위만, 1 = 작은 돌도")]
        [SerializeField, Range(1, 2)] int minPushers = 2;
        [Tooltip("바위 표면이 이 거리(m) 안으로 오면 부서진다")]
        [SerializeField, Min(0.05f)] float breakDistance = 0.3f;
        [Tooltip("부서질 때 꺼질 모델·충돌 묶음. 비우면 이 오브젝트 전체")]
        [SerializeField] GameObject intact;
        [Tooltip("부서질 때 띄울 이펙트 (비워도 됨)")]
        [SerializeField] GameObject breakVfx;
        [SerializeField] float breakVfxScale = 2.5f;
        [Range(0f, 1f), Tooltip("부서질 때 소리 크기. 0이면 소리 없음")]
        [SerializeField] float breakVolume = 0.9f;

        Collider[] solids;

        public bool Broken { get; private set; }

        void Awake()
        {
            var root = intact != null ? intact : gameObject;
            solids = root.GetComponentsInChildren<Collider>(true);
        }

        void Update()
        {
            if (Broken || solids == null) return;
            foreach (var p in Pushable.All)
            {
                if (p == null || p.RequiredPushers < minPushers) continue;
                if (!Near(p)) continue;
                Break();
                return;
            }
        }

        bool Near(Pushable p)
        {
            // 바위 중심 (오브젝트 원점은 바닥이라 쓰면 안 된다 → 충돌 모양의 가운데)
            Vector3 center = p.transform.position;
            foreach (var pc in p.GetComponentsInChildren<Collider>())
            {
                if (pc == null || pc.isTrigger || !pc.enabled) continue;
                center = pc.bounds.center;
                break;
            }
            foreach (var col in solids)
            {
                if (col == null || col.isTrigger || !col.enabled) continue;
                var mesh = col as MeshCollider;
                Vector3 onMe = mesh != null && !mesh.convex ? col.bounds.ClosestPoint(center) : col.ClosestPoint(center);
                if (p.SurfaceDistance(onMe) <= breakDistance) return true;
            }
            return false;
        }

        void Break()
        {
            Broken = true;
            Vector3 pos = transform.position;
            if (solids.Length > 0 && solids[0] != null) pos = solids[0].bounds.center;
            if (breakVolume > 0f) AudioSource.PlayClipAtPoint(TempSfx.Thud, pos, breakVolume);
            if (breakVfx != null)
            {
                var fx = Instantiate(breakVfx, pos, Quaternion.identity);
                fx.transform.localScale *= breakVfxScale;
            }
            (intact != null ? intact : gameObject).SetActive(false);
        }

        // 테스트: 플레이 중 컴포넌트 메뉴 → Test Break
        [ContextMenu("Test Break")]
        public void TestBreak()
        {
            if (Application.isPlaying && !Broken) Break();
        }
    }
}
