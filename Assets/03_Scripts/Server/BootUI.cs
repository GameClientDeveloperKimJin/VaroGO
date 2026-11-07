using UnityEngine;

namespace VaroGO.Net
{
    /// <summary>
    /// 개발용 접속창(그레이 IMGUI). NetBootstrap과 같은 오브젝트에 둔다.
    /// 기본은 숨김. 에디터·개발 빌드에서 F11로 켜고 끈다(계약 §7.1). Solo 테스트·코드 접속 확인용.
    /// 정식 로비는 LobbyScene의 LobbyUI.
    /// </summary>
    [RequireComponent(typeof(NetBootstrap))]
    public class BootUI : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        NetBootstrap bootstrap;
        string joinCode = string.Empty;
        bool visible;

        void Awake() => bootstrap = GetComponent<NetBootstrap>();

        void OnGUI()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F11)
            {
                visible = !visible;
                e.Use();
            }
            if (!visible || bootstrap.State == SessionState.InGame) return;

            const float w = 360f;
            var area = new Rect(10f, Screen.height * 0.2f, w, 420f);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("VAROGO 개발용 접속 (F11)");

            if (bootstrap.State == SessionState.Idle || bootstrap.State == SessionState.Browsing)
            {
                if (GUILayout.Button("방 만들기 (공개)", GUILayout.Height(30))) bootstrap.CreateRoom();
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                joinCode = GUILayout.TextField(joinCode, SessionCode.Length, GUILayout.Height(28)).ToUpperInvariant();
                if (GUILayout.Button("참가", GUILayout.Width(80), GUILayout.Height(28))) bootstrap.JoinRoom(joinCode);
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
                if (GUILayout.Button("Solo (혼자 테스트)", GUILayout.Height(28))) bootstrap.StartSolo();
            }
            else
            {
                if (!string.IsNullOrEmpty(bootstrap.SessionCode))
                {
                    var big = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                    GUILayout.Label(bootstrap.SessionCode, big, GUILayout.Height(44));
                }
                GUILayout.Label($"상태 {bootstrap.State}  인원 {bootstrap.PlayerCount}  방장 {bootstrap.IsRoomHost}");
                if (bootstrap.State == SessionState.Lobby)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("레드")) bootstrap.RequestTeam(VaroGO.Shared.PlayerRole.A_Red);
                    if (GUILayout.Button("블루")) bootstrap.RequestTeam(VaroGO.Shared.PlayerRole.B_Blue);
                    if (GUILayout.Button(bootstrap.LocalReady ? "준비 취소" : "준비")) bootstrap.SetReady(!bootstrap.LocalReady);
                    GUILayout.EndHorizontal();
                    GUI.enabled = bootstrap.CanStartGame;
                    if (GUILayout.Button("시작")) bootstrap.StartGame();
                    GUI.enabled = true;
                }
                GUILayout.Space(6);
                if (GUILayout.Button("나가기")) bootstrap.Leave();
            }

            GUILayout.Space(8);
            if (!string.IsNullOrEmpty(bootstrap.Status))
            {
                var prev = GUI.color;
                GUI.color = bootstrap.IsFailure ? new Color(1f, 0.5f, 0.5f) : Color.white;
                GUILayout.Label(bootstrap.Status);
                GUI.color = prev;
            }

            GUILayout.EndArea();
        }
#endif
    }
}
