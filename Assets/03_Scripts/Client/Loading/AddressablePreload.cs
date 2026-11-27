using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace VaroGO.Client.Loading
{
    // 다음 씬에서 쓸 무거운 Addressable 프리팹(로비 숲 배경 등)을 지금 씬에 있을 때 미리 메모리에 올려 둔다.
    // 씬이 바뀌어도 핸들을 들고 있다가(번들·텍스처가 내려가지 않게), 다음 씬의 AddressablePrefabLoader가 같은 프리팹을 다 만들면 놓는다.
    // 예: TitleScene에서 로비 숲 배경을 미리 로드 → LobbyScene 진입 때 바로 뜬다 (2026-11-28 로비 "맨 무대" 버그).
    public class AddressablePreload : MonoBehaviour
    {
        [Tooltip("미리 올려 둘 프리팹 (Addressable로 등록된 것)")]
        [SerializeField] AssetReferenceGameObject prefab;
        [Tooltip("이 씬의 배경 로드가 끝난 뒤에 시작한다 (같이 받으면 둘 다 늦어진다)")]
        [SerializeField] bool waitForSceneLoader = true;

        // 키(GUID) → 미리 받은 핸들. 씬이 바뀌어도 유지된다
        static readonly Dictionary<string, AsyncOperationHandle<GameObject>> held = new();

        void Start()
        {
            if (prefab == null || !prefab.RuntimeKeyIsValid()) return;
            var loader = waitForSceneLoader ? AddressablePrefabLoader.FindInScene() : null;
            if (loader != null && !loader.IsDone) loader.Loaded += _ => Begin();
            else Begin();
        }

        void Begin()
        {
            string key = prefab.AssetGUID;
            if (held.ContainsKey(key)) return;
            held[key] = Addressables.LoadAssetAsync<GameObject>(key);
        }

        /// <summary>로더가 프리팹 생성을 끝내면 부른다. 미리 받아 둔 핸들이 있으면 놓는다(인스턴스가 참조를 이어받음).</summary>
        public static void ReleaseIfHeld(string assetGuid)
        {
            if (string.IsNullOrEmpty(assetGuid) || !held.TryGetValue(assetGuid, out var h)) return;
            held.Remove(assetGuid);
            if (h.IsValid()) Addressables.Release(h);
        }

        /// <summary>미리 받기가 진행 중이거나 끝났는지.</summary>
        public static bool IsHeld(string assetGuid) => !string.IsNullOrEmpty(assetGuid) && held.ContainsKey(assetGuid);
    }
}
