using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// C2-11: 파트너가 화면 밖이면 화면 가장자리에 파트너 쪽을 가리키는 화살표와 거리(m)를 띄운다.
    /// 파트너가 카메라 뒤에 있으면 화면 투영이 뒤집히므로 방향을 반대로 돌린다. 화면 안이면 숨긴다.
    /// 씬에 따로 놓지 않는다. 게임 시작 시 스스로 하나 만들어 씬이 바뀌어도 유지한다.
    /// 그레이박스용 OnGUI 표시. UI 아트가 들어오면 교체한다.
    /// </summary>
    public class PartnerIndicator : MonoBehaviour
    {
        const float EdgeMargin = 48f;     // 화면 가장자리에서 띄우는 거리 (px)
        const float ArrowSize = 36f;
        const float HeadHeight = 1.2f;    // 파트너 발밑 대신 몸 가운데를 가리킨다
        const float ViewportPadding = 0.02f; // 화면 끝에 살짝 걸친 정도는 "화면 안"으로 본다

        static Texture2D arrowTex;

        bool visible;
        Vector2 guiPos;   // GUI 좌표 (왼쪽 위 0,0)
        float angle;      // 0 = 위쪽, 시계 방향
        float distance;
        Color color;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("PartnerIndicator");
            DontDestroyOnLoad(go);
            go.AddComponent<PartnerIndicator>();
        }

        void LateUpdate()
        {
            visible = false;
            var cam = Camera.main;
            PlayerController local = null, partner = null;
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsLocal) { if (local == null) local = all[i]; }
                else if (partner == null) partner = all[i];
            }
            if (cam == null || local == null || partner == null) return;

            Vector3 target = partner.transform.position + Vector3.up * HeadHeight;
            Vector3 vp = cam.WorldToViewportPoint(target);
            bool behind = vp.z < 0f;
            if (!behind && vp.x >= -ViewportPadding && vp.x <= 1f + ViewportPadding
                        && vp.y >= -ViewportPadding && vp.y <= 1f + ViewportPadding)
                return; // 화면 안 → 숨김

            // 화면 가운데 기준 방향 (위쪽이 +y). 카메라 뒤면 투영이 뒤집혀 있으니 반대로
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (behind) dir = -dir;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down; // 정확히 뒤 → 아래쪽에 표시
            dir.Normalize();

            // 화면 직사각형 가장자리까지 늘린다
            float halfW = Screen.width * 0.5f - EdgeMargin;
            float halfH = Screen.height * 0.5f - EdgeMargin;
            float k = Mathf.Min(Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                                Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
            Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + dir * k;

            guiPos = new Vector2(screen.x, Screen.height - screen.y);
            angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            distance = Vector3.Distance(local.transform.position, partner.transform.position);
            var localRole = SwitchStates.LocalRole;
            // 파트너 역할 색 (역할 미정이면 노랑)
            color = localRole == PlayerRole.A_Red ? new Color(0.3f, 0.55f, 1f)
                  : localRole == PlayerRole.B_Blue ? new Color(1f, 0.3f, 0.25f)
                  : new Color(0.95f, 0.85f, 0.3f);
            visible = true;
        }

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;
            if (arrowTex == null) arrowTex = MakeArrow(64);

            var prevColor = GUI.color;
            var prevMatrix = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, guiPos);
            GUI.DrawTexture(new Rect(guiPos.x - ArrowSize * 0.5f, guiPos.y - ArrowSize * 0.5f, ArrowSize, ArrowSize), arrowTex);
            GUI.matrix = prevMatrix;

            // 거리는 화살표보다 화면 안쪽에 쓴다
            Vector2 inward = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * (ArrowSize * 0.9f);
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            style.normal.textColor = color;
            GUI.Label(new Rect(guiPos.x + inward.x - 40f, guiPos.y + inward.y - 12f, 80f, 24f), $"{distance:0}m", style);
            GUI.color = prevColor;
        }

        // 위를 가리키는 흰 삼각형
        static Texture2D MakeArrow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "PartnerArrow", filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                // 텍스처 y는 아래가 0. 위쪽 끝이 뾰족하다
                float t = (float)y / (size - 1);
                float halfWidth = (1f - t) * 0.5f * size;
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - size * 0.5f);
                    float a = Mathf.Clamp01(halfWidth - dx + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
