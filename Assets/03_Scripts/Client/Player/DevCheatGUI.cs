using UnityEngine;
using UnityEngine.InputSystem;

namespace VaroGO.Client
{
    /// <summary>
    /// 개발 치트 패널 (OnGUI). 씬에 빈 오브젝트로 두고 이 컴포넌트를 붙인다. 에디터·개발 빌드에서만 동작한다.
    /// 버튼 1 / [ 키: 혼자 올라가기 (매달렸을 때 점프 또는 벽 쪽 이동으로 스스로 올라감)
    /// 버튼 2 / ] 키: 무중력 비행 (Space 위, Left Ctrl / Q 아래)
    /// ` (백쿼트) 키: 패널 숨기기/보이기. 버튼을 마우스로 누르려면 Esc로 커서를 먼저 푼다.
    /// 치트 값은 DevCheats(정적)라 씬이 바뀌어도 유지된다.
    /// </summary>
    public class DevCheatGUI : MonoBehaviour
    {
        const float Width = 250f, Height = 150f;

        [SerializeField] bool showOnStart = true;
        [Tooltip("패널 왼쪽 위 위치 (화면 픽셀). 맨 위 줄은 OfflineBootstrap 역할 표시가 쓴다")]
        [SerializeField] Vector2 position = new(12f, 56f);

        bool visible;
        GUIStyle box, button, label;

        Rect PanelRect => new(position.x, position.y, Width, Height);

        void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            visible = showOnStart;
#else
            // 정식 빌드에는 치트를 남기지 않는다
            DevCheats.SelfClimb = false;
            DevCheats.Fly = false;
            enabled = false;
#endif
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame) visible = !visible;
            if (kb.leftBracketKey.wasPressedThisFrame) DevCheats.SelfClimb = !DevCheats.SelfClimb;
            if (kb.rightBracketKey.wasPressedThisFrame) DevCheats.Fly = !DevCheats.Fly;
        }

        // PlayerInputReader가 클릭하면 커서를 다시 잠근다 → 패널 위 클릭이면 풀어 둔다 (버튼이 눌리도록)
        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (!visible || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 p = mouse.position.ReadValue();
            p.y = Screen.height - p.y; // IMGUI 좌표 (위가 0)
            if (PanelRect.Contains(p))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void OnGUI()
        {
            if (!visible) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
                button = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 30 };
                label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            }

            GUILayout.BeginArea(PanelRect, box);
            GUILayout.Label("개발 치트   ` 숨기기 · Esc 커서", label);

            if (GUILayout.Button($"[  혼자 올라가기 : {(DevCheats.SelfClimb ? "ON" : "OFF")}", button))
                DevCheats.SelfClimb = !DevCheats.SelfClimb;

            if (GUILayout.Button($"]  무중력 비행 : {(DevCheats.Fly ? "ON" : "OFF")}", button))
                DevCheats.Fly = !DevCheats.Fly;

            if (DevCheats.Fly) GUILayout.Label("Space 위 / Left Ctrl·Q 아래", label);
            else if (DevCheats.SelfClimb) GUILayout.Label("매달린 뒤 점프 또는 벽 쪽 이동", label);
            GUILayout.EndArea();
        }
    }
}
