using System;
using System.Collections.Generic;
using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // Stage2 Z1 비대칭 유리 다리 (5열 × 6줄 = 30칸).
    // - 줄마다 안전 칸은 1칸(빨강 또는 파랑) 또는 2칸(빨강 + 파랑). 어느 색이든 두 사람 모두 밟을 수 있다.
    //   색은 "탑뷰에서 누가 그 칸을 볼 수 있느냐"만 뜻한다 (TopViewConsole을 켜면 자기 색 칸만 표시).
    // - 모든 칸은 서 있으면 흔들리다 깨진다. 가짜 칸은 짧게, 안전 칸은 오래.
    // - 한 명이라도 떨어지면 배치를 새로 뽑고, 깨진 칸을 되살리고, 다리 구역에 있는 두 사람 모두 시작점으로 돌아간다.
    // 온라인: 같은 오브젝트의 IGlassBridgeState(NetGlassBridgeState)가 배치·깨진 칸·라운드를 공유한다.
    //   깨짐 판정·추락 판정은 각자 자기 캐릭터만, 자기 PC에서 한다. 순간이동은 플레이어 동기화가 전달한다.
    public class GlassBridge : MonoBehaviour
    {
        public const int Rows = 6;
        public const int Cols = 5;
        public const int TileCount = Rows * Cols;

        [Tooltip("Glass_R{줄}_C{열} 칸들의 부모")]
        [SerializeField] Transform tilesRoot;
        [Tooltip("떨어졌을 때 돌아갈 시작 위치 (관측 발판 위). 빨강은 왼쪽, 파랑은 오른쪽으로 조금 벌려 세운다")]
        [SerializeField] Transform startPoint;
        [SerializeField, Min(0f)] float startSpread = 1.5f;
        [Tooltip("탑뷰에서 빨강 칸 표시 재질 / 파랑 칸 표시 재질")]
        [SerializeField] Material redHint;
        [SerializeField] Material blueHint;

        [Header("깨짐 (초)")]
        [Tooltip("가짜 칸은 밟는 순간 깨지기 시작해 이 시간 뒤 깨진다 (바로 내려와도 깨진다)")]
        [SerializeField, Min(0f)] float fakeBreakDelay = 0.1f;
        [Tooltip("안전 칸도 이 시간 이상 계속 서 있으면 깨진다 (시간 압박)")]
        [SerializeField, Min(0.05f)] float safeBreakDelay = 3f;
        [Tooltip("깨지기 직전 이 시간 동안 흔들린다")]
        [SerializeField, Min(0f)] float shakeTime = 0.6f;
        [Tooltip("흔들림 세기 (m)")]
        [SerializeField, Min(0f)] float shakeAmount = 0.05f;

        [Header("구역 (월드 좌표)")]
        [Tooltip("이 높이 아래로 떨어지면 실패 (다리 윗면 기준 약 0)")]
        [SerializeField] float fallHeight = -1.5f;
        [Tooltip("실패 때 시작점으로 돌려보낼 범위 (관측 발판 ~ 도착 발판)")]
        [SerializeField] Bounds zone = new Bounds(new Vector3(0f, -1f, 33f), new Vector3(26f, 16f, 44f));

        class Tile
        {
            public Transform transform;
            public Vector3 basePosition;
            public Collider[] colliders;
            public Renderer[] renderers;
            public GameObject hint;
            public bool occupied;  // 이번 프레임에 누군가 서 있다
            public float stood;    // 누군가 계속 서 있는 시간 (흔들림 표시용)
            public float myStood;  // 내 캐릭터가 서 있는 시간 (로컬 몸 중 가장 긴 값)
            public readonly float[] stoodBy = new float[MaxLocals]; // 로컬 몸마다 서 있는 시간 (온라인은 0번만 씀)
            public bool requested; // 이번 라운드에 깨짐 요청을 보냈다
            public float touchedAt = -1f; // 가짜 칸: 내 캐릭터가 처음 밟은 시각 (-1 = 안 밟음)
        }

        class Shard
        {
            public Transform t;
            public Vector3 v;
            public Vector3 spin;
            public float life;
        }

        Tile[] tiles;
        // 로컬 몸 수. 온라인은 1, 한 에디터 2인 테스트(DuoPlaytest)는 2
        const int MaxLocals = 2;
        readonly PlayerController[] locals = new PlayerController[MaxLocals];
        readonly int[] localTiles = new int[MaxLocals];
        readonly List<Shard> shards = new List<Shard>();
        float tileTop;

        long localLayout;
        int localBroken;
        int localRound;
        int appliedBroken = -1;
        long appliedLayout = -1;
        bool hintsOn;
        float graceUntil;

        IGlassBridgeState net;
        bool netResolved;
        IGlassBridgeState Net
        {
            get
            {
                if (!GameServices.IsOnline) return null;
                if (!netResolved)
                {
                    netResolved = true;
                    net = GetComponent<IGlassBridgeState>();
                    if (net != null) { net.Changed += OnNetChanged; net.Failed += OnNetFailed; }
                }
                return net != null && net.Ready ? net : null;
            }
        }

        bool Ready => !GameServices.IsOnline || Net != null;
        long Layout => Net != null ? Net.Layout : localLayout;
        int Broken => Net != null ? Net.BrokenMask : localBroken;
        int Round => Net != null ? Net.Round : localRound;

        public static int Kind(long layout, int index) => (int)((layout >> (index * 2)) & 3L);
        public int KindOf(int index) => Kind(Layout, index);
        public bool HintsVisible => hintsOn;

        void Awake()
        {
            if (tilesRoot == null) tilesRoot = transform;
            tiles = new Tile[TileCount];
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var t = tilesRoot.Find($"Glass_R{r + 1}_C{c + 1}");
                    if (t == null) { Debug.LogWarning($"[GlassBridge] Glass_R{r + 1}_C{c + 1} 칸이 없습니다", this); continue; }
                    var tile = new Tile
                    {
                        transform = t,
                        basePosition = t.localPosition,
                        colliders = t.GetComponents<Collider>(),
                        renderers = t.GetComponents<Renderer>(),
                    };
                    tile.hint = MakeHint(t);
                    tiles[r * Cols + c] = tile;
                    var b = tile.colliders.Length > 0 ? tile.colliders[0].bounds : new Bounds(t.position, Vector3.zero);
                    tileTop = b.max.y;
                }
        }

        GameObject MakeHint(Transform tile)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = tile.name + "_Hint";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, true);
            var top = tile.GetComponent<Collider>() != null ? tile.GetComponent<Collider>().bounds.max.y : tile.position.y;
            go.transform.position = new Vector3(tile.position.x, top + 0.05f, tile.position.z);
            go.transform.localScale = new Vector3(1.6f, 0.04f, 1.6f);
            go.SetActive(false);
            return go;
        }

        void Start()
        {
            if (!GameServices.IsOnline) NewLocalRound();
            Apply();
        }

        void OnDestroy()
        {
            if (net != null) { net.Changed -= OnNetChanged; net.Failed -= OnNetFailed; }
        }

        // ---- 배치 생성 (오프라인·방장 공용) ----
        // 앞에서 뒤까지 끊기지 않는 길(이웃 줄과 열 차이 1 이하)을 하나 만들고, 길의 색은 빨강 3 · 파랑 3으로 섞는다.
        // 절반 정도의 줄에는 다른 색 안전 칸을 하나 더 둔다.
        public static long Generate(long previous)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                long layout = 0;
                int[] colors = { 1, 1, 1, 2, 2, 2 };
                for (int i = colors.Length - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int tmp = colors[i]; colors[i] = colors[j]; colors[j] = tmp;
                }
                int col = UnityEngine.Random.Range(0, Cols);
                for (int r = 0; r < Rows; r++)
                {
                    if (r > 0) col = Mathf.Clamp(col + UnityEngine.Random.Range(-1, 2), 0, Cols - 1);
                    layout |= (long)colors[r] << ((r * Cols + col) * 2);
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        int other = UnityEngine.Random.Range(0, Cols - 1);
                        if (other >= col) other++;
                        layout |= (long)(3 - colors[r]) << ((r * Cols + other) * 2);
                    }
                }
                if (layout != previous) return layout;
            }
            return previous;
        }

        void NewLocalRound()
        {
            localLayout = Generate(localLayout);
            localBroken = 0;
            localRound++;
        }

        // ---- 공유 상태 반영 ----
        void OnNetChanged() => Apply();

        void OnNetFailed()
        {
            Apply();
            SendLocalPlayerBack();
        }

        void Apply()
        {
            if (tiles == null) return;
            if (!Ready)
            {
                // 배치가 오기 전에는 칸만 보여 준다 (아무것도 깨지지 않음)
                return;
            }
            long layout = Layout;
            int broken = Broken;
            if (layout != appliedLayout)
            {
                appliedLayout = layout;
                foreach (var t in tiles) if (t != null) { t.stood = t.myStood = 0f; Array.Clear(t.stoodBy, 0, MaxLocals); t.requested = false; t.touchedAt = -1f; }
            }
            for (int i = 0; i < TileCount; i++)
            {
                var t = tiles[i];
                if (t == null) continue;
                bool isBroken = (broken & (1 << i)) != 0;
                bool wasBroken = appliedBroken >= 0 && (appliedBroken & (1 << i)) != 0;
                foreach (var c in t.colliders) if (c != null) c.enabled = !isBroken;
                foreach (var r in t.renderers) if (r != null) r.enabled = !isBroken;
                if (isBroken && !wasBroken && appliedBroken >= 0) SpawnShards(t);
                if (!isBroken && !t.requested) { t.transform.localPosition = t.basePosition; t.touchedAt = -1f; }
            }
            appliedBroken = broken;
            RefreshHints();
        }

        // ---- 탑뷰 표시 (TopViewConsole이 켠다) ----
        public void SetHintsVisible(bool on)
        {
            hintsOn = on;
            RefreshHints();
        }

        void RefreshHints()
        {
            if (tiles == null) return;
            var role = SwitchStates.LocalRole;
            bool all = MagicVisibility.DebugShowAll;
            long layout = Layout;
            int broken = Broken;
            for (int i = 0; i < TileCount; i++)
            {
                var t = tiles[i];
                if (t == null || t.hint == null) continue;
                int kind = Kind(layout, i);
                bool mine = (kind == 1 && (role == PlayerRole.A_Red || all)) || (kind == 2 && (role == PlayerRole.B_Blue || all));
                bool show = hintsOn && Ready && mine && (broken & (1 << i)) == 0;
                if (show)
                {
                    var mat = kind == 1 ? redHint : blueHint;
                    var r = t.hint.GetComponent<Renderer>();
                    if (mat != null && r != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
                }
                if (t.hint.activeSelf != show) t.hint.SetActive(show);
            }
        }

        // ---- 매 프레임: 서 있는 시간·흔들림·깨짐·추락 ----
        void Update()
        {
            UpdateShards();
            if (tiles == null || !Ready) return;

            foreach (var t in tiles) if (t != null) t.occupied = false;
            int localCount = 0;
            foreach (var p in PlayerController.All)
            {
                if (p == null || p.IsDead || p.IsSeated) continue;
                int i = TileUnder(p.transform.position);
                if (p.IsLocal && localCount < MaxLocals) { locals[localCount] = p; localTiles[localCount] = i; localCount++; }
                if (i >= 0) tiles[i].occupied = true;
            }

            int broken = Broken;
            long layout = Layout;
            for (int i = 0; i < TileCount; i++)
            {
                var t = tiles[i];
                if (t == null) continue;
                bool isBroken = (broken & (1 << i)) != 0;
                if (isBroken) { t.stood = t.myStood = 0f; Array.Clear(t.stoodBy, 0, MaxLocals); continue; }
                // 흔들림은 누가 서 있든 두 화면에 보이고, 깨짐 요청은 내 캐릭터 시간으로만 보낸다
                t.stood = t.occupied ? t.stood + Time.deltaTime : 0f;
                // 내 캐릭터마다 따로 센다 (온라인은 로컬 1명이라 기존과 같다)
                bool mineOn = false;
                t.myStood = 0f;
                for (int k = 0; k < MaxLocals; k++)
                {
                    bool on = k < localCount && localTiles[k] == i;
                    t.stoodBy[k] = on ? t.stoodBy[k] + Time.deltaTime : 0f;
                    if (on) mineOn = true;
                    if (t.stoodBy[k] > t.myStood) t.myStood = t.stoodBy[k];
                }

                bool fake = Kind(layout, i) == 0;
                if (fake)
                {
                    // 가짜 칸은 밟는 순간부터 깨진다. 바로 내려와도 멈추지 않는다
                    if (mineOn && t.touchedAt < 0f) t.touchedAt = Time.time;
                    if (t.touchedAt >= 0f && !t.requested && Time.time - t.touchedAt >= fakeBreakDelay) Break(i);
                    if (t.occupied || t.touchedAt >= 0f)
                    {
                        var j = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)) * shakeAmount * 1.4f;
                        t.transform.localPosition = t.basePosition + t.transform.parent.InverseTransformVector(j);
                    }
                    else t.transform.localPosition = t.basePosition;
                    continue;
                }
                float delay = safeBreakDelay;
                float shakeStart = Mathf.Max(0f, delay - shakeTime);
                if (t.stood > shakeStart)
                {
                    float k = Mathf.Clamp01((t.stood - shakeStart) / Mathf.Max(0.01f, shakeTime));
                    var jitter = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)) * shakeAmount * (0.4f + k);
                    t.transform.localPosition = t.basePosition + t.transform.parent.InverseTransformVector(jitter);
                }
                else t.transform.localPosition = t.basePosition;

                // 깨짐 요청은 서 있는 본인 PC만 보낸다
                if (mineOn && !t.requested && t.myStood >= delay) Break(i);
            }

            for (int k = 0; k < localCount; k++) CheckFall(locals[k]);
            for (int k = 0; k < MaxLocals; k++) locals[k] = null;
        }

        int TileUnder(Vector3 feet)
        {
            if (Mathf.Abs(feet.y - tileTop) > 0.45f) return -1;
            for (int i = 0; i < TileCount; i++)
            {
                var t = tiles[i];
                if (t == null || t.colliders.Length == 0 || !t.colliders[0].enabled) continue;
                var b = t.colliders[0].bounds;
                if (feet.x >= b.min.x && feet.x <= b.max.x && feet.z >= b.min.z && feet.z <= b.max.z) return i;
            }
            return -1;
        }

        void Break(int index)
        {
            var t = tiles[index];
            t.requested = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Z1] 깨짐 요청 칸={index} 종류={Kind(Layout, index)} 라운드={Round}");
#endif
            var n = Net;
            if (n != null) { n.RequestBreak(index, n.Round); return; }
            if (GameServices.IsOnline) return;
            localBroken |= 1 << index;
            Apply();
        }

        void CheckFall(PlayerController local)
        {
            if (Time.time < graceUntil) return;
            var pos = local.transform.position;
            if (pos.y >= fallHeight || !InZoneXZ(pos)) return;
            graceUntil = Time.time + 1f;
            var n = Net;
            if (n != null) n.RequestFail(n.Round);
            else if (!GameServices.IsOnline) { NewLocalRound(); Apply(); }
            // 떨어진 본인은 기다리지 않고 바로 돌아간다 (상대는 Failed를 받고 돌아간다)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Z1] 시작점으로 복귀 (원인: 내가 떨어짐) 역할={local.Role}");
#endif
            Teleport(local);
            // 오프라인에서 로컬 몸이 둘이면(한 에디터 2인 테스트) 다른 몸도 Failed를 받은 것처럼 돌려보낸다
            if (!GameServices.IsOnline)
                foreach (var p in PlayerController.All)
                {
                    if (p == null || p == local || !p.IsLocal || p.IsDead || p.IsSeated || !zone.Contains(p.transform.position)) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Z1] 시작점으로 복귀 (원인: 상대 추락) 역할={p.Role}");
#endif
                    Teleport(p);
                }
        }

        bool InZoneXZ(Vector3 p) => p.x >= zone.min.x && p.x <= zone.max.x && p.z >= zone.min.z && p.z <= zone.max.z;

        void SendLocalPlayerBack()
        {
            foreach (var p in PlayerController.All)
            {
                if (p == null || !p.IsLocal || p.IsDead || p.IsSeated) continue;
                if (Time.time < graceUntil) return; // 방금 내가 떨어져 이미 돌아갔다
                if (!zone.Contains(p.transform.position)) return; // 다리 구역 밖(이미 Z2로 감)은 그대로
                graceUntil = Time.time + 1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Z1] 시작점으로 복귀 (원인: 상대 추락) 역할={p.Role}");
#endif
                Teleport(p);
            }
        }

        void Teleport(PlayerController p)
        {
            if (startPoint == null) return;
            float side = p.Role == PlayerRole.B_Blue ? 1f : -1f;
            var pos = startPoint.position + startPoint.right * side * startSpread;
            p.Teleport(pos, startPoint.rotation);
            ScreenFade.Set(1f);
            ScreenFade.FadeIn(0.6f);
        }

        // ---- 깨진 유리 파편 (표시만, 충돌 없음) ----
        void SpawnShards(Tile t)
        {
            var mat = t.renderers.Length > 0 && t.renderers[0] != null ? t.renderers[0].sharedMaterial : null;
            var center = t.transform.position;
            for (int i = 0; i < 6; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.name = "GlassShard";
                go.transform.SetParent(transform, true);
                go.transform.position = center + new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), 0f, UnityEngine.Random.Range(-1.2f, 1.2f));
                go.transform.rotation = UnityEngine.Random.rotation;
                go.transform.localScale = new Vector3(UnityEngine.Random.Range(0.4f, 0.9f), 0.08f, UnityEngine.Random.Range(0.4f, 0.9f));
                if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
                shards.Add(new Shard
                {
                    t = go.transform,
                    v = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0f, 1.5f), UnityEngine.Random.Range(-1f, 1f)),
                    spin = UnityEngine.Random.insideUnitSphere * 360f,
                    life = 2f,
                });
            }
        }

        void UpdateShards()
        {
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var s = shards[i];
                s.life -= Time.deltaTime;
                if (s.t == null || s.life <= 0f)
                {
                    if (s.t != null) Destroy(s.t.gameObject);
                    shards.RemoveAt(i);
                    continue;
                }
                s.v += Physics.gravity * Time.deltaTime;
                s.t.position += s.v * Time.deltaTime;
                s.t.Rotate(s.spin * Time.deltaTime, Space.Self);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string DebugLayout()
        {
            var s = $"round={Round} broken={Convert.ToString(Broken, 2)}\n";
            for (int r = Rows - 1; r >= 0; r--)
            {
                for (int c = 0; c < Cols; c++) s += "·RB?"[KindOf(r * Cols + c)];
                s += "\n";
            }
            return s;
        }
#endif
    }
}
