using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VaroGO.Client.EditorTools
{
    // PlayScene_Stage1 구역(Z0~Z8) 스케일 도구(R) 보조.
    // 구역 루트를 키우면 섬 바닥(Walkable_Grass·Solid_Cliff)만 넓히고, 발판·기믹·장식·길가 나무·장식 돌은
    // 크기 그대로 위치만 퍼뜨린다. 구역 루트의 스케일은 항상 1로 되돌린다. 높이(Y) 스케일은 무시한다.
    // 구역을 처음 선택하면 기준점(루트 위치)을 구역 가운데로 옮긴다 (자식 실제 위치는 그대로).
    [InitializeOnLoad]
    static class Stage1ZoneScaleTool
    {
        const string ScenePath = "Assets/01_Scenes/PlayScene_Stage1.unity";
        const float MemberMargin = 1.5f; // 섬 가장자리 밖이라도 이 거리 안의 나무·돌은 그 구역 것으로 본다

        enum Kind { Ground, Point, Rigid }

        struct Item
        {
            public Transform t;
            public Kind kind;
            public Vector3 pos, scale, center;
        }

        class Snapshot
        {
            public Transform zone;
            public Vector3 pivot;
            public Quaternion rot;
            public readonly List<Item> items = new List<Item>();
        }

        static Snapshot active;

        static Stage1ZoneScaleTool()
        {
            Undo.postprocessModifications += OnModifications;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += () => active = null;
            EditorApplication.update += () => { if (active != null && GUIUtility.hotControl == 0) active = null; };
        }

        static bool IsZone(Transform t)
        {
            if (t == null || t.parent == null || !t.parent.name.StartsWith("HiddenPath") || t.parent.parent != null) return false;
            if (t.gameObject.scene.path != ScenePath) return false;
            return t.name.Length > 1 && t.name[0] == 'Z' && char.IsDigit(t.name[1]);
        }

        // ---------- 기준점을 구역 가운데로 ----------

        static void OnSelectionChanged()
        {
            if (EditorApplication.isPlaying) return;
            var t = Selection.activeTransform;
            if (!IsZone(t) || t.childCount == 0) return;
            Vector3 c;
            if (!ZoneCenter(t, out c)) return;
            c.y = t.position.y;
            if ((c - t.position).sqrMagnitude < 0.25f) return;

            var kids = new List<Transform>(); foreach (Transform k in t) kids.Add(k);
            var world = new List<Vector3>(); foreach (var k in kids) world.Add(k.position);
            Undo.RecordObject(t, "구역 기준점을 가운데로");
            foreach (var k in kids) Undo.RecordObject(k, "구역 기준점을 가운데로");
            t.position = c;
            for (int i = 0; i < kids.Count; i++) kids[i].position = world[i];
        }

        static bool ZoneCenter(Transform zone, out Vector3 center)
        {
            bool has = false; var b = new Bounds();
            foreach (var g in zone.GetComponentsInChildren<Transform>(false))
            {
                if (g.name != "Walkable_Grass") continue;
                var s = g.lossyScale; var rot = Quaternion.Euler(0, g.eulerAngles.y, 0);
                for (int i = -1; i <= 1; i += 2) for (int k = -1; k <= 1; k += 2)
                {
                    var p = g.position + rot * new Vector3(i * s.x / 2, 0, k * s.z / 2);
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
                }
            }
            if (!has) return TryUnitCenter(zone, out center);
            center = b.center;
            return true;
        }

        // ---------- 스케일 가로채기 ----------

        static UndoPropertyModification[] OnModifications(UndoPropertyModification[] mods)
        {
            if (EditorApplication.isPlaying) return mods;
            HashSet<Transform> zones = null;
            var keep = new List<UndoPropertyModification>(mods.Length);
            foreach (var m in mods)
            {
                var pm = m.currentValue;
                var t = pm != null ? pm.target as Transform : null;
                if (t != null && pm.propertyPath.StartsWith("m_LocalScale") && IsZone(t))
                {
                    if (zones == null) zones = new HashSet<Transform>();
                    zones.Add(t);
                    continue; // 루트 스케일 변경은 기록하지 않는다 (아래에서 1로 되돌림)
                }
                keep.Add(m);
            }
            if (zones == null) return mods;

            foreach (var zone in zones)
            {
                var s = zone.localScale;
                float kx = Mathf.Max(0.05f, Mathf.Abs(s.x)), kz = Mathf.Max(0.05f, Mathf.Abs(s.z));
                zone.localScale = Vector3.one;
                if (active == null || active.zone != zone) active = TakeSnapshot(zone);
                ApplySnapshot(active, kx, kz);
                EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
            }
            return keep.ToArray();
        }

        static Snapshot TakeSnapshot(Transform zone)
        {
            var snap = new Snapshot { zone = zone, rot = zone.rotation };
            Vector3 c;
            snap.pivot = ZoneCenter(zone, out c) ? c : zone.position;

            var footprints = new List<Matrix4x4>();
            var tops = new List<float>();
            foreach (Transform unit in zone)
            {
                if (unit.Find("Walkable_Grass") != null)
                {
                    foreach (Transform ch in unit)
                    {
                        bool ground = ch.name == "Walkable_Grass" || ch.name == "Solid_Cliff";
                        snap.items.Add(new Item { t = ch, kind = ground ? Kind.Ground : Kind.Point, pos = ch.position, scale = ch.localScale });
                        if (ch.name == "Walkable_Grass")
                        {
                            var ls = ch.lossyScale;
                            footprints.Add(Matrix4x4.TRS(ch.position, Quaternion.Euler(0, ch.eulerAngles.y, 0),
                                new Vector3(ls.x / 2 + MemberMargin, 1, ls.z / 2 + MemberMargin)).inverse);
                            tops.Add(ch.position.y + ls.y / 2);
                        }
                    }
                }
                else
                {
                    Vector3 uc;
                    if (!TryUnitCenter(unit, out uc)) continue; // 빈 묶음
                    snap.items.Add(new Item { t = unit, kind = Kind.Rigid, pos = unit.position, center = uc });
                }
            }

            // 이 구역 섬 위(가장자리 포함)의 길가 나무·장식 돌
            var scene = zone.gameObject.scene;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name != "Art_Forest") continue;
                foreach (var groupName in new[] { "Trees_OnPath", "PathRocks" })
                {
                    var g = r.transform.Find(groupName);
                    if (g == null) continue;
                    foreach (Transform ch in g)
                    {
                        // 섬 범위 안 + 섬 윗면 높이(±1.5m)에 있는 것만. 위아래 능선 나무를 잘못 끌고 가지 않게.
                        bool on = false;
                        for (int i = 0; i < footprints.Count && !on; i++)
                        {
                            var l = footprints[i].MultiplyPoint3x4(ch.position);
                            on = Mathf.Abs(l.x) <= 1f && Mathf.Abs(l.z) <= 1f && Mathf.Abs(ch.position.y - tops[i]) <= 1.5f;
                        }
                        if (on) snap.items.Add(new Item { t = ch, kind = Kind.Point, pos = ch.position });
                    }
                }
            }
            return snap;
        }

        static void ApplySnapshot(Snapshot snap, float kx, float kz)
        {
            var inv = Quaternion.Inverse(snap.rot);
            System.Func<Vector3, Vector3> H = p =>
            {
                var l = inv * (p - snap.pivot);
                l.x *= kx; l.z *= kz;
                var w = snap.pivot + snap.rot * l;
                w.y = p.y;
                return w;
            };
            foreach (var it in snap.items)
            {
                if (it.t == null) continue;
                Undo.RecordObject(it.t, "구역 크기");
                switch (it.kind)
                {
                    case Kind.Ground:
                        it.t.position = H(it.pos);
                        it.t.localScale = new Vector3(it.scale.x * kx, it.scale.y, it.scale.z * kz);
                        break;
                    case Kind.Point:
                        it.t.position = H(it.pos);
                        break;
                    case Kind.Rigid:
                        var d = H(it.center) - it.center;
                        it.t.position = it.pos + new Vector3(d.x, 0, d.z);
                        break;
                }
            }
        }

        // 렌더러·충돌체 범위의 가운데 (화면에서 숨긴 렌더러 포함, 꺼진 오브젝트 제외).
        // 아무것도 없으면 자식 없는 점(스폰 위치 등)은 자기 위치, 빈 묶음은 false.
        static bool TryUnitCenter(Transform unit, out Vector3 center)
        {
            bool has = false; var b = new Bounds();
            System.Action<Bounds> Add = x => { if (x.size == Vector3.zero && x.center == Vector3.zero) return; if (!has) { b = x; has = true; } else b.Encapsulate(x); };
            foreach (var r in unit.GetComponentsInChildren<Renderer>(false)) Add(r.bounds);
            foreach (var c in unit.GetComponentsInChildren<Collider>(false)) if (c.enabled) Add(c.bounds);
            if (has) { center = b.center; return true; }
            center = unit.position;
            return unit.childCount == 0 && unit.position != Vector3.zero;
        }
    }
}
