using UnityEditor;
using UnityEngine;

namespace VaroGO.Client.EditorTools
{
    // 씬 뷰에서 KillZone 태그 트리거 범위를 반투명 빨강 박스로 표시한다 (에디터 전용, 게임 화면엔 안 보임)
    [InitializeOnLoad]
    static class KillZoneGizmo
    {
        const string KillZoneTag = "KillZone";
        static readonly Color Fill = new Color(1f, 0.15f, 0.1f, 0.12f);
        static readonly Color Line = new Color(1f, 0.15f, 0.1f, 0.8f);

        static KillZoneGizmo()
        {
            SceneView.duringSceneGui += Draw;
        }

        static void Draw(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            GameObject[] zones;
            try { zones = GameObject.FindGameObjectsWithTag(KillZoneTag); }
            catch (UnityException) { return; } // 태그가 없는 프로젝트
            foreach (var go in zones)
            {
                foreach (var box in go.GetComponents<BoxCollider>())
                {
                    if (!box.enabled) continue;
                    var t = go.transform;
                    using (new Handles.DrawingScope(Matrix4x4.TRS(t.TransformPoint(box.center), t.rotation, Vector3.Scale(t.lossyScale, box.size))))
                    {
                        Handles.color = Fill;
                        DrawFaces();
                        Handles.color = Line;
                        Handles.DrawWireCube(Vector3.zero, Vector3.one);
                    }
                }
            }
        }

        static void DrawFaces()
        {
            for (int axis = 0; axis < 3; axis++)
            {
                Handles.DrawAAConvexPolygon(Face(-0.5f, axis));
                Handles.DrawAAConvexPolygon(Face(0.5f, axis));
            }
        }

        // 단위 큐브의 한 면 꼭짓점 (axis 축 방향으로 d 위치)
        static Vector3[] Face(float d, int axis)
        {
            var v = new Vector3[4];
            float[,] uv = { { -0.5f, -0.5f }, { 0.5f, -0.5f }, { 0.5f, 0.5f }, { -0.5f, 0.5f } };
            for (int i = 0; i < 4; i++)
            {
                var p = Vector3.zero;
                p[axis] = d;
                p[(axis + 1) % 3] = uv[i, 0];
                p[(axis + 2) % 3] = uv[i, 1];
                v[i] = p;
            }
            return v;
        }
    }
}
