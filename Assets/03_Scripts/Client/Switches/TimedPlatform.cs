using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-11: 상태가 on인 동안만 Collider와 Renderer가 켜진다. 꺼지기 직전에 깜빡인다.
    // 남은 시간은 ISwitchDetails(자동 꺼짐 시간)가 있을 때만 알 수 있다.
    // MagicVisibility와 같은 오브젝트에 붙이지 않는다 (둘 다 Renderer를 켜고 끈다).
    public class TimedPlatform : MonoBehaviour
    {
        [Tooltip("연결할 스위치 오브젝트 (상태 컴포넌트가 붙은 GameObject)")]
        [SerializeField] GameObject stateObject;
        [SerializeField, HideInInspector] MonoBehaviour stateSource; // 예전 참조. OnValidate에서 stateObject로 옮긴다
        [Tooltip("꺼지기 몇 초 전부터 깜빡일지")]
        [SerializeField] float blinkBefore = 1f;
        [Tooltip("초당 깜빡임 횟수")]
        [SerializeField] float blinkRate = 8f;

        ISwitchState state;
        Renderer[] renderers;
        Collider[] colliders;
        bool on;

        void Awake()
        {
            state = SwitchStates.Resolve(stateObject, stateSource, this);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        void OnValidate()
        {
            if (stateObject == null && stateSource != null) stateObject = stateSource.gameObject;
        }

        void OnEnable()
        {
            if (state == null) return;
            state.Changed += OnChanged;
            OnChanged(state.IsOn);
        }

        void OnDisable()
        {
            if (state != null) state.Changed -= OnChanged;
        }

        void OnChanged(bool isOn)
        {
            on = isOn;
            foreach (var c in colliders) c.enabled = isOn;
            SetRenderers(isOn);
        }

        void Update()
        {
            if (!on || !(state is ISwitchDetails details) || details.AutoOffSeconds <= 0f) return;
            double remaining = details.LastOnTime + details.AutoOffSeconds - SwitchStates.Now;
            bool visible = remaining > blinkBefore || Mathf.Repeat((float)remaining * blinkRate, 1f) > 0.5f;
            SetRenderers(visible);
        }

        void SetRenderers(bool visible)
        {
            foreach (var r in renderers)
                if (r.enabled != visible) r.enabled = visible;
        }
    }
}
