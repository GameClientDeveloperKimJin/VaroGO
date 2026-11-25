using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VaroGO.Client.EditorTools
{
    // 성능 측정용 기록기 (에디터 전용, 빌드에 안 들어감).
    // 메뉴 VaroGO/Perf/Zone Logger 를 켜면 Play 시작부터 끝까지(타이틀·로비·스테이지 모두) 1초마다 한 줄씩 CSV에 남긴다:
    // 시각, 씬, 구역(HiddenPath 아래 Z0~Z8 루트), 위치, FPS, 메인스레드 ms(평균/최대), 드로우콜·배치·SetPass·삼각형, GC 할당, 메모리.
    // 씬 로드·자동 진행 단계는 *_events.csv에 따로 남긴다(Play 진입 시간, 씬 로드 시각 등).
    // 저장 위치: <프로젝트>/Logs/Perf/  (MPPM 클론은 자기 프로젝트 폴더의 Logs/Perf)
    // MCP 호출 없이 기록하므로 측정 중 에디터가 멈추지 않는다.
    [InitializeOnLoad]
    static class PerfZoneLogger
    {
        const string PrefKey = "VaroGO.PerfZoneLogger";
        const string MenuPath = "VaroGO/Perf/Zone Logger";
        const string PlayClickKey = "VaroGO.PerfZoneLogger.PlayClick";
        const int Capacity = 1000;

        struct Zone { public string name; public Bounds b; }

        public static string Marker = ""; // PerfTour가 현재 순회 중인 구역 이름을 넣는다

        static bool Enabled { get => EditorPrefs.GetBool(PrefKey, false); set => EditorPrefs.SetBool(PrefKey, value); }

        static ProfilerRecorder mainThread, drawCalls, batches, setPass, tris, gcAlloc, gcReserved, sysUsed, gfxUsed;
        static List<Zone> zones = new List<Zone>();
        static string zoneScene, filePath;
        static StreamWriter writer, events;
        static double lastTime, lastEvent;
        static int lastFrame;
        static readonly StringBuilder sb = new StringBuilder();
        static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        static PerfZoneLogger()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) SessionState.SetString(PlayClickKey, System.DateTime.Now.Ticks.ToString());
                else if (s == PlayModeStateChange.EnteredPlayMode && Enabled)
                {
                    Open();
                    long ticks;
                    if (long.TryParse(SessionState.GetString(PlayClickKey, ""), out ticks))
                        Event("Play 진입 완료 (Play 누른 뒤 " + (System.DateTime.Now - new System.DateTime(ticks)).TotalMilliseconds.ToString("F0", ci) + "ms)");
                }
                else if (s == PlayModeStateChange.ExitingPlayMode) Close();
            };
            SceneManager.sceneUnloaded += sc => { if (writer != null) Event("씬 내림: " + sc.name); };
            SceneManager.sceneLoaded += (sc, mode) => { if (writer != null) Event("씬 로드됨: " + sc.name + " (" + mode + ")"); };
        }

        [MenuItem(MenuPath)]
        static void Toggle() { Enabled = !Enabled; Debug.Log("[Perf] Zone Logger " + (Enabled ? "ON" : "OFF")); }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }

        // 이벤트 한 줄: 시각, Play 후 경과초, 직전 이벤트 후 경과ms, 내용
        public static void Event(string what)
        {
            if (!Enabled || !EditorApplication.isPlaying) return;
            if (writer == null) Open();
            double now = EditorApplication.timeSinceStartup;
            events.WriteLine(System.DateTime.Now.ToString("HH:mm:ss.fff") + "," + Time.realtimeSinceStartup.ToString("F2", ci) + ","
                + ((now - lastEvent) * 1000).ToString("F0", ci) + "," + SceneManager.GetActiveScene().name + "," + what.Replace(',', ' '));
            events.Flush();
            lastEvent = now;
            Debug.Log("[Perf] " + what);
        }

        static void Tick()
        {
            if (writer == null || !EditorApplication.isPlaying || EditorApplication.isPaused) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - lastTime < 1.0) return;
            double dt = now - lastTime;
            int frames = Time.frameCount - lastFrame;
            lastTime = now; lastFrame = Time.frameCount;

            var scene = SceneManager.GetActiveScene();
            if (zoneScene != scene.path) BuildZones(scene);

            double mtAvg, mtMax; int spikes;
            Stats(mainThread, 1e-6, out mtAvg, out mtMax, out spikes);
            double dcAvg, dcMax, bAvg, bMax, spAvg, spMax, trAvg, trMax, gcAvg, gcMax; int _;
            Stats(drawCalls, 1, out dcAvg, out dcMax, out _);
            Stats(batches, 1, out bAvg, out bMax, out _);
            Stats(setPass, 1, out spAvg, out spMax, out _);
            Stats(tris, 1, out trAvg, out trMax, out _);
            Stats(gcAlloc, 1.0 / 1024, out gcAvg, out gcMax, out _);

            Vector3 pos; string zone = FindZone(out pos);
            sb.Clear();
            sb.Append(System.DateTime.Now.ToString("HH:mm:ss")).Append(',')
              .Append(scene.name).Append(',').Append(zone).Append(',').Append(Marker).Append(',')
              .Append(pos.x.ToString("F1", ci)).Append(',').Append(pos.y.ToString("F1", ci)).Append(',').Append(pos.z.ToString("F1", ci)).Append(',')
              .Append((frames / dt).ToString("F1", ci)).Append(',')
              .Append(mtAvg.ToString("F2", ci)).Append(',').Append(mtMax.ToString("F2", ci)).Append(',').Append(spikes).Append(',')
              .Append(dcAvg.ToString("F0", ci)).Append(',').Append(bAvg.ToString("F0", ci)).Append(',').Append(spAvg.ToString("F0", ci)).Append(',')
              .Append(trAvg.ToString("F0", ci)).Append(',').Append(trMax.ToString("F0", ci)).Append(',')
              .Append((gcAvg * frames).ToString("F1", ci)).Append(',').Append(gcMax.ToString("F1", ci)).Append(',')
              .Append((gcReserved.LastValue / 1048576.0).ToString("F0", ci)).Append(',')
              .Append((sysUsed.LastValue / 1048576.0).ToString("F0", ci)).Append(',')
              .Append((gfxUsed.LastValue / 1048576.0).ToString("F0", ci));
            writer.WriteLine(sb.ToString());
            writer.Flush();
        }

        // 지난 1초 동안 쌓인 프레임 샘플의 평균/최대. spikes = 33ms(30FPS 미만) 넘은 프레임 수 (메인스레드용)
        static void Stats(ProfilerRecorder r, double scale, out double avg, out double max, out int spikes)
        {
            avg = 0; max = 0; spikes = 0;
            if (!r.Valid) return;
            int n = r.Count;
            for (int i = 0; i < n; i++)
            {
                double v = r.GetSample(i).Value * scale;
                avg += v; if (v > max) max = v; if (v > 33.3) spikes++;
            }
            if (n > 0) avg /= n;
            r.Reset();
            r.Start();
        }

        static void Open()
        {
            if (writer != null) return;
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", Capacity);
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", Capacity);
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", Capacity);
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", Capacity);
            tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", Capacity);
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", Capacity);
            gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            sysUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            gfxUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            zoneScene = null;

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Perf");
            Directory.CreateDirectory(dir);
            string baseName = Path.Combine(dir, string.Format("play_{0:yyyyMMdd_HHmmss}_pid{1}", System.DateTime.Now, System.Diagnostics.Process.GetCurrentProcess().Id));
            filePath = baseName + ".csv";
            writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            writer.WriteLine("time,scene,zone,tour,x,y,z,fps,main_ms_avg,main_ms_max,spikes33,drawcalls,batches,setpass,tris_avg,tris_max,gc_kb_per_s,gc_kb_max_frame,gc_reserved_mb,sys_used_mb,gfx_used_mb");
            events = new StreamWriter(baseName + "_events.csv", false, new UTF8Encoding(true));
            events.WriteLine("time,t_since_start_s,since_prev_ms,scene,event");
            lastTime = lastEvent = EditorApplication.timeSinceStartup; lastFrame = Time.frameCount;
            Debug.Log("[Perf] 기록 시작: " + filePath);
        }

        static void Close()
        {
            if (writer != null) { writer.Close(); writer = null; events.Close(); events = null; Debug.Log("[Perf] 기록 끝: " + filePath); }
            mainThread.Dispose(); drawCalls.Dispose(); batches.Dispose(); setPass.Dispose(); tris.Dispose();
            gcAlloc.Dispose(); gcReserved.Dispose(); sysUsed.Dispose(); gfxUsed.Dispose();
            zoneScene = null;
        }

        // HiddenPath* 루트 아래 "Z숫자..." 자식마다 렌더러 범위(XZ)를 씬이 바뀔 때 한 번만 계산
        static void BuildZones(Scene scene)
        {
            zoneScene = scene.path;
            zones.Clear();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("HiddenPath")) continue;
                foreach (Transform z in root.transform)
                {
                    if (z.name.Length < 2 || z.name[0] != 'Z' || !char.IsDigit(z.name[1])) continue;
                    var rs = z.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    zones.Add(new Zone { name = z.name, b = b });
                }
            }
        }

        // 로컬 플레이어 위치가 들어있는 구역 중 가장 작은 것. 없으면 가장 가까운 구역 앞에 "~"
        static string FindZone(out Vector3 pos)
        {
            pos = Vector3.zero;
            PlayerController local = null;
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++) if (all[i].IsLocal) { local = all[i]; break; }
            if (local == null) return "none";
            pos = local.transform.position;
            if (zones.Count == 0) return "-";
            string best = null; float bestArea = float.MaxValue, nearD = float.MaxValue; string near = "none";
            var p2 = new Vector3(pos.x, 0, pos.z);
            foreach (var z in zones)
            {
                var c = z.b.center; var e = z.b.extents;
                bool inside = Mathf.Abs(pos.x - c.x) <= e.x && Mathf.Abs(pos.z - c.z) <= e.z;
                if (inside) { float a = e.x * e.z; if (a < bestArea) { bestArea = a; best = z.name; } }
                else
                {
                    var flat = new Bounds(new Vector3(c.x, 0, c.z), new Vector3(e.x * 2, 1, e.z * 2));
                    float d = flat.SqrDistance(p2);
                    if (d < nearD) { nearD = d; near = z.name; }
                }
            }
            return best ?? ("~" + near);
        }
    }
}
