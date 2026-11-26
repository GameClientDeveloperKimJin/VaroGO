#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Cinemachine;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using VaroGO.Net;
using VaroGO.Shared;

namespace VaroGO.Client.Perf
{
    // 빌드 성능 측정 (Development Build에만 들어간다. 정식 빌드에는 컴파일되지 않음).
    // 실행 인자에 -perf 가 있을 때만 켜진다. 에디터에서는 인자가 없어서 아무것도 하지 않는다.
    //   VaroGO.exe -perf [-perfrounds 2] [-perfcourse Assets/01_Scenes/PlayScene_Stage1.unity] [-perfout <폴더>] [-perfquit] [-perftrace]
    //   -perftrace: 실행 내내 쓰인 그래픽 상태(PSO)를 기록해 측정 끝에 <perfout>/Stage1.graphicsstate로 저장 (Stage1ShaderWarmup이 쓰는 파일)
    // 하는 일 (방장 역할, 에디터 PerfAutoRun의 메인 쪽과 같은 흐름):
    //   Title → Lobby → 비밀방 만들기 → 방 코드를 %TEMP%/varogo_perf_room.txt에 씀 → 레드 팀·준비
    //   → 상대(에디터, PerfAutoRun "Join Only")가 들어와 준비하면 시작 → 스테이지 도착 5초 뒤 구역 순회를 rounds번
    //   1회차 = 처음 가는 곳(셰이더 준비 끊김 포함), 2회차 = 준비 끝난 상태
    // 기록: 1초마다 한 줄(perf_*.csv), 0.2초 넘는 프레임은 멈춤으로 따로(perf_*_hitch.csv), 진행 단계(perf_*_events.csv)
    // GPU 시간은 FrameTimingManager를 쓴다(빌드 때 Frame Timing Stats를 켜야 값이 나옴 → PerfBuild가 켜고 빌드)
    public class PerfRuntime : MonoBehaviour
    {
        enum Step { Title, WaitList, CreateRoom, InRoom, WaitCourse, Settle, Touring, Done }
        struct Zone { public string name; public Bounds b; }
        struct Stop { public string zone; public Vector3 pos; }

        const float HitchSec = 0.2f, TourSettle = 2f, TourSpin = 8f;
        static readonly string CodeFile = Path.Combine(Path.GetTempPath(), "varogo_perf_room.txt");
        static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        int rounds = 2;
        string course, outDir;
        bool quitAtEnd, trace;
        UnityEngine.Experimental.Rendering.GraphicsStateCollection traceCollection;

        Step step;
        float stepStart, lastTry;
        StreamWriter rows, hitches, events;
        string baseName;
        float lastRow, lastEventT;
        int lastFrame;
        double gpuSum, cpuSum, gpuMax, cpuMax; int timingN, spikes;
        ProfilerRecorder drawCalls, tris, gcAlloc, gcReserved, sysUsed, gfxUsed;
        readonly List<Zone> zones = new List<Zone>();
        string zoneScene;
        readonly FrameTiming[] timing = new FrameTiming[1];
        readonly StringBuilder sb = new StringBuilder();

        List<Stop> stops;
        int stopIndex, round, tourPhase;
        float phaseStart;
        string marker = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-perf") < 0) return;
            var go = new GameObject("[PerfRuntime]");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<PerfRuntime>();
            int r;
            if (int.TryParse(Arg(args, "-perfrounds"), out r) && r > 0) p.rounds = r;
            p.course = Arg(args, "-perfcourse") ?? "Assets/01_Scenes/PlayScene_Stage1.unity";
            p.outDir = Arg(args, "-perfout") ?? Path.Combine(Application.persistentDataPath, "Perf");
            p.quitAtEnd = Array.IndexOf(args, "-perfquit") >= 0;
            p.trace = Array.IndexOf(args, "-perftrace") >= 0;
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
        }

        void Start()
        {
            Directory.CreateDirectory(outDir);
            baseName = Path.Combine(outDir, string.Format("perf_{0:yyyyMMdd_HHmmss}", DateTime.Now));
            rows = new StreamWriter(baseName + ".csv", false, new UTF8Encoding(true));
            rows.WriteLine("time,t_s,scene,zone,tour,round,fps,gpu_ms_avg,gpu_ms_max,cpu_ms_avg,cpu_ms_max,spikes33,drawcalls,tris,gc_kb_per_s,gc_reserved_mb,sys_used_mb,gfx_used_mb");
            hitches = new StreamWriter(baseName + "_hitch.csv", false, new UTF8Encoding(true));
            hitches.WriteLine("time,t_s,frame_ms,scene,zone,tour,round,step");
            events = new StreamWriter(baseName + "_events.csv", false, new UTF8Encoding(true));
            events.WriteLine("time,t_s,since_prev_ms,scene,event");

            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 300);
            tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 300);
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 300);
            gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            sysUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            gfxUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");

            SceneManager.sceneLoaded += (sc, mode) => Event("씬 로드됨: " + sc.name + " (" + mode + ")");
            SceneManager.sceneUnloaded += sc => Event("씬 내림: " + sc.name);
            if (File.Exists(CodeFile)) File.Delete(CodeFile);
            if (trace)
            {
                traceCollection = new UnityEngine.Experimental.Rendering.GraphicsStateCollection();
                traceCollection.BeginTrace();
            }
            lastRow = lastEventT = Time.realtimeSinceStartup; lastFrame = Time.frameCount;
            Event("시작: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType + " / " + Screen.width + "x" + Screen.height
                + " / 품질 " + QualitySettings.names[QualitySettings.GetQualityLevel()] + " / 순회 " + rounds + "회 / 코스 " + course);
            Go(Step.Title);
        }

        void OnDestroy()
        {
            rows?.Close(); hitches?.Close(); events?.Close();
            drawCalls.Dispose(); tris.Dispose(); gcAlloc.Dispose(); gcReserved.Dispose(); sysUsed.Dispose(); gfxUsed.Dispose();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > HitchSec && Time.frameCount > 2)
            {
                hitches.WriteLine(Now() + "," + Time.realtimeSinceStartup.ToString("F2", ci) + "," + (dt * 1000).ToString("F0", ci) + ","
                    + SceneManager.GetActiveScene().name + "," + FindZone() + "," + marker + "," + round + "," + step);
                hitches.Flush();
            }

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                double g = timing[0].gpuFrameTime, c = timing[0].cpuMainThreadFrameTime;
                if (g > 0) { gpuSum += g; if (g > gpuMax) gpuMax = g; }
                cpuSum += c; if (c > cpuMax) cpuMax = c;
                timingN++;
            }
            if (dt > 1f / 30f) spikes++;

            WriteRow();
            Flow();
            Tour();
        }

        void WriteRow()
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastRow < 1f) return;
            float span = now - lastRow;
            int frames = Time.frameCount - lastFrame;
            lastRow = now; lastFrame = Time.frameCount;
            var scene = SceneManager.GetActiveScene();
            if (zoneScene != scene.path) BuildZones(scene);

            sb.Clear();
            sb.Append(Now()).Append(',').Append(now.ToString("F1", ci)).Append(',').Append(scene.name).Append(',').Append(FindZone()).Append(',')
              .Append(marker).Append(',').Append(round).Append(',')
              .Append((frames / span).ToString("F1", ci)).Append(',')
              .Append(timingN > 0 ? (gpuSum / timingN).ToString("F2", ci) : "").Append(',').Append(gpuMax.ToString("F2", ci)).Append(',')
              .Append(timingN > 0 ? (cpuSum / timingN).ToString("F2", ci) : "").Append(',').Append(cpuMax.ToString("F2", ci)).Append(',')
              .Append(spikes).Append(',')
              .Append(Avg(drawCalls).ToString("F0", ci)).Append(',').Append(Avg(tris).ToString("F0", ci)).Append(',')
              .Append((Sum(gcAlloc) / 1024.0 / span).ToString("F1", ci)).Append(',')
              .Append((gcReserved.LastValue / 1048576.0).ToString("F0", ci)).Append(',')
              .Append((sysUsed.LastValue / 1048576.0).ToString("F0", ci)).Append(',')
              .Append((gfxUsed.LastValue / 1048576.0).ToString("F0", ci));
            rows.WriteLine(sb.ToString());
            rows.Flush();
            gpuSum = cpuSum = gpuMax = cpuMax = 0; timingN = 0; spikes = 0;
            Restart(drawCalls); Restart(tris); Restart(gcAlloc);
        }

        static void Restart(ProfilerRecorder r) { if (!r.Valid) return; r.Reset(); r.Start(); }

        static double Avg(ProfilerRecorder r)
        {
            if (!r.Valid || r.Count == 0) return 0;
            double s = Sum(r) / r.Count;
            return s;
        }

        static double Sum(ProfilerRecorder r)
        {
            if (!r.Valid) return 0;
            double s = 0;
            for (int i = 0; i < r.Count; i++) s += r.GetSample(i).Value;
            return s;
        }

        void Event(string what)
        {
            if (events == null) return;
            float now = Time.realtimeSinceStartup;
            events.WriteLine(Now() + "," + now.ToString("F2", ci) + "," + ((now - lastEventT) * 1000).ToString("F0", ci) + ","
                + SceneManager.GetActiveScene().name + "," + what.Replace(',', ' '));
            events.Flush();
            lastEventT = now;
            Debug.Log("[PerfRuntime] " + what);
        }

        static string Now() => DateTime.Now.ToString("HH:mm:ss.fff");

        // ---------- 자동 진행 (방장) ----------

        void Go(Step s)
        {
            step = s; stepStart = Time.realtimeSinceStartup; lastTry = -999f;
            Event("auto:" + s);
        }

        bool Every(float sec)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastTry < sec) return false;
            lastTry = now;
            return true;
        }

        void Flow()
        {
            if (step == Step.Done) return;
            var net = NetBootstrap.Instance;
            string scene = SceneManager.GetActiveScene().path;
            float inStep = Time.realtimeSinceStartup - stepStart;
            // 상대 에디터가 들어올 때까지 기다리는 InRoom만 길게 허용
            if (step != Step.Touring && inStep > (step == Step.InRoom ? 300f : 120f))
            {
                Event("자동 진행 멈춤(시간 초과): " + step + " / net=" + (net != null ? net.State.ToString() : "null"));
                Go(Step.Done);
                return;
            }

            switch (step)
            {
                case Step.Title:
                    if (scene == NetBootstrap.LobbyScenePath) { Go(Step.WaitList); return; }
                    if (scene != NetBootstrap.TitleScenePath || inStep < 2f) return;
                    if (!PlayerNickname.HasNickname) PlayerNickname.TrySave("PerfBuild");
                    SceneManager.LoadSceneAsync(SceneUtility.GetBuildIndexByScenePath(NetBootstrap.LobbyScenePath));
                    Go(Step.WaitList);
                    return;

                case Step.WaitList:
                    if (net != null && net.State == SessionState.Browsing && net.ListReady) Go(Step.CreateRoom);
                    return;

                case Step.CreateRoom:
                    if (net == null) return;
                    if (net.State == SessionState.WaitingPartner || net.State == SessionState.Lobby)
                    {
                        if (!string.IsNullOrEmpty(net.SessionCode))
                        {
                            File.WriteAllText(CodeFile, net.SessionCode + "\n" + DateTime.Now.Ticks);
                            Event("방 코드 " + net.SessionCode + " → " + CodeFile);
                        }
                        SetCourse(net);
                        Go(Step.InRoom);
                        return;
                    }
                    if (net.State == SessionState.Browsing && Every(3f)) net.CreateRoom("PerfBuild", true);
                    return;

                case Step.InRoom:
                    if (net == null) return;
                    if (net.State == SessionState.LoadingCourse || net.State == SessionState.InGame) { Go(Step.WaitCourse); return; }
                    if (!Every(2f)) return;
                    net.RequestTeam(PlayerRole.A_Red);
                    if (net.State == SessionState.Lobby) net.SetReady(true);
                    if (net.CanStartGame) net.StartGame();
                    return;

                case Step.WaitCourse:
                    if (net != null && net.State == SessionState.InGame && scene.Contains("PlayScene_") && Local() != null) Go(Step.Settle);
                    return;

                case Step.Settle:
                    if (inStep < 5f) return;
                    round = 1;
                    BeginTour();
                    Go(Step.Touring);
                    return;

                case Step.Touring:
                    if (stops != null) return;
                    if (round < rounds) { round++; BeginTour(); return; }
                    if (traceCollection != null)
                    {
                        traceCollection.EndTrace();
                        string path = Path.Combine(outDir, "Stage1.graphicsstate");
                        bool saved = traceCollection.SaveToFile(path);
                        Event("그래픽 상태 기록 " + (saved ? "저장" : "저장 실패") + ": 변형 " + traceCollection.variantCount + "개 / 상태 " + traceCollection.totalGraphicsStateCount + "개 → " + path);
                    }
                    Event("측정 끝");
                    Go(Step.Done);
                    if (quitAtEnd) Application.Quit();
                    return;
            }
        }

        // 타이틀 씬의 NetBootstrap 코스 경로는 브랜치마다 다르다 → 측정 때만 런타임에 바꾼다(씬 파일은 그대로)
        void SetCourse(NetBootstrap net)
        {
            var f = typeof(NetBootstrap).GetField("courseScenePath", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) { Event("코스 경로 필드 없음 → 기본 코스로 진행"); return; }
            if (SceneUtility.GetBuildIndexByScenePath(course) < 0) { Event("빌드에 코스 씬 없음: " + course); return; }
            Event("코스 " + f.GetValue(net) + " → " + course);
            f.SetValue(net, course);
        }

        // ---------- 구역 순회 (에디터 PerfTour와 같은 방식) ----------

        void BeginTour()
        {
            stops = BuildStops(SceneManager.GetActiveScene());
            stopIndex = -1;
            Event("순회 " + round + "회차 시작: 지점 " + stops.Count + "개");
            NextStop();
        }

        void NextStop()
        {
            stopIndex++;
            var p = Local();
            if (stopIndex >= stops.Count || p == null)
            {
                Event("순회 " + round + "회차 끝" + (p == null ? " (로컬 플레이어 없음)" : ""));
                stops = null; marker = "";
                return;
            }
            var s = stops[stopIndex];
            p.Teleport(s.pos, p.transform.rotation);
            tourPhase = 0; phaseStart = Time.realtimeSinceStartup;
            marker = s.zone;
            Event("tour " + (stopIndex + 1) + "/" + stops.Count + " " + s.zone + " 도착");
        }

        void Tour()
        {
            if (stops == null) return;
            float t = Time.realtimeSinceStartup - phaseStart;
            if (tourPhase == 0)
            {
                if (t < TourSettle) return;
                tourPhase = 1; phaseStart = Time.realtimeSinceStartup;
                return;
            }
            var orbit = Orbit();
            if (orbit != null)
            {
                float yaw = t / TourSpin * 360f;
                orbit.HorizontalAxis.Value = orbit.HorizontalAxis.ClampValue(Mathf.Repeat(yaw + 180f, 360f) - 180f);
            }
            if (t >= TourSpin) NextStop();
        }

        static PlayerController Local()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++) if (all[i].IsLocal) return all[i];
            return null;
        }

        static CinemachineOrbitalFollow Orbit()
        {
            foreach (var o in FindObjectsByType<CinemachineOrbitalFollow>(FindObjectsSortMode.None))
                if (o.isActiveAndEnabled) return o;
            return null;
        }

        static List<Transform> ZoneRoots(Scene scene, bool includeInactive)
        {
            var roots = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("HiddenPath")) continue;
                foreach (Transform z in root.transform)
                    if ((includeInactive || z.gameObject.activeInHierarchy) && z.name.Length > 1 && z.name[0] == 'Z' && char.IsDigit(z.name[1])) roots.Add(z);
            }
            roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return roots;
        }

        static bool ZoneBounds(Transform z, bool includeInactive, out Bounds b)
        {
            var rs = z.GetComponentsInChildren<Renderer>(includeInactive);
            b = default;
            if (rs.Length == 0) return false;
            b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return true;
        }

        static List<Stop> BuildStops(Scene scene)
        {
            var list = new List<Stop>();
            foreach (var z in ZoneRoots(scene, false))
            {
                Bounds b;
                if (!ZoneBounds(z, false, out b)) continue;
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
            }
            return list;
        }

        void BuildZones(Scene scene)
        {
            zoneScene = scene.path;
            zones.Clear();
            foreach (var z in ZoneRoots(scene, true))
            {
                Bounds b;
                if (ZoneBounds(z, true, out b)) zones.Add(new Zone { name = z.name, b = b });
            }
        }

        string FindZone()
        {
            var local = Local();
            if (local == null) return "none";
            if (zones.Count == 0) return "-";
            var pos = local.transform.position;
            string best = null; float bestArea = float.MaxValue;
            foreach (var z in zones)
            {
                var c = z.b.center; var e = z.b.extents;
                if (Mathf.Abs(pos.x - c.x) <= e.x && Mathf.Abs(pos.z - c.z) <= e.z && e.x * e.z < bestArea) { bestArea = e.x * e.z; best = z.name; }
            }
            return best ?? "~";
        }
    }
}
#endif
