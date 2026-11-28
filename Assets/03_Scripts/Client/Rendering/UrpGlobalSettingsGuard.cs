using UnityEngine;
using UnityEngine.Rendering;

namespace VaroGO.Client.Rendering
{
    // 빌드에서 URP 전역 설정(UniversalRenderPipelineGlobalSettings)이 "현재 설정"으로 연결되지 않아
    // 매 프레임 UniversalRenderPipeline 생성이 실패하고 화면이 안 그려지는 문제(2026-11-26 확인)를 막는다.
    // 첫 프레임 그리기 전에 연결 여부를 보고, 비어 있으면 Resources의 참조 에셋으로 연결한다. 정상이면 아무것도 하지 않는다.
    // 참조 에셋: Assets/Resources/UrpGlobalSettingsRef.asset (빌드에 전역 설정 에셋을 함께 넣는 역할도 한다)
    static class UrpGlobalSettingsGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Ensure()
        {
            RenderPipelineGlobalSettings current;
            if (GraphicsSettings.TryGetCurrentRenderPipelineGlobalSettings(out current) && current != null) return;

            var r = Resources.Load<UrpGlobalSettingsRef>("UrpGlobalSettingsRef");
            if (r == null || r.settings == null)
            {
                Debug.LogError("[Rendering] URP 전역 설정이 연결되지 않았고 Resources/UrpGlobalSettingsRef도 없습니다 → 화면이 안 그려질 수 있음");
                return;
            }
            bool ok = GraphicsSettings.TrySetCurrentRenderPipelineGlobalSettings(r.settings);
            Debug.Log("[Rendering] URP 전역 설정 연결 " + (ok ? "완료" : "실패") + ": " + r.settings.name);
        }
    }
}
