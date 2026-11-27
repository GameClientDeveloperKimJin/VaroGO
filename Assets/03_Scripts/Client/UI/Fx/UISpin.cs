using UnityEngine;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// UI 부품을 계속 돌린다 (풍차 날개 등). 피벗이 회전축 — 날개 그림 중심을 축에 맞춘다. 로컬 연출, timeScale 무관.
    /// </summary>
    public class UISpin : MonoBehaviour
    {
        [Tooltip("초당 각도. 음수면 시계 방향")]
        [SerializeField] float degreesPerSecond = -30f;
        [Tooltip("속도가 살짝 빨라졌다 느려졌다 (바람 느낌). 0이면 일정")]
        [Range(0f, 1f)]
        [SerializeField] float gust = 0.25f;
        [SerializeField] float gustPeriod = 7f;

        float angle;

        void Awake() => angle = transform.localEulerAngles.z;

        void LateUpdate()
        {
            float k = 1f;
            if (gust > 0f && gustPeriod > 0f) k += Mathf.Sin(Time.unscaledTime / gustPeriod * Mathf.PI * 2f) * gust;
            angle += degreesPerSecond * k * Time.unscaledDeltaTime;
            angle = Mathf.Repeat(angle, 360f);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
