using Fusion;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 서버 임시 세션 UI(그레이 IMGUI, S2-11g). NetBootstrap과 같은 오브젝트에 둔다(DontDestroyOnLoad라 01_Lobby에서도 그린다).
    /// 대기실(00_Boot): 방 목록·방 만들기·Solo / 로비(01_Lobby): Red·Blue 선택, 준비, 나가기.
    /// 정식 UI는 계약(P20) 추가 후 클라가 uGUI로 만든다.
    /// </summary>
    [RequireComponent(typeof(NetBootstrap))]
    public class BootUI : MonoBehaviour
    {
        const float Width = 480f;

        NetBootstrap bootstrap;
        string roomName = string.Empty;
        Vector2 listScroll;
        GUIStyle titleStyle, bigStyle, centerStyle;

        void Awake() => bootstrap = GetComponent<NetBootstrap>();

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            centerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            var state = bootstrap.State;
            if (state == SessionState.InGame) return;
            EnsureStyles();

            // 파트너 끊김 중에는 클라 대기 화면(C2-03)을 가리지 않게 상단에 작게만
            if (state == SessionState.PartnerLost)
            {
                GUILayout.BeginArea(new Rect((Screen.width - 320f) * 0.5f, 10f, 320f, 60f), GUI.skin.box);
                GUILayout.Label($"방 {bootstrap.SessionCode} · 파트너 재접속 대기 {bootstrap.PartnerLostSeconds:0}/{NetSession.ReconnectTimeoutSeconds:0}초", centerStyle);
                if (GUILayout.Button("나가기")) bootstrap.Leave();
                GUILayout.EndArea();
                return;
            }

            var area = new Rect((Screen.width - Width) * 0.5f, Screen.height * 0.12f, Width, Screen.height * 0.8f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("VAROGO", titleStyle, GUILayout.Height(36));

            switch (state)
            {
                case SessionState.Idle:
                case SessionState.RoomList:
                    DrawRoomList(state == SessionState.RoomList);
                    break;
                case SessionState.WaitingPartner:
                case SessionState.Lobby:
                    DrawLobby();
                    break;
                default: // Connecting, LoadingCourse
                    GUILayout.Space(16);
                    GUILayout.Label(string.IsNullOrEmpty(bootstrap.SessionCode) ? "" : $"방 {bootstrap.SessionCode}", bigStyle);
                    GUILayout.Space(8);
                    if (state == SessionState.Connecting && GUILayout.Button("취소", GUILayout.Height(28))) bootstrap.Leave();
                    break;
            }

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(bootstrap.Status))
            {
                var prev = GUI.color;
                GUI.color = bootstrap.IsFailure ? new Color(1f, 0.5f, 0.5f) : Color.white;
                GUILayout.Label(bootstrap.Status, centerStyle);
                GUI.color = prev;
            }
            GUILayout.EndArea();
        }

        // ---- 대기실 ----

        void DrawRoomList(bool listReady)
        {
            GUILayout.Label("대기실", bigStyle);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("번호", GUILayout.Width(60));
            GUILayout.Label("이름", GUILayout.Width(200));
            GUILayout.Label("인원", GUILayout.Width(80));
            GUILayout.EndHorizontal();

            // 입장을 누르면 목록이 비워지므로(대기실 러너 종료) 그리는 동안에는 기억만 하고 루프가 끝난 뒤 부른다
            string joinNumber = null;
            listScroll = GUILayout.BeginScrollView(listScroll, GUI.skin.box, GUILayout.Height(240));
            if (!listReady)
            {
                GUILayout.Label("대기실 연결 중...", centerStyle);
            }
            else if (bootstrap.Rooms.Count == 0)
            {
                GUILayout.Label("열린 방이 없습니다", centerStyle);
            }
            else
            {
                foreach (var room in bootstrap.Rooms)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(room.Number, GUILayout.Width(60));
                    GUILayout.Label(room.Name, GUILayout.Width(200));
                    GUILayout.Label($"{room.PlayerCount}/{NetBootstrap.MaxPlayers}{(room.InGame ? " · 게임중" : "")}", GUILayout.Width(80));
                    // S2-11 §2.1: 입장 / 가득 참 / 재접속 / 게임중
                    string label = room.InGame ? (room.IsFull ? "게임중" : "재접속") : (room.IsFull ? "가득 참" : "입장");
                    bool prev = GUI.enabled;
                    GUI.enabled = room.CanJoin;
                    if (GUILayout.Button(label, GUILayout.Width(90))) joinNumber = room.Number;
                    GUI.enabled = prev;
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            if (joinNumber != null) bootstrap.JoinRoom(joinNumber);

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label("방 이름", GUILayout.Width(60));
            roomName = GUILayout.TextField(roomName, NetBootstrap.MaxRoomNameLength, GUILayout.Height(28));
            bool prevEnabled = GUI.enabled;
            GUI.enabled = listReady;
            if (GUILayout.Button("방 만들기", GUILayout.Width(100), GUILayout.Height(28))) bootstrap.CreateRoom(roomName);
            GUI.enabled = prevEnabled;
            GUILayout.EndHorizontal();
            GUILayout.Label("이름을 비우면 방 번호가 이름이 됩니다", centerStyle);

            GUILayout.Space(8);
            if (GUILayout.Button("Solo (혼자 테스트)", GUILayout.Height(28))) bootstrap.StartSolo();
        }

        // ---- 로비 (01_Lobby) ----

        void DrawLobby()
        {
            GUILayout.Label($"방 {bootstrap.SessionCode} · {bootstrap.RoomName}", bigStyle);
            GUILayout.Label(bootstrap.IsHost ? "(방장)" : "", centerStyle);
            GUILayout.Space(8);

            var session = NetSession.Instance;
            if (session == null)
            {
                GUILayout.Label("방 정보 받는 중...", centerStyle);
            }
            else
            {
                GUILayout.BeginHorizontal();
                DrawSlot(session, PlayerRole.A_Red, "Red", new Color(1f, 0.55f, 0.55f));
                DrawSlot(session, PlayerRole.B_Blue, "Blue", new Color(0.55f, 0.7f, 1f));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12);
            var mine = bootstrap.LocalSlot;
            bool ready = bootstrap.LocalReady;
            bool prev = GUI.enabled;
            GUI.enabled = mine != PlayerRole.None;
            if (GUILayout.Button(ready ? "준비 취소" : "준비", GUILayout.Height(40))) bootstrap.SetReady(!ready);
            GUI.enabled = prev;

            string partner = !bootstrap.PartnerConnected ? "파트너 대기 중..."
                : bootstrap.PartnerRole == PlayerRole.None ? "파트너: 역할 고르는 중"
                : bootstrap.PartnerReady ? "파트너: 준비 완료" : "파트너: 준비 안 됨";
            GUILayout.Label(partner, centerStyle);
            GUILayout.Label("두 사람이 모두 준비하면 바로 시작합니다", centerStyle);

            GUILayout.Space(8);
            if (GUILayout.Button("나가기", GUILayout.Height(28))) bootstrap.Leave();
        }

        void DrawSlot(NetSession session, PlayerRole slot, string title, Color color)
        {
            var owner = session.OwnerOf(slot);
            var me = bootstrap.Runner != null ? bootstrap.Runner.LocalPlayer : default;
            bool mine = owner != PlayerRef.None && owner == me;
            bool partners = owner != PlayerRef.None && !mine;

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Width * 0.5f - 12f));
            var prevColor = GUI.color;
            GUI.color = color;
            GUILayout.Label(title, bigStyle);
            GUI.color = prevColor;

            string who = mine ? "나" : partners ? "파트너" : "비어 있음";
            string readyText = owner == PlayerRef.None ? "" : session.IsReady(slot) ? " · 준비" : "";
            GUILayout.Label(who + readyText, centerStyle);

            bool prev = GUI.enabled;
            GUI.enabled = !partners; // 상대 칸은 비활성
            if (mine)
            {
                if (GUILayout.Button("선택 취소", GUILayout.Height(28))) bootstrap.SelectRole(PlayerRole.None);
            }
            else if (GUILayout.Button("선택", GUILayout.Height(28))) bootstrap.SelectRole(slot);
            GUI.enabled = prev;
            GUILayout.EndVertical();
        }
    }
}
