using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace VaroGO.Client.Loading
{
    // 무거운 배경(로비 숲, 타이틀 숲 등)을 씬에 직접 두지 않고 Addressables로 비동기 생성한다 (2026-11-27 Addressables 도입).
    // 이 오브젝트의 자식으로 만들고, 이 오브젝트가 사라지면(씬 전환) 같이 해제한다. 네트워크와 무관한 로컬 연출 전용.
    // 화면 가림막·진입 연출은 IsDone / Loaded로 로드가 끝날 때를 기다린다 (실패해도 끝난 것으로 본다 — 화면이 영원히 가려지지 않게).
    public class AddressablePrefabLoader : MonoBehaviour
    {
        [Tooltip("불러올 프리팹 (Addressable로 등록된 것)")]
        [SerializeField] AssetReferenceGameObject prefab;

        AsyncOperationHandle<GameObject> handle;

        public bool IsDone { get; private set; }
        public GameObject Instance { get; private set; }
        /// <summary>로드가 끝나면(성공·실패 모두) 한 번. 이미 끝났으면 구독하지 말고 IsDone을 본다.</summary>
        public event Action<AddressablePrefabLoader> Loaded;

        /// <summary>씬에서 처음 찾은 로더(없으면 null).</summary>
        public static AddressablePrefabLoader FindInScene() => FindFirstObjectByType<AddressablePrefabLoader>();

        void Start()
        {
            if (prefab == null || !prefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[Loading] {name}: 불러올 프리팹이 연결되지 않았습니다");
                Finish(null);
                return;
            }
            handle = prefab.InstantiateAsync(transform, false);
            handle.Completed += h =>
            {
                if (h.Status != AsyncOperationStatus.Succeeded)
                    Debug.LogError($"[Loading] {name}: 프리팹 생성 실패 {prefab.RuntimeKey}");
                Finish(h.Status == AsyncOperationStatus.Succeeded ? h.Result : null);
            };
        }

        void Finish(GameObject result)
        {
            if (IsDone) return;
            IsDone = true;
            Instance = result;
            // 앞 씬에서 미리 받아 둔 같은 프리팹이 있으면 이제 놓는다 (인스턴스가 번들 참조를 이어받았다)
            if (prefab != null) AddressablePreload.ReleaseIfHeld(prefab.AssetGUID);
            Loaded?.Invoke(this);
        }

        void OnDestroy()
        {
            if (handle.IsValid()) Addressables.ReleaseInstance(handle);
        }
    }
}
