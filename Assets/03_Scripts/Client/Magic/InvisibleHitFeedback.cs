using UnityEngine;

namespace VaroGO.Client
{
    // C-07: 안 보이는 마법에 닿으면 접점에 작은 반짝임과 "통" 소리를 낸다. 윤곽은 드러내지 않는다.
    // Player 프리팹에 붙인다. 로컬 플레이어만 이벤트가 오므로 원격에서는 아무것도 하지 않는다.
    public class InvisibleHitFeedback : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("월드 공간 파티클. Emit으로 접점에서 터뜨린다")]
        [SerializeField] ParticleSystem sparkle;
        [SerializeField] int sparkleCount = 12;
        [SerializeField] AudioSource audioSource;
        [Tooltip("비어 있으면 임시 합성음을 쓴다")]
        [SerializeField] AudioClip landClip;
        [Tooltip("비어 있으면 임시 합성음을 쓴다")]
        [SerializeField] AudioClip wallClip;
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.6f;

        static AudioClip fallbackLand;
        static AudioClip fallbackWall;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (sparkle == null) sparkle = GetComponentInChildren<ParticleSystem>(true);
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (landClip == null) landClip = fallbackLand ??= TempSfx.Make("InvisibleLand", 140f, 0.12f);
            if (wallClip == null) wallClip = fallbackWall ??= TempSfx.Make("InvisibleWall", 320f, 0.08f);
        }

        void OnEnable()
        {
            if (player != null) player.InvisibleHit += OnInvisibleHit;
        }

        void OnDisable()
        {
            if (player != null) player.InvisibleHit -= OnInvisibleHit;
        }

        void OnInvisibleHit(Vector3 point, Vector3 normal, bool landing)
        {
            if (sparkle != null)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = point + normal * 0.05f,
                    applyShapeToPosition = true,
                };
                sparkle.Emit(emit, sparkleCount);
            }
            if (audioSource != null) audioSource.PlayOneShot(landing ? landClip : wallClip, volume);
        }
    }
}
