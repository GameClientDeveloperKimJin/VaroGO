using UnityEngine;
using UnityEngine.UI;

namespace VaroGO.Client
{
    /// <summary>
    /// 던지기 파워 게이지 (화면 HUD). 로컬 플레이어가 충전하는 동안 매 틱 Show(0~1)로 값을 알려 주고,
    /// 잠시 안 불리면 사라진다. 궤적 가이드라인은 표시하지 않는다.
    /// </summary>
    public class ThrowPowerGauge : MonoBehaviour
    {
        [Tooltip("채워지는 이미지 (Image Type = Filled)")]
        [SerializeField] Image fill;
        [Tooltip("켜고 끄는 묶음 (없으면 이 오브젝트의 첫 자식)")]
        [SerializeField] GameObject root;
        [Tooltip("파워 낮음 → 중간 → 최대 색")]
        [SerializeField] Color lowColor = new Color(0.45f, 0.85f, 0.35f);
        [SerializeField] Color midColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField] Color highColor = new Color(1f, 0.35f, 0.25f);
        [Tooltip("이 시간(초) 동안 Show()가 안 오면 숨긴다")]
        [SerializeField] float hideAfter = 0.15f;

        float lastShown = float.NegativeInfinity;
        float power;

        void Awake()
        {
            if (root == null && transform.childCount > 0) root = transform.GetChild(0).gameObject;
            if (root != null) root.SetActive(false);
        }

        public void Show(float value)
        {
            power = Mathf.Clamp01(value);
            lastShown = Time.time;
        }

        public void Hide() => lastShown = float.NegativeInfinity;

        void LateUpdate()
        {
            bool on = Time.time - lastShown <= hideAfter;
            if (root != null && root.activeSelf != on) root.SetActive(on);
            if (!on || fill == null) return;
            fill.fillAmount = power;
            fill.color = power < 0.5f ? Color.Lerp(lowColor, midColor, power * 2f) : Color.Lerp(midColor, highColor, (power - 0.5f) * 2f);
        }
    }
}
