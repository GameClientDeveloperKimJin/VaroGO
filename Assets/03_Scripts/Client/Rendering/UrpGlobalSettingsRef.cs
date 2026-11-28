using UnityEngine;
using UnityEngine.Rendering;

namespace VaroGO.Client.Rendering
{
    // UrpGlobalSettingsGuard가 빌드에서 읽는 참조 (Assets/Resources/UrpGlobalSettingsRef.asset)
    [CreateAssetMenu(menuName = "VaroGO/Rendering/URP Global Settings Ref")]
    public class UrpGlobalSettingsRef : ScriptableObject
    {
        public RenderPipelineGlobalSettings settings;
    }
}
