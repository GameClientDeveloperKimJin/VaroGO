using UnityEngine;

namespace VaroGO.Client.Voice
{
    /// <summary>
    /// 상대 목소리 AudioSource에 공간 음성을 적용한다 (설계서 06~08).
    /// - 거리: 2m 이내 100%, 2~15m 부드럽게 줄어듦, 15m 이상 30%에서 멈춤 (무음 구간 없음)
    /// - 방향: Spatial Blend 1 (3D)
    /// - 벽 가림: 0.1초마다 리스너→상대 Linecast, 막히면 고음을 서서히 깎음 (AudioLowPassFilter)
    /// - 울림: 레벨에 AudioReverbZone을 놓으면 Unity가 알아서 적용 (코드 없음)
    /// - 상대 음성 볼륨: 곡선 위에 곱함 (AudioSource.volume)
    /// </summary>
    public class VoiceSpatialAudio
    {
        public const float FullVolumeDistance = 2f;
        public const float MaxDistance = 15f;
        public const float MinVolume = 0.3f;

        const float OcclusionInterval = 0.1f;
        const float OpenCutoff = 22000f;
        const float BlockedCutoff = 1500f;
        const float CutoffLerpSpeed = 6f;   // 초당 (지수 보간 계수)

        static AnimationCurve rolloff;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        AudioSource source;
        AudioLowPassFilter lowPass;
        float nextOcclusionTime;
        bool occluded;

        public static AnimationCurve Rolloff
        {
            get
            {
                if (rolloff == null)
                {
                    // Custom Rolloff 곡선의 가로축은 거리/MaxDistance (0~1). MaxDistance 밖은 마지막 값(0.3)으로 유지된다
                    float t = FullVolumeDistance / MaxDistance;
                    rolloff = new AnimationCurve(
                        new Keyframe(0f, 1f, 0f, 0f),
                        new Keyframe(t, 1f, 0f, 0f),
                        new Keyframe(1f, MinVolume, 0f, 0f));
                }
                return rolloff;
            }
        }

        public void Update(AudioSource partnerAudio, Vector3? listenerPos, float volume)
        {
            if (partnerAudio != source) Attach(partnerAudio);
            if (source == null) return;

            source.volume = volume;
            UpdateOcclusion(listenerPos);
        }

        void Attach(AudioSource s)
        {
            source = s;
            lowPass = null;
            occluded = false;
            if (s == null) return;

            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Custom;
            s.minDistance = FullVolumeDistance;
            s.maxDistance = MaxDistance;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            s.dopplerLevel = 0f;
            s.spread = 0f;

            lowPass = s.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = s.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = OpenCutoff;
        }

        void UpdateOcclusion(Vector3? listenerPos)
        {
            if (lowPass == null) return;
            if (listenerPos.HasValue && Time.unscaledTime >= nextOcclusionTime)
            {
                nextOcclusionTime = Time.unscaledTime + OcclusionInterval;
                occluded = IsBlocked(listenerPos.Value, source.transform.position);
            }
            float target = occluded ? BlockedCutoff : OpenCutoff;
            float k = 1f - Mathf.Exp(-CutoffLerpSpeed * Time.unscaledDeltaTime);
            lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, target, k);
        }

        // 플레이어 몸·트리거는 벽으로 치지 않는다
        static bool IsBlocked(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return false;
            int n = Physics.RaycastNonAlloc(from, dir / dist, hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i].collider.GetComponentInParent<PlayerController>() == null) return true;
            return false;
        }
    }
}
