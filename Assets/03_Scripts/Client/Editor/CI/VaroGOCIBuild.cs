using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VaroGO.Client.EditorTools
{
    // CI(GitHub Actions) 전용 Windows 빌드. Tools/CI/ci_build.ps1이 배치 모드로 호출한다.
    // Unity.exe -batchmode -quit -projectPath <path> -executeMethod VaroGO.Client.EditorTools.VaroGOCIBuild.Build
    //           -varogoOutput <폴더> [-varogoScenes "11_SpiralCourse,Assets/01_Scenes/Other.unity"] [-varogoRelease]
    // Build Settings는 보지 않는다(서버 소유). 씬은 이름 또는 경로, 쉼표로 여러 개, 첫 번째가 시작 씬. 기본: 11_SpiralCourse
    // 종료 코드: 0 성공 / 1 빌드 실패 / 2 인자 오류
    public static class VaroGOCIBuild
    {
        const string DefaultScene = "Assets/01_Scenes/11_SpiralCourse.unity";
        const string ExeName = "VaroGO.exe";
        const string Tag = "[VaroGOCIBuild]";

        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            string output = Arg(args, "-varogoOutput");
            string sceneArg = Arg(args, "-varogoScenes");
            bool development = !args.Contains("-varogoRelease");

            if (string.IsNullOrEmpty(output))
            {
                Debug.LogError($"{Tag} -varogoOutput 필요");
                EditorApplication.Exit(2);
                return;
            }

            try
            {
                var sceneNames = string.IsNullOrWhiteSpace(sceneArg)
                    ? new[] { DefaultScene }
                    : sceneArg.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                var scenes = sceneNames.Select(ResolveScene).Distinct().ToArray();

                Directory.CreateDirectory(output);
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(output, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                };
                Debug.Log($"{Tag} 시작: {string.Join(", ", scenes)} {(development ? "Development" : "Release")} → {output}");
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"{Tag} 빌드 실패 ({report.summary.result}, 에러 {report.summary.totalErrors}개)\n{FirstErrors(report)}");
                    EditorApplication.Exit(1);
                    return;
                }

                WriteBuildInfo(output, scenes, development);
                Debug.Log($"{Tag} 성공 ({report.summary.totalTime.TotalSeconds:F0}s)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} {e.Message}");
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
        }

        // 씬 이름(11_SpiralCourse) 또는 경로(Assets/.../X.unity) → 경로
        static string ResolveScene(string scene)
        {
            if (scene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || scene.Contains('/'))
            {
                var path = scene.Replace('\\', '/');
                if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) path += ".unity";
                if (!File.Exists(path)) throw new Exception($"씬 없음: {path}");
                return path;
            }

            var matches = AssetDatabase.FindAssets($"t:Scene {scene}", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), scene, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0) throw new Exception($"씬 못 찾음: {scene}");
            if (matches.Length > 1) throw new Exception($"같은 이름 씬이 여러 개: {string.Join(", ", matches)} — 경로로 지정");
            return matches[0];
        }

        static string FirstErrors(BuildReport report)
        {
            var errors = report.steps
                .SelectMany(s => s.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content.Split('\n')[0])
                .Distinct()
                .Take(5)
                .ToArray();
            return errors.Length == 0 ? "로그 확인" : string.Join("\n", errors);
        }

        // 커밋·브랜치는 GitHub Actions 환경변수에서 가져온다
        static void WriteBuildInfo(string output, string[] scenes, bool development)
        {
            var sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
            var sb = new StringBuilder();
            sb.AppendLine($"VaroGO {PlayerSettings.bundleVersion}");
            sb.AppendLine($"build: {(development ? "Development" : "Release")}");
            sb.AppendLine($"built: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"branch: {Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? "?"}");
            sb.AppendLine($"commit: {(sha != null && sha.Length >= 7 ? sha.Substring(0, 7) : "?")}");
            sb.AppendLine($"unity: {Application.unityVersion}");
            sb.AppendLine("scenes:");
            foreach (var s in scenes) sb.AppendLine($"  - {s}");
            File.WriteAllText(Path.Combine(output, "build_info.txt"), sb.ToString(), new UTF8Encoding(true));
        }
    }
}
