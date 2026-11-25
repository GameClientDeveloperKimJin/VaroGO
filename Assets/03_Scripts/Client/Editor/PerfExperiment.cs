using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace VaroGO.Client.EditorTools
{
    // 성능 원인 쪼개기 실험 (에디터 전용). 메뉴 VaroGO/Perf/Experiment 가 켜져 있으면 PlayScene_* 플레이 시:
    //  지정 구역(Z0, Z2)마다 바닥에 서서 카메라를 한 바퀴 돌려 삼각형이 가장 많은 방향을 찾고, 그 방향에 카메라를 고정한 채
    //  설정을 하나씩 바꿔(그림자·SSAO·후처리·해상도·LOD·시야 거리·지형·오브젝트 그룹 숨김) 4초씩 GPU/CPU 시간을 잰다.
    // 바꾼 값은 조건마다 바로 되돌리고, Play를 멈출 때도 남은 것을 모두 되돌린다(URP 에셋·품질 설정 포함).
    // 결과: <프로젝트>/Logs/Perf/experiment_*.csv
    [InitializeOnLoad]
    static class PerfExperiment
    {
        const string PrefKey = "VaroGO.PerfExperiment";
        const string MenuPath = "VaroGO/Perf/Experiment";
        static readonly string[] Views = { "Z0_Entrance", "Z2_MushroomAscent" };
        // Settle: 설정을 바꾼 직후 셰이더·파이프라인 재생성으로 몇 초 멈출 수 있어 넉넉히 둔다 (1.5초로는 그림자·SSAO 조건이 오염됨)
        const float Settle = 3f, Sample = 4f, Spin = 10f;
        const int Capacity = 1000;

        static bool Enabled { get => EditorPrefs.GetBool(PrefKey, false); set => EditorPrefs.SetBool(PrefKey, value); }

        [MenuItem(MenuPath)]
        static void Toggle() { Enabled = !Enabled; Debug.Log("[Perf] Experiment " + (Enabled ? "ON" : "OFF")); }
        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }

        struct Cond { public string name; public Func<Action> apply; }

        enum Phase { Idle, WaitScene, Move, Spin, Settle, Sample, Done }
        static Phase phase;
        static double phaseStart;
        static int viewIndex, condIndex;
        static List<PerfTour.Stop> stops;
        static List<Cond> conds;
        static Action revert;
        static readonly List<Action> pending = new List<Action>(); // Play 정지 때 되돌릴 것
        static float yaw, bestYaw; static long bestTris;
        static StreamWriter writer;
        static string filePath;
        static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        static ProfilerRecorder rGpu, rCpuMain, rCpuRender, rDraw, rSetPass, rTris, rCasters, rUpdate, rPhysics, rWait;
        static int frame0; static double time0;

        static PerfExperiment()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && Enabled) { phase = Phase.WaitScene; phaseStart = EditorApplication.timeSinceStartup; }
                if (s == PlayModeStateChange.ExitingPlayMode) Finish("플레이 종료");
            };
        }

        static void Go(Phase p) { phase = p; phaseStart = EditorApplication.timeSinceStartup; }
        static double T => EditorApplication.timeSinceStartup - phaseStart;

        static void Tick()
        {
            if (phase == Phase.Idle || phase == Phase.Done || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            HoldCamera();
            switch (phase)
            {
                case Phase.WaitScene:
                    if (!SceneManager.GetActiveScene().name.StartsWith("PlayScene_") || PerfTour.Local() == null || T < 3) return;
                    Begin();
                    return;
                case Phase.Move:
                    if (T < 3) return;
                    yaw = 0; bestYaw = 0; bestTris = -1;
                    Go(Phase.Spin);
                    return;
                case Phase.Spin:
                    yaw = Mathf.Repeat((float)(T / Spin) * 360f + 180f, 360f) - 180f;
                    if (rTris.Valid && rTris.LastValue > bestTris) { bestTris = rTris.LastValue; bestYaw = yaw; }
                    if (T < Spin) return;
                    yaw = bestYaw;
                    Log("view " + Views[viewIndex] + " 카메라 고정 yaw=" + bestYaw.ToString("F0", ci) + " (삼각형 " + bestTris + ")");
                    condIndex = 0; StartCond();
                    return;
                case Phase.Settle:
                    if (T < Settle) return;
                    ResetRecorders(); Go(Phase.Sample);
                    return;
                case Phase.Sample:
                    if (T < Sample) return;
                    WriteRow();
                    revert?.Invoke(); revert = null;
                    condIndex++;
                    if (condIndex < conds.Count) { StartCond(); return; }
                    viewIndex++;
                    if (viewIndex < Views.Length) { MoveTo(viewIndex); return; }
                    Finish("모든 실험 완료");
                    return;
            }
        }

        static void Begin()
        {
            var scene = SceneManager.GetActiveScene();
            stops = PerfTour.BuildStops(scene);
            conds = BuildConds(scene);
            StartRecorders();
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Perf");
            Directory.CreateDirectory(dir);
            filePath = Path.Combine(dir, string.Format("experiment_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now));
            writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            writer.WriteLine("view,yaw,condition,fps,gpu_ms,cpu_main_ms,cpu_render_ms,update_ms,physics_ms,waitpresent_ms,drawcalls,setpass,tris,shadow_casters");
            Log("실험 시작: 조건 " + conds.Count + "개 x 구역 " + Views.Length + "개 → " + filePath + (rGpu.Valid ? "" : " (GPU 시간 카운터 없음)"));
            viewIndex = 0; MoveTo(0);
        }

        static void MoveTo(int i)
        {
            var s = stops.FirstOrDefault(x => x.zone == Views[i]);
            var p = PerfTour.Local();
            if (s.zone == null || p == null) { Finish("구역/플레이어 없음: " + Views[i]); return; }
            p.Teleport(s.pos, p.transform.rotation);
            Log("view " + Views[i] + " 이동 " + s.pos);
            Go(Phase.Move);
        }

        static void StartCond()
        {
            var c = conds[condIndex];
            try { revert = c.apply(); }
            catch (Exception e) { Debug.LogWarning("[Perf] 조건 적용 실패 " + c.name + ": " + e.Message); revert = null; }
            if (revert != null) pending.Add(revert);
            Go(Phase.Settle);
        }

        static void HoldCamera()
        {
            if (phase != Phase.Spin && phase != Phase.Settle && phase != Phase.Sample) return;
            var o = PerfTour.Orbit();
            if (o != null) o.HorizontalAxis.Value = o.HorizontalAxis.ClampValue(yaw);
        }

        static void Finish(string why)
        {
            if (phase == Phase.Idle || phase == Phase.Done) { RestoreAll(); return; }
            revert?.Invoke(); revert = null;
            RestoreAll();
            if (writer != null) { writer.Close(); writer = null; }
            DisposeRecorders();
            Log("실험 끝: " + why + " → " + filePath);
            phase = Phase.Done;
        }

        static void RestoreAll()
        {
            for (int i = pending.Count - 1; i >= 0; i--) { try { pending[i](); } catch { } }
            pending.Clear();
        }

        static void Log(string s) { Debug.Log("[Perf] " + s); }

        // ---------- 조건 ----------

        static List<Cond> BuildConds(Scene scene)
        {
            var list = new List<Cond>();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var sun = RenderSettings.sun != null ? RenderSettings.sun : UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            var cam = Camera.main;

            list.Add(new Cond { name = "기준", apply = () => null });
            if (sun != null)
            {
                list.Add(new Cond { name = "그림자 끔(태양)", apply = () => { var o = sun.shadows; sun.shadows = LightShadows.None; return () => sun.shadows = o; } });
                if (sun.shadows == LightShadows.Soft)
                    list.Add(new Cond { name = "부드러운→딱딱한 그림자", apply = () => { var o = sun.shadows; sun.shadows = LightShadows.Hard; return () => sun.shadows = o; } });
            }
            if (urp != null)
            {
                list.Add(new Cond { name = "캐스케이드 " + urp.shadowCascadeCount + "→2", apply = () => { var o = urp.shadowCascadeCount; urp.shadowCascadeCount = 2; return () => urp.shadowCascadeCount = o; } });
                list.Add(new Cond { name = "그림자 거리 " + urp.shadowDistance + "→25", apply = () => { var o = urp.shadowDistance; urp.shadowDistance = 25; return () => urp.shadowDistance = o; } });
                list.Add(new Cond { name = "HDR 끔", apply = () => { var o = urp.supportsHDR; urp.supportsHDR = false; return () => urp.supportsHDR = o; } });
                list.Add(new Cond { name = "렌더 스케일 0.5", apply = () => { var o = urp.renderScale; urp.renderScale = 0.5f; return () => urp.renderScale = o; } });
                var ssao = Features(urp).FirstOrDefault(f => f != null && f.name.Contains("AmbientOcclusion"));
                if (ssao != null && ssao.isActive)
                    list.Add(new Cond { name = "SSAO 끔", apply = () => { ssao.SetActive(false); return () => ssao.SetActive(true); } });
            }
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && data.renderPostProcessing)
                    list.Add(new Cond { name = "후처리 끔", apply = () => { data.renderPostProcessing = false; return () => data.renderPostProcessing = true; } });
                list.Add(new Cond { name = "시야 거리 절반", apply = () => { var o = cam.farClipPlane; cam.farClipPlane = o * 0.5f; return () => cam.farClipPlane = o; } });
            }
            list.Add(new Cond { name = "LOD bias " + QualitySettings.lodBias + "→1", apply = () => { var o = QualitySettings.lodBias; QualitySettings.lodBias = 1; return () => QualitySettings.lodBias = o; } });
            var terrains = Terrain.activeTerrains;
            if (terrains.Length > 0)
                list.Add(new Cond { name = "지형 숨김", apply = () => { foreach (var t in terrains) { t.drawHeightmap = false; t.drawTreesAndFoliage = false; } return () => { foreach (var t in terrains) { t.drawHeightmap = true; t.drawTreesAndFoliage = true; } }; } });

            // 오브젝트 그룹 숨김: 최상위 그룹 + Art_Forest·HiddenPath 바로 아래 자식(렌더러 10개 이상)
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentsInChildren<Renderer>().Length == 0) continue;
                list.Add(Hide(root.transform, root.name));
                if (root.name == "Art_Forest" || root.name.StartsWith("HiddenPath"))
                    foreach (Transform c in root.transform)
                        if (c.gameObject.activeInHierarchy && c.GetComponentsInChildren<Renderer>().Length >= 10) list.Add(Hide(c, root.name + "/" + c.name));
            }
            list.Add(new Cond { name = "기준(마지막 확인)", apply = () => null });
            return list;
        }

        static Cond Hide(Transform t, string label)
        {
            return new Cond
            {
                name = "숨김: " + label + " (렌더러 " + t.GetComponentsInChildren<Renderer>().Length + ")",
                apply = () =>
                {
                    var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                    foreach (var r in rs) r.enabled = false;
                    return () => { foreach (var r in rs) if (r != null) r.enabled = true; };
                }
            };
        }

        static IEnumerable<ScriptableRendererFeature> Features(UniversalRenderPipelineAsset urp)
        {
            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var datas = f?.GetValue(urp) as ScriptableRendererData[];
            if (datas == null) yield break;
            foreach (var d in datas) if (d != null) foreach (var feat in d.rendererFeatures) yield return feat;
        }

        // ---------- 기록 ----------

        static void StartRecorders()
        {
            rGpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", Capacity);
            rCpuMain = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Main Thread Frame Time", Capacity);
            rCpuRender = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Render Thread Frame Time", Capacity);
            rDraw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", Capacity);
            rSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", Capacity);
            rTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", Capacity);
            rCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count", Capacity);
            rUpdate = ProfilerRecorder.StartNew(new ProfilerCategory("PlayerLoop"), "Update.ScriptRunBehaviourUpdate", Capacity);
            rPhysics = ProfilerRecorder.StartNew(new ProfilerCategory("PlayerLoop"), "FixedUpdate.PhysicsFixedUpdate", Capacity);
            rWait = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", Capacity);
        }

        static void ResetRecorders()
        {
            foreach (var r in All()) { r.Reset(); r.Start(); }
            frame0 = Time.frameCount; time0 = EditorApplication.timeSinceStartup;
        }

        static ProfilerRecorder[] All() => new[] { rGpu, rCpuMain, rCpuRender, rDraw, rSetPass, rTris, rCasters, rUpdate, rPhysics, rWait };

        static void DisposeRecorders() { foreach (var r in All()) r.Dispose(); }

        static double Avg(ProfilerRecorder r, double scale)
        {
            if (!r.Valid || r.Count == 0) return -1;
            double s = 0; int n = r.Count;
            for (int i = 0; i < n; i++) s += r.GetSample(i).Value;
            return s / n * scale;
        }

        static void WriteRow()
        {
            double fps = (Time.frameCount - frame0) / (EditorApplication.timeSinceStartup - time0);
            const double ms = 1e-6;
            string row = string.Join(",", new[]
            {
                Views[viewIndex], yaw.ToString("F0", ci), conds[condIndex].name.Replace(',', ' '), fps.ToString("F1", ci),
                Avg(rGpu, ms).ToString("F2", ci), Avg(rCpuMain, ms).ToString("F2", ci), Avg(rCpuRender, ms).ToString("F2", ci),
                Avg(rUpdate, ms).ToString("F2", ci), Avg(rPhysics, ms).ToString("F2", ci), Avg(rWait, ms).ToString("F2", ci),
                Avg(rDraw, 1).ToString("F0", ci), Avg(rSetPass, 1).ToString("F0", ci), Avg(rTris, 1).ToString("F0", ci), Avg(rCasters, 1).ToString("F0", ci)
            });
            writer.WriteLine(row); writer.Flush();
            Log("실험 " + Views[viewIndex] + " | " + conds[condIndex].name + " | fps " + fps.ToString("F1", ci) + " gpu " + Avg(rGpu, ms).ToString("F1", ci) + "ms");
        }
    }
}
