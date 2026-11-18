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

        [Tooltip("윗면 상자 자식 이름. 비우면 Walkable_Grass")]
        [SerializeField] string grassName;
        [Tooltip("옆면(절벽) 상자 자식 이름. 비우면 Solid_Cliff")]
        [SerializeField] string cliffName;
        [SerializeField] Material topMaterial;
        [SerializeField] Material cliffMaterial;
        [Tooltip("절벽 아래 끝 높이 (월드). 강바닥 아래로 둔다")]
        [SerializeField] float bottomY = -15.5f;
        [Tooltip("텍스처 한 장이 덮는 크기 (m)")]
        [SerializeField] float textureSize = 4f;
        [Tooltip("절벽 면 울퉁불퉁한 정도 (m)")]
        [SerializeField] float cliffRoughness = 0.45f;

        [Header("모양 (모두 0·끔이면 기존 사각형 그대로)")]
        [Tooltip("모서리를 둥글게 깎는 반지름 (m)")]
        [SerializeField, Min(0f)] float cornerRadius;
        [Tooltip("윤곽을 안쪽으로 들쭉날쭉하게 깎는 최대 폭 (m). 윤곽은 Walkable_Grass 상자 밖으로 나가지 않는다")]
        [SerializeField, Min(0f)] float edgeWobble;
        [Tooltip("아래로 갈수록 좁아지는 정도. 0 = 수직 절벽, 0.3 = 바닥이 30% 좁음 (바위 기둥 느낌)")]
        [SerializeField, Range(0f, 0.9f)] float taper;
        [Tooltip("켜면 보이는 모양 그대로 밟힌다 (Walkable_Grass·Solid_Cliff 상자 판정은 끈다). 둥근 모양을 쓸 때 켠다")]
        [SerializeField] bool meshCollider;
        [SerializeField, HideInInspector] bool boxesDisabledByShape;

        [Header("식물")]
        [SerializeField] FoliageLayer[] foliage;
        [Tooltip("식물을 그리는 최대 거리 (m)")]
        [SerializeField] float grassDistance = 90f;
        [Tooltip("섬 가장자리에서 식물을 비우는 폭 (m)")]
        [SerializeField] float grassEdgeInset = 0.3f;

        Transform grass, cliff;
        Mesh mesh;
        Mesh colliderMesh; // Mesh Collider 옵션용
        GameObject visual;
        string builtKey;

        Matrix4x4[][] grassLocal, grassWorld;
        Matrix4x4 grassWorldFrom;
        Bounds grassBoundsLocal;
        int grassClearVersion = -1; // GrassClearing이 바뀌면 풀만 다시 심는다

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
            if (colliderMesh != null) DestroyImmediate(colliderMesh);
            visual = null; mesh = null; colliderMesh = null;
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
                   (topMaterial ? topMaterial.GetInstanceID() : 0) + "|" + (cliffMaterial ? cliffMaterial.GetInstanceID() : 0) +
                   (Shaped ? "|" + cornerRadius + "," + edgeWobble + "," + taper + "," + meshCollider + transform.position.x.ToString("F2") + transform.position.z.ToString("F2") : "");
        }

        bool Shaped => cornerRadius > 0f || edgeWobble > 0f || taper > 0f || meshCollider;
        System.Collections.Generic.List<Vector3> outline; // 모양 옵션을 쓸 때의 윗면 윤곽 (섬 로컬 좌표)

        void Rebuild()
        {
            grass = transform.Find(string.IsNullOrEmpty(grassName) ? "Walkable_Grass" : grassName);
            cliff = transform.Find(string.IsNullOrEmpty(cliffName) ? "Solid_Cliff" : cliffName);
            if (grass == null) return;
            var key = Key();
            if (key == builtKey && visual != null) return;
            builtKey = key;

            if (!Application.isPlaying) FitCliff();
            BuildMesh();
            ApplyCollider();
            BuildGrass();
        }

        // Mesh Collider 옵션: 만든 메시로 판정하고 상자 판정은 끈다. 옵션을 끄면 껐던 상자를 다시 켠다
        void ApplyCollider()
        {
            if (visual == null) return;
            var mc = visual.GetComponent<MeshCollider>();
            if (meshCollider)
            {
                if (mc == null) mc = visual.AddComponent<MeshCollider>();
                mc.sharedMesh = null;
                mc.sharedMesh = colliderMesh != null ? colliderMesh : mesh;
                visual.layer = grass.gameObject.layer;
                SetBox(grass, false); SetBox(cliff, false);
                boxesDisabledByShape = true;
            }
            else
            {
                if (mc != null) DestroyImmediate(mc);
                if (boxesDisabledByShape) { SetBox(grass, true); SetBox(cliff, true); boxesDisabledByShape = false; }
            }
        }

        static void SetBox(Transform t, bool on)
        {
            if (t == null) return;
            var col = t.GetComponent<Collider>();
            if (col != null && col.enabled != on) col.enabled = on;
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
            outline = null;
            if (Shaped) { BuildShapedMesh(); return; }
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

        // 모양 옵션용: 둥근 모서리 + 들쭉날쭉한 윤곽 + 아래로 좁아지는 절벽. 윗면은 평평하고 Walkable_Grass 상자 안쪽에만 생긴다
        void BuildShapedMesh()
        {
            var gp = grass.localPosition; var gs = grass.localScale;
            var rot = Quaternion.Euler(0, grass.localEulerAngles.y, 0);
            float top = gp.y + gs.y / 2f;
            float bottom = bottomY - transform.position.y;
            var c = new Vector3(gp.x, top, gp.z);
            var origin = transform.position;
            float hx = gs.x / 2f, hz = gs.z / 2f;

            // 윤곽 (위에서 볼 때 시계 방향: 기존 꼭짓점 순서와 같다)
            float r = Mathf.Min(cornerRadius, Mathf.Min(hx, hz));
            int segs = r > 0.01f ? Mathf.Max(2, Mathf.CeilToInt(r * Mathf.PI * 0.5f)) : 0;
            float[] sx = { -1, -1, 1, 1 }, sz = { -1, 1, 1, -1 }, a0 = { 270, 180, 90, 0 };
            var raw = new System.Collections.Generic.List<Vector2>();
            for (int k = 0; k < 4; k++)
            {
                float cx = sx[k] * (hx - r), cz = sz[k] * (hz - r);
                for (int s = 0; s <= segs; s++)
                {
                    float ang = (a0[k] - (segs == 0 ? 0f : 90f * s / segs)) * Mathf.Deg2Rad;
                    raw.Add(new Vector2(cx + Mathf.Cos(ang) * r, cz + Mathf.Sin(ang) * r));
                }
            }
            var flat = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < raw.Count; i++)
            {
                Vector2 a = raw[i], b = raw[(i + 1) % raw.Count];
                float len = Vector2.Distance(a, b);
                if (len < 0.001f) continue;
                int cuts = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
                for (int s = 0; s < cuts; s++) flat.Add(Vector2.Lerp(a, b, (float)s / cuts));
            }
            float maxCut = Mathf.Min(edgeWobble, Mathf.Min(hx, hz) * 0.45f);
            outline = new System.Collections.Generic.List<Vector3>(flat.Count);
            foreach (var f in flat)
            {
                var p = f;
                if (maxCut > 0f && p.sqrMagnitude > 0.0001f)
                {
                    var w = origin + c + rot * new Vector3(p.x, 0, p.y);
                    float n = Mathf.PerlinNoise(w.x * 0.22f + 13.7f, w.z * 0.22f + 4.1f) * 0.7f + Mathf.PerlinNoise(w.x * 0.6f - 7.3f, w.z * 0.6f + 21.9f) * 0.3f;
                    p -= p.normalized * (maxCut * Mathf.Clamp01(n));
                }
                outline.Add(c + rot * new Vector3(p.x, 0, p.y));
            }

            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var topTris = new System.Collections.Generic.List<int>();
            var sideTris = new System.Collections.Generic.List<int>();
            int count = outline.Count;

            var smooth = new System.Collections.Generic.List<Vector3>(); // 판정용 (verts와 같은 순서, 돌기 없음)

            // 윗면: 가운데에서 부채꼴
            verts.Add(c); smooth.Add(c); { var w = origin + c; uvs.Add(new Vector2(w.x, w.z) / textureSize); }
            foreach (var p in outline) { verts.Add(p); smooth.Add(p); var w = origin + p; uvs.Add(new Vector2(w.x, w.z) / textureSize); }
            for (int i = 0; i < count; i++) topTris.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % count });

            // 옆면: 윤곽을 따라 한 바퀴
            float height = top - bottom;
            int rows = Mathf.Max(1, Mathf.CeilToInt(height / 2f));
            int start = verts.Count;
            float perim = 0;
            for (int i = 0; i <= count; i++)
            {
                var p0 = outline[i % count];
                if (i > 0) perim += Vector3.Distance(outline[i - 1], p0);
                var out2 = new Vector3(p0.x - c.x, 0, p0.z - c.z);
                var radial = out2.normalized;
                for (int j = 0; j <= rows; j++)
                {
                    float t = (float)j / rows;
                    var p = new Vector3(c.x, 0, c.z) + out2 * (1f - taper * t);
                    p.y = Mathf.Lerp(top, bottom, t);
                    var w = origin + p;
                    float bump = j == 0 ? 0 : Mathf.PerlinNoise(w.x * 0.35f + w.y * 0.2f, w.z * 0.35f - w.y * 0.25f) * cliffRoughness * Mathf.Min(1f, t * 3f);
                    verts.Add(p + radial * bump);
                    smooth.Add(p);
                    uvs.Add(new Vector2(perim, w.y) / textureSize);
                }
            }
            for (int i = 0; i < count; i++)
            for (int j = 0; j < rows; j++)
            {
                int v00 = start + i * (rows + 1) + j, v10 = v00 + rows + 1;
                sideTris.AddRange(new[] { v00, v00 + 1, v10, v10, v00 + 1, v10 + 1 });
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

            // 판정용 메시: 절벽의 울퉁불퉁함을 뺀 매끈한 면 (돌기에 캐릭터가 걸려 서지 않게)
            if (meshCollider)
            {
                if (colliderMesh == null) { colliderMesh = new Mesh { name = "IslandColliderMesh" }; colliderMesh.hideFlags = HideFlags.DontSave; }
                var all = new System.Collections.Generic.List<int>(topTris.Count + sideTris.Count);
                all.AddRange(topTris); all.AddRange(sideTris);
                colliderMesh.Clear();
                colliderMesh.SetVertices(smooth);
                colliderMesh.SetTriangles(all, 0);
                colliderMesh.RecalculateBounds();
            }
        }

        // 점이 윤곽 안쪽인가 (위에서 본 2D)
        bool InsideOutline(Vector3 p)
        {
            if (outline == null) return true;
            bool inside = false;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                Vector3 a = outline[i], b = outline[j];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }

        // 식물 위치는 섬 이름·종류로 고정된 난수로 정한다 (다시 만들어도 같은 자리).
        // 무리(patch)는 월드 위치 노이즈로 정해서, 옆 섬과도 자연스럽게 이어진다.
        void BuildGrass()
        {
            grassLocal = null; grassWorld = null;
            grassClearVersion = GrassClearing.Version;
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
                    if (!InsideOutline(p)) continue;
                    if (GrassClearing.All.Count > 0 && GrassClearing.Covers(transform.TransformPoint(p))) continue;
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
            if (cam == null || !isActiveAndEnabled) return;
            if (grass != null && grassClearVersion != GrassClearing.Version) BuildGrass();
            if (grassLocal == null) return;
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
