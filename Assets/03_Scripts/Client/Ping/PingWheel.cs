using UnityEngine;
using VaroGO.Shared;

namespace VaroGO.Client
{
    /// <summary>
    /// 핑 입력. 핑 키(마우스 휠 클릭 / 패드 RB)를 누르고 있으면 원형 메뉴가 열린다.
    ///   마우스(패드는 오른쪽 스틱)를 위로 = 여기 밟아!, 아래로 = 위험!
    ///   키를 떼면 화면 가운데 조준점 위치에 고른 핑을 찍는다. 가운데에서 떼면 취소
    /// 핑을 찍으면 쿨타임 동안 다시 못 찍는다. 남은 시간은 원형 메뉴 가운데에 작게 보인다.
    /// 메뉴가 열려 있는 동안 카메라는 멈추고 이동은 그대로 된다.
    ///
    /// 조준: 카메라 가운데에서 레이를 쏜다. 마법 오브젝트는 안 보여도 콜라이더가 항상 켜져 있어서(계약 R1)
    /// 내 눈에만 보이는 발판에 찍어도 그 자리(발판 윗면)에 핑이 생기고, 상대 화면에는 허공에 뜬다.
    /// 내 캐릭터와 트리거(체크포인트·킬존 등)는 무시한다.
    /// PingMarkers가 함께 만든다.
    /// </summary>
    public class PingWheel : MonoBehaviour
    {
        const float Cooldown = 2f;           // 핑을 찍은 뒤 다음 핑까지 (초)
        const float MaxDistance = 80f;       // 핑을 찍을 수 있는 최대 거리 (m)
        const float WheelRadius = 110f;      // 원형 메뉴 크기 (px)
        const float SelectDeadzone = 30f;    // 가운데에서 이만큼은 움직여야 고른 것으로 본다 (px)
        const float WalkableNormalY = 0.6f;  // 이보다 평평하면 바닥으로 본다
        const float AirDistance = 20f;       // 조준점에 아무것도 없을 때 내 캐릭터에서 이만큼 앞 허공에 띄운다 (m)
        const float StickDeadzone = 0.3f;    // 패드 스틱을 이만큼은 기울여야 메뉴 커서가 움직인다
        const int IgnoreRaycastLayer = 2;

        static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        PlayerController local;
        PlayerInputReader input;
        bool wasHeld;
        bool wheelOpen;
        Vector2 wheelCursor;   // 원형 메뉴 안 선택 위치 (px, 위쪽이 +y)
        float lastPingTime = float.NegativeInfinity;
        string failText;       // 핑을 못 찍었을 때 잠깐 보여줄 이유
        float failUntil;

        static Texture2D circleTex, dotTex;
        GUIStyle wheelStyle, failStyle;

        void Update()
        {
            RefreshLocal();
            bool canPing = local != null && input != null && input.enabled && !local.IsDead
                           && !PlayerInputReader.Blocked && !PlayerInputReader.MenuOpen && !PlayerInputReader.PauseOpen;
            bool held = canPing && input.PingHeld;

            if (held && !wasHeld)
            {
                wheelCursor = Vector2.zero;
                SetWheelOpen(true);
            }

            if (held && wheelOpen)
            {
                // 마우스가 멈춘 프레임은 입력 장치가 비어 있어 스틱으로 잡히므로, 입력이 0이면 커서를 그대로 둔다
                Vector2 look = input.PingLook;
                if (input.LookIsPointer)
                    wheelCursor = Vector2.ClampMagnitude(wheelCursor + look, WheelRadius);
                else if (look.sqrMagnitude > StickDeadzone * StickDeadzone)
                    wheelCursor = Vector2.ClampMagnitude(look, 1f) * WheelRadius; // 스틱은 기울인 방향 그대로 (놓으면 마지막 선택 유지)
            }

            if (!held && wasHeld)
            {
                // 메뉴·사망 등으로 끊겼거나 가운데에서 떼면 취소
                if (canPing && wheelOpen && TryGetSelection(out var type)) TryPing(type);
            }

            if (!held && wheelOpen) SetWheelOpen(false);
            wasHeld = held;
        }

        void OnDisable() => SetWheelOpen(false);

        void SetWheelOpen(bool open)
        {
            wheelOpen = open;
            PlayerInputReader.PingWheelOpen = open;
        }

        void RefreshLocal()
        {
            if (local != null && local.IsLocal && local.isActiveAndEnabled) return;
            local = null;
            input = null;
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].IsLocal) continue;
                local = all[i];
                input = local.GetComponentInChildren<PlayerInputReader>();
                break;
            }
        }

        bool TryGetSelection(out PingType type)
        {
            type = PingType.Step;
            if (wheelCursor.magnitude < SelectDeadzone) return false;
            type = wheelCursor.y >= 0f ? PingType.Step : PingType.Danger;
            return true;
        }

        void TryPing(PingType type)
        {
            if (CooldownLeft > 0f) return; // 원형 메뉴에 남은 시간이 보인다
            if (!TryAim(type, out Vector3 position, out Vector3 normal))
            {
                ShowFail("카메라를 찾을 수 없어요");
                return;
            }
            lastPingTime = Time.time;
            PingMarkers.Service.RequestPing(type, position, normal, SwitchStates.LocalRole);
        }

        float CooldownLeft => Mathf.Max(0f, Cooldown - (Time.time - lastPingTime));

        void ShowFail(string text)
        {
            failText = text;
            failUntil = Time.time + 1.5f;
        }

        bool TryAim(PingType type, out Vector3 position, out Vector3 normal)
        {
            position = default;
            normal = Vector3.up;
            var cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // 카메라와 내 캐릭터 사이(캐릭터 뒤쪽)에 걸린 것은 찍지 않는다
            float minDistance = Mathf.Max(0f, Vector3.Dot(local.transform.position - ray.origin, ray.direction) - 0.5f);
            int mask = ~(1 << IgnoreRaycastLayer);
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, MaxDistance + minDistance, mask, QueryTriggerInteraction.Ignore);

            bool found = false;
            RaycastHit best = default;
            for (int i = 0; i < count; i++)
            {
                var h = hitBuffer[i];
                if (h.distance < minDistance) continue;
                if (found && h.distance >= best.distance) continue;
                var owner = h.collider.GetComponentInParent<PlayerController>();
                if (owner == local) continue;
                best = h;
                found = true;
            }
            if (!found)
            {
                // 하늘·허공: 조준선 위 AirDistance 지점(내 캐릭터 기준)에 띄운다. 링은 수평
                position = ray.origin + ray.direction * (minDistance + 0.5f + AirDistance);
                normal = Vector3.up;
                return true;
            }

            position = best.point;
            normal = best.normal;

            // "여기 밟아!"를 발판 옆면에 찍었으면 그 발판의 윗면으로 올린다
            if (type == PingType.Step && best.normal.y < WalkableNormalY)
            {
                var col = best.collider;
                var b = col.bounds;
                Vector3 inside = best.point - best.normal * 0.1f;
                var down = new Ray(new Vector3(inside.x, b.max.y + 0.5f, inside.z), Vector3.down);
                if (col.Raycast(down, out var top, b.size.y + 1f) && top.normal.y >= WalkableNormalY)
                {
                    position = top.point;
                    normal = top.normal;
                }
            }
            return true;
        }

        void OnGUI()
        {
            if (local == null || input == null || !input.enabled) return;
            if (PlayerInputReader.Blocked || PlayerInputReader.MenuOpen || PlayerInputReader.PauseOpen) return;
            if (Cursor.lockState != CursorLockMode.Locked && !wheelOpen && Time.time >= failUntil) return;
            EnsureTextures();

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var prev = GUI.color;

            // 조준점 (핑이 찍히는 곳)
            float dot = wasHeld ? 8f : 6f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(center.x - dot * 0.5f - 1f, center.y - dot * 0.5f - 1f, dot + 2f, dot + 2f), dotTex);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(center.x - dot * 0.5f, center.y - dot * 0.5f, dot, dot), dotTex);

            if (Time.time < failUntil && !string.IsNullOrEmpty(failText))
            {
                failStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
                var r = new Rect(center.x - 200f, center.y + 28f, 400f, 26f);
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), failText, failStyle);
                GUI.color = new Color(1f, 0.55f, 0.45f, 1f);
                GUI.Label(r, failText, failStyle);
            }

            if (wheelOpen)
            {
                wheelStyle ??= new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                };
                float size = WheelRadius * 2f + 40f;
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), circleTex);

                float cool = CooldownLeft;
                bool ready = cool <= 0f;
                bool selected = TryGetSelection(out var type);
                DrawOption(center + new Vector2(0f, -WheelRadius * 0.6f), "여기 밟아!", new Color(0.3f, 1f, 0.55f), selected && type == PingType.Step, ready);
                DrawOption(center + new Vector2(0f, WheelRadius * 0.6f), "위험!", new Color(1f, 0.22f, 0.18f), selected && type == PingType.Danger, ready);

                // 쿨타임: 가운데 위에 작게 남은 시간 + 짧은 막대
                if (!ready)
                {
                    wheelStyle.fontSize = 13;
                    GUI.color = new Color(1f, 0.85f, 0.4f, 0.95f);
                    GUI.Label(new Rect(center.x - 60f, center.y - 34f, 120f, 20f), $"쿨타임 {cool:0.0}초", wheelStyle);
                    wheelStyle.fontSize = 20;
                    const float barW = 60f;
                    GUI.color = new Color(1f, 1f, 1f, 0.2f);
                    GUI.DrawTexture(new Rect(center.x - barW * 0.5f, center.y - 16f, barW, 3f), Texture2D.whiteTexture);
                    GUI.color = new Color(1f, 0.85f, 0.4f, 0.95f);
                    GUI.DrawTexture(new Rect(center.x - barW * 0.5f, center.y - 16f, barW * (1f - cool / Cooldown), 3f), Texture2D.whiteTexture);
                }

                // 선택 커서 (GUI 좌표는 아래쪽이 +y)
                Vector2 cursor = center + new Vector2(wheelCursor.x, -wheelCursor.y);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(cursor.x - 6f, cursor.y - 6f, 12f, 12f), dotTex);
                if (!selected)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.7f);
                    wheelStyle.fontSize = 14;
                    GUI.Label(new Rect(center.x - 60f, center.y + 10f, 120f, 24f), "취소", wheelStyle);
                    wheelStyle.fontSize = 20;
                }
            }
            GUI.color = prev;
        }

        void DrawOption(Vector2 pos, string text, Color color, bool highlighted, bool ready)
        {
            var rect = new Rect(pos.x - 90f, pos.y - 22f, 180f, 44f);
            if (!ready) // 쿨타임 중: 회색으로 흐리게
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                GUI.Label(rect, text, wheelStyle);
                return;
            }
            if (highlighted)
            {
                GUI.color = new Color(color.r, color.g, color.b, 0.35f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = highlighted ? Color.white : new Color(color.r, color.g, color.b, 0.9f);
            GUI.Label(rect, text, wheelStyle);
        }

        static void EnsureTextures()
        {
            if (circleTex == null) circleTex = MakeCircle(128);
            if (dotTex == null) dotTex = MakeCircle(16);
        }

        static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
