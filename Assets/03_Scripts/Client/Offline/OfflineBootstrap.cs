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
            if (GameServices.IsOnline)
            {
                DisableScenePlayers();
                return;
            }
            // 이전 플레이에서 남은 오프라인 등록(도메인 리로드를 끈 경우)은 새로 덮어쓴다
            if (GameServices.Role != null && !(GameServices.Role is OfflineRoleProvider)) return;
            MagicVisibility.DebugShowAll = false;
            roleProvider = new OfflineRoleProvider();
            GameServices.Register(roleProvider, new OfflineGameClock(), isOnline: false);
        }

        // 계약 §2: 온라인이면 씬에 직접 배치한 오프라인 Player를 끈다.
        // 씬 로드 Awake 시점이라 스폰된 Player_Net은 아직 없다. 같은 씬 오브젝트만 대상으로 한다
        void DisableScenePlayers()
        {
            var drivers = FindObjectsByType<OfflinePlayerDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var driver in drivers)
                if (driver.gameObject.scene == gameObject.scene) driver.gameObject.SetActive(false);
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
