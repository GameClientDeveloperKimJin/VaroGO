using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // 씬에 하나 둔다. 온라인 부트스트랩이 먼저 등록했으면 아무것도 하지 않는다.
    [DefaultExecutionOrder(-1000)]
    public class OfflineBootstrap : MonoBehaviour
    {
        OfflineRoleProvider roleProvider;

        void Awake()
        {
            // 이전 플레이에서 남은 오프라인 등록(도메인 리로드를 끈 경우)은 새로 덮어쓴다
            if (GameServices.IsOnline || (GameServices.Role != null && !(GameServices.Role is OfflineRoleProvider))) return;
            MagicVisibility.DebugShowAll = false;
            roleProvider = new OfflineRoleProvider();
            GameServices.Register(roleProvider, new OfflineGameClock(), isOnline: false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || roleProvider == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) roleProvider.SetRole(PlayerRole.A_Red);
            if (keyboard.f2Key.wasPressedThisFrame) roleProvider.SetRole(PlayerRole.B_Blue);
            if (keyboard.f3Key.wasPressedThisFrame) MagicVisibility.DebugShowAll = !MagicVisibility.DebugShowAll;
        }

        void OnGUI()
        {
            if (GameServices.Role == null) return;
            string label = $"ROLE: {GameServices.Role.LocalRole.ToString().ToUpperInvariant()}";
            if (MagicVisibility.DebugShowAll) label += "  [F3: 마법 전부 표시]";
            GUI.Label(new Rect(10, 10, 400, 24), label);
        }
#endif
    }
}
