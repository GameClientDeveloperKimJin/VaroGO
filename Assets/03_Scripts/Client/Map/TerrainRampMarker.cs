using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client
{
    // 흙 경사로(언덕) 한 개 = 오브젝트 한 개. Unity 기본 조작으로 다룬다.
    //   W 이동 · E(Y축) 방향 · R 스케일 X = 폭, Y = 높이 차, Z = 길이 (Z 앞쪽이 위 끝)
    // 길 + 양옆 흙 둔덕(강바닥까지) 메시와 충돌체를 만든다. 만든 메시는 씬에 저장되지 않고 켜질 때마다 다시 만든다.
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    public class TerrainRampMarker : MonoBehaviour
    {
        const string VisualName = "Ramp_Visual";
        const float RiverBed = -15.5f;
        static readonly float SideTan = Mathf.Tan(70f * Mathf.Deg2Rad);

        [SerializeField] Material pathMaterial;
        [SerializeField] Material sideMaterial;
        [Tooltip("텍스처 한 장이 덮는 크기 (m)")]
        [SerializeField] float textureSize = 4f;

        // 아래 끝·위 끝 (월드). 오브젝트 가운데 기준으로 Z 뒤쪽 아래 → Z 앞쪽 위
        public Vector3 LowEnd => transform.TransformPoint(new Vector3(0, -0.5f, -0.5f));
        public Vector3 HighEnd => transform.TransformPoint(new Vector3(0, 0.5f, 0.5f));
        public float Width => Mathf.Abs(transform.lossyScale.x);
        public float Length { get { var d = HighEnd - LowEnd; d.y = 0; return d.magnitude; } }
        public float Rise => HighEnd.y - LowEnd.y;
        public float SlopeDegrees => Mathf.Atan2(Mathf.Abs(Rise), Mathf.Max(0.01f, Length)) * Mathf.Rad2Deg;

        GameObject visual;
        Mesh mesh;
        string builtKey;

        void OnEnable() { builtKey = null; Rebuild(); }

        void OnDisable()
        {
            if (visual != null) DestroyImmediate(visual);
            if (mesh != null) DestroyImmediate(mesh);
            visual = null; mesh = null;
        }

        void Update() { if (!Application.isPlaying) Rebuild(); }

        void OnValidate() => builtKey = null;

        void Rebuild()
        {
            var key = transform.localToWorldMatrix.ToString("F4") + textureSize +
                      (pathMaterial ? pathMaterial.GetInstanceID() : 0) + "|" + (sideMaterial ? sideMaterial.GetInstanceID() : 0);
            if (key == builtKey && visual != null) return;
            builtKey = key;
            Build(LowEnd, HighEnd, Width);
        }

        // 아래 끝 → 위 끝. 양 끝을 살짝 완만하게 이어 턱이 없게 한다.
        static float Profile(float a, float lh, float y0, float y1)
        {
            float t = Mathf.Clamp01(a / lh);
            t = Mathf.SmoothStep(0, 1, t) * 0.25f + t * 0.75f;
            return Mathf.Lerp(y0, y1, t);
        }

        void Build(Vector3 a, Vector3 b, float width)
        {
            if (b.y < a.y) { var tmp = a; a = b; b = tmp; }
            var flat = b - a; flat.y = 0;
            float lh = flat.magnitude;
            if (lh < 0.5f || width < 0.1f) return;
            var dir = flat / lh;
            var right = Vector3.Cross(Vector3.up, dir);
            int n = Mathf.Max(2, Mathf.CeilToInt(lh / 0.5f));

            var verts = new List<Vector3>(); var uvs = new List<Vector2>();
            var pathTris = new List<int>(); var sideTris = new List<int>();
            System.Action<Vector3, Vector2> V = (w, uv) => { verts.Add(transform.InverseTransformPoint(w)); uvs.Add(uv / textureSize); };

            // 길 바닥: 샘플마다 왼쪽·오른쪽 2점
            int pathStart = verts.Count;
            for (int k = 0; k <= n; k++)
            {
                float s = lh * k / n; float y = Profile(s, lh, a.y, b.y) - 0.02f;
                var p = new Vector3(a.x, y, a.z) + dir * s;
                V(p - right * width / 2, new Vector2(-width / 2, s));
                V(p + right * width / 2, new Vector2(width / 2, s));
            }
            for (int k = 0; k < n; k++)
            {
                int l0 = pathStart + k * 2, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                pathTris.AddRange(new[] { l0, l1, r0, r0, l1, r1 });
            }

            // 양옆 둔덕: 길 가장자리에서 70°로 강바닥까지
            for (int side = -1; side <= 1; side += 2)
            {
                int start = verts.Count;
                for (int k = 0; k <= n; k++)
                {
                    float s = lh * k / n; float y = Profile(s, lh, a.y, b.y) - 0.02f;
                    var p = new Vector3(a.x, y, a.z) + dir * s;
                    var top = p + right * side * width / 2;
                    var bot = new Vector3(top.x, RiverBed, top.z) + right * side * (y - RiverBed) / SideTan;
                    V(top, new Vector2(s, y));
                    V(bot, new Vector2(s, RiverBed));
                }
                for (int k = 0; k < n; k++)
                {
                    int t0 = start + k * 2, b0 = t0 + 1, t1 = t0 + 2, b1 = t0 + 3;
                    if (side > 0) sideTris.AddRange(new[] { t0, t1, b0, b0, t1, b1 });
                    else sideTris.AddRange(new[] { t0, b0, t1, b0, b1, t1 });
                }
            }

            if (visual == null)
            {
                var old = transform.Find(VisualName);
                if (old != null) DestroyImmediate(old.gameObject);
                visual = new GameObject(VisualName);
                visual.hideFlags = HideFlags.DontSave | HideFlags.NotEditable | HideFlags.HideInHierarchy;
                visual.transform.SetParent(transform, false);
                visual.AddComponent<MeshFilter>();
                visual.AddComponent<MeshRenderer>();
                visual.AddComponent<MeshCollider>();
            }
            if (mesh == null) { mesh = new Mesh { name = "RampMesh" }; mesh.hideFlags = HideFlags.DontSave; }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(pathTris, 0);
            mesh.SetTriangles(sideTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials = new[] { pathMaterial, sideMaterial };
            var col = visual.GetComponent<MeshCollider>();
            col.sharedMesh = null;
            col.sharedMesh = mesh;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.95f);
            Gizmos.DrawSphere(LowEnd, 0.25f);
            Gizmos.DrawSphere(HighEnd, 0.25f);
            Gizmos.DrawLine(LowEnd, HighEnd);
        }
    }
}
