using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VaroGO.Client
{
    // Stage2 Z1 관측 장치. 범위(트리거) 안에서 상호작용 키(E)를 누르면 내 화면만 위에서 내려다보는 시점으로 바뀐다.
    // 켜져 있는 동안: 움직일 수 없고, GlassBridge의 내 색 안전 칸 표시가 보인다. 다시 E를 누르면 돌아온다.
    // 각자 화면 연출이라 동기화하지 않는다. 횟수 제한 없음.
    [RequireComponent(typeof(Collider))]
    public class TopViewConsole : MonoBehaviour
    {
        [Tooltip("내려다보는 카메라 (CinemachineCamera). 평소에는 꺼져 있다")]
        [SerializeField] CinemachineCamera topCamera;
        [Tooltip("탑뷰에서 칸 표시를 켤 유리 다리")]
        [SerializeField] GlassBridge bridge;
        [Tooltip("탑뷰 동안 내 화면에서만 숨길 것 (천장 등). 켜 있던 렌더러만 숨겼다가 돌려 놓는다")]
        [SerializeField] Renderer[] hideWhileViewing;
        bool[] hiddenStates;

        PlayerController inRange;
        PlayerController viewer;
        int openedFrame = -1;
        PlayerInputReader reader;
        bool wasHeld;
        const float ReopenGuard = 0.3f;
        float closedAt = float.NegativeInfinity;
        Collider rangeCollider;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void Awake()
        {
            if (topCamera != null)
            {
                topCamera.Priority = 50;
                topCamera.enabled = false;
            }
        }

        void OnDisable()
        {
            Close();
            SetInRange(null);
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null && player.IsLocal) SetInRange(player);
        }

        void OnTriggerExit(Collider other)
        {
            if (inRange != null && other.gameObject == inRange.gameObject && viewer == null) SetInRange(null);
        }

        void SetInRange(PlayerController player)
        {
            if (inRange != null) inRange.Interacted -= OnInteract;
            inRange = player;
            if (inRange != null) inRange.Interacted += OnInteract;
        }

        bool StillInRange(PlayerController p)
        {
            if (p == null || !p.isActiveAndEnabled || p.IsDead) return false;
            if (rangeCollider == null) rangeCollider = GetComponent<Collider>();
            if (rangeCollider == null || !rangeCollider.enabled) return false;
            var b = rangeCollider.bounds;
            b.Expand(0.5f);
            return b.Contains(p.transform.position + Vector3.up * 0.5f);
        }

        void OnInteract()
        {
            if (inRange == null || !StillInRange(inRange)) return;
            // 끄는 E가 같은 순간 플레이어 쪽 상호작용으로도 들어와 바로 다시 켜지는 것을 막는다
            if (Time.unscaledTime - closedAt < ReopenGuard) return;
            if (viewer == null && inRange != null) Open(inRange);
        }

        void Open(PlayerController player)
        {
            viewer = player;
            openedFrame = Time.frameCount;
            wasHeld = true; // 켤 때 누른 키를 떼기 전까지는 끄지 않는다
            if (topCamera != null) topCamera.enabled = true;
            if (bridge != null) bridge.SetHintsVisible(true);
            if (hideWhileViewing != null)
            {
                hiddenStates = new bool[hideWhileViewing.Length];
                for (int i = 0; i < hideWhileViewing.Length; i++)
                {
                    var r = hideWhileViewing[i];
                    if (r == null) continue;
                    hiddenStates[i] = r.enabled;
                    r.enabled = false;
                }
            }
        }

        void Close()
        {
            if (viewer == null) return;
            viewer.InputLocked = false;
            viewer = null;
            closedAt = Time.unscaledTime;
            if (hideWhileViewing != null && hiddenStates != null)
                for (int i = 0; i < hideWhileViewing.Length && i < hiddenStates.Length; i++)
                    if (hideWhileViewing[i] != null) hideWhileViewing[i].enabled = hiddenStates[i];
            hiddenStates = null;
            if (topCamera != null) topCamera.enabled = false;
            if (bridge != null) bridge.SetHintsVisible(false);
        }

        void Update()
        {
            // 순간이동(추락 복귀·디버그 move 등)으로 범위를 벗어나면 OnTriggerExit가 오지 않는다 → 직접 거리로 확인
            if (inRange != null && viewer == null && !StillInRange(inRange)) SetInRange(null);
            if (viewer == null) return;
            // 죽거나 다른 연출(좌석 등)에 들어가면 닫는다
            if (viewer.IsDead || viewer.IsSeated || !viewer.isActiveAndEnabled) { Close(); return; }
            viewer.InputLocked = true; // 잠그는 쪽이 매 프레임 정한다 (PlayerController.InputLocked 규칙)
            // 잠긴 동안은 Interacted가 오지 않으므로 상호작용 입력을 직접 읽는다 (새로 눌렀을 때만). 켠 프레임의 같은 입력은 무시
            if (reader == null || reader.gameObject != viewer.gameObject) reader = viewer.GetComponent<PlayerInputReader>();
            bool held = reader != null ? reader.InteractHeld : (Keyboard.current != null && Keyboard.current.eKey.isPressed);
            bool pressed = held && !wasHeld;
            wasHeld = held;
            if (Time.frameCount != openedFrame && pressed) Close();
        }

        // 그레이박스용 임시 안내. UI가 들어오면 교체한다.
        void OnGUI()
        {
            if (inRange == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            GUI.Label(new Rect(0, Screen.height * 0.65f, Screen.width, 30), viewer != null ? "E: 돌아가기" : "E: 위에서 보기", style);
        }
    }
}
