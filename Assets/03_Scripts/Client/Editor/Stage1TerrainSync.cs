using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VaroGO.Client.EditorTools
{
    // PlayScene_Stage1 배경 지형(Art_Forest/Terrain_Stage1)을 다시 굽는다: 강바닥 + 북·남 능선 + 양 끝 언덕 + 폭포 홈(WaterfallWater_* 위치).
    // 섬은 IslandVisual, 흙 경사로는 TerrainRampMarker가 각자 메시로 만들기 때문에 여기서는 다루지 않는다.
    // 높이 → 칠 → 풀(디테일) 순서로 전부 다시 만든다. 지형 나무는 새 높이에 맞춰 내린다. 폭포를 옮겼을 때만 실행하면 된다.
    static class Stage1TerrainSync
    {
        const string ScenePath = "Assets/01_Scenes/PlayScene_Stage1.unity";
        const float RiverBed = -15f;

        // 지형 레이어 순서 (TD_Stage1_Forest)
        const int LGrass1 = 0, LGrass2 = 1, LForest = 2, LSoil1 = 3, LRock = 4, LMoss = 5, LSoil2 = 6;

        public static bool IsStage1Open
        {
            get { var s = EditorSceneManager.GetSceneByPath(ScenePath); return s.IsValid() && s.isLoaded; }
        }

        [MenuItem("VAROGO/Map/Stage1 배경 지형 다시 굽기")]
        static void Run()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Stage1 지형", "Play 모드에서는 실행할 수 없습니다.", "확인"); return; }
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { EditorUtility.DisplayDialog("Stage1 지형", "PlayScene_Stage1을 먼저 열어 주세요.", "확인"); return; }

            Terrain terrain = null;
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == "Art_Forest") terrain = r.GetComponentInChildren<Terrain>();
            if (terrain == null) { EditorUtility.DisplayDialog("Stage1 지형", "Art_Forest/Terrain을 찾지 못했습니다.", "확인"); return; }

            var td = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Stage1 배경 지형 다시 굽기");
            var falls = CollectWaterfalls(scene);
            try
            {
                EditorUtility.DisplayProgressBar("Stage1 지형", "높이", 0.1f);
                BakeHeights(terrain, falls);
                EditorUtility.DisplayProgressBar("Stage1 지형", "나무 높이", 0.4f);
                SnapTerrainTrees(terrain);
                EditorUtility.DisplayProgressBar("Stage1 지형", "칠", 0.55f);
                PaintSplat(terrain, falls);
                EditorUtility.DisplayProgressBar("Stage1 지형", "풀", 0.8f);
                PaintDetails(terrain);
            }
            finally { EditorUtility.ClearProgressBar(); }

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Stage1 지형] 배경 지형 다시 굽기 완료 — 폭포 홈 {falls.Count}");
        }

        static List<Vector3> CollectWaterfalls(UnityEngine.SceneManagement.Scene scene)
        {
            var list = new List<Vector3>();
            foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(false))
                if (t.name.StartsWith("WaterfallWater_")) list.Add(t.position);
            return list;
        }

        // ---------- 높이 ----------

        static float S01(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
        static float PN(float a, float b) => Mathf.PerlinNoise(a, b);

        // 강바닥 + 북·남 능선 + 양 끝 언덕
        static float BaseHeight(float x, float z)
        {
            float h = RiverBed + (PN(x * 0.05f + 11, z * 0.05f + 7) - 0.5f) * 1.2f;
            float north = 5f + (z > 104 ? (PN(x * 0.12f, z * 0.12f) - 0.5f) * 0.8f : 0) + S01((z - 121f) / 45f) * 22f * (0.5f + PN(x * 0.02f + 3, z * 0.02f + 5));
            float south = -2f + (z < -4 ? (PN(x * 0.12f + 5, z * 0.12f) - 0.5f) * 0.8f : 0) + S01((-15f - z) / 40f) * 18f * (0.5f + PN(x * 0.02f + 9, z * 0.02f + 1));
            h = Mathf.Lerp(h, north, S01((z - 101f) / 1.5f));
            h = Mathf.Lerp(h, south, S01((-1f - z) / 1.5f));
            float endW = 5f + S01((-22f - x) / 45f) * 20f * (0.5f + PN(x * 0.02f + 2, z * 0.02f + 8));
            float endE = 5f + S01((x - 302f) / 45f) * 20f * (0.5f + PN(x * 0.02f + 6, z * 0.02f + 4));
            h = Mathf.Lerp(h, Mathf.Max(h, endW), S01((-17f - x) / 1.5f));
            h = Mathf.Lerp(h, Mathf.Max(h, endE), S01((x - 297f) / 1.5f));
            return h;
        }

        static void BakeHeights(Terrain terrain, List<Vector3> falls)
        {
            var td = terrain.terrainData; var tp = terrain.transform.position; var sz = td.size;
            int res = td.heightmapResolution;
            float dx = sz.x / (res - 1), dz = sz.z / (res - 1);
            var W = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            {
                float z = tp.z + iz * dz;
                for (int ix = 0; ix < res; ix++)
                {
                    float x = tp.x + ix * dx;
                    float h = BaseHeight(x, z);
                    // 폭포 뒤 절벽을 판다 (폭포 앞 4m ~ 뒤 2.4m)
                    foreach (var f in falls)
                        if (Mathf.Abs(x - f.x) <= 4f && z >= f.z - 4f && z <= f.z + 2.4f && h > RiverBed) h = RiverBed;
                    W[iz, ix] = h;
                }
            }
            var H = new float[res, res];
            for (int iz = 0; iz < res; iz++) for (int ix = 0; ix < res; ix++) H[iz, ix] = Mathf.Clamp01((W[iz, ix] - tp.y) / sz.y);
            td.SetHeights(0, 0, H);
        }

        static void SnapTerrainTrees(Terrain terrain)
        {
            var td = terrain.terrainData;
            var trees = td.treeInstances;
            for (int i = 0; i < trees.Length; i++)
            {
                var p = trees[i].position;
                p.y = td.GetInterpolatedHeight(p.x, p.z) / td.size.y;
                trees[i].position = p;
            }
            td.SetTreeInstances(trees, false);
        }

        // ---------- 칠 ----------

        static void PaintSplat(Terrain terrain, List<Vector3> falls)
        {
            var td = terrain.terrainData; var tp = terrain.transform.position; var sz = td.size;
            int am = td.alphamapResolution, nl = td.terrainLayers.Length;
            var A = new float[am, am, nl];

            for (int az = 0; az < am; az++)
            for (int ax = 0; ax < am; ax++)
            {
                float nx = (ax + 0.5f) / am, nz = (az + 0.5f) / am;
                float x = tp.x + nx * sz.x, z = tp.z + nz * sz.z;
                float h = td.GetInterpolatedHeight(nx, nz) + tp.y;
                float rock = S01((td.GetSteepness(nx, nz) - 38f) / 20f);
                if (h < -11f)
                {
                    float n = PN(x * 0.08f, z * 0.08f);
                    A[az, ax, LSoil2] = 0.6f + n * 0.4f;
                    A[az, ax, LRock] = 0.4f - n * 0.3f + rock;
                }
                else
                {
                    float g = PN(x * 0.04f + 50, z * 0.04f + 50);
                    A[az, ax, LGrass1] = g; A[az, ax, LGrass2] = 1 - g;
                    A[az, ax, LMoss] = S01((PN(x * 0.09f + 7, z * 0.09f + 3) - 0.62f) / 0.1f) * 0.8f;
                    A[az, ax, LRock] = rock * 3f;
                }
            }

            // 나무 밑 숲 바닥
            foreach (var ti in td.treeInstances)
            {
                if (ti.prototypeIndex >= 17) continue;
                ForCircle(am, tp, sz, tp.x + ti.position.x * sz.x, tp.z + ti.position.z * sz.z, 4f, (az, ax, d) =>
                    A[az, ax, LForest] += 2.5f * (1 - d / 4f) * (1 - Mathf.Min(1, A[az, ax, LRock]) * 0.7f));
            }

            // 폭포 홈 안쪽: 바위 + 젖은 흙
            foreach (var f in falls)
            {
                var b = new Bounds(new Vector3(f.x, 0, f.z - 2.5f), new Vector3(11f, 1f, 12f));
                ForBox(am, tp, sz, b, (az, ax) =>
                {
                    float nx = (ax + 0.5f) / am, nz = (az + 0.5f) / am;
                    if (td.GetSteepness(nx, nz) < 40f && td.GetInterpolatedHeight(nx, nz) + tp.y > -11f) return;
                    for (int k = 0; k < nl; k++) A[az, ax, k] = 0;
                    A[az, ax, LRock] = 0.6f; A[az, ax, LSoil2] = 0.4f;
                });
            }

            for (int az = 0; az < am; az++)
            for (int ax = 0; ax < am; ax++)
            {
                float sum = 0; for (int k = 0; k < nl; k++) sum += A[az, ax, k];
                if (sum <= 0) { A[az, ax, LGrass1] = 1; sum = 1; }
                for (int k = 0; k < nl; k++) A[az, ax, k] /= sum;
            }
            td.SetAlphamaps(0, 0, A);
        }

        static void ForCircle(int am, Vector3 tp, Vector3 sz, float cx, float cz, float rad, System.Action<int, int, float> fn)
        {
            int ax0 = Mathf.Max(0, (int)((cx - rad - tp.x) / sz.x * am)), ax1 = Mathf.Min(am - 1, (int)((cx + rad - tp.x) / sz.x * am));
            int az0 = Mathf.Max(0, (int)((cz - rad - tp.z) / sz.z * am)), az1 = Mathf.Min(am - 1, (int)((cz + rad - tp.z) / sz.z * am));
            for (int az = az0; az <= az1; az++)
            for (int ax = ax0; ax <= ax1; ax++)
            {
                float x = tp.x + (ax + 0.5f) / am * sz.x, z = tp.z + (az + 0.5f) / am * sz.z;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d <= rad) fn(az, ax, d);
            }
        }

        static void ForBox(int am, Vector3 tp, Vector3 sz, Bounds b, System.Action<int, int> fn)
        {
            int ax0 = Mathf.Max(0, (int)((b.min.x - tp.x) / sz.x * am)), ax1 = Mathf.Min(am - 1, (int)((b.max.x - tp.x) / sz.x * am));
            int az0 = Mathf.Max(0, (int)((b.min.z - tp.z) / sz.z * am)), az1 = Mathf.Min(am - 1, (int)((b.max.z - tp.z) / sz.z * am));
            for (int az = az0; az <= az1; az++) for (int ax = ax0; ax <= ax1; ax++) fn(az, ax);
        }

        // ---------- 풀 ----------
        // 디테일 13종 순서: 0 작은풀 1 중간풀 2 큰풀 3 큰풀B 4 나무잔해 5 돌 6 나뭇가지 7 숲식물 8 고사리1 9 고사리2 10 낙엽 11 꽃1 12 꽃2

        static void PaintDetails(Terrain terrain)
        {
            var td = terrain.terrainData; var tp = terrain.transform.position; var sz = td.size;
            int dr = td.detailWidth, nd = td.detailPrototypes.Length;
            if (nd < 13) { Debug.LogWarning("[Stage1 지형] 풀 종류가 13개가 아니라 풀은 건너뜁니다."); return; }
            int am = td.alphamapResolution;
            var A = td.GetAlphamaps(0, 0, am, am);
            var L = new int[nd][,]; for (int i = 0; i < nd; i++) L[i] = new int[dr, dr];
            var rnd = new System.Random(5);

            for (int y = 0; y < dr; y++)
            for (int x = 0; x < dr; x++)
            {
                float nx = (x + 0.5f) / dr, nz = (y + 0.5f) / dr;
                if (td.GetInterpolatedHeight(nx, nz) + tp.y < -11f) continue;
                float st = td.GetSteepness(nx, nz); if (st > 32f) continue;
                int ax = Mathf.Min(am - 1, (int)(nx * am)), az = Mathf.Min(am - 1, (int)(nz * am));
                float path = A[az, ax, LSoil1], forest = A[az, ax, LForest], rock = A[az, ax, LRock];
                if (path > 0.25f || rock > 0.5f) continue;
                float wx = tp.x + nx * sz.x, wz = tp.z + nz * sz.z;
                float n1 = PN(wx * 0.05f + 3, wz * 0.05f + 9), n2 = PN(wx * 0.11f + 40, wz * 0.11f + 1), n3 = PN(wx * 0.07f + 90, wz * 0.07f + 30);
                float open = 1f - Mathf.Clamp01(forest * 1.5f), slopeF = 1f - st / 32f;
                System.Action<int, float> Put = (li, w) => { int v = Mathf.Clamp((int)(w * 255f), 0, 255); if (v > 8) L[li][y, x] = v; };
                Put(0, (0.55f + 0.45f * n1) * slopeF * 2.9f);
                float m = Mathf.Clamp01((n2 - 0.45f) * 2.2f) * 0.8f * slopeF;
                if (m * 255f > 8f) L[1][y, x] = Mathf.Min(255, (int)(m * 255f * 2.2f + 40));
                Put(2, Mathf.Clamp01((n3 - 0.68f) * 4f) * 0.35f * open);
                Put(3, Mathf.Clamp01((n1 - 0.72f) * 4f) * 0.3f);
                Put(7, forest * 0.5f * n2); Put(8, forest * 0.45f * n3); Put(9, forest * 0.35f * (1 - n3));
                Put(10, forest * 0.6f); Put(4, forest * 0.15f); Put(5, forest * 0.2f * n1);
                Put(6, rnd.NextDouble() < 0.02 ? 0.4f : 0f);
                float fl = Mathf.Clamp01((PN(wx * 0.09f + 200, wz * 0.09f + 200) - 0.7f) * 5f) * open;
                Put(11, fl * 0.5f); Put(12, fl * 0.4f * n2);
            }
            for (int i = 0; i < nd; i++) td.SetDetailLayer(0, 0, i, L[i]);
        }
    }
}
