using UnityEngine;
using UnityEngine.UI;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// RawImage 텍스처를 계속 흘린다 (폭포 물줄기 등). 텍스처 Wrap Mode가 Repeat여야 이음새 없이 이어진다.
    /// 로컬 연출, timeScale 무관.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class UIUvScroll : MonoBehaviour
    {
        [Tooltip("초당 UV 이동. 폭포는 y를 양수로 (아래로 흐름)")]
        [SerializeField] Vector2 speed = new(0f, 0.35f);

        RawImage raw;
        Rect baseUv;

        void Awake()
        {
            raw = GetComponent<RawImage>();
            baseUv = raw.uvRect;
            if (raw.texture != null && raw.texture.wrapMode != TextureWrapMode.Repeat)
                Debug.LogWarning($"[UIUvScroll] {name}: 텍스처 Wrap Mode를 Repeat로 해야 이음새가 안 보인다", this);
        }

        void OnDisable()
        {
            if (raw != null) raw.uvRect = baseUv;
        }

        void LateUpdate()
        {
            var uv = baseUv;
            uv.x = Mathf.Repeat(baseUv.x + Time.unscaledTime * speed.x, 1f);
            uv.y = Mathf.Repeat(baseUv.y + Time.unscaledTime * speed.y, 1f);
            raw.uvRect = uv;
        }
    }
}
