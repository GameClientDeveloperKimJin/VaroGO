using UnityEngine;
using VaroGO.Client;
using VaroGO.Shared;

namespace HiddenPath.Blockout
{
    // Presentation only: RevealablePlatform and the switch still own collision and expiry.
    [RequireComponent(typeof(RevealablePlatform))]
    public sealed class Z4ExpiryWarning : MonoBehaviour
    {
        public float warningSeconds = 3f;
        public float slowFrequency = 0.65f;
        public float fastFrequency = 2.5f;
        [Range(0f, 1f)] public float minimumBrightness = 0.18f;
        public PlayerRole hiddenHintRole = PlayerRole.A_Red;
        public float RemainingSeconds { get; private set; }
        public float CurrentFrequency { get; private set; }
        public float Brightness { get; private set; } = 1f;
        Renderer[] surfaces;
        Color[] colors;
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            surfaces = GetComponentsInChildren<Renderer>(true);
            colors = new Color[surfaces.Length];
            block = new MaterialPropertyBlock();
            for (int i = 0; i < surfaces.Length; i++)
            {
                var m = surfaces[i].sharedMaterial;
                colors[i] = m != null && m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : Color.white;
            }
        }

        ISwitchState cachedState;
        bool cachedOnline;
        bool resolved;

        // Resolve calls GetComponents, so cache it and only re-resolve when the online mode changes.
        ISwitchState State()
        {
            if (!resolved || cachedOnline != GameServices.IsOnline)
            {
                cachedOnline = GameServices.IsOnline;
                cachedState = SwitchStates.Resolve(gameObject, null, this);
                resolved = true;
            }
            return cachedState;
        }

        void LateUpdate()
        {
            var state = State();
            var details = state as ISwitchDetails;
            Brightness = 1f;
            CurrentFrequency = 0f;
            RemainingSeconds = 0f;
            if (state != null && state.IsOn && details != null && details.AutoOffSeconds > 0f)
            {
                RemainingSeconds = Mathf.Max(0, details.AutoOffSeconds - (float)(SwitchStates.Now - details.LastOnTime));
                float window = Mathf.Min(warningSeconds, details.AutoOffSeconds * 0.5f);
                if (window > 0 && RemainingSeconds < window)
                {
                    float elapsed = window - RemainingSeconds;
                    float slope = (fastFrequency - slowFrequency) / window;
                    CurrentFrequency = slowFrequency + slope * elapsed;
                    // Integral of frequency: phase remains continuous while the pulses accelerate.
                    float phase = 2f * Mathf.PI * (slowFrequency * elapsed + 0.5f * slope * elapsed * elapsed);
                    Brightness = Mathf.Lerp(minimumBrightness, 1f, 0.5f + 0.5f * Mathf.Cos(phase));
                }
            }
            for (int i = 0; i < surfaces.Length; i++)
            {
                var r = surfaces[i];
                if (r == null) continue;
                // Only the scout sees an unrevealed outline. A revealed platform is shared.
                r.enabled = state != null && state.IsOn || SwitchStates.LocalRole == hiddenHintRole;
                r.GetPropertyBlock(block);
                // Keep the existing reveal/fade alpha. Never multiply last frame's RGB.
                float alpha = block.GetColor(BaseColor).a;
                Color c = colors[i] * Brightness;
                c.a = alpha;
                block.SetColor(BaseColor, c);
                block.SetColor(ColorId, c);
                block.SetColor(Emission, colors[i] * (0.2f * Brightness));
                r.SetPropertyBlock(block);
            }
        }
    }
}
