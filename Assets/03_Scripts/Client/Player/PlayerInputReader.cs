using UnityEngine;
using UnityEngine.InputSystem;

namespace VaroGO.Client
{
    // 입력은 Update에서 읽어 버퍼에 저장하고, PlayerController.Simulate()에서 소비한다.
    // 온라인에서는 Simulate가 Fusion 틱에서 호출되므로 한 번 눌리는 입력은 플래그로 버퍼링한다.
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap map;
        InputAction move, look, jump, interact;
        bool jumpPressed, interactPressed;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        // true면 Look은 이번 프레임 마우스 이동량(픽셀), false면 스틱 기울기(-1~1)
        public bool LookIsPointer { get; private set; }

        void Awake()
        {
            // 플레이어가 둘이어도 서로의 Enable/Disable에 영향받지 않도록 복제해서 쓴다
            map = Instantiate(actions).FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            jump = map.FindAction("Jump", true);
            interact = map.FindAction("Interact", true);
        }

        void OnEnable()
        {
            map.Enable();
            Cursor.lockState = CursorLockMode.Locked;
#if UNITY_EDITOR
            // Device Simulator를 한 번 쓰면 Game 뷰로 바꿔도 마우스 장치가 꺼진 채 남는다
            if (Mouse.current != null && !Mouse.current.enabled)
            {
                Debug.LogWarning("[PlayerInputReader] 마우스 장치가 꺼져 있어서 다시 켭니다. Device Simulator 탭을 닫아주세요.");
                InputSystem.EnableDevice(Mouse.current);
            }
#endif
        }

        void OnDisable()
        {
            map.Disable();
            Move = Look = Vector2.zero;
            jumpPressed = interactPressed = false;
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            Look = look.ReadValue<Vector2>();
            LookIsPointer = look.activeControl != null && look.activeControl.device is Pointer;
            // 커서가 풀려 있으면 마우스로 카메라를 돌리지 않는다
            if (LookIsPointer && Cursor.lockState != CursorLockMode.Locked) Look = Vector2.zero;
            if (jump.WasPressedThisFrame()) jumpPressed = true;
            if (interact.WasPressedThisFrame()) interactPressed = true;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.Locked;
        }

        public bool ConsumeJump()
        {
            bool pressed = jumpPressed;
            jumpPressed = false;
            return pressed;
        }

        public bool ConsumeInteract()
        {
            bool pressed = interactPressed;
            interactPressed = false;
            return pressed;
        }
    }
}
