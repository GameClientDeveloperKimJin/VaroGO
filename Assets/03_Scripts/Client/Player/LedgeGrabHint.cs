using UnityEngine;

namespace VaroGO.Client
{
    /// <summary>
    /// P18 붙잡기 점프 손 표시. 로컬 플레이어가 매 틱 Show()로 위치를 알려 주고, 잠시 안 불리면 사라진다.
    /// 카메라를 바라보고 살짝 커졌다 작아진다. 그림은 자식 SpriteRenderer 등 아무 렌더러나 쓴다.
    /// </summary>
    public class LedgeGrabHint : MonoBehaviour
    {
        [Tooltip("이 시간(초) 동안 Show()가 안 오면 숨긴다 (틱과 화면 프레임이 어긋나도 깜빡이지 않게)")]
        [SerializeField] float hideAfter = 0.15f;
        [Tooltip("나타날 때·사라질 때 커지고 작아지는 속도")]
        [SerializeField] float fadeSpeed = 10f;
        [Tooltip("두근거리는 크기 폭 (0 = 고정)")]
        [SerializeField] float pulseAmount = 0.08f;
        [Tooltip("두근거림 속도 (초당 횟수)")]
        [SerializeField] float pulseRate = 2f;

        Vector3 baseScale;
        float lastShown = float.NegativeInfinity;
        float visible;
        Renderer[] renderers;

        void Awake()
        {
            baseScale = transform.localScale;
            renderers = GetComponentsInChildren<Renderer>(true);
            Apply(0f);
        }

        public void Show(Vector3 position)
        {
            transform.position = position;
            lastShown = Time.time;
        }

        public void Hide() => lastShown = float.NegativeInfinity;

        void LateUpdate()
        {
            float target = Time.time - lastShown <= hideAfter ? 1f : 0f;
            visible = Mathf.MoveTowards(visible, target, fadeSpeed * Time.deltaTime);
            Apply(visible);
            if (visible <= 0f) return;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
        }

        void Apply(float v)
        {
            bool on = v > 0f;
            foreach (var r in renderers)
                if (r != null && r.enabled != on) r.enabled = on;
            if (!on) return;
            float pulse = 1f + Mathf.Sin(Time.time * pulseRate * Mathf.PI * 2f) * pulseAmount;
            transform.localScale = baseScale * (Mathf.SmoothStep(0f, 1f, v) * pulse);
        }
    }
}
