using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // P18: 돌멩이에 맞으면 드러나는 숨겨진 발판. 반투명으로 나타나 서서히 불투명해지고, 일정 시간 뒤 사라진다.
    // 켜짐/꺼짐은 같은 GameObject의 LocalSwitchState(온라인: NetSwitchState)에 맡긴다 → 두 PC가 같은 상태.
    // 보이는 시간 = LocalSwitchState의 autoOffSeconds (0이면 한 번 드러나면 계속 보임).
    // 재질은 투명(Transparent) URP 재질이어야 서서히 나타난다 (예: 04_Resources/Material/Client/Level/RevealablePlatform_Transparent)
    [RequireComponent(typeof(LocalSwitchState))]
    public class RevealablePlatform : MonoBehaviour
    {
        [Tooltip("숨어 있을 때 투명도. 0 = 완전히 안 보임, 0.1 = 아주 희미하게 힌트")]
        [SerializeField, Range(0f, 1f)] float hiddenAlpha = 0f;
        [Tooltip("맞은 순간 투명도 (반투명)")]
        [SerializeField, Range(0f, 1f)] float revealAlpha = 0.35f;
        [Tooltip("반투명 → 불투명까지 걸리는 시간 (초)")]
        [SerializeField] float fadeInTime = 0.6f;
        [Tooltip("사라질 때 걸리는 시간 (초). 보이는 시간이 끝나기 이 시간 전부터 흐려진다")]
        [SerializeField] float fadeOutTime = 0.8f;
        [Tooltip("켜면 숨어 있어도 밟을 수 있다 (보이지 않는 발판). 끄면 드러났을 때만 밟힌다")]
        [SerializeField] bool solidWhenHidden = false;
        [Tooltip("모양을 그리는 렌더러. 비우면 자식 전체")]
        [SerializeField] Renderer[] renderers;
        [Tooltip("밟는 콜라이더. 비우면 자식 전체 (트리거 제외)")]
        [SerializeField] Collider[] colliders;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        ISwitchState state;
        ISwitchDetails details;
        bool subscribed;
        MaterialPropertyBlock mpb;
        Color[] baseColors;
        float alpha = -1f;
        bool solid = true;
        float offSince;
        float offAlpha;

        public bool Revealed => State != null && State.IsOn;

        ISwitchState State
        {
            get
            {
                if (state == null)
                {
                    state = SwitchStates.Resolve(gameObject, null, this);
                    details = state as ISwitchDetails;
                    if (state != null && !subscribed)
                    {
                        state.Changed += OnChanged;
                        subscribed = true;
                    }
                }
                return state;
            }
        }

        void Awake()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            if (colliders == null || colliders.Length == 0)
            {
                var found = GetComponentsInChildren<Collider>(true);
                var list = new System.Collections.Generic.List<Collider>();
                foreach (var c in found) if (!c.isTrigger) list.Add(c);
                colliders = list.ToArray();
            }
            mpb = new MaterialPropertyBlock();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i] != null ? renderers[i].sharedMaterial : null;
                baseColors[i] = m == null ? Color.white : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
            }
        }

        void OnEnable() => GameServices.Registered += ResetState;

        void OnDisable()
        {
            GameServices.Registered -= ResetState;
            ResetState();
        }

        void ResetState()
        {
            if (state != null && subscribed) state.Changed -= OnChanged;
            state = null;
            details = null;
            subscribed = false;
        }

        // 돌멩이가 맞으면 권한 PC에서 호출. 이미 보이는 중이면 시간이 다시 채워진다
        public void Reveal(PlayerRole by) => State?.RequestSet(true, by);

        void OnChanged(bool on)
        {
            if (!on)
            {
                offSince = Time.time;
                offAlpha = Mathf.Max(alpha, 0f);
            }
        }

        void Update()
        {
            var s = State;
            float target;
            if (s != null && s.IsOn)
            {
                float elapsed = details != null ? (float)(SwitchStates.Now - details.LastOnTime) : fadeInTime;
                target = Mathf.Lerp(revealAlpha, 1f, fadeInTime > 0f ? Mathf.Clamp01(elapsed / fadeInTime) : 1f);
                float duration = details != null ? details.AutoOffSeconds : 0f;
                if (duration > 0f && fadeOutTime > 0f)
                {
                    float remaining = duration - elapsed;
                    if (remaining < fadeOutTime) target = Mathf.Min(target, Mathf.Lerp(hiddenAlpha, 1f, Mathf.Clamp01(remaining / fadeOutTime)));
                }
            }
            else
            {
                // 시간이 다 되기 전에 꺼진 경우(구역 리셋 등)도 부드럽게 사라진다
                float k = fadeOutTime > 0f ? Mathf.Clamp01((Time.time - offSince) / fadeOutTime) : 1f;
                target = alpha < 0f ? hiddenAlpha : Mathf.Lerp(offAlpha, hiddenAlpha, k);
                if (alpha >= 0f && alpha <= hiddenAlpha + 0.001f) target = hiddenAlpha;
            }
            SetAlpha(target);
            SetSolid(solidWhenHidden || (s != null && s.IsOn));
        }

        void SetAlpha(float a)
        {
            if (Mathf.Abs(a - alpha) < 0.002f) return;
            alpha = a;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                bool show = a > 0.01f;
                if (r.enabled != show) r.enabled = show;
                if (!show) continue;
                Color c = baseColors[i];
                c.a *= a;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(mpb);
            }
        }

        // 숨어 있으면 트리거로 바꿔 돌멩이 판정은 남기고 사람은 통과시킨다
        void SetSolid(bool value)
        {
            if (solid == value) return;
            solid = value;
            foreach (var c in colliders)
                if (c != null) c.isTrigger = !value;
        }
    }
}
