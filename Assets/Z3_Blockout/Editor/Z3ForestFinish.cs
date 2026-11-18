using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HiddenPath.Blockout;
using VaroGO.Client;
using VaroGO.Shared;

public static class Z3ForestFinish
{
    const string ScenePath = "Assets/01_Scenes/MapDesign/MapDesign_Stage1_Z0~Z4.unity";
    const string Prefabs = "Assets/04_Resources/Map/TriForge Assets/Fantasy Worlds - Forest/Prefabs/";

    // Explicit batch entry point; never runs automatically on domain reload.
    [MenuItem("Tools/Hidden Path/Finish Z3 Forest")]
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Captures/Z3");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!Application.isBatchMode) throw new Exception("Open MapDesign_Stage1_Z0~Z4 before running this command");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first");
            var puzzle = UnityEngine.Object.FindFirstObjectByType<Z3ForestPuzzle>();
            if (puzzle == null) throw new Exception("Saved Z3 puzzle missing");
            var root = puzzle.transform;
            string before = OutsideSnapshot(root);
            puzzle.bounceDistance = 12f;
            puzzle.bounceHeight = 10f;
            puzzle.bounceDuration = 1.8f;
            var choices = root.GetComponentsInChildren<Z3RouteTrigger>().Where(t => t.kind == Z3RouteTrigger.TriggerKind.Choice).ToArray();
            int questionCount = puzzle.forkReturns.Length;
            if (questionCount == 0 || choices.Length != questionCount * 3 || puzzle.forkReturns.Any(t => t == null))
                throw new Exception("Each question needs three choices and a return point");
            // Plank details must fall with the collapsing slab rather than float in mid-air.
            int seams = 0;
            foreach (var seam in root.GetComponentsInChildren<Transform>().Where(t => t.name == "Plank_Seam").ToArray())
            {
                var floor = choices.Select(t => t.trapFloor).OrderBy(t => (t.position - seam.position).sqrMagnitude).First();
                if (seam.parent != floor && Vector3.Distance(seam.position, floor.position) < 7f)
                { seam.SetParent(floor, true); seams++; }
            }
            if (root.Find("Forest_Undergrowth") == null)
            {
                var detail = new GameObject("Forest_Undergrowth").transform;
                detail.SetParent(root, false);
                var random = new System.Random(31415);
                foreach (var fork in root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Fork_") && t.name.Length == 6).ToArray())
                {
                    // Keep the central fan-out and the 5.4m approach lanes clear.
                    for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 9; i++)
                    {
                        float x = side * (14.2f + (float)random.NextDouble() * 1.8f);
                        float z = -9f + i * 2.3f;
                        Place("Trees/Summer/P_FW01_Bush_0" + (1 + i % 3) + ".prefab", detail,
                            fork.TransformPoint(new Vector3(x, .04f, z)), 1.1f + (float)random.NextDouble(), random.Next(360));
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        float x = (i % 2 == 0 ? -1 : 1) * 15.5f;
                        Place("Rocks/P_fwOF_RockMossy_01.prefab", detail,
                            fork.TransformPoint(new Vector3(x, .02f, -8f + (i / 2) * 7f)), 1.2f, random.Next(360));
                    }
                }
            }
            var visibility = root.GetComponentsInChildren<MagicVisibility>();
            if (visibility.Length != 2 || visibility.Count(v => v.IsVisibleTo(PlayerRole.A_Red)) != 1 ||
                visibility.Count(v => v.IsVisibleTo(PlayerRole.B_Blue)) != 1)
                throw new Exception("Role-specific sign validation failed");
            if (before != OutsideSnapshot(root)) throw new Exception("Transforms outside Z3 changed");
            EditorUtility.SetDirty(puzzle);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Captures/Z3/validation.txt", "PASS: " + questionCount + " questions, " + choices.Length + " choices, role-specific sign, outside-Z3 transforms unchanged.\nBounce: 12m distance / 10m height / 1.8s.\nAttached plank details: " + seams + "\nRuntime multiplayer and walking tests not performed by this editor validation.\n");
            Capture("Overview", new Vector3(180, 175, 80), new Vector3(190, 20, 205), 125f);
            var sign = root.GetComponentsInChildren<Transform>().First(t => t.name == "Magic_Pictorial_Sign");
            var signRenderers = visibility.SelectMany(v => v.GetComponentsInChildren<Renderer>()).ToArray();
            var enabledStates = signRenderers.Select(r => r.enabled).ToArray();
            foreach (var role in new[] { PlayerRole.A_Red, PlayerRole.B_Blue })
            {
                foreach (var v in visibility) foreach (var r in v.GetComponentsInChildren<Renderer>()) r.enabled = v.IsVisibleTo(role);
                Capture("Sign_" + role, sign.TransformPoint(new Vector3(0, 3.6f, -16)), sign.TransformPoint(new Vector3(0, 3.6f, 0)), 4.4f);
            }
            for (int i = 0; i < signRenderers.Length; i++) signRenderers[i].enabled = enabledStates[i];
            // Preview visibility is deliberately not saved.
            Debug.Log("Z3_FINISH_SUCCESS");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    static string OutsideSnapshot(Transform root)
    {
        return string.Join("\n", UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t != root && !t.IsChildOf(root)).OrderBy(t => t.GetInstanceID())
            .Select(t => t.GetInstanceID() + "|" + t.localPosition.ToString("F5") + "|" + t.localRotation.ToString("F5") + "|" + t.localScale.ToString("F5")));
    }

    static void Place(string asset, Transform parent, Vector3 position, float height, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + asset);
        if (prefab == null) throw new Exception("Missing forest prefab: " + asset);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.localScale *= height / Mathf.Max(.01f, bounds.size.y);
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.position += Vector3.up * (position.y - bounds.min.y);
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    static void Capture(string name, Vector3 from, Vector3 target, float size)
    {
        var go = new GameObject("Z3_Temporary_Preview_Camera");
        var camera = go.AddComponent<Camera>();
        go.transform.position = from;
        go.transform.LookAt(target);
        camera.orthographic = true;
        camera.orthographicSize = size;
        camera.farClipPlane = 1500;
        var rt = new RenderTexture(1600, 900, 24);
        camera.targetTexture = rt;
        camera.Render();
        var old = RenderTexture.active;
        RenderTexture.active = rt;
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        texture.Apply();
        File.WriteAllBytes("Captures/Z3/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = old;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
