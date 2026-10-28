using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VaroGO.Client.EditorTools
{
    // Tools > Build — CI 빌드 요청 (Tools/CI/trigger_build.ps1 호출). 빌드는 빌드 PC 러너가 하고, 결과는 공유 폴더에 vN으로 올라간다
    // 빌드되는 건 GitHub에 푸시된 커밋. 저장·커밋·푸시 안 된 변경이 있으면 먼저 확인창
    public static class VaroGOCIBuildMenu
    {
        const string Menu = "Tools/Build/";
        const string Title = "CI 빌드";
        static Task<(int code, string output)> running;

        [MenuItem(Menu + "기본 씬 빌드 (Development)", priority = 0)]
        static void BuildDefault() => Request("", false);

        [MenuItem(Menu + "기본 씬 빌드 (Release)", priority = 1)]
        static void BuildDefaultRelease() => Request("", true);

        [MenuItem(Menu + "현재 씬 빌드 (Development)", priority = 20)]
        static void BuildCurrent()
        {
            var path = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(path)) { EditorUtility.DisplayDialog(Title, "저장된 씬이 아님", "확인"); return; }
            Request(path, false);
        }

        [MenuItem(Menu + "빌드 상태 보기 (GitHub Actions)", priority = 40)]
        static void OpenActions()
        {
            var r = Run("git", "remote get-url origin");
            var m = Regex.Match(r.output, @"github\.com[:/](.+?/.+?)(\.git)?\s*$");
            if (m.Success) Application.OpenURL($"https://github.com/{m.Groups[1].Value}/actions/workflows/build.yml");
            else EditorUtility.DisplayDialog(Title, "GitHub 저장소 주소를 못 찾음", "확인");
        }

        [MenuItem(Menu + "기본 씬 빌드 (Development)", true)]
        [MenuItem(Menu + "기본 씬 빌드 (Release)", true)]
        [MenuItem(Menu + "현재 씬 빌드 (Development)", true)]
        static bool CanRequest() => running == null;

        static void Request(string scenes, bool release)
        {
            // 저장 안 된 씬은 git에도 없으니 빌드에 빠진다. 대신 저장하지 않는다
            bool unsaved = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                unsaved |= EditorSceneManager.GetSceneAt(i).isDirty;
            if (unsaved && !EditorUtility.DisplayDialog(Title, "저장 안 된 씬이 있음 — 빌드에 안 들어감.\n그래도 요청?", "요청", "취소"))
                return;

            Start(scenes, release, false);
        }

        static void Start(string scenes, bool release, bool yes)
        {
            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(ProjectRoot, "Tools/CI/trigger_build.ps1")}\"";
            if (!string.IsNullOrEmpty(scenes)) args += $" -Scenes \"{scenes}\"";
            if (release) args += " -Release";
            if (yes) args += " -Yes";

            Debug.Log($"[{Title}] 요청 중… {(string.IsNullOrEmpty(scenes) ? "기본 씬" : scenes)} / {(release ? "Release" : "Development")}");
            running = Task.Run(() => Run("powershell", args));
            EditorApplication.update += Poll;

            void Poll()
            {
                if (!running.IsCompleted) return;
                EditorApplication.update -= Poll;
                var (code, output) = running.Result;
                running = null;
                OnFinished(code, output, scenes, release);
            }
        }

        static void OnFinished(int code, string output, string scenes, bool release)
        {
            var text = Regex.Replace(output, @"^RUN_(ID|URL)=.*$\r?\n?", "", RegexOptions.Multiline).Trim();
            switch (code)
            {
                case 0:
                    Debug.Log($"[{Title}] {output.Trim()}");
                    var url = Regex.Match(output, @"^RUN_URL=(\S+)", RegexOptions.Multiline).Groups[1].Value;
                    if (EditorUtility.DisplayDialog(Title, text + "\n\n끝나면 공유 폴더에 vN으로 올라감 (2~5분)", "진행 상황 보기", "닫기") && url.Length > 0)
                        Application.OpenURL(url);
                    break;
                case 3: // 푸시 안 된 변경 경고
                    if (EditorUtility.DisplayDialog(Title, text + "\n\nGitHub에 올라간 커밋으로 빌드할까?", "그대로 빌드", "취소"))
                        Start(scenes, release, true);
                    break;
                default:
                    Debug.LogError($"[{Title}] {output.Trim()}");
                    EditorUtility.DisplayDialog(Title, text.Length > 0 ? text : $"실패 (종료 코드 {code})", "확인");
                    break;
            }
        }

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        static (int code, string output) Run(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    WorkingDirectory = ProjectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                using var p = Process.Start(psi);
                var stderr = p.StandardError.ReadToEndAsync();
                var stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return (p.ExitCode, stdout + stderr.Result);
            }
            catch (System.Exception e)
            {
                return (1, $"{file} 실행 실패: {e.Message}");
            }
        }
    }
}
