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
            if (GameServices.IsOnline || GameServices.Role != null) return;
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
        }

        void OnGUI()
        {
            if (GameServices.Role == null) return;
            GUI.Label(new Rect(10, 10, 300, 24), $"ROLE: {GameServices.Role.LocalRole.ToString().ToUpperInvariant()}");
        }
#endif
    }
}
