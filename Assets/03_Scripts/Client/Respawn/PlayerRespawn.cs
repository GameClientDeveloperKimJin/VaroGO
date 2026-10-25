using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace VaroGO.Client
{
    // C-10: Player 프리팹에 붙인다. 로컬 플레이어만 죽음을 판정하고 부활시킨다.
    // KillZone 태그 트리거에 들어가거나 killHeight 아래로 떨어지면 사망
    // → 화면 페이드 → 마지막으로 지나간 체크포인트(없으면 시작 위치)에서 부활
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

        PlayerController player;
        Checkpoint current;
        Vector3 startPosition;
        Quaternion startRotation;
        float deathTimer;
        float fadeAlpha;

        public Checkpoint Current => current;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        public void ReachCheckpoint(Checkpoint checkpoint)
        {
            if (!player.IsLocal || player.IsDead || checkpoint == current) return;
            current = checkpoint;
            Debug.Log($"[Respawn] 체크포인트 {checkpoint.name} (order {checkpoint.Order})");
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
        }

        void Update()
        {
            if (!player.IsLocal) return;

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
#endif
        }

        void RespawnAtCurrent()
        {
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

        // F6: 현재 체크포인트 다음 순서로 이동한다. 마지막이면 처음으로 돌아간다.
        void JumpToNextCheckpoint()
        {
            var list = Checkpoint.All;
            if (list.Count == 0) return;
            int index = current == null ? -1 : IndexOf(current);
            current = list[(index + 1) % list.Count];
            RespawnAtCurrent();
        }

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
            if (fadeAlpha <= 0f) return;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
