using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace VaroGO.Client.Loading
{
    // 무거운 배경(로비 숲 등)을 씬에 직접 두지 않고 Addressables로 비동기 생성한다 (2026-11-27 Addressables 도입).
    // 이 오브젝트의 자식으로 만들고, 이 오브젝트가 사라지면(씬 전환) 같이 해제한다. 네트워크와 무관한 로컬 연출 전용.
    public class AddressablePrefabLoader : MonoBehaviour
    {
        [Tooltip("불러올 프리팹 (Addressable로 등록된 것)")]
        [SerializeField] AssetReferenceGameObject prefab;

        AsyncOperationHandle<GameObject> handle;

        void Start()
        {
            if (prefab == null || !prefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[Loading] {name}: 불러올 프리팹이 연결되지 않았습니다");
                return;
            }
            handle = prefab.InstantiateAsync(transform, false);
            handle.Completed += h =>
            {
                if (h.Status != AsyncOperationStatus.Succeeded)
                    Debug.LogError($"[Loading] {name}: 프리팹 생성 실패 {prefab.RuntimeKey}");
            };
        }

        void OnDestroy()
        {
            if (handle.IsValid()) Addressables.ReleaseInstance(handle);
        }
    }
}
