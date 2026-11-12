using UnityEngine;
using UnityEngine.Rendering;

namespace VaroGO.Client
{
    // 섬 윗면 식물 한 종류 (풀·고사리·꽃 등). 무리 크기를 주면 군데군데 모여 자란다.
    [System.Serializable]
    public class FoliageLayer
    {
        public string name;
        public Mesh mesh;
        public Material material;
        [Tooltip("1m²당 포기 수 (무리 안에서)")]
        public float density = 1f;
        public Vector2 scale = new Vector2(1f, 1.5f);
        [Tooltip("무리 크기 (m). 0이면 섬 전체에 고르게")]
        public float patchSize;
        [Range(0.05f, 1f), Tooltip("무리가 섬을 덮는 비율")]
        public float patchCover = 1f;
    }

    // 섬 겉모습. 섬 오브젝트(Walkable_Grass·Solid_Cliff를 자식으로 가진 묶음)에 붙인다.
    // Walkable_Grass(바닥 판정 박스) 크기에 맞춰 잔디 윗면 + 바위 절벽 메시를 만들고, 윗면에 식물을 그린다.
    // 섬을 옮기거나 Walkable_Grass를 늘리면 바로 따라간다. 판정은 기존 박스 그대로이고, Solid_Cliff는 Walkable_Grass 바닥 크기에 맞춘다.
    // 만든 메시는 씬에 저장되지 않고 켜질 때마다 다시 만든다.
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    public class IslandVisual : MonoBehaviour
    {
        const string VisualName = "Island_Visual";

        [SerializeField] Material topMaterial;
        [SerializeField] Material cliffMaterial;
        [Tooltip("절벽 아래 끝 높이 (월드). 강바닥 아래로 둔다")]
        [SerializeField] float bottomY = -15.5f;
        [Tooltip("텍스처 한 장이 덮는 크기 (m)")]
        [SerializeField] float textureSize = 4f;
        [Tooltip("절벽 면 울퉁불퉁한 정도 (m)")]
        [SerializeField] float cliffRoughness = 0.45f;

        [Header("식물")]
        [SerializeField] FoliageLayer[] foliage;
        [Tooltip("식물을 그리는 최대 거리 (m)")]
        [SerializeField] float grassDistance = 90f;
        [Tooltip("섬 가장자리에서 식물을 비우는 폭 (m)")]
        [SerializeField] float grassEdgeInset = 0.3f;

        Transform grass, cliff;
        Mesh mesh;
        GameObject visual;
        string builtKey;

        Matrix4x4[][] grassLocal, grassWorld;
        Matrix4x4 grassWorldFrom;
        Bounds grassBoundsLocal;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += DrawGrass;
            builtKey = null;
            Rebuild();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= DrawGrass;
            if (visual != null) DestroyImmediate(visual);
            if (mesh != null) DestroyImmediate(mesh);
            visual = null; mesh = null;
        }

        void Update()
        {
            if (!Application.isPlaying) Rebuild();
        }

        void OnValidate() => builtKey = null;

        string Key()
        {
            if (grass == null) return "";
            return grass.localPosition.ToString("F3") + grass.localScale.ToString("F3") + grass.localEulerAngles.y.ToString("F2") +
                   transform.position.y.ToString("F3") + bottomY + textureSize + cliffRoughness + grassEdgeInset +
                   (topMaterial ? topMaterial.GetInstanceID() : 0) + "|" + (cliffMaterial ? cliffMaterial.GetInstanceID() : 0);
        }

        void Rebuild()
        {
            grass = transform.Find("Walkable_Grass");
            cliff = transform.Find("Solid_Cliff");
            if (grass == null) return;
            var key = Key();
            if (key == builtKey && visual != null) return;
            builtKey = key;

            if (!Application.isPlaying) FitCliff();
            BuildMesh();
            BuildGrass();
        }

        // 판정용 Solid_Cliff 박스를 Walkable_Grass 바닥 크기·위치에 맞춘다 (높이는 그대로)
        void FitCliff()
        {
            if (cliff == null) return;
            var gp = grass.localPosition; var gs = grass.localScale; var cs = cliff.localScale;
            var pos = new Vector3(gp.x, cliff.localPosition.y, gp.z);
            var scl = new Vector3(gs.x, cs.y, gs.z);
            var rot = Quaternion.Euler(0, grass.localEulerAngles.y, 0);
            if ((cliff.localPosition - pos).sqrMagnitude > 1e-6f) cliff.localPosition = pos;
            if ((cliff.localScale - scl).sqrMagnitude > 1e-6f) cliff.localScale = scl;
            if (Quaternion.Angle(cliff.localRotation, rot) > 0.01f) cliff.localRotation = rot;
        }

        void EnsureVisual()
        {
            if (visual == null)
            {
                var old = transform.Find(VisualName);
                if (old != null) DestroyImmediate(old.gameObject);
                visual = new GameObject(VisualName);
                visual.hideFlags = HideFlags.DontSave | HideFlags.NotEditable | HideFlags.HideInHierarchy;
                visual.transform.SetParent(transform, false);
                visual.AddComponent<MeshFilter>();
                visual.AddComponent<MeshRenderer>();
            }
            if (mesh == null) { mesh = new Mesh { name = "IslandMesh" }; mesh.hideFlags = HideFlags.DontSave; }
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials = new[] { topMaterial, cliffMaterial };
        }

        // 윗면(잔디) + 네 옆면(바위). 옆면은 위 가장자리는 곧게, 아래로 갈수록 노이즈로 울퉁불퉁하게.
        void BuildMesh()
        {
            EnsureVisual();
            var gp = grass.localPosition; var gs = grass.localScale;
            var rot = Quaternion.Euler(0, grass.localEulerAngles.y, 0);
            float top = gp.y + gs.y / 2f;
            float bottom = bottomY - transform.position.y;
            var c = new Vector3(gp.x, top, gp.z);
            var corners = new[]
            {
                c + rot * new Vector3(-gs.x / 2, 0, -gs.z / 2),
                c + rot * new Vector3(-gs.x / 2, 0, gs.z / 2),
                c + rot * new Vector3(gs.x / 2, 0, gs.z / 2),
                c + rot * new Vector3(gs.x / 2, 0, -gs.z / 2),
            };

            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var topTris = new System.Collections.Generic.List<int>();
            var sideTris = new System.Collections.Generic.List<int>();
            var origin = transform.position;

            // 윗면
            foreach (var p in corners) { verts.Add(p); var w = origin + p; uvs.Add(new Vector2(w.x, w.z) / textureSize); }
            topTris.AddRange(new[] { 0, 1, 2, 0, 2, 3 });

            // 옆면
            float height = top - bottom;
            int rows = Mathf.Max(1, Mathf.CeilToInt(height / 2f));
            float perim = 0;
            for (int e = 0; e < 4; e++)
            {
                var a = corners[e]; var b = corners[(e + 1) % 4];
                float len = Vector3.Distance(a, b);
                int cols = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
                int start = verts.Count;
                for (int i = 0; i <= cols; i++)
                {
                    var p0 = Vector3.Lerp(a, b, (float)i / cols);
                    var radial = new Vector3(p0.x - c.x, 0, p0.z - c.z).normalized;
                    for (int j = 0; j <= rows; j++)
                    {
                        float t = (float)j / rows;
                        var p = new Vector3(p0.x, Mathf.Lerp(top, bottom, t), p0.z);
                        var w = origin + p;
                        float bump = j == 0 ? 0 : Mathf.PerlinNoise(w.x * 0.35f + w.y * 0.2f, w.z * 0.35f - w.y * 0.25f) * cliffRoughness * Mathf.Min(1f, t * 3f);
                        verts.Add(p + radial * bump);
                        uvs.Add(new Vector2(perim + len * i / cols, w.y) / textureSize);
                    }
                }
                for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    int v00 = start + i * (rows + 1) + j, v10 = v00 + rows + 1;
                    sideTris.AddRange(new[] { v00, v00 + 1, v10, v10, v00 + 1, v10 + 1 });
                }
                perim += len;
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(topTris, 0);
            mesh.SetTriangles(sideTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        // 식물 위치는 섬 이름·종류로 고정된 난수로 정한다 (다시 만들어도 같은 자리).
        // 무리(patch)는 월드 위치 노이즈로 정해서, 옆 섬과도 자연스럽게 이어진다.
        void BuildGrass()
        {
            grassLocal = null; grassWorld = null;
            if (foliage == null || foliage.Length == 0) return;
            var gp = grass.localPosition; var gs = grass.localScale;
            float w = gs.x - grassEdgeInset * 2, d = gs.z - grassEdgeInset * 2;
            if (w <= 0 || d <= 0) return;
            var rot = Quaternion.Euler(0, grass.localEulerAngles.y, 0);
            float top = gp.y + gs.y / 2f;
            var origin = transform.position;
            float maxScale = 1f;
            grassLocal = new Matrix4x4[foliage.Length][];
            for (int li = 0; li < foliage.Length; li++)
            {
                var layer = foliage[li];
                if (layer == null || layer.mesh == null || layer.material == null || layer.density <= 0) { grassLocal[li] = new Matrix4x4[0]; continue; }
                maxScale = Mathf.Max(maxScale, layer.scale.y * Mathf.Max(layer.mesh.bounds.size.y, 1f));
                bool patchy = layer.patchSize > 0 && layer.patchCover < 1f;
                int tries = Mathf.Min(20000, Mathf.RoundToInt(w * d * layer.density));
                var rnd = new System.Random(name.GetHashCode() * 31 + li);
                float seed = li * 37.7f;
                var list = new System.Collections.Generic.List<Matrix4x4>(tries);
                for (int i = 0; i < tries && list.Count < 6000; i++)
                {
                    var p = new Vector3(gp.x, top, gp.z) + rot * new Vector3(((float)rnd.NextDouble() - 0.5f) * w, 0, ((float)rnd.NextDouble() - 0.5f) * d);
                    float s = Mathf.Lerp(layer.scale.x, layer.scale.y, (float)rnd.NextDouble());
                    float yaw = (float)rnd.NextDouble() * 360f;
                    if (patchy)
                    {
                        var wp = origin + p;
                        float n = Mathf.PerlinNoise(wp.x / layer.patchSize + seed, wp.z / layer.patchSize - seed);
                        if (n < 1f - layer.patchCover) continue;
                    }
                    list.Add(Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one * s));
                }
                grassLocal[li] = list.ToArray();
            }
            grassBoundsLocal = new Bounds(new Vector3(gp.x, top, gp.z), new Vector3(Mathf.Max(gs.x, gs.z) * 1.5f, maxScale * 2f, Mathf.Max(gs.x, gs.z) * 1.5f));
            grassWorldFrom = Matrix4x4.zero;
        }

        void DrawGrass(ScriptableRenderContext ctx, Camera cam)
        {
            if (grassLocal == null || cam == null || !isActiveAndEnabled) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
            var l2w = transform.localToWorldMatrix;
            if (grassWorld == null || grassWorld.Length != grassLocal.Length || l2w != grassWorldFrom)
            {
                grassWorld = new Matrix4x4[grassLocal.Length][];
                for (int li = 0; li < grassLocal.Length; li++)
                {
                    var src = grassLocal[li]; var dst = new Matrix4x4[src.Length];
                    for (int i = 0; i < src.Length; i++) dst[i] = l2w * src[i];
                    grassWorld[li] = dst;
                }
                grassWorldFrom = l2w;
            }
            var wb = new Bounds(l2w.MultiplyPoint3x4(grassBoundsLocal.center), grassBoundsLocal.size);
            if (wb.SqrDistance(cam.transform.position) > grassDistance * grassDistance) return;
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), wb)) return;

            for (int li = 0; li < grassWorld.Length && li < foliage.Length; li++)
            {
                var m = grassWorld[li]; var layer = foliage[li];
                if (m.Length == 0 || layer == null || layer.mesh == null || layer.material == null) continue;
                var rp = new RenderParams(layer.material) { camera = cam, worldBounds = wb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer };
                for (int start = 0; start < m.Length; start += 1023)
                    Graphics.RenderMeshInstanced(rp, layer.mesh, 0, m, Mathf.Min(1023, m.Length - start), start);
            }
        }
    }
}
