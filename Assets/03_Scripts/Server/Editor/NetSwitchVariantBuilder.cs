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
    /// 상태가 있는 레벨 오브젝트의 Net Variant 생성·코스 씬 교체(계약 §6). (S-08 클라 작성 C-1028-4 → 서버가 S-09 골까지 확장)
    /// Lever_*·PressurePlate_*: 루트에 NetworkObject + LocalSwitchState 옆에 NetSwitchState.
    /// Set_*: Set 루트에만 NetworkObject, 안쪽 LocalSwitchState마다 NetSwitchState (중첩 NetworkObject 없음).
    /// Goal: 루트에 NetworkObject + LocalGoalState 옆에 NetGoalState.
    /// 메뉴 0 하나로 Variant 갱신 → 코스 씬 원본 인스턴스 전부 교체 → 연결·누락 점검 → 저장. 여러 번 실행해도 안전.
    /// </summary>
    public static class NetSwitchVariantBuilder
    {
        const string CoursePath = "Assets/01_Scenes/11_SpiralCourse.unity";

        // 원본 폴더 → Variant 폴더, 대상 이름 접두사
        static readonly (string Source, string Target, string[] Prefixes)[] Groups =
        {
            ("Assets/02_Prefabs/Client/Level/Switches", "Assets/02_Prefabs/Net/Level/Switches", new[] { "Lever_", "PressurePlate_", "Set_" }),
            ("Assets/02_Prefabs/Client/Level/Progress", "Assets/02_Prefabs/Net/Level/Progress", new[] { "Goal" }),
        };

        [MenuItem("VAROGO/Net/0. 코스 씬 온라인 동기화 적용 (전체)")]
        public static void ApplyAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildVariants();
            BuildGimmickVariants();
            SwapCourse();
        }

        [MenuItem("VAROGO/Net/1. 스위치·골 Net Variant 만들기")]
        public static void BuildVariants()
        {
            int count = 0;
            foreach (var group in Groups)
            {
                Directory.CreateDirectory(group.Target);
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { group.Source }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (!group.Prefixes.Any(name.StartsWith)) continue;
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!HasState(source)) continue;

                    var target = $"{group.Target}/{name}_Net.prefab";
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
                    if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing) != source)
                    {
                        Debug.LogError($"[NetVariant] {target}가 {name}의 Variant가 아닙니다. 건너뜀");
                        continue;
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    try
                    {
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
            }
            BuildCheckpointStatePrefab();
            AssetDatabase.SaveAssets();
            Debug.Log($"[NetVariant] Variant 완료 {count}개");
        }

        // S2-06: 체크포인트 공용 상태. 원본 프리팹이 없는 서버 전용 오브젝트라 Variant가 아니다
        const string CheckpointStatePath = "Assets/02_Prefabs/Net/Level/Progress/NetCheckpointState.prefab";

        static GameObject BuildCheckpointStatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointStatePath);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(CheckpointStatePath));
            var go = new GameObject("NetCheckpointState");
            try
            {
                var no = go.AddComponent<NetworkObject>();
                no.Flags |= NetworkObjectFlags.MasterClientObject;
                no.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);
                go.AddComponent<NetCheckpointState>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, CheckpointStatePath, out bool ok);
                Debug.Log($"[NetVariant] {(ok ? "저장" : "실패")}: {CheckpointStatePath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // 코스에 체크포인트가 있고 NetCheckpointState가 없으면 1개 놓는다
        static bool PlaceCheckpointState(UnityEngine.SceneManagement.Scene scene)
        {
            bool hasCheckpoint = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentsInChildren<NetCheckpointState>(true).Length > 0) return false;
                if (root.GetComponentsInChildren<Checkpoint>(true).Length > 0) hasCheckpoint = true;
            }
            if (!hasCheckpoint) return false;
            var prefab = BuildCheckpointStatePrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Debug.Log($"[NetVariant] 배치: {instance.name}", instance);
            return true;
        }

        static bool HasState(GameObject go) =>
            go.GetComponentsInChildren<LocalSwitchState>(true).Length > 0 || go.GetComponentsInChildren<LocalGoalState>(true).Length > 0;

        static void Prepare(GameObject root)
        {
            var no = root.GetComponent<NetworkObject>();
            if (no == null) no = root.AddComponent<NetworkObject>();
            no.Flags |= NetworkObjectFlags.MasterClientObject;
            // Fusion 인스펙터와 같게: Master Client Object면 아래 두 플래그는 꺼야 한다 (방장이 나가도 상태 유지)
            no.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);

            foreach (var nested in root.GetComponentsInChildren<NetworkObject>(true))
                if (nested != no) Debug.LogError($"[NetVariant] 중첩 NetworkObject 발견: {nested.name} ({root.name})");

            // 추가만 한다 (R8)
            foreach (var local in root.GetComponentsInChildren<LocalSwitchState>(true))
                if (local.GetComponent<NetSwitchState>() == null) local.gameObject.AddComponent<NetSwitchState>();
            foreach (var local in root.GetComponentsInChildren<LocalGoalState>(true))
                if (local.GetComponent<NetGoalState>() == null) local.gameObject.AddComponent<NetGoalState>();
        }

        // P18 기믹: 미는 오브젝트·돌멩이(NetworkTransform으로 위치 동기화), 숨겨진 발판(NetSwitchState)
        static readonly (string source, string target)[] GimmickDirs =
        {
            ("Assets/02_Prefabs/Client/Level/Obstacles", "Assets/02_Prefabs/Net/Level/Obstacles"),
            ("Assets/02_Prefabs/Client/Level/Magic", "Assets/02_Prefabs/Net/Level/Magic"),
            ("Assets/02_Prefabs/Client/Level/Flight", "Assets/02_Prefabs/Net/Level/Flight"), // P19 공중 비행 (C-1103-2)
        };

        [MenuItem("VAROGO/Net/4. 기믹 Net Variant 만들기 (밀기·돌멩이·숨겨진 발판)")]
        public static void BuildGimmickVariants()
        {
            int count = 0;
            foreach (var (sourceDir, targetDir) in GimmickDirs)
            {
                if (!AssetDatabase.IsValidFolder(sourceDir)) continue;
                Directory.CreateDirectory(targetDir);
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { sourceDir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = Path.GetFileNameWithoutExtension(path);
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    bool needs = source.GetComponentInChildren<LocalPushableState>(true) != null
                        || source.GetComponentInChildren<LocalCarryState>(true) != null
                        || source.GetComponentInChildren<LocalSwitchState>(true) != null
                        || source.GetComponentInChildren<LocalFlightState>(true) != null;
                    if (!needs) continue;

                    var target = $"{targetDir}/{name}_Net.prefab";
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
                    if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing) != source)
                    {
                        Debug.LogError($"[NetVariant] {target}가 {name}의 Variant가 아닙니다. 건너뜀");
                        continue;
                    }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    try
                    {
                        Prepare(instance);
                        foreach (var p in instance.GetComponentsInChildren<LocalPushableState>(true))
                            if (p.GetComponent<NetPushableState>() == null) p.gameObject.AddComponent<NetPushableState>();
                        foreach (var c in instance.GetComponentsInChildren<LocalCarryState>(true))
                            if (c.GetComponent<NetCarryState>() == null) c.gameObject.AddComponent<NetCarryState>();
                        foreach (var f in instance.GetComponentsInChildren<LocalFlightState>(true))
                            if (f.GetComponent<NetFlightState>() == null) f.gameObject.AddComponent<NetFlightState>();
                        // 움직이는 기믹은 위치를 맞춘다 (루트만. 중첩 NetworkObject 없음)
                        bool moves = instance.GetComponent<Pushable>() != null || instance.GetComponent<ThrowableStone>() != null;
                        if (moves && instance.GetComponent<NetworkTransform>() == null) instance.AddComponent<NetworkTransform>();

                        PrefabUtility.SaveAsPrefabAsset(instance, target, out bool ok);
                        Debug.Log($"[NetVariant] {(ok ? "저장" : "실패")}: {target}");
                        if (ok) count++;
                    }
                    finally
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[NetVariant] 기믹 완료 {count}개");
        }

        [MenuItem("VAROGO/Net/2. 11_SpiralCourse 원본을 Net Variant로 교체")]
        public static void SwapCourse()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(CoursePath, OpenSceneMode.Single);

            var map = new Dictionary<GameObject, GameObject>();
            foreach (var group in Groups)
            {
                if (!AssetDatabase.IsValidFolder(group.Target)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { group.Target }))
                {
                    var variant = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(variant);
                    if (source != null) map[source] = variant;
                }
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
                if (source == null || !map.TryGetValue(source, out var variant)) continue; // 이미 Variant면 source가 Variant라 건너뜀
                var name = go.name;
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, variant, settings, InteractionMode.AutomatedAction);
                go.name = name; // 교체하면 이름이 Variant 이름으로 바뀐다
                Debug.Log($"[NetVariant] 교체: {name} → {variant.name}", go);
                count++;
            }

            if (PlaceCheckpointState(scene)) count++;

            CheckLinks(scene);
            int missing = Report(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[NetVariant] 씬 교체 완료 {count}개, 동기화 누락 {missing}개");
        }

        [MenuItem("VAROGO/Net/3. 열린 씬 온라인 동기화 점검")]
        public static void CheckOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int missing = Report(scene);
            if (missing == 0) Debug.Log($"[NetVariant] {scene.name}: 동기화 누락 없음");
        }

        static int Report(UnityEngine.SceneManagement.Scene scene)
        {
            var missing = NetSyncCheck.FindMissing(scene);
            foreach (var m in missing) Debug.LogError($"[NetVariant] 온라인 동기화 안 됨: {m}");
            return missing.Count;
        }

        // 교체 후 문·시간 발판·골 연결이 살아 있는지 점검
        static void CheckLinks(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(mb is Door) && !(mb is TimedPlatform) && !(mb is GoalZone) && !(mb is ClearUI)) continue;
                var so = new SerializedObject(mb);
                var obj = so.FindProperty("stateObject");
                var legacy = so.FindProperty("stateSource");
                bool linked = (obj != null && obj.objectReferenceValue != null) || (legacy != null && legacy.objectReferenceValue != null);
                if (linked) Debug.Log($"[NetVariant] 연결 OK: {mb.GetType().Name} {mb.name} → {(obj?.objectReferenceValue != null ? obj.objectReferenceValue.name : legacy.objectReferenceValue.name)}", mb);
                else Debug.LogError($"[NetVariant] 연결 끊김: {mb.GetType().Name} {mb.name}", mb);
            }
        }
    }
}
