using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VaroGO.Client;

namespace VaroGO.Net.Editor
{
    /// <summary>
    /// S-08 Net Variant 생성·씬 교체(계약 §6). 클라가 대신 작성 (전달 C-1028-4).
    /// Lever_*·PressurePlate_*: 루트에 NetworkObject + LocalSwitchState 옆에 NetSwitchState.
    /// Set_*: Set 루트에만 NetworkObject, 안쪽 LocalSwitchState마다 NetSwitchState (중첩 NetworkObject 없음).
    /// </summary>
    public static class NetSwitchVariantBuilder
    {
        const string SourceDir = "Assets/02_Prefabs/Client/Level/Switches";
        const string TargetDir = "Assets/02_Prefabs/Net/Level/Switches";
        const string CoursePath = "Assets/01_Scenes/11_SpiralCourse.unity";
        static readonly string[] Prefixes = { "Lever_", "PressurePlate_", "Set_" };

        [MenuItem("VAROGO/Net/1. 스위치 Net Variant 만들기")]
        public static void BuildVariants()
        {
            Directory.CreateDirectory(TargetDir);
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SourceDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (!Prefixes.Any(name.StartsWith)) continue;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source.GetComponentsInChildren<LocalSwitchState>(true).Length == 0) continue;

                var target = $"{TargetDir}/{name}_Net.prefab";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
                    if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing) != source)
                    {
                        Debug.LogError($"[NetVariant] {target}가 {name}의 Variant가 아닙니다. 건너뜀");
                        continue;
                    }
                    Prepare(instance);
                    PrefabUtility.SaveAsPrefabAsset(instance, target, out bool ok);
                    Debug.Log($"[NetVariant] {(ok ? "저장" : "실패")}: {target}");
                    if (ok) count++;
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[NetVariant] 완료 {count}개");
        }

        static void Prepare(GameObject root)
        {
            var no = root.GetComponent<NetworkObject>();
            if (no == null) no = root.AddComponent<NetworkObject>();
            no.Flags |= NetworkObjectFlags.MasterClientObject;
            // Fusion 인스펙터와 같게: Master Client Object면 아래 두 플래그는 꺼야 한다 (방장이 나가도 스위치 유지)
            no.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);

            foreach (var nested in root.GetComponentsInChildren<NetworkObject>(true))
                if (nested != no) Debug.LogError($"[NetVariant] 중첩 NetworkObject 발견: {nested.name} ({root.name})");

            foreach (var local in root.GetComponentsInChildren<LocalSwitchState>(true))
                if (local.GetComponent<NetSwitchState>() == null) local.gameObject.AddComponent<NetSwitchState>();
        }

        [MenuItem("VAROGO/Net/2. 11_SpiralCourse 스위치를 Net Variant로 교체")]
        public static void SwapCourse()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(CoursePath, OpenSceneMode.Single);

            var map = new Dictionary<GameObject, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { TargetDir }))
            {
                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var source = PrefabUtility.GetCorrespondingObjectFromSource(variant);
                if (source != null) map[source] = variant;
            }

            var settings = new PrefabReplacingSettings
            {
                logInfo = true,
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
            };

            int count = 0;
            foreach (var go in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList())
            {
                if (go == null || !PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue; // 앞선 교체로 자식이 다시 만들어지면 null
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source == null || !map.TryGetValue(source, out var variant)) continue;
                var name = go.name;
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, variant, settings, InteractionMode.AutomatedAction);
                go.name = name; // 교체하면 이름이 Variant 이름으로 바뀐다
                Debug.Log($"[NetVariant] 교체: {name} → {variant.name}", go);
                count++;
            }

            CheckDoors(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[NetVariant] 씬 교체 완료 {count}개");
        }

        // 교체 후 문·시간 발판 연결이 살아 있는지 점검
        static void CheckDoors(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(mb is Door) && !(mb is TimedPlatform)) continue;
                var so = new SerializedObject(mb);
                var obj = so.FindProperty("stateObject");
                var legacy = so.FindProperty("stateSource");
                bool linked = (obj != null && obj.objectReferenceValue != null) || (legacy != null && legacy.objectReferenceValue != null);
                if (linked) Debug.Log($"[NetVariant] 연결 OK: {mb.name} → {(obj?.objectReferenceValue != null ? obj.objectReferenceValue.name : legacy.objectReferenceValue.name)}", mb);
                else Debug.LogError($"[NetVariant] 연결 끊김: {mb.name}", mb);
            }
        }
    }
}
