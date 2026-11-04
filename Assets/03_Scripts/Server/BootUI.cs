using UnityEngine;

namespace VaroGO.Net
{
    /// <summary>00_Boot 씬 임시 세션 UI(그레이 IMGUI). NetBootstrap과 같은 오브젝트에 둔다.</summary>
    [RequireComponent(typeof(NetBootstrap))]
    public class BootUI : MonoBehaviour
    {
        NetBootstrap bootstrap;
        string joinCode = string.Empty;

        void Awake() => bootstrap = GetComponent<NetBootstrap>();

        void OnGUI()
        {
            if (bootstrap.State == SessionState.InGame) return;

            const float w = 360f;
            var area = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.2f, w, 420f);
            GUILayout.BeginArea(area);

            GUILayout.Label("VAROGO");

            if (bootstrap.State == SessionState.Idle)
            {
                if (GUILayout.Button("방 만들기", GUILayout.Height(36))) bootstrap.CreateRoom();
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                joinCode = GUILayout.TextField(joinCode, SessionCode.Length, GUILayout.Height(30)).ToUpperInvariant();
                if (GUILayout.Button("참가", GUILayout.Width(80), GUILayout.Height(30))) bootstrap.JoinRoom(joinCode);
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                if (GUILayout.Button("Solo (혼자 테스트)", GUILayout.Height(30))) bootstrap.StartSolo();
            }
            else
            {
                if (!string.IsNullOrEmpty(bootstrap.SessionCode))
                {
                    var big = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
                    GUILayout.Label(bootstrap.SessionCode, big, GUILayout.Height(56));
                    if (GUILayout.Button("복사")) GUIUtility.systemCopyBuffer = bootstrap.SessionCode;
                }
                GUILayout.Space(8);
                if (GUILayout.Button("취소")) bootstrap.Leave();
            }

            GUILayout.Space(12);
            if (!string.IsNullOrEmpty(bootstrap.Status))
            {
                var prev = GUI.color;
                GUI.color = bootstrap.IsFailure ? new Color(1f, 0.5f, 0.5f) : Color.white;
                GUILayout.Label(bootstrap.Status);
                GUI.color = prev;
            }

            GUILayout.EndArea();
        }
    }
}
