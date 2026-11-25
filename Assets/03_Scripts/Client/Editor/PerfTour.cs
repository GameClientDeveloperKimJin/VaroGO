using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VaroGO.Client.EditorTools
{
    // 성능 측정용 자동 순회 (에디터 전용). PlayScene_* 플레이 중 PerfTour.Begin() 호출:
    // HiddenPath* 아래 Z숫자 구역 루트를 이름 순으로 돌며, 구역 바닥(아래로 레이캐스트)에 로컬 플레이어를 순간이동시키고
    // settle초 기다린 뒤 spin초 동안 카메라를 한 바퀴 돌린다. 진행 상황은 PerfZoneLogger의 tour 열과 [PerfTour] 로그로 남는다.
    [InitializeOnLoad]
    static class PerfTour
    {
        internal struct Stop { public string zone; public Vector3 pos; }

        const float Settle = 2f, Spin = 8f;
        static List<Stop> stops;
        static int index;
        static double phaseStart;
        static int phase; // 0=이동 직후 대기, 1=카메라 회전
        public static bool Running => stops != null;

        static PerfTour()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) End("플레이 종료"); };
        }

        public static string Begin()
        {
            if (!EditorApplication.isPlaying) return "플레이 중이 아님";
            stops = BuildStops(SceneManager.GetActiveScene());
            index = -1;
            Debug.Log("[PerfTour] 시작: 지점 " + stops.Count + "개 (" + string.Join(", ", stops.ConvertAll(s => s.zone)) + ")");
            Next();
            return "지점 " + stops.Count + "개";
        }

        public static void End(string why)
        {
            if (stops == null) return;
            stops = null;
            PerfZoneLogger.Marker = "";
            PerfZoneLogger.Event("tour 끝: " + why);
        }

        static void Next()
        {
            index++;
            if (index >= stops.Count) { End("모든 구역 완료"); return; }
            var p = Local();
            if (p == null) { End("로컬 플레이어 없음"); return; }
            var s = stops[index];
            p.Teleport(s.pos, p.transform.rotation);
            phase = 0;
            phaseStart = EditorApplication.timeSinceStartup;
            PerfZoneLogger.Marker = s.zone;
            PerfZoneLogger.Event("tour " + (index + 1) + "/" + stops.Count + " " + s.zone + " 도착 " + s.pos);
        }

        static void Tick()
        {
            if (stops == null || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            double t = EditorApplication.timeSinceStartup - phaseStart;
            if (phase == 0)
            {
                if (t < Settle) return;
                phase = 1; phaseStart = EditorApplication.timeSinceStartup;
                return;
            }
            var orbit = Orbit();
            if (orbit != null)
            {
                // 에디터 update 간격과 무관하게 경과 시간 비율로 각도를 정한다
                float yaw = (float)(t / Spin) * 360f;
                orbit.HorizontalAxis.Value = orbit.HorizontalAxis.ClampValue(Mathf.Repeat(yaw + 180f, 360f) - 180f);
            }
            if (t >= Spin) Next();
        }

        internal static PlayerController Local()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++) if (all[i].IsLocal) return all[i];
            return null;
        }

        internal static CinemachineOrbitalFollow Orbit()
        {
            foreach (var o in Object.FindObjectsByType<CinemachineOrbitalFollow>(FindObjectsSortMode.None))
                if (o.isActiveAndEnabled) return o;
            return null;
        }

        // 구역마다 9x9 격자로 위에서 아래로 레이캐스트해, 그 구역에 속한 평평한 바닥 중 구역 가운데에 가장 가까운 점
        internal static List<Stop> BuildStops(Scene scene)
        {
            var list = new List<Stop>();
            var roots = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("HiddenPath")) continue;
                foreach (Transform z in root.transform)
                    if (z.gameObject.activeInHierarchy && z.name.Length > 1 && z.name[0] == 'Z' && char.IsDigit(z.name[1])) roots.Add(z);
            }
            roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var z in roots)
            {
                var rs = z.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                bool found = false; float best = float.MaxValue; Vector3 bestPos = Vector3.zero;
                for (int ix = 0; ix < 9; ix++)
                for (int iz = 0; iz < 9; iz++)
                {
                    var o = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (ix + 0.5f) / 9f), b.max.y + 5f, Mathf.Lerp(b.min.z, b.max.z, (iz + 0.5f) / 9f));
                    RaycastHit hit;
                    if (!Physics.Raycast(o, Vector3.down, out hit, b.size.y + 20f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.7f || !hit.collider.transform.IsChildOf(z)) continue;
                    float d = new Vector2(hit.point.x - b.center.x, hit.point.z - b.center.z).sqrMagnitude;
                    if (d < best) { best = d; bestPos = hit.point + Vector3.up * 0.3f; found = true; }
                }
                if (found) list.Add(new Stop { zone = z.name, pos = bestPos });
                else Debug.Log("[PerfTour] 바닥 못 찾음, 건너뜀: " + z.name);
            }
            return list;
        }
    }
}
