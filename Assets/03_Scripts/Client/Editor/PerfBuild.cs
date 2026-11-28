using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VaroGO.Client.EditorTools
{
    // 성능 측정용 Development Build (에디터 전용). 메뉴 VaroGO/Perf/Build Dev (Perf)
    // 씬: TitleScene → LobbyScene → PlayScene_Stage1. Build Settings(서버 소유)는 건드리지 않는다.
    // FrameTimingManager로 GPU 시간을 재려면 Frame Timing Stats가 켜져 있어야 해서 빌드하는 동안만 켰다가 되돌린다.
    // 출력: 프로젝트 바깥 ../VaroGoBuilds/PerfDev/VaroGO.exe (git에 안 들어감)
    // 실행: VaroGO.exe -perf -perfrounds 2 -perfquit  (자세한 인자는 PerfRuntime 참고)
    static class PerfBuild
    {
        const string Tag = "[PerfBuild]";
        static readonly string[] Scenes =
        {
            "Assets/01_Scenes/TitleScene.unity",
            "Assets/01_Scenes/LobbyScene.unity",
            "Assets/01_Scenes/PlayScene_Stage1.unity",
        };

        public static string OutputDir => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "VaroGoBuilds", "PerfDev"));

        [MenuItem("VaroGO/Perf/Build Dev (Perf)")]
        public static void BuildFromMenu() => EditorApplication.delayCall += Build;

        public static void Build()
        {
            bool timingBefore = PlayerSettings.enableFrameTimingStats;
            PlayerSettings.enableFrameTimingStats = true;
            try
            {
                Directory.CreateDirectory(OutputDir);
                var options = new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = Path.Combine(OutputDir, "VaroGO.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development,
                };
                Debug.Log(Tag + " 시작 → " + options.locationPathName);
                var report = BuildPipeline.BuildPlayer(options);
                var sum = report.summary;
                if (sum.result == BuildResult.Succeeded)
                    Debug.Log(Tag + " 성공 (" + sum.totalTime.TotalSeconds.ToString("F0") + "s, " + (sum.totalSize / 1048576) + "MB) → " + options.locationPathName);
                else
                    Debug.LogError(Tag + " 실패: " + sum.result + " / 에러 " + sum.totalErrors + "개");
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = timingBefore;
            }
        }
    }
}
