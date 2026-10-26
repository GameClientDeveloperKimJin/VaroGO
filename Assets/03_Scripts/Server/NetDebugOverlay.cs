using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VaroGO.Client;
using VaroGO.Shared;

namespace VaroGO.Net
{
    /// <summary>
    /// 네트워크 디버그 오버레이(S-13, F10). NetBootstrap과 같은 오브젝트에 둔다.
    /// 켜져 있는 동안 1초마다 [Clock] 로그를 남긴다. 같은 PC(MPPM)면 utc가 같으므로
    /// 두 로그의 (now - utc) 차이가 두 PC 시계 오차다 (S-07 검증).
    /// ` 키: 명령 입력창 (move hammer, move z3, list). 로컬 플레이어만 옮긴다.
    /// </summary>
    [RequireComponent(typeof(NetBootstrap))]
    public class NetDebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const float LogInterval = 1f;
        const string CommandControl = "NetDebugCommand";
        const float LandingDistance = 3f;   // 대상에서 이만큼 떨어진 땅에 내린다 (해머에 바로 맞지 않게)
        const float RayHeight = 30f;

        // 자주 쓰는 이름. 그 밖의 단어는 오브젝트 이름 부분 일치로 찾는다
        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "hammer", "Hammer_Common" },
            { "hammerb", "Hammer_Blue_OnlyB" },
            { "z1", "Z1_" }, { "z2", "Z2_" }, { "z3", "Z3_" }, { "z4", "Z4_" }, { "z5", "Z5_" },
        };

        NetBootstrap bootstrap;
        bool visible;
        bool commandOpen;
        bool focusPending;
        string command = string.Empty;
        string commandResult = string.Empty;
        float nextLogTime;
        GUIStyle style;

        void Awake() => bootstrap = GetComponent<NetBootstrap>();

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f10Key.wasPressedThisFrame) visible = !visible;
                if (keyboard.backquoteKey.wasPressedThisFrame && !commandOpen)
                {
                    commandOpen = true;
                    focusPending = true;
                    command = string.Empty;
                }
            }

            if (!visible || Time.unscaledTime < nextLogTime) return;
            nextLogTime = Time.unscaledTime + LogInterval;

            var runner = bootstrap.Runner;
            if (runner == null || !runner.IsRunning || GameServices.Clock == null) return;
            long utcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Debug.Log($"[Clock] utc={utcMs} now={GameServices.Clock.Now:F4} sim={runner.SimulationTime:F4} " +
                      $"local={runner.LocalRenderTime:F4} tick={runner.Tick.Raw} master={runner.IsSharedModeMasterClient}");
        }

        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
            if (visible) DrawStats();
            if (commandOpen) DrawCommand();
        }

        void DrawStats()
        {
            var runner = bootstrap.Runner;
            string text;
            if (runner == null || !runner.IsRunning)
            {
                text = "NET: 오프라인";
            }
            else
            {
                double rttMs = runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0;
                double now = GameServices.Clock != null ? GameServices.Clock.Now : 0.0;
                string role = GameServices.Role != null ? GameServices.Role.LocalRole.ToString() : "-";
                text = $"세션 {bootstrap.SessionCode}   {runner.GameMode}\n" +
                       $"역할 {role}   Master {runner.IsSharedModeMasterClient}\n" +
                       $"인원 {CountPlayers(runner)}   RTT {rttMs:F0} ms\n" +
                       $"Tick {runner.Tick.Raw}  ({runner.TickRate} Hz)\n" +
                       $"Clock.Now {now:F3}\n" +
                       $"Tick*dt   {runner.Tick.Raw * runner.DeltaTime:F3}";
            }

            const float w = 220f, h = 110f;
            GUI.Box(new Rect(Screen.width - w - 10f, 10f, w, h), text, style);
        }

        // 키보드만으로 쓴다(카메라가 커서를 잠가도 동작). Enter 실행, Esc 닫기
        void DrawCommand()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    commandResult = Execute(command.Trim());
                    Debug.Log($"[Cmd] {command} → {commandResult}");
                    CloseCommand();
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.Escape)
                {
                    CloseCommand();
                    e.Use();
                    return;
                }
            }

            const float w = 360f;
            var rect = new Rect(10f, Screen.height - 60f, w, 22f);
            GUI.Box(new Rect(rect.x, rect.y - 22f, w, 20f), "명령: move hammer | move z1~z5 | move <이름> | list", style);
            GUI.SetNextControlName(CommandControl);
            command = GUI.TextField(rect, command).Replace("`", string.Empty);
            if (focusPending)
            {
                GUI.FocusControl(CommandControl);
                focusPending = false;
            }
        }

        void CloseCommand()
        {
            commandOpen = false;
            command = string.Empty;
            GUI.FocusControl(null);
        }

        string Execute(string input)
        {
            if (input.Length == 0) return "빈 명령";
            var parts = input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string verb = parts[0].ToLowerInvariant();

            if (verb == "list") return ListTargets();
            if (verb != "move" || parts.Length < 2) return "알 수 없는 명령: " + input;

            string key = parts[1].Trim();
            string search = Aliases.TryGetValue(key, out var alias) ? alias : key;
            var target = FindByName(search);
            if (target == null) return $"'{search}' 오브젝트 없음 (list로 확인)";

            var player = FindLocalPlayer();
            if (player == null) return "로컬 플레이어 없음";

            Vector3 landing = FindLanding(target);
            Vector3 look = target.position - landing;
            look.y = 0f;
            var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : player.transform.rotation;
            player.Teleport(landing, rot);
            return $"{target.name} 근처로 이동";
        }

        static string ListTargets()
        {
            var names = new List<string>();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("Z") || t.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) >= 0)
                        names.Add(t.name);
            string text = names.Count == 0 ? "없음" : string.Join(", ", names);
            Debug.Log("[Cmd] 대상 목록: " + text);
            return $"{names.Count}개 (콘솔에 목록 출력)";
        }

        // 정확히 같은 이름 → 앞부분 일치 → 부분 일치 순. 활성 오브젝트만
        static Transform FindByName(string search)
        {
            Transform starts = null, contains = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>())
                {
                    if (string.Equals(t.name, search, StringComparison.OrdinalIgnoreCase)) return t;
                    if (starts == null && t.name.StartsWith(search, StringComparison.OrdinalIgnoreCase)) starts = t;
                    if (contains == null && t.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) contains = t;
                }
            }
            return starts != null ? starts : contains;
        }

        static PlayerController FindLocalPlayer()
        {
            if (NetPlayer.Local != null) return NetPlayer.Local.GetComponent<PlayerController>();
            foreach (var p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (p.IsLocal) return p;
            return null;
        }

        // 대상 둘레 8방향으로 땅을 찾아 처음 맞는 곳에 내린다. 대상 자신과 트리거는 무시
        static Vector3 FindLanding(Transform target)
        {
            Vector3 center = target.position;
            for (int i = 0; i < 8; i++)
            {
                var dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.back;
                var origin = center + dir * LandingDistance + Vector3.up * RayHeight;
                var hits = Physics.RaycastAll(origin, Vector3.down, RayHeight * 2f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.transform.IsChildOf(target) || hit.collider.GetComponentInParent<PlayerController>() != null) continue;
                    if (hit.normal.y < 0.7f) continue;
                    return hit.point + Vector3.up * 0.1f;
                }
            }
            return center + Vector3.up * 2f;
        }

        static int CountPlayers(Fusion.NetworkRunner runner)
        {
            int n = 0;
            foreach (var _ in runner.ActivePlayers) n++;
            return n;
        }
#endif
    }
}
