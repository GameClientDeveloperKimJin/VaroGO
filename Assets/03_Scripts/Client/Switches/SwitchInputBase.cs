using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-11 입력 쪽 공통: 상태 연결, 역할 색 표시, 권한 없을 때 "삑".
    // 원격 플레이어는 요청을 보내지 않는다 (온라인 중복 요청 방지) → 로컬 PlayerController만 다룬다.
    public abstract class SwitchInputBase : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("ISwitchState를 구현한 컴포넌트 (LocalSwitchState 등)")]
        [SerializeField] protected MonoBehaviour stateSource;
        [Tooltip("누를 수 있는 역할 색으로 칠할 렌더러. 켜지면 밝아진다")]
        [SerializeField] Renderer indicator;
        [SerializeField] AudioSource audioSource;
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.6f;

        protected ISwitchState state;
        MaterialPropertyBlock block;

        protected virtual void Awake()
        {
            state = SwitchStates.Resolve(stateSource, this);
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        protected virtual void OnEnable()
        {
            if (state != null) state.Changed += OnStateChanged;
            RefreshIndicator();
        }

        protected virtual void OnDisable()
        {
            if (state != null) state.Changed -= OnStateChanged;
        }

        void OnStateChanged(bool on)
        {
            RefreshIndicator();
            Play(TempSfx.Click);
        }

        protected static PlayerController LocalPlayerOf(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            return player != null && player.IsLocal ? player : null;
        }

        // 권한이 없으면 삑 소리만 내고 false
        protected bool TryRequest(bool on)
        {
            if (state == null) return false;
            var role = SwitchStates.LocalRole;
            if (!SwitchStates.CanUse(state, role))
            {
                Play(TempSfx.Beep);
                return false;
            }
            state.RequestSet(on, role);
            return true;
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null) audioSource.PlayOneShot(clip, volume);
            else AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

        void RefreshIndicator()
        {
            if (indicator == null) return;
            var role = state is ISwitchDetails details ? details.AllowedRole : PlayerRole.None;
            Color c = role == PlayerRole.A_Red ? new Color(1f, 0.25f, 0.2f)
                    : role == PlayerRole.B_Blue ? new Color(0.25f, 0.5f, 1f)
                    : new Color(0.9f, 0.85f, 0.3f);
            bool on = state != null && state.IsOn;
            block ??= new MaterialPropertyBlock();
            indicator.GetPropertyBlock(block);
            block.SetColor(BaseColorId, on ? c : c * 0.45f);
            block.SetColor(EmissionColorId, on ? c * 1.5f : Color.black);
            indicator.SetPropertyBlock(block);
        }
    }
}
