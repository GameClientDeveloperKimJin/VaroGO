using UnityEngine;
using UnityEngine.InputSystem;

namespace VaroGO.Client
{
    /// <summary>
    /// 개발 치트 패널 (OnGUI). 씬에 빈 오브젝트로 두고 이 컴포넌트를 붙인다. 에디터·개발 빌드에서만 동작한다.
    /// 버튼 1 / [ 키: 혼자 올라가기 (매달렸을 때 점프 또는 벽 쪽 이동으로 스스로 올라감)
    /// 버튼 2 / ] 키: 무중력 비행 (Space 위, Left Ctrl / Q 아래)
    /// 버튼 3 / \ 키: 마지막 구간으로 이동 (비행 포탈 앞, 포탈이 없으면 마지막 체크포인트. 내 캐릭터만. 테스트용)
    /// ` (백쿼트) 키: 패널 숨기기/보이기. 버튼을 마우스로 누르려면 Esc로 커서를 먼저 푼다.
    /// 치트 값은 DevCheats(정적)라 씬이 바뀌어도 유지된다.
    /// </summary>
    public class DevCheatGUI : MonoBehaviour
    {
        const float Width = 250f, Height = 190f;

        [SerializeField] bool showOnStart = true;
        [Tooltip("화면 오른쪽 위 모서리에서 떨어진 거리 (픽셀). 바로 위는 NetDebugOverlay 네트워크 정보 창(높이 약 120)이 쓴다")]
        [SerializeField] Vector2 rightTopMargin = new(10f, 130f);

        bool visible;
        GUIStyle box, button, label;

        Rect PanelRect => new(Screen.width - Width - rightTopMargin.x, rightTopMargin.y, Width, Height);

        void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            visible = showOnStart;
#else
            // 정식 빌드에는 치트를 남기지 않는다
            DevCheats.SelfClimb = false;
            DevCheats.Fly = false;
            enabled = false;
#endif
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame) visible = !visible;
            if (kb.leftBracketKey.wasPressedThisFrame) DevCheats.SelfClimb = !DevCheats.SelfClimb;
            if (kb.rightBracketKey.wasPressedThisFrame) DevCheats.Fly = !DevCheats.Fly;
            if (kb.backslashKey.wasPressedThisFrame) TeleportToLastSection();
        }

        // 마지막 구간 = 비행 포탈(FlightPortal) 앞. 포탈이 없는 씬이면 order가 가장 큰 체크포인트로 간다.
        // 체크포인트 저장은 하지 않는다: 도착 후 체크포인트를 밟으면 평소처럼 저장된다
        const float PortalFrontDistance = 3.5f; // 포탈 판정 반경(기본 2m) 바깥, 포탈 정면 쪽
        static FlightPortal portal;

        static FlightPortal FindPortal()
        {
            if (portal == null) portal = FindFirstObjectByType<FlightPortal>();
            return portal;
        }

        static Checkpoint LastCheckpoint()
        {
            var list = Checkpoint.All;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] != null) return list[i];
            return null;
        }

        static string LastSectionName()
        {
            var fp = FindPortal();
            if (fp != null) return fp.name + " 앞";
            var cp = LastCheckpoint();
            return cp != null ? cp.name : "대상 없음";
        }

        static void TeleportToLastSection()
        {
            PlayerController local = null;
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].IsLocal) { local = all[i]; break; }
            if (local == null || local.IsDead)
            {
                Debug.Log("[DevCheat] 마지막 구간 이동 실패: 내 캐릭터 없음/사망");
                return;
            }

            var fp = FindPortal();
            if (fp != null)
            {
                Transform t = fp.transform;
                // "앞" = 플레이어가 다가오는 쪽: 포탈에서 가장 가까운 체크포인트 방향 (포탈 회전은 씬마다 달라 믿지 않는다)
                Vector3 fwd = Vector3.zero;
                float bestSqr = float.PositiveInfinity;
                var cps = Checkpoint.All;
                for (int i = 0; i < cps.Count; i++)
                {
                    if (cps[i] == null) continue;
                    Vector3 d = Vector3.ProjectOnPlane(cps[i].transform.position - t.position, Vector3.up);
                    if (d.sqrMagnitude < bestSqr && d.sqrMagnitude > 0.01f) { bestSqr = d.sqrMagnitude; fwd = d; }
                }
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 pos = t.position + fwd * PortalFrontDistance;
                // 땅 높이에 맞춘다 (위에서 아래로 쏴서 트리거는 무시)
                if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                    pos = hit.point + Vector3.up * 0.05f;
                local.Teleport(pos, Quaternion.LookRotation(-fwd)); // 포탈을 바라본다. 온라인은 NetPlayer가 다음 틱에 다시 적용
                Debug.Log($"[DevCheat] 마지막 구간으로 이동: {fp.name} 앞 {pos}");
                return;
            }

            var cp = LastCheckpoint();
            if (cp == null)
            {
                Debug.Log("[DevCheat] 마지막 구간 이동 실패: 포탈·체크포인트 없음");
                return;
            }
            var p = cp.SpawnPoint;
            local.Teleport(p.position, p.rotation);
            Debug.Log($"[DevCheat] 마지막 구간으로 이동: {cp.name} (order {cp.Order})");
        }

        // PlayerInputReader가 클릭하면 커서를 다시 잠근다 → 패널 위 클릭이면 풀어 둔다 (버튼이 눌리도록)
        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (!visible || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 p = mouse.position.ReadValue();
            p.y = Screen.height - p.y; // IMGUI 좌표 (위가 0)
            if (PanelRect.Contains(p))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void OnGUI()
        {
            if (!visible) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
                button = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 30 };
                label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            }

            GUILayout.BeginArea(PanelRect, box);
            GUILayout.Label("개발 치트   ` 숨기기 · Esc 커서", label);

            if (GUILayout.Button($"[  혼자 올라가기 : {(DevCheats.SelfClimb ? "ON" : "OFF")}", button))
                DevCheats.SelfClimb = !DevCheats.SelfClimb;

            if (GUILayout.Button($"]  무중력 비행 : {(DevCheats.Fly ? "ON" : "OFF")}", button))
                DevCheats.Fly = !DevCheats.Fly;

            if (GUILayout.Button($"\\  마지막 구간 이동 ({LastSectionName()})", button))
                TeleportToLastSection();

            if (DevCheats.Fly) GUILayout.Label("Space 위 / Left Ctrl·Q 아래", label);
            else if (DevCheats.SelfClimb) GUILayout.Label("매달린 뒤 점프 또는 벽 쪽 이동", label);
            GUILayout.EndArea();
        }
    }
}
