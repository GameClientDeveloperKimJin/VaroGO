using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VaroGO.Net.Editor
{
    /// <summary>
    /// MPPM 클론(Player 2) 에디터에서만 Play 시작 씬을 TitleScene으로 고정한다.
    /// 메인이 Play 중일 때 클론을 다시 켜면 클론은 메인의 현재 씬(코스)을 연 채로 Play에 들어가 그 씬이 잠깐 보인다 →
    /// 시작 씬을 지정하면 처음부터 타이틀로 시작한다(실제 빌드와 같음). 메인 에디터는 건드리지 않는다.
    /// 에디터 시작 직후엔 MPPM이 준비 전이라 판별이 안 되므로 준비될 때까지 다음 틱에 다시 시도하고, Play 진입 직전에도 한 번 더 확인한다.
    /// </summary>
    [InitializeOnLoad]
    static class MppmCloneStartScene
    {
        const int MaxTries = 50;
        static int tries;

        static MppmCloneStartScene()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode) Apply();
            };
        }

        static void Apply()
        {
            if (!MppmCloneSceneGuard.TryIsMainEditor(out bool isMain))
            {
                if (++tries < MaxTries) EditorApplication.delayCall += Apply;
                return;
            }
            if (isMain) return;
            var title = AssetDatabase.LoadAssetAtPath<SceneAsset>(NetBootstrap.TitleScenePath);
            if (title == null || EditorSceneManager.playModeStartScene == title) return;
            EditorSceneManager.playModeStartScene = title;
            Debug.Log("[Net] MPPM 클론: Play 시작 씬 = TitleScene");
        }
    }
}
