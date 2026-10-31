using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C2-06: 퍼즐 리셋 장치. 범위(트리거) 안에서 Interact를 누르면 같은 구역 스위치를 처음 상태로 돌린다.
    // 죽음은 부활만 한다. 퍼즐을 되돌리는 건 이 장치뿐 → IZoneReset.RequestReset (로컬 플레이어만, R4)
    [RequireComponent(typeof(Collider))]
    public class ZoneResetButton : MonoBehaviour
    {
        [Tooltip("되돌릴 퍼즐 구역 번호. 스위치(LocalSwitchState)의 Zone과 같은 번호를 넣는다")]
        [SerializeField] int zone;
        [Tooltip("한 번 누른 뒤 다시 누를 수 있을 때까지 (초)")]
        [SerializeField] float cooldown = 1f;
        [Tooltip("누르면 잠깐 눌리는 버튼 머리 (비워도 됨)")]
        [SerializeField] Transform buttonHead;
        [SerializeField] float pressDepth = 0.08f;
        [SerializeField] AudioSource audioSource;
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.6f;

        PlayerController inRange;
        float lastPressTime = -999f;
        Vector3 headRest;

        public int Zone => zone;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (buttonHead != null) headRest = buttonHead.localPosition;
        }

        void OnDisable()
        {
            SetInRange(null);
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null && player.IsLocal) SetInRange(player);
        }

        void OnTriggerExit(Collider other)
        {
            if (inRange != null && other.gameObject == inRange.gameObject) SetInRange(null);
        }

        void SetInRange(PlayerController player)
        {
            if (inRange != null) inRange.Interacted -= OnInteract;
            inRange = player;
            if (inRange != null) inRange.Interacted += OnInteract;
        }

        void OnInteract()
        {
            if (Time.time - lastPressTime < cooldown) return;
            var reset = GameServices.ZoneReset;
            var role = SwitchStates.LocalRole;
            if (reset == null || role == PlayerRole.None)
            {
                Play(TempSfx.Beep); // 온라인에서 서버 구현이 아직 없을 때
                return;
            }
            lastPressTime = Time.time;
            reset.RequestReset(zone, role);
            Play(TempSfx.Chime);
            Debug.Log($"[ZoneReset] 구역 {zone} 리셋 요청 ({role})");
        }

        void Update()
        {
            if (buttonHead == null) return;
            bool pressed = Time.time - lastPressTime < 0.25f;
            var target = pressed ? headRest + Vector3.down * pressDepth : headRest;
            buttonHead.localPosition = Vector3.MoveTowards(buttonHead.localPosition, target, Time.deltaTime);
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null) audioSource.PlayOneShot(clip, volume);
            else AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

        // 그레이박스용 임시 안내. UI가 들어오면 교체한다.
        void OnGUI()
        {
            if (inRange == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            GUI.Label(new Rect(0, Screen.height * 0.65f, Screen.width, 30), $"E: 퍼즐 리셋 (구역 {zone})", style);
        }
    }
}
