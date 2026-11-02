using UnityEngine;
using UnityEngine.InputSystem;

namespace VaroGO.Net
{
    /// <summary>
    /// 디버그: Shift+F1로 마우스 잠금 풀기/다시 잠그기 (언리얼 Shift+F1과 같은 용도).
    /// ESC와 달리 일시정지 메뉴를 열지 않는다. 풀린 동안은 클라 PlayerInputReader가 마우스 카메라 회전을 막는다.
    /// 게임 화면을 왼쪽 클릭해도(클라 동작) 다시 잠긴다.
    /// 씬에 둘 필요 없음: 게임 시작 시 스스로 생기고 씬이 바뀌어도 남는다. 에디터·개발 빌드에서만 동작.
    /// </summary>
    public class DebugCursorToggle : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject(nameof(DebugCursorToggle));
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<DebugCursorToggle>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f1Key.wasPressedThisFrame || !keyboard.shiftKey.isPressed) return;

            bool unlock = Cursor.lockState != CursorLockMode.None;
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
            Debug.Log($"[Debug] Shift+F1 마우스 {(unlock ? "풀림" : "잠금")}");
        }
#endif
    }
}
