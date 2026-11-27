using UnityEngine;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// UI 부품을 천천히 떠다니게 한다 (열기구·구름 덩이 등). 로컬 연출, timeScale 무관.
    /// 좌우 이동: Wrap = 한 방향으로 계속 흘러가다 화면 밖으로 나가면 반대편에서 다시 나옴 / PingPong = 좁은 범위에서 왕복.
    /// 위아래 흔들림 + 살짝 기울기를 더한다. 씬에 놓인 위치가 기준점이다.
    /// </summary>
    public class UIDrift : MonoBehaviour
    {
        public enum Move { None, Wrap, PingPong }

        [Header("좌우")]
        [SerializeField] Move move = Move.PingPong;
        [Tooltip("Wrap: 초당 이동(px, 음수면 왼쪽). PingPong: 왕복 거리(px, 기준점에서 ±)")]
        [SerializeField] float horizontal = 40f;
        [Tooltip("PingPong 한 번 왕복 시간(초)")]
        [SerializeField] float pingPongPeriod = 24f;
        [Tooltip("Wrap: 화면 밖 여유(px). 부품이 완전히 사라진 뒤 반대편에서 나온다")]
        [SerializeField] float wrapMargin = 40f;

        [Header("위아래")]
        [SerializeField] float bobAmount = 8f;
        [SerializeField] float bobPeriod = 5f;

        [Header("기울기")]
        [SerializeField] float tiltDegrees = 3f;
        [SerializeField] float tiltPeriod = 6.5f;

        [Tooltip("켜면 시작 위상을 무작위로 (같은 부품 여러 개가 맞춰 움직이지 않게)")]
        [SerializeField] bool randomPhase = true;
        [Range(0f, 1f)]
        [SerializeField] float phase = 0f;

        RectTransform rt;
        Vector2 basePos;
        Quaternion baseRot;
        float wrapX;
        float t0;

        void Awake()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
            baseRot = rt.localRotation;
            if (randomPhase) phase = Random.value;
        }

        void OnDisable()
        {
            if (rt == null) return;
            rt.anchoredPosition = basePos;
            rt.localRotation = baseRot;
        }

        void LateUpdate()
        {
            float t = Time.unscaledTime + phase * 100f;
            var off = Vector2.zero;

            switch (move)
            {
                case Move.PingPong:
                    if (pingPongPeriod > 0f) off.x = Mathf.Sin(t / pingPongPeriod * Mathf.PI * 2f) * horizontal;
                    break;
                case Move.Wrap:
                    wrapX += horizontal * Time.unscaledDeltaTime;
                    off.x = wrapX;
                    WrapAround(ref off.x);
                    break;
            }
            if (bobPeriod > 0f) off.y = Mathf.Sin(t / bobPeriod * Mathf.PI * 2f) * bobAmount;
            rt.anchoredPosition = basePos + off;

            if (tiltPeriod > 0f && tiltDegrees != 0f)
                rt.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t / tiltPeriod * Mathf.PI * 2f) * tiltDegrees);
        }

        // 부모 영역 밖으로 완전히 나가면 반대편 바깥으로 옮긴다
        void WrapAround(ref float x)
        {
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            float half = parent.rect.width * 0.5f;
            float self = rt.rect.width * Mathf.Abs(rt.localScale.x) * 0.5f + wrapMargin;
            // 부모 중심 기준 현재 x (앵커가 가운데가 아니어도 대략 맞도록 localPosition 사용)
            float center = rt.localPosition.x - rt.anchoredPosition.x + basePos.x + x;
            float span = (half + self) * 2f;
            if (horizontal > 0f && center > half + self) { wrapX -= span; x -= span; }
            else if (horizontal < 0f && center < -half - self) { wrapX += span; x += span; }
        }
    }
}
