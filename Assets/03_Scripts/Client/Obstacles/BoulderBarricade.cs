using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 길을 막은 통나무 더미. 사람은 지나갈 수 없고, 큰 바위(Pushable)를 밀어 부딪치면 부서져 길이 열린다.
    // 바위 위치는 네트워크로 맞춰지므로 각 PC가 자기 화면에서 "바위가 닿았는가"를 본다.
    // 같은 오브젝트에 스위치 상태(ISwitchState)가 있으면 "켜짐 = 부서짐"으로 알린다 → 온라인(Barricade_Log_Net)은
    // 나중에 들어온 PC도 부서진 상태로 본다. 상태가 없으면 예전처럼 각자 부순다.
    // 한 번 부서지면 영구. 구역 리셋으로 상태가 꺼지면 다시 켜 달라고 요청해 PC끼리 어긋나지 않게 한다.
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
        ISwitchState state;
        bool stateOnline;
        float enabledAt;

        public bool Broken { get; private set; }

        void Awake()
        {
            var root = intact != null ? intact : gameObject;
            solids = root.GetComponentsInChildren<Collider>(true);
        }

        void OnEnable() => enabledAt = Time.time;

        // intact가 이 오브젝트 자신이면 부서질 때 꺼지므로 OnDisable에서는 구독을 풀지 않고 파괴될 때 푼다
        void OnDestroy()
        {
            if (state != null) state.Changed -= OnStateChanged;
        }

        // 온라인 등록이 늦게 올 수 있어 온라인 여부가 바뀌면 다시 고른다. 스위치 상태가 없으면 null (경고 없이)
        ISwitchState State()
        {
            if (state == null || stateOnline != GameServices.IsOnline)
            {
                if (state != null) state.Changed -= OnStateChanged;
                stateOnline = GameServices.IsOnline;
                state = GetComponent<ISwitchState>() != null ? SwitchStates.Resolve(gameObject, null, this) : null;
                if (state != null) state.Changed += OnStateChanged;
            }
            return state;
        }

        // 부서진 뒤 구역 리셋 등으로 꺼지면 다시 켠다 (권한 PC만 실제로 반영, 중복 요청은 무해)
        void OnStateChanged(bool on)
        {
            if (!on && Broken) state?.RequestSet(true, PlayerRole.None);
        }

        void Update()
        {
            if (Broken || solids == null) return;
            var s = State();
            // 다른 PC에서 부서졌다 (늦게 들어온 경우 처음부터 켜져 있음 → 소리·이펙트 없이)
            if (s != null && s.IsOn) { Break(Time.time - enabledAt > 1f); return; }
            foreach (var p in Pushable.All)
            {
                if (p == null || p.RequiredPushers < minPushers) continue;
                if (!Near(p)) continue;
                s?.RequestSet(true, PlayerRole.None);
                Break(true);
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

        void Break(bool effects)
        {
            Broken = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Barricade] {name} 부서짐 (연출={effects})");
#endif
            Vector3 pos = transform.position;
            if (solids.Length > 0 && solids[0] != null) pos = solids[0].bounds.center;
            if (!effects) { (intact != null ? intact : gameObject).SetActive(false); return; }
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
            if (!Application.isPlaying || Broken) return;
            State()?.RequestSet(true, PlayerRole.None);
            Break(true);
        }
    }
}
