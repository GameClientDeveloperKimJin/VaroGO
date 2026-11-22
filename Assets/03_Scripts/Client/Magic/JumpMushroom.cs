using UnityEngine;

namespace VaroGO.Client
{
    // P18: 밟으면 높이 튕겨 주는 점프 버섯. 판정은 밟은 사람의 PC에서 한다.
    // 투명 버섯: MagicVisibility(Red/Blue)를 같이 붙이면 한 역할에게만 보인다 (충돌·튕김은 둘 다)
    public class JumpMushroom : MonoBehaviour
    {
        [Tooltip("튕겨 올라가는 속도 (m/s). 16 ≈ 5m 높이 (중력 -25 기준)")]
        [SerializeField] float launchSpeed = 16f;
        [Tooltip("Optional sideways launch in local space (m/s). Zero preserves the normal vertical bounce.")]
        [SerializeField] Vector3 lateralLaunchVelocity;
        [Tooltip("밟힐 때 찌그러지는 모델 (비우면 연출 없음)")]
        [SerializeField] Transform squashVisual;
        [Tooltip("찌그러지는 정도. 0.3 = 높이 30% 줄어듦")]
        [SerializeField, Range(0f, 0.9f)] float squashAmount = 0.3f;
        [Tooltip("찌그러졌다 돌아오는 시간 (초)")]
        [SerializeField] float squashTime = 0.25f;

        Vector3 baseScale;
        float squashStart = float.NegativeInfinity;

        public float LaunchSpeed => launchSpeed;
        public Vector3 LateralLaunchVelocity => Vector3.ProjectOnPlane(transform.TransformDirection(lateralLaunchVelocity), Vector3.up);

        void Awake()
        {
            if (squashVisual != null) baseScale = squashVisual.localScale;
        }

        public void PlayBounce() => squashStart = Time.time;

        // 원격 플레이어가 밟은 버섯은 이 PC에서 판정하지 않으므로, 날아간 위치 근처의 가장 가까운 버섯을 찌그러뜨린다
        static readonly System.Collections.Generic.List<JumpMushroom> all = new System.Collections.Generic.List<JumpMushroom>();
        const float RemoteBounceRadius = 2.5f;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static bool PlayNearest(Vector3 position)
        {
            var best = FindNearest(position);
            if (best == null) return false;
            best.PlayBounce();
            return true;
        }

        public static JumpMushroom FindNearest(Vector3 position)
        {
            JumpMushroom best = null;
            float bestSqr = RemoteBounceRadius * RemoteBounceRadius;
            foreach (var m in all)
            {
                if (m == null) continue;
                float d = (m.transform.position - position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = m; }
            }
            return best;
        }

        void Update()
        {
            if (squashVisual == null) return;
            float t = squashTime > 0f ? (Time.time - squashStart) / squashTime : 1f;
            if (t >= 1f)
            {
                if (squashVisual.localScale != baseScale) squashVisual.localScale = baseScale;
                return;
            }
            float k = Mathf.Sin(t * Mathf.PI) * squashAmount;
            squashVisual.localScale = new Vector3(baseScale.x * (1f + k * 0.5f), baseScale.y * (1f - k), baseScale.z * (1f + k * 0.5f));
        }
    }
}
