using UnityEngine;
using VaroGO.Shared;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace VaroGO.Client
{
    // C-10 / C2-05: Player 프리팹에 붙인다. 로컬 플레이어만 죽음을 판정하고 부활시킨다.
    // KillZone 태그 트리거에 들어가거나 killHeight 아래로 떨어지면 사망
    // → 화면 페이드 → 공용 체크포인트(ICheckpointState.ActiveOrder, 없으면 시작 위치)에서 부활.
    // 죽은 플레이어만 이동한다. 파트너는 그대로
    [RequireComponent(typeof(PlayerController))]
    public class PlayerRespawn : MonoBehaviour
    {
        const string KillZoneTag = "KillZone";

        [Tooltip("이 높이(Y)보다 낮아지면 사망")]
        [SerializeField] float killHeight = -5f;
        [Tooltip("사망부터 부활까지 (초). 이 동안 화면이 어두워진다")]
        [SerializeField] float deathDuration = 0.5f;
        [Tooltip("부활 후 화면이 다시 밝아지는 시간 (초)")]
        [SerializeField] float fadeInDuration = 0.25f;
        [Tooltip("파트너에게 밀린 뒤 이 시간(초) 안에 죽으면 \"밀려 떨어짐\"으로 본다")]
        [SerializeField] float pushedDeathWindow = 3f;
        [Tooltip("사망 원인 메시지를 부활 후 더 보여주는 시간 (초)")]
        [SerializeField] float deathMessageDuration = 2f;

        // C2-04: 재접속하면 코스 씬이 처음부터 다시 열린다. 로컬이 된 직후 이 시간 안에
        // 공용 체크포인트가 활성이면(= 이어서 하는 중) 그 지점으로 옮긴다. 새 게임은 ActiveOrder가 -1이라 그대로
        const float StartPlacementWindow = 3f;

        PlayerController player;
        Vector3 startPosition;
        Quaternion startRotation;
        float deathTimer;
        float fadeAlpha;
        float startTimer;
        bool startPlaced;
        string deathMessage;
        float deathMessageUntil;

        // C2-07: 마지막 사망 원인. 나를 민 역할, 혼자 떨어졌으면 None
        public PlayerRole LastDeathPushedBy { get; private set; }

        // 지금 부활할 체크포인트. 없으면 null (시작 위치)
        public Checkpoint Current
        {
            get
            {
                var state = GameServices.Checkpoints;
                return state != null && state.ActiveOrder >= 0 ? Checkpoint.Find(state.ActiveOrder) : null;
            }
        }

        void Awake()
        {
            player = GetComponent<PlayerController>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        public void ReachCheckpoint(Checkpoint checkpoint)
        {
            if (!player.IsLocal || player.IsDead) return;
            var state = GameServices.Checkpoints;
            var role = SwitchStates.LocalRole;
            startPlaced = true; // 직접 체크포인트를 밟았으면 시작 위치 옮기기는 하지 않는다
            if (state == null || role == PlayerRole.None || state.IsReached(checkpoint.Order, role)) return;
            // 앞 구간으로만 갱신하는 판단은 상태 구현이 한다
            state.ReportReached(checkpoint.Order, role);
            Debug.Log($"[Respawn] 체크포인트 도착 {checkpoint.name} (order {checkpoint.Order}, {role}) → 활성 order {state.ActiveOrder}");
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(KillZoneTag)) Kill();
        }

        public void Kill()
        {
            if (!player.IsLocal || player.IsDead) return;
            player.Die();
            deathTimer = 0f;
            RecordDeathCause();
        }

        // C2-07: 밀린 PC가 기록한 LastPushedBy/LastPushedAt으로 판정한다 (온라인·오프라인 같음)
        void RecordDeathCause()
        {
            bool pushed = player.LastPushedBy != PlayerRole.None && Time.time - player.LastPushedAt <= pushedDeathWindow;
            LastDeathPushedBy = pushed ? player.LastPushedBy : PlayerRole.None;
            deathMessage = pushed ? $"{RoleLabel(LastDeathPushedBy)}에게 밀려 떨어짐" : "혼자 떨어짐";
            deathMessageUntil = float.PositiveInfinity; // 부활할 때 남은 시간을 정한다
            Debug.Log($"[Respawn] 사망 원인: {deathMessage}");
        }

        static string RoleLabel(PlayerRole role)
        {
            return role == PlayerRole.A_Red ? "A(빨강)" : role == PlayerRole.B_Blue ? "B(파랑)" : "파트너";
        }

        void Update()
        {
            if (!player.IsLocal) return;
            if (!startPlaced) TryPlaceAtStart();

            if (player.IsDead)
            {
                deathTimer += Time.deltaTime;
                fadeAlpha = Mathf.Clamp01(deathTimer / Mathf.Max(0.01f, deathDuration));
                if (deathTimer >= deathDuration) RespawnAtCurrent();
            }
            else
            {
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeInDuration));
                if (transform.position.y < killHeight) Kill();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = Keyboard.current;
            if (keyboard == null || player.IsDead) return;
            if (keyboard.f5Key.wasPressedThisFrame) RespawnAtCurrent();
            if (keyboard.f6Key.wasPressedThisFrame) JumpToNextCheckpoint();
            if (keyboard.f7Key.wasPressedThisFrame) DebugPushedByPartner();
#endif
        }

        // 체크포인트 상태는 접속 뒤에 늦게 도착할 수 있어서 잠깐 기다린다
        void TryPlaceAtStart()
        {
            startTimer += Time.deltaTime;
            var current = Current;
            if (current != null && !player.IsDead)
            {
                var p = current.SpawnPoint;
                player.Teleport(p.position, p.rotation);
                startPlaced = true;
                Debug.Log($"[Respawn] 이어서 시작: 활성 체크포인트 {current.name} (order {current.Order})");
            }
            else if (startTimer >= StartPlacementWindow)
            {
                startPlaced = true;
            }
        }

        void RespawnAtCurrent()
        {
            if (deathMessage != null) deathMessageUntil = Time.time + deathMessageDuration;
            var current = Current;
            if (current != null)
            {
                var p = current.SpawnPoint;
                player.Respawn(p.position, p.rotation);
            }
            else
            {
                player.Respawn(startPosition, startRotation);
            }
        }

        // F6: 마지막으로 F6 이동한 곳(없으면 활성 체크포인트) 다음 순서로 이동하고 내 역할 도착을 알린다.
        // 마지막이면 처음으로 돌아간다. 저장은 공용 규칙을 따른다 (온라인이면 파트너도 밟아야 저장)
        Checkpoint devTarget;

        void JumpToNextCheckpoint()
        {
            var list = Checkpoint.All;
            if (list.Count == 0) return;
            var from = devTarget != null ? devTarget : Current;
            int index = from == null ? -1 : IndexOf(from);
            devTarget = list[(index + 1) % list.Count];
            var p = devTarget.SpawnPoint;
            player.Teleport(p.position, p.rotation);
            ReachCheckpoint(devTarget);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // F7: 파트너에게 뒤로 밀린 것처럼 (C2-07 "밀려 떨어짐" 테스트)
        void DebugPushedByPartner()
        {
            var role = SwitchStates.LocalRole;
            var from = role == PlayerRole.B_Blue ? PlayerRole.A_Red : PlayerRole.B_Blue;
            player.DebugReceivePush(from, -transform.forward);
        }
#endif

        static int IndexOf(Checkpoint checkpoint)
        {
            var list = Checkpoint.All;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == checkpoint) return i;
            return -1;
        }

        // 그레이박스용 임시 페이드. UI가 들어오면 교체한다.
        void OnGUI()
        {
            if (!player.IsLocal) return;
            if (fadeAlpha > 0f)
            {
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            // 그레이박스용 사망 원인 메시지. UI가 들어오면 교체한다
            if (deathMessage != null && Time.time < deathMessageUntil)
            {
                bool byPartner = LastDeathPushedBy != PlayerRole.None;
                var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 32, fontStyle = FontStyle.Bold };
                style.normal.textColor = byPartner ? new Color(1f, 0.55f, 0.3f) : Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 50), deathMessage, style);
            }
        }
    }
}
