using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VaroGO.Client.EditorTools
{
    // 타이틀 3D 숲 배경 프리팹(Title_ForestBackground)을 만든다. 숲 에셋은 Addressables Shared_Forest 그룹을 그대로 쓰고,
    // 프리팹 자체는 Title_Background 그룹에 넣어 TitleScene(빌드 목록 씬)이 숲 에셋을 직접 참조하지 않게 한다.
    // 카메라 기준: (0, 1.7, 0)에서 +Z를 본다. 화면 왼쪽 1/3은 로고·버튼 자리라 어둡게 두고, 오른쪽 중앙에 빛나는 돌무더기.
    static class TitleForestBuilder
    {
        public const string PrefabPath = "Assets/02_Prefabs/Client/Map/Title/Title_ForestBackground.prefab";
        const string Forest = "Assets/04_Resources/Map/TriForge Assets/Fantasy Worlds - Forest/";
        const string GroundMat = "Assets/04_Resources/KIMJIN_ArtWork/Material/LobbyStage/M_LobbyGround.mat";
        const string LobbyPrefab = "Assets/02_Prefabs/Client/Map/Lobby/Lobby_CampBackground.prefab";

        static Transform root;

        [MenuItem("VAROGO/Title/Build Title Forest Prefab")]
        public static void BuildMenu()
        {
            var go = Build();
            SavePrefab(go);
            Object.DestroyImmediate(go);
        }

        public static GameObject Build()
        {
            var go = new GameObject("Title_ForestBackground");
            root = go.transform;

            // 땅
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(0f, 0f, 40f);
            ground.transform.localScale = new Vector3(14f, 1f, 14f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMat);

            var trees = Group("Trees");
            // 앞 왼쪽 큰 나무: 로고 뒤를 어둡게 감싼다
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_L_1", new Vector3(-7.5f, 0f, 9f), 20f, 1.15f);
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_M_1", new Vector3(-11f, 0f, 16f), 140f, 1.1f);
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_M_2", new Vector3(-6f, 0f, 24f), 75f, 1f);
            Put(trees, "Prefabs/Trees/P_FW01_Tree_B_03", new Vector3(-9f, 0f, 34f), 10f, 1.2f);
            // 앞 오른쪽 큰 나무: 오른쪽 끝을 막아 시선이 숲길로 모이게
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_L_1", new Vector3(9.5f, 0f, 12f), 200f, 1.1f);
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_M_1", new Vector3(12f, 0f, 22f), 300f, 1.05f);
            Put(trees, "Prefabs/Trees/P_FW01_Tree_B_06", new Vector3(7f, 0f, 30f), 45f, 1.1f);
            // 위를 덮는 나뭇잎 지붕 (하늘이 덜 보이게)
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_L_1", new Vector3(-15f, 0f, 7f), 260f, 1.3f);
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_L_1", new Vector3(16f, 0f, 9f), 100f, 1.3f);
            Put(trees, "Prefabs/Trees/P_fwOF_Tree_M_2", new Vector3(-2.5f, 0f, 19f), 200f, 1.25f);
            // 먼 나무 벽
            var rng = new System.Random(11);
            string[] far = { "P_fwOF_Tree_M_1", "P_fwOF_Tree_M_2", "P_fwOF_Tree_S_1", "P_fwOF_Tree_S_2", "P_FW01_Tree_B_01", "P_FW01_Tree_B_04", "P_FW01_Tree_B_08" };
            for (int i = 0; i < 26; i++)
            {
                float x = Mathf.Lerp(-30f, 30f, (float)rng.NextDouble());
                float z = Mathf.Lerp(42f, 70f, (float)rng.NextDouble());
                if (Mathf.Abs(x - 3f) < 5f && z < 52f) x += x < 3f ? -6f : 6f; // 마을·길 끝은 비운다
                Put(trees, "Prefabs/Trees/" + far[rng.Next(far.Length)], new Vector3(x, 0f, z), rng.Next(360), 0.9f + 0.4f * (float)rng.NextDouble());
            }

            // 숲길: 카메라 앞에서 돌무더기를 지나 마을까지
            var path = Group("Path");
            string[] stones = { "P_fwOF_Stone_01", "P_fwOF_Stone_02", "P_fwOF_Stone_03", "P_fwOF_Stone_04", "P_fwOF_Stone_05" };
            for (int i = 0; i < 28; i++)
            {
                float t = i / 27f;
                float z = Mathf.Lerp(3f, 44f, t);
                float cx = Mathf.Lerp(0.6f, 3.2f, t) + Mathf.Sin(t * 5f) * 0.8f;
                float x = cx + ((float)rng.NextDouble() - 0.5f) * 2.2f;
                Put(path, "Prefabs/Rocks/" + stones[rng.Next(stones.Length)], new Vector3(x, 0f, z), rng.Next(360), 0.6f + 0.5f * (float)rng.NextDouble());
            }
            for (int i = 0; i < 10; i++)
            {
                float z = Mathf.Lerp(4f, 30f, (float)rng.NextDouble());
                Put(path, "Prefabs/Foliage/P_fwOF_Leaf_Pile_0" + (1 + rng.Next(2)), new Vector3(Mathf.Lerp(-1f, 4f, (float)rng.NextDouble()), 0f, z), rng.Next(360), 1f);
            }

            // 빛나는 돌무더기 (화면 오른쪽 중앙)
            var cairn = Group("Cairn");
            cairn.localPosition = new Vector3(2.3f, 0f, 7.5f);
            cairn.localScale = Vector3.one * 1.6f;
            // 중심 선돌 (2026-11-28 KJ: 이끼 돌무더기 → 선돌). 룬 면이 카메라(원점)를 보게 돌린다. 그룹이 1.6배라 실제 크기 = 0.4 × 1.6
            float faceCam = Mathf.Atan2(-cairn.localPosition.x, -cairn.localPosition.z) * Mathf.Rad2Deg;
            Put(cairn, "Prefabs/Doodads/P_fwOF_Standing_Stone_01", Vector3.zero, faceCam, 0.3f);
            Put(cairn, "Particles/VFX_FW01_LootSparkle_01", new Vector3(0f, 1.5f, -0.2f), 0f, 0.8f);
            var glow = new GameObject("CairnGlow", typeof(Light));
            glow.transform.SetParent(cairn, false);
            glow.transform.localPosition = new Vector3(0f, 0.35f, -1.1f); // 선돌 발치 앞에서 위로 비춘다 (룬 면이 하얗게 날아가지 않게)
            var gl = glow.GetComponent<Light>();
            gl.type = LightType.Point; gl.color = new Color(1f, 0.82f, 0.45f); gl.intensity = 1.6f; gl.range = 3.5f; gl.shadows = LightShadows.None;

            // 선돌(Standing Stone) 고리: 돌무더기를 제단처럼 뒤에서 반원으로 둘러싼다. 각 돌은 고리 중심을 향한다
            var ring = Group("StandingStones");
            ring.localPosition = cairn.localPosition;
            // 가운데(숲길·뒤 마을 쪽)는 비워 두고 양옆에만 세운다
            string[] standing = { "P_fwOF_Standing_Stone_03", "P_fwOF_Standing_Stone_02", "P_fwOF_Standing_Stone_06", "P_fwOF_Standing_Stone_04", "P_fwOF_Standing_Stone_01" };
            float[] angles = { -80f, -48f, 42f, 72f, 102f }; // 0 = 돌무더기 바로 뒤(+Z)
            float[] heights = { 0.55f, 0.5f, 0.55f, 0.5f, 0.6f };
            const float radius = 5f, back = 2.5f; // 고리 중심을 돌무더기보다 조금 뒤로
            for (int i = 0; i < standing.Length; i++)
            {
                float a = angles[i] * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius + back);
                float yaw = Mathf.Atan2(-pos.x, -(pos.z - back)) * Mathf.Rad2Deg + rng.Next(-8, 9);
                Put(ring, "Prefabs/Doodads/" + standing[i], pos, yaw, heights[i]);
            }
            // 숲 깊은 곳 왼쪽에 하나 (깊이감, 안개 속에서 룬이 은은하게)
            Put(ring, "Prefabs/Doodads/P_fwOF_Standing_Stone_05", new Vector3(-8f, 0f, 16f), 25f, 0.6f);

            // 바위·풀·고사리
            var deco = Group("Deco");
            Put(deco, "Prefabs/Rocks/P_fwOF_RockMossy_02", new Vector3(-3.2f, 0f, 7f), 60f, 0.9f);
            Put(deco, "Prefabs/Rocks/P_fwOF_RockMossy_04", new Vector3(5.6f, 0f, 7.5f), 200f, 0.8f);
            Put(deco, "Prefabs/Rocks/P_fwOF_Rock_L_02", new Vector3(-12f, 0f, 28f), 90f, 1f);
            string[] plants = { "P_fwOF_Fern_01", "P_fwOF_Fern_02", "P_fwOF_Fern_03", "P_fwOF_ForestPlant_01", "P_fwOF_ForestPlant_02", "P_fwOF_ForestPlant_B_03", "P_fwOF_Grass_L_1", "P_fwOF_Grass_L_2", "P_fwOF_Grass_M_1" };
            string[] flowers = { "P_fwOF_Flowers_02_A", "P_fwOF_Flowers_03_B", "P_fwOF_Flowers_04_C", "P_FWOF_Flower_Large_A_01", "P_FWOF_Flower_Large_B_02" };
            // 앞쪽 가장자리를 풀숲으로 감싼다 (화면 아래 양 모서리)
            string[] edge = { "P_fwOF_Fern_01", "P_fwOF_Fern_02", "P_fwOF_Fern_03", "P_fwOF_Grass_L_1", "P_fwOF_Grass_L_2", "P_fwOF_Grass_L_3" };
            for (int i = 0; i < 24; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = Mathf.Lerp(1.6f, 5f, (float)rng.NextDouble());
                float x = side * Mathf.Lerp(1.4f, 4.5f, (float)rng.NextDouble()) + (side > 0 ? 1.5f : 0f);
                Put(deco, "Prefabs/Foliage/" + edge[rng.Next(edge.Length)], new Vector3(x, 0f, z), rng.Next(360), 1.1f + 0.6f * (float)rng.NextDouble());
            }
            string[] cover = { "P_fwOF_Grass_L_1", "P_fwOF_Grass_L_2", "P_fwOF_Grass_M_1", "P_fwOF_Grass_S_01", "P_fwOF_Clover_01", "P_fwOF_Clover_02", "P_fwOF_Moss_M_1" };
            for (int i = 0; i < 220; i++)
            {
                float z = Mathf.Lerp(2f, 40f, Mathf.Pow((float)rng.NextDouble(), 1.4f));
                float pathX = Mathf.Lerp(0.6f, 3.2f, z / 44f);
                float off = Mathf.Lerp(1.3f, 14f, Mathf.Pow((float)rng.NextDouble(), 0.8f)) * (rng.Next(2) == 0 ? -1f : 1f);
                Put(deco, "Prefabs/Foliage/" + cover[rng.Next(cover.Length)], new Vector3(pathX + off, 0f, z), rng.Next(360), 1.4f + 0.9f * (float)rng.NextDouble());
            }
            for (int i = 0; i < 90; i++)
            {
                float z = Mathf.Lerp(2.5f, 36f, Mathf.Pow((float)rng.NextDouble(), 1.6f));
                float side = rng.Next(2) == 0 ? -1f : 1f;
                float pathX = Mathf.Lerp(0.6f, 3.2f, z / 44f);
                float x = pathX + side * Mathf.Lerp(2.2f, 9f, (float)rng.NextDouble());
                bool flower = rng.Next(5) == 0;
                string name = flower ? flowers[rng.Next(flowers.Length)] : plants[rng.Next(plants.Length)];
                Put(deco, "Prefabs/Foliage/" + name, new Vector3(x, 0f, z), rng.Next(360), 0.8f + 0.6f * (float)rng.NextDouble());
            }

            // 뒤쪽 옛 마을(폐허 벽) — 안개 속에 지붕 없는 집처럼 보이게
            var village = Group("Village");
            village.localPosition = new Vector3(4f, 0f, 46f);
            Put(village, "Prefabs/Ruins/P_FW01_Ruins_Wall_Window_01", new Vector3(-3f, 0f, 0f), 170f, 1f);
            Put(village, "Prefabs/Ruins/P_FW01_Ruins_Wall_Door_01", new Vector3(1.5f, 0f, -1f), 190f, 1f);
            Put(village, "Prefabs/Ruins/P_FW01_Ruins_Wall_Corner_Tall", new Vector3(5f, 0f, 1f), 200f, 1f);
            Put(village, "Prefabs/Ruins/P_FW01_Ruins_Wall_Window_02", new Vector3(-7f, 0f, 4f), 150f, 1f);
            Put(village, "Prefabs/Ruins/P_FW01_Ruins_Pillar", new Vector3(-1f, 0f, -3f), 0f, 1f);
            Put(root, "Prefabs/Mountains/P_fwOF_BackgroundMountain_01", new Vector3(10f, 0f, 160f), 180f, 1.4f);

            // 빛·입자
            var fx = Group("FX");
            Put(fx, "Particles/VFX_FW01_LightRays_01", new Vector3(5f, 7f, 18f), 0f, 1.4f);
            Put(fx, "Particles/VFX_FW01_LightRays_01", new Vector3(-1f, 8f, 30f), 0f, 1.2f);
            Put(fx, "Particles/VFX_FW01_Mist_01", new Vector3(2f, 0.2f, 22f), 0f, 1.5f);
            Put(fx, "Particles/VFX_FW01_Mist_02", new Vector3(4f, 0.2f, 40f), 0f, 2f);
            Put(fx, "Particles/VFX_FW01_Dust_01", new Vector3(2.4f, 1.5f, 9f), 0f, 1f);
            Put(fx, "Particles/VFX_FW01_BlowingLeaves_01", new Vector3(0f, 4f, 10f), 0f, 1f);
            Put(fx, "Particles/VFX_FW01_Butterfly_01", new Vector3(4f, 1f, 8f), 0f, 1f);
            CopyFromLobby(fx, "Fireflies", new Vector3(1.5f, 1.4f, 8f));

            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.SetParent(root, false);
            sun.transform.localRotation = Quaternion.Euler(28f, -150f, 0f); // 뒤 오른쪽에서 들어오는 역광
            var sl = sun.GetComponent<Light>();
            sl.type = LightType.Directional; sl.color = new Color(1f, 0.9f, 0.72f); sl.intensity = 1.6f; sl.shadows = LightShadows.Soft;
            var fill = new GameObject("Fill", typeof(Light));
            fill.transform.SetParent(root, false);
            fill.transform.localRotation = Quaternion.Euler(20f, 20f, 0f);
            var fl = fill.GetComponent<Light>();
            fl.type = LightType.Directional; fl.color = new Color(0.55f, 0.75f, 0.6f); fl.intensity = 0.35f; fl.shadows = LightShadows.None;

            return go;
        }

        // 타이틀 씬과 같은 안개·환경광 값 (TitleScene에 그대로 적용한다)
        public static void ApplyRenderSettings()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.56f, 0.66f, 0.55f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.46f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.18f, 0.12f);
        }

        // 빈 임시 씬에 숲을 만들고 카메라 구도별로 PNG를 남긴다 (씬·프리팹은 저장하지 않음)
        public static string RenderPreview(string dir, Vector3[] camPos, Vector3[] camRot, float fov)
        {
            System.IO.Directory.CreateDirectory(dir);
            var prev = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var tmp = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
            try
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(tmp);
                Build();
                ApplyRenderSettings();
                var cg = new GameObject("PreviewCam", typeof(Camera));
                var cam = cg.GetComponent<Camera>();
                cam.scene = tmp; cam.fieldOfView = fov; cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor;
                var data = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                var v = new GameObject("PreviewVolume").AddComponent<UnityEngine.Rendering.Volume>();
                v.isGlobal = true;
                v.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(VolumePath);
                var rt = new RenderTexture(960, 540, 24);
                cam.targetTexture = rt;
                for (int i = 0; i < camPos.Length; i++)
                {
                    cg.transform.SetPositionAndRotation(camPos[i], Quaternion.Euler(camRot[i]));
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    tex.Apply();
                    System.IO.File.WriteAllBytes($"{dir}/p{i}.png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                return "ok";
            }
            catch (System.Exception e) { return "err " + e; }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(prev);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(tmp, true);
            }
        }

        public const string VolumePath = "Assets/04_Resources/Map/TriForge Assets/Fantasy Worlds - Forest/Scenes/PostProcessing/VP_FW01_Summer.asset";

        public static void SavePrefab(GameObject go)
        {
            System.IO.Directory.CreateDirectory("Assets/02_Prefabs/Client/Map/Title");
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        static GameObject Put(Transform parent, string rel, Vector3 pos, float yaw, float scale)
        {
            var asset = Find(rel);
            if (asset == null) { Debug.LogWarning("[TitleForest] 없음: " + rel); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = go.transform.localScale * scale;
            return go;
        }

        // 프리팹은 하위 폴더(계절별)에 흩어져 있어 이름으로 찾는다. 같은 이름이면 경로가 짧은 것(기본 여름판)
        static readonly Dictionary<string, GameObject> cache = new();
        static GameObject Find(string rel)
        {
            string name = System.IO.Path.GetFileName(rel);
            if (cache.TryGetValue(name, out var hit)) return hit;
            GameObject best = null; string bestPath = null;
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { Forest.TrimEnd('/') }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) != name) continue;
                if (bestPath == null || p.Length < bestPath.Length) { bestPath = p; }
            }
            if (bestPath != null) best = AssetDatabase.LoadAssetAtPath<GameObject>(bestPath);
            cache[name] = best;
            return best;
        }

        static void CopyFromLobby(Transform parent, string child, Vector3 pos)
        {
            var lobby = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefab);
            var src = lobby != null ? lobby.transform.Find("FX/" + child) : null;
            if (src == null) return;
            var go = Object.Instantiate(src.gameObject, parent, false);
            go.name = child;
            go.transform.localPosition = pos;
        }
    }
}
