using UnityEngine;
using UnityEngine.UI;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// 바람에 펄럭이는 깃발 천 (UI). Image 대신 이 컴포넌트에 깃발 스프라이트를 넣는다.
    /// 천을 잘게 나눈 격자를 매 프레임 물결 모양으로 휜다 — 깃대 쪽 변은 고정, 끝으로 갈수록 크게 흔들린다.
    /// 셰이더 없이 기본 UI 머티리얼로 그린다. 로컬 연출, timeScale 무관.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIFlag : MaskableGraphic
    {
        public enum PoleSide { Left, Right, Top, Bottom }

        [SerializeField] Sprite sprite;
        [Tooltip("깃대가 붙은 변")]
        [SerializeField] PoleSide pole = PoleSide.Left;
        [Tooltip("가로(깃대→끝) 나눔 수. 클수록 부드러움")]
        [SerializeField, Range(2, 48)] int segments = 16;
        [Tooltip("끝부분 흔들림 크기(px)")]
        [SerializeField] float amplitude = 10f;
        [Tooltip("천 위에 걸리는 물결 수")]
        [SerializeField] float waves = 1.3f;
        [Tooltip("초당 물결 속도")]
        [SerializeField] float speed = 1.6f;
        [Tooltip("깃발마다 다르게 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] float phase = 0f;
        [Tooltip("물결 따라 천이 깃대 쪽으로 살짝 줄어드는 정도 (0~1)")]
        [Range(0f, 0.3f)]
        [SerializeField] float squash = 0.06f;

        public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

        public Sprite Sprite
        {
            get => sprite;
            set { sprite = value; SetAllDirty(); }
        }

        void Update()
        {
            if (Application.isPlaying) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            Vector4 uv = sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
            float t = Application.isPlaying ? Time.unscaledTime : 0f;
            bool horizontal = pole == PoleSide.Left || pole == PoleSide.Right;
            int n = Mathf.Max(2, segments);
            Color32 c = color;

            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n;                       // 0 = 깃대, 1 = 끝
                float s = (pole == PoleSide.Left || pole == PoleSide.Bottom) ? a : 1f - a; // 사각형 안 비율(왼→오 / 아래→위)
                float w = a * a * (3f - 2f * a) * 0.65f + a * 0.35f; // 깃대 쪽 0 → 끝 1 (부드럽게 커짐)
                float wave = Mathf.Sin((a * waves - t * speed - phase) * Mathf.PI * 2f);
                float disp = wave * amplitude * w;
                float shrink = 1f - squash * w * (0.5f + 0.5f * wave);

                // 깃대 쪽 기준으로 길이를 살짝 줄인다
                float sPos = (pole == PoleSide.Left || pole == PoleSide.Bottom) ? s * shrink : 1f - (1f - s) * shrink;

                Vector2 p0, p1; Vector2 uv0, uv1;
                if (horizontal)
                {
                    float x = Mathf.Lerp(r.xMin, r.xMax, sPos);
                    p0 = new Vector2(x, r.yMin + disp);
                    p1 = new Vector2(x, r.yMax + disp);
                    float u = Mathf.Lerp(uv.x, uv.z, s);
                    uv0 = new Vector2(u, uv.y); uv1 = new Vector2(u, uv.w);
                }
                else
                {
                    float y = Mathf.Lerp(r.yMin, r.yMax, sPos);
                    p0 = new Vector2(r.xMin + disp, y);
                    p1 = new Vector2(r.xMax + disp, y);
                    float v = Mathf.Lerp(uv.y, uv.w, s);
                    uv0 = new Vector2(uv.x, v); uv1 = new Vector2(uv.z, v);
                }
                // 물결 경사에 따라 살짝 밝기 변화 (주름 음영)
                float shade = 1f - 0.12f * w * Mathf.Cos((a * waves - t * speed - phase) * Mathf.PI * 2f);
                var cc = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), c.a);
                vh.AddVert(p0, cc, uv0);
                vh.AddVert(p1, cc, uv1);
            }
            for (int i = 0; i < n; i++)
            {
                int k = i * 2;
                vh.AddTriangle(k, k + 1, k + 3);
                vh.AddTriangle(k, k + 3, k + 2);
            }
        }

        public override void SetNativeSize()
        {
            if (sprite == null) return;
            float refPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            rectTransform.sizeDelta = sprite.rect.size * (refPpu / sprite.pixelsPerUnit);
        }
    }
}
