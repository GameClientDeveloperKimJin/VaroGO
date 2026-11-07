#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VaroGO.Net
{
    /// <summary>
    /// 에디터 전용. MPPM 클론(Player 2)을 메인이 Play 중일 때 다시 켜면 클론은 메인의 현재 씬(코스)으로 바로 시작해
    /// NetBootstrap 없이 오프라인으로 돈다 → 게임 중 재접속을 테스트할 수 없다.
    /// 클론이 메뉴 씬이 아닌 곳에서 NetBootstrap 없이 시작하면 TitleScene으로 보낸다(실제 빌드처럼 타이틀부터).
    /// 메인 에디터는 건드리지 않는다(코스 씬 바로 실행 = 오프라인 테스트 그대로).
    /// 보통은 Editor/MppmCloneStartScene이 클론의 Play 시작 씬을 TitleScene으로 지정해 여기까지 오지 않는다(예비용).
    /// </summary>
    public static class MppmCloneSceneGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnPlay()
        {
            if (IsMainEditor()) return;
            if (NetBootstrap.Instance != null) return;
            string path = SceneManager.GetActiveScene().path;
            if (path == NetBootstrap.TitleScenePath || path == NetBootstrap.LobbyScenePath) return;
            if (SceneUtility.GetBuildIndexByScenePath(NetBootstrap.TitleScenePath) < 0) return;
            Debug.LogWarning($"[Net] MPPM 클론이 '{path}'에서 바로 시작됨 → TitleScene으로 이동 (온라인 테스트·재접속용)");
            // 씬 교체는 프레임 끝에 일어나 코스가 한두 프레임 보인다 → 코스 오브젝트를 먼저 꺼 둔다(곧 내려가는 씬, 에디터 클론 전용)
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
            SceneManager.LoadScene(NetBootstrap.TitleScenePath);
        }

        // CurrentPlayer.IsMainEditor (MPPM). Unity 6.3 내장 = Unity.Multiplayer.PlayMode, 패키지 = Unity.Multiplayer.Playmode.
        // 찾지 못하면 메인으로 본다(아무것도 안 함)
        static readonly string[] TypeNames = { "Unity.Multiplayer.PlayMode.CurrentPlayer", "Unity.Multiplayer.Playmode.CurrentPlayer" };

        public static bool IsMainEditor() => !TryIsMainEditor(out bool main) || main;

        /// <summary>MPPM이 아직 준비 안 됐으면(에디터 시작 직후) false. 준비됐으면 true + 결과.</summary>
        public static bool TryIsMainEditor(out bool isMain)
        {
            isMain = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var name in TypeNames)
                {
                    Type type;
                    try { type = asm.GetType(name, false); }
                    catch { continue; }
                    if (type == null) continue;
                    var prop = type.GetProperty("IsMainEditor", BindingFlags.Public | BindingFlags.Static);
                    if (prop == null) continue;
                    try
                    {
                        if (prop.GetValue(null) is bool main) { isMain = main; return true; }
                    }
                    catch { return false; } // "CurrentPlayerApi type for editor must be set before use"
                }
            }
            return true; // MPPM 없음 = 메인
        }
    }
}
#endif
