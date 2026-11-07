using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VaroGO.Net.Lobby
{
    /// <summary>
    /// TitleScene "게임 시작" 버튼(BootMenu/MainPanel/StartButton)에 붙는 서버 연결 스크립트.
    /// 누르면 LobbyScene(방 리스트)으로 간다. 서버 연결·로딩 표시는 LobbyScene에서 한다 (로비 기획서 p4 ②).
    /// 씬을 불러오는 동안 같은 씬의 UI_Message 로딩 화면(정렬 100)을 먼저 덮는다.
    /// (클라 BootMenuUI가 같은 버튼으로 예전 시작 패널을 열기 때문에, 그대로 두면 로딩 동안 그 패널이 보인다 — S-1107-2)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TitleStartLink : MonoBehaviour
    {
        bool loading;

        void Awake() => GetComponent<Button>().onClick.AddListener(GoToLobby);

        void GoToLobby()
        {
            if (loading) return;
            int index = SceneUtility.GetBuildIndexByScenePath(NetBootstrap.LobbyScenePath);
            if (index < 0)
            {
                Debug.LogError($"[Lobby] 빌드 세팅에 로비 씬이 없습니다: {NetBootstrap.LobbyScenePath}");
                return;
            }
            loading = true;
            ShowLoadingCover();
            // 코루틴은 쓰지 않는다: BootMenuUI가 이 버튼의 부모 패널을 끄면 코루틴이 멈춘다.
            // 비동기 로드는 씬 전환 전까지 로딩 화면이 그려진다
            SceneManager.LoadSceneAsync(index);
        }

        void ShowLoadingCover()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "UI_Message") continue;
                var overlay = root.transform.Find("LoadingOverlay");
                if (overlay == null) break;
                root.SetActive(true);
                overlay.gameObject.SetActive(true);
                var text = overlay.Find("LoadingText");
                if (text != null && text.TryGetComponent<TMP_Text>(out var tmp)) tmp.text = "로비로 이동 중...";
                return;
            }
            Debug.LogWarning("[Lobby] TitleScene에 UI_Message/LoadingOverlay가 없어 로딩 화면 없이 이동합니다");
        }
    }
}
