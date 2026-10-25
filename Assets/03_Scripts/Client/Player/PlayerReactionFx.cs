using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Shared;

namespace VaroGO.Client
{
    // C-15: 날아갈 때 비명, 착지 먼지, 부활 반짝임. EventRaised만 보므로
    // 서버가 PlayRemoteEvent로 전달하면 원격 플레이어도 똑같이 재생한다.
    // 친구가 날아가는 모습을 보는 게 코미디의 절반이다.
    public class PlayerReactionFx : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("끄면 원격 플레이어에게만 재생한다")]
        [SerializeField] bool playOnLocal = true;

        [Header("파티클 (월드 공간, Emit으로 터뜨린다)")]
        [SerializeField] ParticleSystem dust;
        [Tooltip("보통 착지 때 먼지 수")]
        [SerializeField] int dustCount = 6;
        [Tooltip("강한 넉백 뒤 착지 때 먼지 수")]
        [SerializeField] int bigDustCount = 24;
        [SerializeField] ParticleSystem respawnSparkle;
        [SerializeField] int sparkleCount = 30;
        [Tooltip("부활 반짝임 높이 (발밑 기준)")]
        [SerializeField] float sparkleHeight = 1f;

        [Header("소리 (비어 있으면 임시 합성음)")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip screamClip;
        [SerializeField] AudioClip heavyLandClip;
        [SerializeField] AudioClip lightHitClip;
        [SerializeField] AudioClip respawnClip;
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.7f;

        bool flying;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (screamClip == null) screamClip = TempSfx.Scream;
            if (heavyLandClip == null) heavyLandClip = TempSfx.Thud;
            if (lightHitClip == null) lightHitClip = TempSfx.Oof;
            if (respawnClip == null) respawnClip = TempSfx.Chime;
        }

        void OnEnable()
        {
            if (player != null) player.EventRaised += OnPlayerEvent;
        }

        void OnDisable()
        {
            if (player != null) player.EventRaised -= OnPlayerEvent;
            flying = false;
        }

        void OnPlayerEvent(PlayerEvent e)
        {
            if (player.IsLocal && !playOnLocal) return;
            switch (e)
            {
                case PlayerEvent.KnockbackHeavy:
                    flying = true;
                    Play(screamClip);
                    break;
                case PlayerEvent.KnockbackLight:
                    Play(lightHitClip);
                    break;
                case PlayerEvent.Landed:
                    Emit(dust, transform.position, flying ? bigDustCount : dustCount);
                    if (flying) Play(heavyLandClip);
                    flying = false;
                    break;
                case PlayerEvent.Died:
                    flying = false;
                    if (audioSource != null) audioSource.Stop(); // 떨어지며 비명이 계속 나지 않게
                    break;
                case PlayerEvent.Respawned:
                    Emit(respawnSparkle, transform.position + Vector3.up * sparkleHeight, sparkleCount);
                    Play(respawnClip);
                    break;
            }
        }

        static void Emit(ParticleSystem ps, Vector3 position, int count)
        {
            if (ps == null || count <= 0) return;
            var emit = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            ps.Emit(emit, count);
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 오프라인 원격 테스트: F7 = 원격 플레이어 날리기(1초 뒤 착지), F8 = 원격 플레이어 부활 연출
        void Update()
        {
            if (GameServices.IsOnline || player == null || player.IsLocal) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f7Key.wasPressedThisFrame) StartCoroutine(DebugFly());
            if (keyboard.f8Key.wasPressedThisFrame) player.PlayRemoteEvent(PlayerEvent.Respawned);
        }

        IEnumerator DebugFly()
        {
            player.PlayRemoteEvent(PlayerEvent.KnockbackHeavy);
            yield return new WaitForSeconds(1f);
            player.PlayRemoteEvent(PlayerEvent.Landed);
        }
#endif
    }
}
