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
    // 최적화 후보 A/B 비교 (에디터 전용). 메뉴 VaroGO/Perf/AB Test 가 켜져 있으면 PlayScene_Stage1 플레이 시:
    //  화면 4곳(Z0·Z2 가장 무거운 방향, Z3 동굴 안, Z3 그림 찾기 구간)에서 후보 설정을 단계별로 하나씩 바꿔
    //  1회차(예열: 셰이더 미리 만들기) → 2회차(측정 4초 + 스크린샷)를 남긴다.
    // 바꾼 값은 변형마다 바로 되돌리고, Play를 멈출 때도 남은 것을 모두 되돌린다. 설정 파일·씬은 저장하지 않는다.
    // 결과: <프로젝트>/Logs/Perf/ab_<시각>/results.csv + <화면>__<후보>__<단계>.jpg (960x540)
    [InitializeOnLoad]
    static class PerfAbTest
    {
        const string PrefKey = "VaroGO.PerfAbTest";
        const string MenuPath = "VaroGO/Perf/AB Test";
        const float Settle = 3f, Sample = 4f, Spin = 10f, WarmSettle = 3f;
        const int Capacity = 1000;

        static bool Enabled { get => EditorPrefs.GetBool(PrefKey, false); set => EditorPrefs.SetBool(PrefKey, value); }

        [MenuItem(MenuPath)]
        static void Toggle() { Enabled = !Enabled; Debug.Log("[Perf] AB Test " + (Enabled ? "ON" : "OFF")); }
        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }

        // ---------- 화면 ----------
        class View
        {
            public string name, target; public bool heavy; // heavy = 삼각형 최대 방향, 아니면 target 중심을 바라봄
            public Vector3 pos, look; public bool ok; public float yaw; public bool yawFound;
        }
        static readonly View[] Views =
        {
            new View { name = "Z0_Entrance", target = "Z0_Entrance", heavy = true },
            new View { name = "Z2_MushroomAscent", target = "Z2_MushroomAscent", heavy = true },
            new View { name = "Z3_CaveTunnel", target = "Z3_CaveTunnel" },
            // Z3_Pictorial_Forest: 범위 가장자리 바닥 찾기가 입구 절벽으로 잡혀 제외 (2026-11-26). 동굴 화면에 그림 액자가 보임
        };

        // 켜면 "기준"과 "묶음"만 측정하고, 측정을 2회 반복한다(묶음끼리 빠르게 비교할 때)
        const string BundlesOnlyKey = "VaroGO.PerfAbTest.BundlesOnly";
        static bool BundlesOnly => EditorPrefs.GetBool(BundlesOnlyKey, false);
        static int MeasurePasses => BundlesOnly ? 2 : 1;

        // ---------- 변형 ----------
        struct Variant { public string cand, step; public Func<Action> apply; }
        static List<Variant> variants;

        enum Phase { Idle, WaitScene, Move, Spin, Settle, Sample, Shot, Done }
        static Phase phase;
        static double phaseStart;
        static int pass, viewIndex, varIndex; // pass 0 = 예열, 1 = 측정
        static Action revert;
        static readonly List<Action> pending = new List<Action>();
        static float bestScore;
        static string outDir, shotPng, shotJpg;
        static StreamWriter writer;
        static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        static ProfilerRecorder rGpu, rCpuMain, rCpuRender, rDraw, rSetPass, rTris, rCasters;
        static int frame0; static double time0;
        static string pendingRow;

        static PerfAbTest()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && Enabled) Go(Phase.WaitScene);
                if (s == PlayModeStateChange.ExitingPlayMode) Finish("플레이 종료");
            };
        }

        static void Go(Phase p) { phase = p; phaseStart = EditorApplication.timeSinceStartup; }
        static double T => EditorApplication.timeSinceStartup - phaseStart;
        static void Log(string s) { Debug.Log("[Perf] AB " + s); }

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
                    var v = Views[viewIndex];
                    // 저장된 카메라 방향이 있으면 찾지 않고 그대로 쓴다(측정끼리 같은 화면 비교용)
                    string yk = "VaroGO.PerfAbTest.Yaw." + v.name;
                    if (!v.yawFound && EditorPrefs.HasKey(yk)) { v.yaw = EditorPrefs.GetFloat(yk); v.yawFound = true; Log("화면 " + v.name + " 저장된 방향 사용 yaw=" + v.yaw.ToString("F0", ci)); }
                    if (v.yawFound) { StartVariant(); return; }
                    bestScore = float.MinValue; Go(Phase.Spin);
                    return;
                case Phase.Spin:
                {
                    var vw = Views[viewIndex];
                    float yaw = Mathf.Repeat((float)(T / Spin) * 360f + 180f, 360f) - 180f;
                    SetYaw(yaw);
                    float score;
                    if (vw.heavy) score = rTris.Valid ? rTris.LastValue : 0;
                    else
                    {
                        var cam = Camera.main;
                        score = cam != null ? Vector3.Dot(cam.transform.forward, (vw.look - cam.transform.position).normalized) : 0;
                    }
                    if (score > bestScore) { bestScore = score; vw.yaw = yaw; }
                    if (T < Spin) return;
                    vw.yawFound = true;
                    Log("화면 " + vw.name + " 카메라 고정 yaw=" + vw.yaw.ToString("F0", ci));
                    StartVariant();
                    return;
                }
                case Phase.Settle:
                    if (T < (pass == 0 ? WarmSettle : Settle)) return;
                    if (pass == 0) { EndVariant(); return; }
                    ResetRecorders(); Go(Phase.Sample);
                    return;
                case Phase.Sample:
                    if (T < Sample) return;
                    pendingRow = Row();
                    var vv = variants[varIndex];
                    string baseName = Safe(Views[viewIndex].name) + "__" + Safe(vv.cand) + "__" + Safe(vv.step);
                    shotPng = Path.Combine(outDir, baseName + ".png");
                    shotJpg = Path.Combine(outDir, baseName + ".jpg");
                    ScreenCapture.CaptureScreenshot(shotPng);
                    Go(Phase.Shot);
                    return;
                case Phase.Shot:
                    if (!File.Exists(shotPng) || T < 0.5) { if (T > 10) { Debug.LogWarning("[Perf] AB 스크린샷 실패: " + shotPng); FinishShot(false); } return; }
                    FinishShot(true);
                    return;
            }
        }

        static void Begin()
        {
            var scene = SceneManager.GetActiveScene();
            variants = BuildVariants(scene);
            var stops = PerfTour.BuildStops(scene);
            foreach (var v in Views) Place(v, scene, stops);
            StartRecorders();
            outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Perf", "ab_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(outDir);
            writer = new StreamWriter(Path.Combine(outDir, "results.csv"), false, new UTF8Encoding(true));
            writer.WriteLine("view,candidate,step,fps,gpu_ms,cpu_main_ms,cpu_render_ms,drawcalls,setpass,tris,shadow_casters,image,pass");
            Log("시작: 변형 " + variants.Count + "개 x 화면 " + Views.Count(v => v.ok) + "곳 (예열 1회 + 측정 1회) → " + outDir);
            pass = 0; viewIndex = -1; NextView();
        }

        static void NextView()
        {
            do viewIndex++; while (viewIndex < Views.Length && !Views[viewIndex].ok);
            if (viewIndex >= Views.Length)
            {
                if (pass < MeasurePasses) { pass++; Log((pass == 1 ? "예열 끝 → 측정 시작" : "측정 " + pass + "회차 시작")); viewIndex = -1; NextView(); return; }
                Finish("모든 변형 완료");
                return;
            }
            var v = Views[viewIndex];
            var p = PerfTour.Local();
            if (p == null) { Finish("로컬 플레이어 없음"); return; }
            p.Teleport(v.pos, p.transform.rotation);
            varIndex = 0;
            Log((pass == 0 ? "예열 " : "측정 ") + v.name + " 이동 " + v.pos);
            Go(Phase.Move);
        }

        static void StartVariant()
        {
            var vr = variants[varIndex];
            try { revert = vr.apply(); }
            catch (Exception e) { Debug.LogWarning("[Perf] AB 적용 실패 " + vr.cand + "/" + vr.step + ": " + e.Message); revert = null; }
            if (revert != null) pending.Add(revert);
            Go(Phase.Settle);
        }

        static void EndVariant()
        {
            if (revert != null) { revert(); pending.Remove(revert); revert = null; }
            varIndex++;
            if (varIndex < variants.Count) StartVariant();
            else NextView();
        }

        static void FinishShot(bool ok)
        {
            string img = "";
            if (ok)
            {
                try
                {
                    var src = new Texture2D(2, 2);
                    src.LoadImage(File.ReadAllBytes(shotPng));
                    var rt = RenderTexture.GetTemporary(960, 540, 0);
                    Graphics.Blit(src, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var dst = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    dst.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); dst.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    File.WriteAllBytes(shotJpg, dst.EncodeToJPG(85));
                    UnityEngine.Object.DestroyImmediate(src); UnityEngine.Object.DestroyImmediate(dst);
                    File.Delete(shotPng);
                    img = Path.GetFileName(shotJpg);
                }
                catch (Exception e) { Debug.LogWarning("[Perf] AB 이미지 변환 실패: " + e.Message); img = Path.GetFileName(shotPng); }
            }
            writer.WriteLine(pendingRow + "," + img + "," + pass); writer.Flush();
            EndVariant();
        }

        static void Finish(string why)
        {
            if (phase == Phase.Idle || phase == Phase.Done) { RestoreAll(); return; }
            revert?.Invoke(); revert = null;
            RestoreAll();
            if (writer != null) { writer.Close(); writer = null; }
            foreach (var r in All()) r.Dispose();
            Log("끝: " + why + " → " + outDir);
            phase = Phase.Done;
        }

        static void RestoreAll()
        {
            for (int i = pending.Count - 1; i >= 0; i--) { try { pending[i](); } catch { } }
            pending.Clear();
        }

        static void HoldCamera()
        {
            if (phase != Phase.Settle && phase != Phase.Sample && phase != Phase.Shot) return;
            if (viewIndex >= 0 && viewIndex < Views.Length && Views[viewIndex].yawFound) SetYaw(Views[viewIndex].yaw);
        }

        static void SetYaw(float yaw)
        {
            var o = PerfTour.Orbit();
            if (o != null) o.HorizontalAxis.Value = o.HorizontalAxis.ClampValue(yaw);
        }

        static string Safe(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '.' ? c : '-');
            return sb.ToString();
        }

        // ---------- 화면 위치 ----------
        static void Place(View v, Scene scene, List<PerfTour.Stop> stops)
        {
            if (v.heavy)
            {
                var s = stops.FirstOrDefault(x => x.zone == v.target);
                v.ok = s.zone != null; v.pos = s.pos;
                if (!v.ok) Log("화면 위치 못 찾음: " + v.name);
                return;
            }
            if (v.target.Contains("Cave"))
            {
                // 동굴 안 판정은 CaveDarkness의 중심선(path) 기준 → 중심선 가운데 점 아래 바닥에 서서 다음 점을 바라봄
                // CaveDarkness가 없는 브랜치에서도 컴파일되도록 이름으로 찾는다
                var cdType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VaroGO.Client.Map.CaveDarkness")).FirstOrDefault(x => x != null);
                var cd = cdType != null ? UnityEngine.Object.FindAnyObjectByType(cdType) : null;
                var path = cd != null ? cdType.GetField("path")?.GetValue(cd) as Vector3[] : null;
                if (path == null || path.Length < 2) { Log("CaveDarkness 중심선 없음"); return; }
                for (int i = path.Length / 2; i < path.Length - 1; i++)
                {
                    RaycastHit h;
                    if (!Physics.Raycast(path[i] + Vector3.up * 2f, Vector3.down, out h, 40f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < 0.6f) continue;
                    v.pos = h.point + Vector3.up * 0.3f; v.look = path[i + 1]; v.ok = true;
                    return;
                }
                Log("동굴 바닥 못 찾음"); return;
            }
            var t = FindDeep(scene, v.target);
            if (t == null) { Log("대상 없음: " + v.target); return; }
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Log("대상에 렌더러 없음: " + v.target); return; }
            var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            v.look = b.center;
            bool cave = v.target.Contains("Cave");
            // 동굴: 안쪽(위에 천장이 있는) 바닥, 긴 축의 1/4 지점. 그림 찾기: 범위 가장자리 바닥에서 가운데를 바라봄
            var cands = new List<Vector3>();
            bool alongX = b.extents.x >= b.extents.z;
            for (int k = -3; k <= 3; k++)
            {
                float f = k / 4f;
                if (cave) cands.Add(b.center + (alongX ? new Vector3(b.extents.x * f, 0, 0) : new Vector3(0, 0, b.extents.z * f)));
                else
                {
                    cands.Add(b.center + new Vector3(b.extents.x * 0.85f, 0, b.extents.z * f));
                    cands.Add(b.center + new Vector3(-b.extents.x * 0.85f, 0, b.extents.z * f));
                    cands.Add(b.center + new Vector3(b.extents.x * f, 0, b.extents.z * 0.85f));
                    cands.Add(b.center + new Vector3(b.extents.x * f, 0, -b.extents.z * 0.85f));
                }
            }
            if (cave) cands = cands.OrderBy(c => Mathf.Abs(Vector3.Distance(c, b.center) - (alongX ? b.extents.x : b.extents.z) * 0.5f)).ToList();
            foreach (var c in cands)
            {
                RaycastHit hit;
                var origin = cave ? new Vector3(c.x, b.center.y, c.z) : new Vector3(c.x, b.max.y + 5f, c.z);
                if (!Physics.Raycast(origin, Vector3.down, out hit, b.size.y + 30f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7f) continue;
                if (cave && !Physics.Raycast(hit.point + Vector3.up * 0.5f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore)) continue; // 천장 없음 = 밖
                v.pos = hit.point + Vector3.up * 0.3f; v.ok = true;
                if (cave) v.look = b.center + (alongX ? new Vector3(Mathf.Sign(b.center.x - c.x) * b.extents.x, 0, 0) : new Vector3(0, 0, Mathf.Sign(b.center.z - c.z) * b.extents.z));
                return;
            }
            Log("화면 바닥 못 찾음: " + v.name);
        }

        static Transform FindDeep(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name && t.gameObject.activeInHierarchy) return t;
            return null;
        }

        // ---------- 변형 목록 ----------
        static List<Variant> BuildVariants(Scene scene)
        {
            var list = new List<Variant>();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var sun = RenderSettings.sun != null ? RenderSettings.sun : UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            var sunData = sun != null ? sun.GetComponent<UniversalAdditionalLightData>() : null;
            var ssao = Features(urp).FirstOrDefault(f => f != null && f.name.Contains("AmbientOcclusion") && f.isActive);
            var terrains = Terrain.activeTerrains;

            Action<string, string, Func<Action>> add = (c, s, a) => list.Add(new Variant { cand = c, step = s, apply = a });

            // 개별 적용 함수 (되돌리기 반환)
            Func<SoftShadowQuality, Func<Action>> soft = q => () => { var o = sunData.softShadowQuality; sunData.softShadowQuality = q; return () => sunData.softShadowQuality = o; };
            Func<Action> hard = () => { var o = sun.shadows; sun.shadows = LightShadows.Hard; return () => sun.shadows = o; };
            Func<int, Func<Action>> casc = n => () => { var o = urp.shadowCascadeCount; urp.shadowCascadeCount = n; return () => urp.shadowCascadeCount = o; };
            Func<float, Func<Action>> sdist = d => () => { var o = urp.shadowDistance; urp.shadowDistance = d; return () => urp.shadowDistance = o; };
            Func<float, Func<Action>> detail = d => () => { var os = terrains.Select(t => t.detailObjectDistance).ToArray(); foreach (var t in terrains) t.detailObjectDistance = d; return () => { for (int i = 0; i < terrains.Length; i++) terrains[i].detailObjectDistance = os[i]; }; };
            Func<float, Func<Action>> lod = x => () => { var o = QualitySettings.lodBias; QualitySettings.lodBias = x; return () => QualitySettings.lodBias = o; };
            Func<float, Func<Action>> scale = x => () => { var o = urp.renderScale; urp.renderScale = x; return () => urp.renderScale = o; };
            Func<Dictionary<string, int>, Func<Action>> ssaoSet = vals => () => SetSsao(ssao, vals);
            Func<Action> ssaoOff = () => { ssao.SetActive(false); return () => ssao.SetActive(true); };
            var decor = DecorRenderers(scene);
            var mountains = scene.GetRootGameObjects().Where(r => r.name == "Background Mountains").SelectMany(r => r.GetComponentsInChildren<Renderer>()).Where(r => r.shadowCastingMode != ShadowCastingMode.Off).ToArray();
            Func<Renderer[], Func<Action>> noShadow = rs => () => { var o = rs.Select(r => r.shadowCastingMode).ToArray(); foreach (var r in rs) r.shadowCastingMode = ShadowCastingMode.Off; return () => { for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].shadowCastingMode = o[i]; }; };
            Func<Func<Action>[], Func<Action>> combo = fs => () => { var rv = fs.Select(f => f()).ToList(); return () => { for (int i = rv.Count - 1; i >= 0; i--) rv[i]?.Invoke(); }; };
            Log("장식 그림자 대상 " + decor.Length + "개, 배경 산 " + mountains.Length + "개");

            add("기준", "현재 설정", () => null);
            if (sunData != null)
            {
                add("부드러운 그림자", "Medium", soft(SoftShadowQuality.Medium));
                add("부드러운 그림자", "Low", soft(SoftShadowQuality.Low));
            }
            if (sun != null && sun.shadows == LightShadows.Soft) add("부드러운 그림자", "딱딱한 그림자", hard);
            if (ssao != null)
            {
                add("SSAO", "깊이만 사용", ssaoSet(new Dictionary<string, int> { { "Source", 0 } }));
                add("SSAO", "해상도 절반", ssaoSet(new Dictionary<string, int> { { "Downsample", 1 } }));
                add("SSAO", "샘플 Low + 블러 Medium", ssaoSet(new Dictionary<string, int> { { "Samples", 2 }, { "BlurQuality", 1 } }));
                add("SSAO", "끔", ssaoOff);
            }
            add("장식 그림자 끄기", "작은 장식 " + decor.Length + "개", noShadow(decor));
            add("장식 그림자 끄기", "배경 산 " + mountains.Length + "개", noShadow(mountains));
            if (urp != null)
            {
                add("캐스케이드", "3", casc(3));
                add("캐스케이드", "2", casc(2));
                add("그림자 거리", "40", sdist(40));
                add("그림자 거리", "30", sdist(30));
            }
            if (terrains.Length > 0)
            {
                add("풀 장식 거리", "60m", detail(60));
                add("풀 장식 거리", "40m", detail(40));
            }
            add("LOD bias", "1.5", lod(1.5f));
            add("LOD bias", "1", lod(1f));
            if (urp != null) add("렌더 스케일", "0.8", scale(0.8f));

            var a = new List<Func<Action>>();
            if (sunData != null) a.Add(soft(SoftShadowQuality.Medium));
            if (ssao != null) a.Add(ssaoSet(new Dictionary<string, int> { { "Source", 0 }, { "Downsample", 1 } }));
            a.Add(noShadow(decor)); a.Add(noShadow(mountains));
            if (urp != null) a.Add(casc(3));
            if (terrains.Length > 0) a.Add(detail(60));
            add("묶음", "A 손해 적은 조합", combo(a.ToArray()));

            var bb = new List<Func<Action>>();
            if (sun != null) bb.Add(hard);
            if (ssao != null) bb.Add(ssaoOff);
            if (urp != null) { bb.Add(casc(2)); bb.Add(sdist(30)); bb.Add(scale(0.8f)); }
            if (terrains.Length > 0) bb.Add(detail(40));
            add("묶음", "B 저사양", combo(bb.ToArray()));

            // 권장 조합 (2026-11-26 A/B 결과): 부드러운 그림자 Low + SSAO 깊이만 사용
            var rec = new List<Func<Action>>();
            if (sunData != null) rec.Add(soft(SoftShadowQuality.Low));
            if (ssao != null) rec.Add(ssaoSet(new Dictionary<string, int> { { "Source", 0 } }));
            add("묶음", "권장 그림자 Low + SSAO 깊이만", combo(rec.ToArray()));
            if (urp != null) add("묶음", "권장 + 캐스케이드 3", combo(rec.Concat(new[] { casc(3) }).ToArray()));

            add("기준", "마지막 확인", () => null);
            if (BundlesOnly) list = list.Where(x => x.cand == "기준" || x.cand == "묶음").ToList();
            return list;
        }

        // 그림자를 꺼도 되는 장식: 콜라이더가 없고(밟는 발판 아님) 작은(대각선 6m 미만) 렌더러. 캐릭터·HiddenPath/Art_Forest 밖은 제외
        static Renderer[] DecorRenderers(Scene scene)
        {
            var list = new List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("HiddenPath") && root.name != "Art_Forest") continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (r.shadowCastingMode == ShadowCastingMode.Off || r is ParticleSystemRenderer) continue;
                    if (r.bounds.size.magnitude >= 6f) continue;
                    if (r.GetComponentInParent<Collider>() != null || r.GetComponentInChildren<Collider>() != null) continue;
                    if (r.GetComponentInParent<PlayerController>() != null) continue;
                    list.Add(r);
                }
            }
            return list.ToArray();
        }

        static Action SetSsao(ScriptableRendererFeature f, Dictionary<string, int> vals)
        {
            var sf = f.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance);
            var settings = sf.GetValue(f);
            var old = new Dictionary<FieldInfo, object>();
            foreach (var kv in vals)
            {
                var fi = settings.GetType().GetField(kv.Key, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                old[fi] = fi.GetValue(settings);
                object v = fi.FieldType == typeof(bool) ? (object)(kv.Value != 0) : fi.FieldType.IsEnum ? Enum.ToObject(fi.FieldType, kv.Value) : (object)kv.Value;
                fi.SetValue(settings, v);
            }
            return () => { foreach (var kv in old) kv.Key.SetValue(settings, kv.Value); };
        }

        static IEnumerable<ScriptableRendererFeature> Features(UniversalRenderPipelineAsset urp)
        {
            if (urp == null) yield break;
            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var datas = f?.GetValue(urp) as ScriptableRendererData[];
            if (datas == null) yield break;
            foreach (var d in datas) if (d != null) foreach (var feat in d.rendererFeatures) yield return feat;
        }

        // ---------- 기록 ----------
        static ProfilerRecorder[] All() => new[] { rGpu, rCpuMain, rCpuRender, rDraw, rSetPass, rTris, rCasters };

        static void StartRecorders()
        {
            rGpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", Capacity);
            rCpuMain = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Main Thread Frame Time", Capacity);
            rCpuRender = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Render Thread Frame Time", Capacity);
            rDraw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", Capacity);
            rSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", Capacity);
            rTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", Capacity);
            rCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count", Capacity);
        }

        static void ResetRecorders()
        {
            foreach (var r in All()) { r.Reset(); r.Start(); }
            frame0 = Time.frameCount; time0 = EditorApplication.timeSinceStartup;
        }

        static double Avg(ProfilerRecorder r, double scale)
        {
            if (!r.Valid || r.Count == 0) return -1;
            double s = 0; int n = r.Count;
            for (int i = 0; i < n; i++) s += r.GetSample(i).Value;
            return s / n * scale;
        }

        static string Row()
        {
            double fps = (Time.frameCount - frame0) / (EditorApplication.timeSinceStartup - time0);
            const double ms = 1e-6;
            var v = variants[varIndex];
            string line = string.Join(",", new[]
            {
                Views[viewIndex].name, v.cand.Replace(',', ' '), v.step.Replace(',', ' '), fps.ToString("F1", ci),
                Avg(rGpu, ms).ToString("F2", ci), Avg(rCpuMain, ms).ToString("F2", ci), Avg(rCpuRender, ms).ToString("F2", ci),
                Avg(rDraw, 1).ToString("F0", ci), Avg(rSetPass, 1).ToString("F0", ci), Avg(rTris, 1).ToString("F0", ci), Avg(rCasters, 1).ToString("F0", ci)
            });
            Log("측정 " + Views[viewIndex].name + " | " + v.cand + " " + v.step + " | fps " + fps.ToString("F1", ci) + " gpu " + Avg(rGpu, ms).ToString("F1", ci) + "ms");
            return line;
        }
    }
}
