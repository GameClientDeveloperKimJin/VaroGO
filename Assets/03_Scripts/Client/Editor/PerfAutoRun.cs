using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VaroGO.Net;
using VaroGO.Shared;
using SessionState = VaroGO.Net.SessionState;

namespace VaroGO.Client.EditorTools
{
    // 성능 측정 자동 진행 (에디터 전용). 메뉴 VaroGO/Perf/Auto Run 이 켜져 있으면 Play 시 메인·MPPM 클론이 각각:
    //  메인: Title → Lobby → 비밀방 만들기 → 방 코드를 임시 파일에 씀 → 레드 팀 → 준비 → 둘 다 준비면 게임 시작 → Stage1 도착 5초 뒤 PerfTour
    //  클론: Title → Lobby → 임시 파일의 방 코드로 참가 → 블루 팀 → 준비
    // MCP 호출 없이 진행되므로 측정 중 에디터가 멈추지 않는다. 단계마다 PerfZoneLogger.Event로 시각을 남긴다.
    // VaroGO/Perf/Auto Run Join Only 를 켜면 메인 에디터도 클론처럼 참가만 한다 (빌드 측정: 방장 = Development Build의 PerfRuntime)
    [InitializeOnLoad]
    static class PerfAutoRun
    {
        const string PrefKey = "VaroGO.PerfAutoRun";
        const string MenuPath = "VaroGO/Perf/Auto Run";
        const string JoinOnlyKey = "VaroGO.PerfAutoRun.JoinOnly";
        const string JoinOnlyMenu = "VaroGO/Perf/Auto Run Join Only";
        static readonly string CodeFile = Path.Combine(Path.GetTempPath(), "varogo_perf_room.txt");

        enum Step { Title, WaitList, CreateOrJoin, InRoom, WaitCourse, Settle, Touring, Done }

        static Step step;
        static bool isMain;
        static double playStart, stepStart, lastTry;

        static bool Enabled { get => EditorPrefs.GetBool(PrefKey, false); set => EditorPrefs.SetBool(PrefKey, value); }
        static bool JoinOnly { get => EditorPrefs.GetBool(JoinOnlyKey, false); set => EditorPrefs.SetBool(JoinOnlyKey, value); }

        static PerfAutoRun()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode || !Enabled) return;
                isMain = MppmCloneSceneGuard.IsMainEditor() && !JoinOnly;
                playStart = EditorApplication.timeSinceStartup;
                if (isMain && File.Exists(CodeFile)) File.Delete(CodeFile);
                Go(Step.Title);
            };
        }

        [MenuItem(MenuPath)]
        static void Toggle() { Enabled = !Enabled; Debug.Log("[Perf] Auto Run " + (Enabled ? "ON" : "OFF")); }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }

        [MenuItem(JoinOnlyMenu)]
        static void ToggleJoinOnly() { JoinOnly = !JoinOnly; Debug.Log("[Perf] Auto Run Join Only " + (JoinOnly ? "ON" : "OFF")); }

        [MenuItem(JoinOnlyMenu, true)]
        static bool ToggleJoinOnlyValidate() { Menu.SetChecked(JoinOnlyMenu, JoinOnly); return true; }

        static void Go(Step s)
        {
            step = s;
            stepStart = EditorApplication.timeSinceStartup;
            lastTry = 0;
            PerfZoneLogger.Event("auto:" + s + (isMain ? " (메인)" : " (클론)"));
        }

        static bool Every(double sec)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastTry < sec) return false;
            lastTry = now;
            return true;
        }

        static void Tick()
        {
            if (!Enabled || !EditorApplication.isPlaying || EditorApplication.isPaused || step == Step.Done) return;
            var net = NetBootstrap.Instance;
            string scene = SceneManager.GetActiveScene().path;
            double inStep = EditorApplication.timeSinceStartup - stepStart;
            if (inStep > 120 && step != Step.Touring) { Debug.LogWarning("[Perf] 자동 진행 멈춤(120초 초과): " + step + " / net=" + (net != null ? net.State.ToString() : "null")); Go(Step.Done); return; }

            switch (step)
            {
                case Step.Title:
                    if (scene != NetBootstrap.TitleScenePath) { if (scene == NetBootstrap.LobbyScenePath) Go(Step.WaitList); return; }
                    if (inStep < 2) return; // 타이틀 첫 프레임들은 그대로 측정
                    if (!PlayerNickname.HasNickname) { if (Every(5)) Debug.LogWarning("[Perf] 닉네임이 없어 진행 불가 — 타이틀에서 닉네임을 정해 주세요"); return; }
                    SceneManager.LoadSceneAsync(SceneUtility.GetBuildIndexByScenePath(NetBootstrap.LobbyScenePath));
                    Go(Step.WaitList);
                    return;

                case Step.WaitList:
                    // 로비 씬에서 Play하면 NetBootstrap이 없어 타이틀로 되돌아간다 → 타이틀부터 다시
                    if (scene == NetBootstrap.TitleScenePath && (net == null || net.State == SessionState.Idle) && inStep > 1) { Go(Step.Title); return; }
                    if (net != null && net.State == SessionState.Browsing && net.ListReady) Go(Step.CreateOrJoin);
                    return;

                case Step.CreateOrJoin:
                    if (net == null) return;
                    if (net.State == SessionState.WaitingPartner || net.State == SessionState.Lobby)
                    {
                        if (isMain && !string.IsNullOrEmpty(net.SessionCode))
                            File.WriteAllText(CodeFile, net.SessionCode + "\n" + System.DateTime.Now.Ticks);
                        Go(Step.InRoom);
                        return;
                    }
                    if (net.State != SessionState.Browsing || !Every(3)) return;
                    if (isMain) { net.CreateRoom("PerfTest", true); return; }
                    string code = ReadFreshCode();
                    if (code != null) net.JoinRoom(code);
                    return;

                case Step.InRoom:
                    if (net == null) return;
                    if (net.State == SessionState.LoadingCourse || net.State == SessionState.InGame) { Go(Step.WaitCourse); return; }
                    if (!Every(2)) return;
                    net.RequestTeam(isMain ? PlayerRole.A_Red : PlayerRole.B_Blue);
                    if (net.State == SessionState.Lobby) net.SetReady(true);
                    if (isMain && net.CanStartGame) net.StartGame();
                    return;

                case Step.WaitCourse:
                    if (net != null && net.State == SessionState.InGame && scene.Contains("PlayScene_") && LocalPlayer() != null) Go(isMain ? Step.Settle : Step.Done);
                    return;

                case Step.Settle:
                    if (inStep < 5) return; // 도착 직후 스폰 지점 모습도 기록
                    PerfZoneLogger.Event("tour:" + PerfTour.Begin());
                    Go(Step.Touring);
                    return;

                case Step.Touring:
                    if (!PerfTour.Running) { PerfZoneLogger.Event("측정 끝 (Play 후 " + (EditorApplication.timeSinceStartup - playStart).ToString("F0") + "초)"); Go(Step.Done); }
                    return;
            }
        }

        // 메인이 이번 Play 이후에 쓴 방 코드만 쓴다 (이전 측정의 남은 파일 무시)
        static string ReadFreshCode()
        {
            if (!File.Exists(CodeFile)) return null;
            var lines = File.ReadAllLines(CodeFile);
            if (lines.Length < 2) return null;
            long ticks;
            if (!long.TryParse(lines[1], out ticks)) return null;
            var written = new System.DateTime(ticks);
            double sincePlay = EditorApplication.timeSinceStartup - playStart;
            if (written < System.DateTime.Now.AddSeconds(-sincePlay - 1)) return null;
            return lines[0].Trim();
        }

        static PlayerController LocalPlayer()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++) if (all[i].IsLocal) return all[i];
            return null;
        }
    }
}
