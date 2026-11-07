using TMPro;
using UnityEngine;

namespace VaroGO.Net.Lobby
{
    /// <summary>클라가 만든 UI를 이름(경로)으로 찾는 도우미. 이름이 바뀌면 에러 로그로 바로 알린다.</summary>
    static class LobbyUiUtil
    {
        public static T Find<T>(Transform root, string path) where T : Component
        {
            if (root == null) return null;
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogError($"[Lobby] UI를 찾지 못했습니다: {root.name}/{path} (클라 UI 이름이 바뀌었는지 확인)");
                return null;
            }
            var c = t.GetComponent<T>();
            if (c == null) Debug.LogError($"[Lobby] {root.name}/{path}에 {typeof(T).Name}이 없습니다");
            return c;
        }

        public static GameObject FindGo(Transform root, string path)
        {
            var t = Find<Transform>(root, path);
            return t != null ? t.gameObject : null;
        }

        /// <summary>버튼 아래 첫 글자(TMP).</summary>
        public static TMP_Text Label(Component button) => button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;

        public static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }

        public static void SetActive(GameObject go, bool value)
        {
            if (go != null && go.activeSelf != value) go.SetActive(value);
        }

        public static string TeamName(VaroGO.Shared.PlayerRole team) =>
            team == VaroGO.Shared.PlayerRole.A_Red ? "레드" : team == VaroGO.Shared.PlayerRole.B_Blue ? "블루" : "팀 미선택";
    }
}
