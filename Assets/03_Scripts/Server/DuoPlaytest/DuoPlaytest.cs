#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HiddenPath.Stage2Z2;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 한 에디터 2인 테스트 (에디터 전용, 오프라인 전용).
    // Play 중 DuoPlaytest.Begin()을 부르면 씬의 플레이어를 복제해 레드·블루 두 몸을 만들고, 둘 다 이 PC의 로컬로 판정받게 한다
    // (PlayerController.DebugRoleOverride). 두 몸 모두 봇이 입력을 넣는다 (PlayerInputReader.DebugDriven).
    // 명령은 DuoPlaytest.Run("red", "goto 0 20; jump; wait 1") 처럼 넣는다. 로그는 [Duo]로 시작한다.
    // 씬에는 아무것도 저장하지 않는다. Play를 멈추면 사라진다.
    public class DuoPlaytest : MonoBehaviour
    {
        public static DuoPlaytest Instance { get; private set; }

        public Bot Red { get; private set; }
        public Bot Blue { get; private set; }
        float startedAt;
        readonly List<string> events = new List<string>();
        string csvPath;

        // ---- 시작 ----
        public static string Begin(bool keyboardRed = false, string csv = null)
        {
            if (!Application.isPlaying) return "Play 중이 아님";
            if (GameServices.IsOnline) return "온라인에서는 쓰지 않음";
            if (Instance != null) return "이미 시작됨";
            PlayerController src = null;
            foreach (var p in PlayerController.All) if (p != null && p.IsLocal) { src = p; break; }
            if (src == null) return "로컬 플레이어 없음";

            var go = new GameObject("DuoPlaytest (runtime)");
            Instance = go.AddComponent<DuoPlaytest>();
            Instance.csvPath = csv;
            Instance.Setup(src, keyboardRed);
            return "OK";
        }

        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        void Setup(PlayerController red, bool keyboardRed)
        {
            startedAt = Time.time;
            var pos = red.transform.position + red.transform.right * 1.5f;
            var copy = Instantiate(red.gameObject, pos, red.transform.rotation);
            copy.name = "Player_Stage2_Blue (Duo)";
            var blue = copy.GetComponent<PlayerController>();

            red.DebugRoleOverride = PlayerRole.A_Red;
            blue.DebugRoleOverride = PlayerRole.B_Blue;
            Red = new Bot(this, red, "red", !keyboardRed);
            Blue = new Bot(this, blue, "blue", true);

            // 온라인 규칙: 2단 박스는 두 명, 체크포인트는 두 명 모두 밟아야 저장
            var solo = typeof(Pushable).GetField("offlineSoloCounts", Any);
            foreach (var p in FindObjectsByType<Pushable>(FindObjectsSortMode.None)) solo?.SetValue(p, false);
            var cp = GameServices.Checkpoints;
            var both = cp != null ? cp.GetType().GetField("requireBoth", Any) : null;
            if (both != null) both.SetValue(cp, true);
            Log($"시작: 레드={Fmt(red.transform.position)} 블루={Fmt(pos)} 체크포인트 둘다필요={(both != null)}");
        }

        void LateUpdate()
        {
            // 복제본의 Start(SetLocal(true))가 카메라를 켠다 → 블루 카메라·오디오는 끈다
            if (Blue != null && !Blue.cameraOff)
            {
                var rigField = typeof(PlayerController).GetField("cameraRig", Any);
                var rig = rigField != null ? rigField.GetValue(Blue.player) as GameObject : null;
                if (rig != null && Blue.player.IsLocal) { rig.SetActive(false); Blue.cameraOff = true; }
                foreach (var l in Blue.player.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
            }
        }

        void Update()
        {
            Red?.Tick(Time.deltaTime);
            Blue?.Tick(Time.deltaTime);
            Watch();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (!string.IsNullOrEmpty(csvPath))
            {
                try { System.IO.File.WriteAllLines(csvPath, events); } catch (Exception e) { Debug.LogWarning("[Duo] CSV 저장 실패 " + e.Message); }
            }
        }

        // ---- 명령 ----
        public static string Run(string who, string script)
        {
            if (Instance == null) return "시작 안 됨";
            var b = who == "blue" ? Instance.Blue : Instance.Red;
            b.Enqueue(script);
            return $"{who} 대기 명령 {b.QueueCount}";
        }

        public static void Stop(string who = null)
        {
            if (Instance == null) return;
            if (who != "blue") Instance.Red.Clear();
            if (who != "red") Instance.Blue.Clear();
        }

        public static bool Idle => Instance == null || (Instance.Red.IsIdle && Instance.Blue.IsIdle);

        public float Elapsed => Time.time - startedAt;

        public void Log(string msg)
        {
            string line = $"{Elapsed:F2},{msg}";
            events.Add(line);
            Debug.Log($"[Duo] t={Elapsed:F2} {msg}");
        }

        public static string Fmt(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2},{2:F2})", v.x, v.y, v.z);

        // ---- 상태 감시 (기록기) ----
        int lastCp = -2, lastGlassRound = -1, lastZ2Rev = -1, lastZ2R = -1, lastZ2B = -1, lastRope = -1;
        GlassBridge glass;
        Z2Sequence z2;
        RopeRescue rope;
        public int GlassFails { get; private set; }
        public int Z2Resets { get; private set; }

        void Watch()
        {
            var cp = GameServices.Checkpoints;
            if (cp != null && cp.ActiveOrder != lastCp) { lastCp = cp.ActiveOrder; Log($"체크포인트 활성 order={lastCp}"); }

            if (glass == null) glass = FindAnyObjectByType<GlassBridge>();
            if (glass != null)
            {
                int round = GlassRound(glass);
                if (round != lastGlassRound) { if (lastGlassRound > 0) GlassFails++; lastGlassRound = round; Log($"Z1 라운드={round}"); }
            }
            if (z2 == null) z2 = FindAnyObjectByType<Z2Sequence>();
            if (z2 != null)
            {
                if (z2.Revision != lastZ2Rev)
                {
                    bool reset = z2.RedIndex == 0 && z2.BlueIndex == 0 && z2.RedUsed == 0 && z2.BlueUsed == 0 && lastZ2Rev >= 0 && (lastZ2R > 0 || lastZ2B > 0);
                    if (reset) Z2Resets++;
                    lastZ2Rev = z2.Revision; lastZ2R = z2.RedIndex; lastZ2B = z2.BlueIndex;
                    Log($"Z2 {(reset ? "리셋" : "단계")} 빨강={z2.RedIndex} 파랑={z2.BlueIndex} 사용R={Convert.ToString(z2.RedUsed, 2)} 사용B={Convert.ToString(z2.BlueUsed, 2)}");
                }
            }
            if (rope == null) rope = FindAnyObjectByType<RopeRescue>();
            if (rope != null)
            {
                int s = RopeStage(rope);
                if (s != lastRope) { lastRope = s; Log($"Z3 밧줄 단계={s}"); }
            }
        }

        public static int GlassRound(GlassBridge g) => (int)typeof(GlassBridge).GetField("localRound", Any).GetValue(g);
        public static long GlassLayout(GlassBridge g) => (long)typeof(GlassBridge).GetField("localLayout", Any).GetValue(g);
        public static int GlassBroken(GlassBridge g) => (int)typeof(GlassBridge).GetField("localBroken", Any).GetValue(g);
        public static int RopeStage(RopeRescue r) => (int)typeof(RopeRescue).GetField("localStage", Any).GetValue(r);

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append($"경과={Elapsed:F1}s 체크포인트={lastCp} Z1실패={GlassFails} Z2리셋={Z2Resets} 밧줄={lastRope} ");
            sb.Append($"레드 죽음={Red.Deaths} 블루 죽음={Blue.Deaths} 레드={Fmt(Red.player.transform.position)} 블루={Fmt(Blue.player.transform.position)}");
            return sb.ToString();
        }

        // ================= 봇 =================
        public class Bot
        {
            public readonly PlayerController player;
            public readonly string name;
            readonly DuoPlaytest owner;
            readonly PlayerInputReader reader;
            readonly bool driven;
            public bool cameraOff;
            readonly Queue<string> queue = new Queue<string>();
            string[] cmd;
            float cmdTime;
            float cmdTimeout;
            bool wasDead;
            bool jumpedThisCmd;
            bool proneOn;
            public int Deaths { get; private set; }

            public int QueueCount => queue.Count + (cmd != null ? 1 : 0);
            public bool IsIdle => cmd == null && queue.Count == 0;

            public Bot(DuoPlaytest owner, PlayerController p, string name, bool driven)
            {
                this.owner = owner;
                player = p;
                this.name = name;
                this.driven = driven;
                reader = p.GetComponent<PlayerInputReader>();
                if (reader != null) reader.DebugDriven = driven;
            }

            public void Enqueue(string script)
            {
                foreach (var part in script.Split(';'))
                {
                    var s = part.Trim();
                    if (s.Length > 0) queue.Enqueue(s);
                }
            }

            public void Clear()
            {
                queue.Clear();
                cmd = null;
                Send(Vector2.zero);
            }

            void Send(Vector2 move, bool jump = false, bool interact = false, bool held = false)
            {
                if (!driven || reader == null) return;
                reader.SetDebugInput(move, jump, interact, held, false, false);
                reader.SetDebugProne(proneOn);
            }

            static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

            Vector2 InputToward(Vector3 worldDir)
            {
                var field = typeof(PlayerController).GetField("cameraTransform", Any);
                var cam = field != null ? field.GetValue(player) as Transform : null;
                if (cam == null && Camera.main != null) cam = Camera.main.transform;
                Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.ProjectOnPlane(cam.up, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                worldDir.y = 0f;
                if (worldDir.sqrMagnitude < 0.0001f) return Vector2.zero;
                worldDir.Normalize();
                return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, fwd));
            }

            float FlatDist(float x, float z)
            {
                var p = player.transform.position;
                return new Vector2(p.x - x, p.z - z).magnitude;
            }

            public void Tick(float dt)
            {
                if (player == null) return;
                bool dead = player.IsDead;
                if (dead && !wasDead) { Deaths++; owner.Log($"{name} 사망 위치={Fmt(player.transform.position)}"); }
                wasDead = dead;

                if (cmd == null)
                {
                    if (queue.Count == 0) { Send(Vector2.zero); return; }
                    var line = queue.Dequeue();
                    cmd = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    cmdTime = 0f;
                    cmdTimeout = 25f;
                    jumpedThisCmd = false;
                    owner.Log($"{name} ▶ {line}");
                }
                cmdTime += dt;
                bool done;
                try { done = Step(); }
                catch (Exception e) { owner.Log($"{name} 명령 오류 {string.Join(" ", cmd)}: {e.Message}"); done = true; }
                if (!done && cmdTime > cmdTimeout)
                {
                    owner.Log($"{name} ✖ 시간 초과 {string.Join(" ", cmd)} 위치={Fmt(player.transform.position)}");
                    done = true;
                }
                if (done) { cmd = null; Send(Vector2.zero); }
            }

            // true = 이 명령 끝
            bool Step()
            {
                var p = player.transform.position;
                switch (cmd[0])
                {
                    case "goto": // goto x z [허용거리]
                    {
                        float x = F(cmd[1]), z = F(cmd[2]);
                        float tol = cmd.Length > 3 ? F(cmd[3]) : 0.35f;
                        if (FlatDist(x, z) <= tol) { owner.Log($"{name} ✔ 도착 {Fmt(p)} {cmdTime:F2}s"); return true; }
                        Send(InputToward(new Vector3(x - p.x, 0f, z - p.z)));
                        return false;
                    }
                    case "jumpto": // jumpto x z [허용거리] : 달리며 바로 점프, 착지 후 도착까지 걷기
                    {
                        float x = F(cmd[1]), z = F(cmd[2]);
                        float tol = cmd.Length > 3 ? F(cmd[3]) : 0.4f;
                        bool jump = !jumpedThisCmd && player.IsGrounded;
                        if (jump) jumpedThisCmd = true;
                        if (jumpedThisCmd && cmdTime > 0.3f && player.IsGrounded && FlatDist(x, z) <= tol)
                        { owner.Log($"{name} ✔ 착지 {Fmt(p)} {cmdTime:F2}s"); return true; }
                        Send(InputToward(new Vector3(x - p.x, 0f, z - p.z)), jump);
                        return false;
                    }
                    case "run": // run dx dz 초 [점프시각] : 방향으로 계속 입력 (점프시각이 있으면 그때 점프)
                    {
                        var dir = new Vector3(F(cmd[1]), 0f, F(cmd[2]));
                        float secs = F(cmd[3]);
                        bool jump = cmd.Length > 4 && !jumpedThisCmd && cmdTime >= F(cmd[4]);
                        if (jump) jumpedThisCmd = true;
                        Send(InputToward(dir), jump);
                        return cmdTime >= secs;
                    }
                    case "jump":
                        Send(Vector2.zero, true);
                        return true;
                    case "prone": // prone on|off : 엎드리기 키를 누르고 있음/뗌
                        proneOn = cmd[1] == "on";
                        Send(Vector2.zero);
                        return true;
                    case "e":
                        Send(Vector2.zero, false, true, true);
                        return true;
                    case "face": // face x z : 그 지점을 바로 보게 돌린다 (밀기 잡기 전)
                    {
                        var d = new Vector3(F(cmd[1]) - p.x, 0f, F(cmd[2]) - p.z);
                        if (d.sqrMagnitude > 0.0001f) player.transform.rotation = Quaternion.LookRotation(d.normalized);
                        return true;
                    }
                    case "tp": // tp x y z (준비용 순간이동, 측정에는 쓰지 않음)
                        player.Teleport(new Vector3(F(cmd[1]), F(cmd[2]), F(cmd[3])), player.transform.rotation);
                        return true;
                    case "wait":
                        Send(Vector2.zero);
                        return cmdTime >= F(cmd[1]);
                    case "grounded":
                        Send(Vector2.zero);
                        return player.IsGrounded && cmdTime > 0.1f;
                    case "waitfor": // waitfor 조건... [시간 제한 60]
                        Send(Vector2.zero);
                        cmdTimeout = 600f;
                        return Condition(1);
                    case "mark": // mark 이름 : 상대 봇이 waitfor partnerLog 이름 으로 기다린다
                        Mark(cmd[1]);
                        return true;
                    case "log":
                        owner.Log($"{name} 메모: {string.Join(" ", cmd, 1, cmd.Length - 1)} 위치={Fmt(p)}");
                        return true;
                    default:
                        owner.Log($"{name} 모르는 명령 {cmd[0]}");
                        return true;
                }
            }

            bool Condition(int i)
            {
                switch (cmd[i])
                {
                    case "z2red": return owner.z2 != null && owner.z2.RedIndex >= (int)F(cmd[i + 1]);
                    case "z2blue": return owner.z2 != null && owner.z2.BlueIndex >= (int)F(cmd[i + 1]);
                    case "rope": return owner.rope != null && RopeStage(owner.rope) >= (int)F(cmd[i + 1]);
                    case "cp": return GameServices.Checkpoints != null && GameServices.Checkpoints.ActiveOrder >= (int)F(cmd[i + 1]);
                    case "partnerNear": // partnerNear x z r
                    {
                        var other = this == owner.Red ? owner.Blue : owner.Red;
                        var q = other.player.transform.position;
                        return new Vector2(q.x - F(cmd[i + 1]), q.z - F(cmd[i + 2])).magnitude <= F(cmd[i + 3]);
                    }
                    case "partnerIdle":
                    {
                        var other = this == owner.Red ? owner.Blue : owner.Red;
                        return other.IsIdle;
                    }
                    case "partnerLog": return owner.HasMark(cmd[i + 1]);
                    case "y": return player.transform.position.y >= F(cmd[i + 1]) && player.IsGrounded; // 내 발 높이 이상에 서 있음
                    case "plat": // plat 이름 x z r : 켜진 Z2 발판이 그 지점 근처
                    case "platY": // platY 이름 y : 켜진 Z2 발판 높이 이상
                    case "platLow": // platLow 이름 y : 켜진 Z2 발판 높이 이하
                    {
                        foreach (var zp in FindObjectsByType<Z2Platform>(FindObjectsSortMode.None))
                        {
                            if (zp.name != cmd[i + 1] || !zp.IsActive) continue;
                            if (zp.role != PlayerRole.None && zp.role != player.Role) continue;
                            var q = zp.transform.position;
                            if (cmd[i] == "platY") { if (q.y >= F(cmd[i + 2])) return true; }
                            else if (cmd[i] == "platLow") { if (q.y <= F(cmd[i + 2])) return true; }
                            else if (new Vector2(q.x - F(cmd[i + 2]), q.z - F(cmd[i + 3])).magnitude <= F(cmd[i + 4])) return true;
                        }
                        return false;
                    }
                    default: throw new Exception("모르는 조건 " + cmd[i]);
                }
            }
        }

        // 봇끼리 맞추는 표시: "mark 이름" 명령 대신 Run 스크립트에서 Mark를 쓴다
        readonly HashSet<string> marks = new HashSet<string>();
        public bool HasMark(string m) => marks.Contains(m);
        public static void Mark(string m) { if (Instance != null) { Instance.marks.Add(m); Instance.Log("표시 " + m); } }
    }
}
#endif
