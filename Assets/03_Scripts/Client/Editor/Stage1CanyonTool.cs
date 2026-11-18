using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace VaroGO.Client.EditorTools
{
    // Stage1 협곡: Z1~Z8 길을 따라 배경 산 메시와 지형을 파낸다.
    // 길(Path) 점은 구역을 지나는 순서대로 적은 월드 XZ 좌표다. 구역을 옮기면 점도 고치고 다시 실행한다 (원본 산 메시에서 다시 파므로 여러 번 실행해도 된다).
    public static class Stage1CanyonTool
    {
        // 평평한 협곡 바닥 반폭(m)과 벽이 올라가는 폭(m)
        public const float FloorHalf = 32f;
        public const float WallWidth = 38f;
        const float CanyonFloorY = -22f;

        public static readonly Vector2[] Path =
        {
            new Vector2(77, 63), new Vector2(115, 108), new Vector2(125, 111), new Vector2(154, 111), new Vector2(154, 139),
            new Vector2(124, 141), new Vector2(93, 141), new Vector2(49, 145), new Vector2(46, 188), new Vector2(46, 208),
            new Vector2(48, 246), new Vector2(42, 284), new Vector2(34, 322), new Vector2(29, 360), new Vector2(19, 398),
            new Vector2(11, 436), new Vector2(7, 500), new Vector2(22, 530), new Vector2(39, 604), new Vector2(18, 678),
            new Vector2(35, 753), new Vector2(58, 753), new Vector2(96, 765), new Vector2(160, 795), new Vector2(203, 806),
            new Vector2(237, 815),
        };

        const string MountainDir = "Assets/04_Resources/Map/Stage1/Mountains";
        const string ReadableFbx = MountainDir + "/MSH_BackgroundMountain_Readable.fbx";
        const string OriginalFbx = "Assets/04_Resources/Map/TriForge Assets/Fantasy Worlds - Forest/Meshes/Mountains/MSH_fwOF_BackgroundMountain_01.fbx";

        public static float Dist(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Path.Length - 1; i++)
            {
                var a = Path[i]; var b = Path[i + 1]; var p = new Vector2(x, z);
                var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = (p - (a + ab * t)).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        // 1 = 협곡 바닥, 0 = 협곡 밖
        public static float Carve(float x, float z) => 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((Dist(x, z) - FloorHalf) / WallWidth));

        [MenuItem("VAROGO/Map/Stage1 협곡 파기 (배경 산 메시)")]
        public static void CarveMountains()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Stage1 협곡", "Play 모드에서는 실행할 수 없습니다.", "확인"); return; }
            var root = GameObject.Find("Background Mountains");
            if (root == null) { EditorUtility.DisplayDialog("Stage1 협곡", "Background Mountains를 찾지 못했습니다.", "확인"); return; }

            if (!System.IO.Directory.Exists(MountainDir)) AssetDatabase.CreateFolder("Assets/04_Resources/Map/Stage1", "Mountains");
            if (AssetDatabase.LoadAssetAtPath<Object>(ReadableFbx) == null)
            {
                AssetDatabase.CopyAsset(OriginalFbx, ReadableFbx);
                AssetDatabase.ImportAsset(ReadableFbx);
            }
            var imp = AssetImporter.GetAtPath(ReadableFbx) as ModelImporter;
            if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }

            Mesh readable = LoadMesh(ReadableFbx), original = LoadMesh(OriginalFbx);
            if (readable == null || original == null) { Debug.LogError("[Stage1 협곡] 산 메시를 찾지 못했습니다."); return; }
            var srcVerts = readable.vertices; var srcNormals = readable.normals;

            int carved = 0, reset = 0, idx = 0;
            foreach (Transform c in root.transform)
            {
                var mf = c.GetComponentInChildren<MeshFilter>();
                if (mf == null) continue;
                var tr = mf.transform;
                var verts = (Vector3[])srcVerts.Clone();
                int changed = 0;
                for (int i = 0; i < verts.Length; i++)
                {
                    var w = tr.TransformPoint(verts[i]);
                    float k = Carve(w.x, w.z);
                    if (k <= 0.001f) continue;
                    float target = Mathf.Min(w.y, CanyonFloorY);
                    w.y = Mathf.Lerp(w.y, target, k);
                    verts[i] = tr.InverseTransformPoint(w);
                    changed++;
                }
                string assetPath = $"{MountainDir}/Mountain_{idx:00}_carved.asset";
                if (changed == 0)
                {
                    if (mf.sharedMesh != original) { mf.sharedMesh = original; reset++; }
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
                }
                else
                {
                    var m = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    bool isNew = m == null;
                    if (isNew) m = new Mesh();
                    m.Clear();
                    m.indexFormat = readable.indexFormat;
                    m.vertices = verts;
                    m.uv = readable.uv; m.uv2 = readable.uv2; m.colors = readable.colors; m.tangents = readable.tangents;
                    m.subMeshCount = readable.subMeshCount;
                    for (int s = 0; s < readable.subMeshCount; s++) m.SetTriangles(readable.GetTriangles(s), s);
                    m.normals = srcNormals;
                    var recalc = new Mesh(); recalc.vertices = verts; recalc.triangles = readable.triangles; recalc.RecalculateNormals();
                    var rn = recalc.normals; var nn = m.normals;
                    for (int i = 0; i < verts.Length; i++) if ((tr.TransformPoint(verts[i]) - tr.TransformPoint(srcVerts[i])).sqrMagnitude > 0.0001f) nn[i] = rn[i];
                    m.normals = nn;
                    m.RecalculateBounds();
                    m.name = $"Mountain_{idx:00}_carved";
                    Object.DestroyImmediate(recalc);
                    if (isNew) AssetDatabase.CreateAsset(m, assetPath); else EditorUtility.SetDirty(m);
                    mf.sharedMesh = m;
                    carved++;
                }
                EditorUtility.SetDirty(mf);
                idx++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Stage1 협곡] 산 {carved}개 파냄, {reset}개 원래대로");
        }

        static Mesh LoadMesh(string fbx)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx)) if (o is Mesh m) return m;
            return null;
        }
    }
}
