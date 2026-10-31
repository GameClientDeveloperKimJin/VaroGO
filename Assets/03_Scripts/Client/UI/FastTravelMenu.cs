using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// C2-08 (임시 채택): 빠른 이동. 빠른 이동 키(M / 패드 Select)로 메뉴를 열면 갈 수 있는 체크포인트 목록이 뜬다.
    /// 고르면 ICheckpointState.CanFastTravel로 한 번 더 확인하고 내 캐릭터만 그 부활 위치로 순간이동한다. 파트너는 그대로.
    /// 메뉴가 열린 동안 플레이어 입력을 잠그고 커서를 푼다. 숫자 키 1~9로도 고를 수 있다. Esc 또는 같은 키로 닫는다.
    /// 씬에 따로 놓지 않는다. 게임 시작 시 스스로 하나 만들어 씬이 바뀌어도 유지한다.
    /// 그레이박스용 OnGUI 표시. UI 아트가 들어오면 교체한다.
    /// </summary>
    public class FastTravelMenu : MonoBehaviour
    {
        static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        readonly List<Checkpoint> options = new List<Checkpoint>();
        bool open;
        PlayerController local;
        GUIStyle titleStyle, buttonStyle, hintStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("FastTravelMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<FastTravelMenu>();
        }

        void OnDestroy()
        {
            if (open) PlayerInputReader.MenuOpen = false;
        }

        void Update()
        {
            local = FindLocal();
            // 플레이어가 없거나 죽었거나 끊김 화면이 뜨면 닫는다
            if (local == null || local.IsDead || PlayerInputReader.Blocked)
            {
                if (open) Close();
                return;
            }

            var reader = local.GetComponent<PlayerInputReader>();
            var keyboard = Keyboard.current;
            bool toggle = reader != null && reader.FastTravelPressed;
            if (!open)
            {
                if (toggle) Open();
                return;
            }

            if (toggle || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                Close();
                return;
            }
            if (keyboard == null) return;
            for (int i = 0; i < NumberKeys.Length && i < options.Count; i++)
            {
                if (keyboard[NumberKeys[i]].wasPressedThisFrame)
                {
                    TravelTo(options[i]);
                    return;
                }
            }
        }

        static PlayerController FindLocal()
        {
            var all = PlayerController.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].IsLocal) return all[i];
            return null;
        }

        void Open()
        {
            RefreshOptions();
            open = true;
            PlayerInputReader.MenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
        }

        void Close()
        {
            open = false;
            PlayerInputReader.MenuOpen = false;
            if (!PlayerInputReader.Blocked) Cursor.lockState = CursorLockMode.Locked;
        }

        // 활성된 체크포인트만 order 순으로 (Checkpoint.All이 이미 정렬돼 있다)
        void RefreshOptions()
        {
            options.Clear();
            var state = GameServices.Checkpoints;
            if (state == null || state.ActiveOrder < 0) return;
            var list = Checkpoint.All;
            for (int i = 0; i < list.Count; i++)
                if (state.CanFastTravel(list[i].Order)) options.Add(list[i]);
        }

        void TravelTo(Checkpoint checkpoint)
        {
            var state = GameServices.Checkpoints;
            if (checkpoint == null || local == null || local.IsDead) return;
            if (state == null || !state.CanFastTravel(checkpoint.Order))
            {
                Debug.Log($"[FastTravel] 갈 수 없는 지점: {checkpoint.name} (order {checkpoint.Order})");
                RefreshOptions();
                return;
            }
            var p = checkpoint.SpawnPoint;
            local.Teleport(p.position, p.rotation);
            Debug.Log($"[FastTravel] {checkpoint.name} (order {checkpoint.Order})로 이동");
            Close();
        }

        void OnGUI()
        {
            if (!open) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            }

            const float width = 420f, rowHeight = 44f, gap = 8f;
            int rows = Mathf.Max(1, options.Count);
            float height = 70f + rows * (rowHeight + gap) + 40f;
            var box = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(box.x, box.y + 12f, width, 40f), "빠른 이동", titleStyle);

            int activeOrder = GameServices.Checkpoints != null ? GameServices.Checkpoints.ActiveOrder : -1;
            float y = box.y + 70f;
            if (options.Count == 0)
            {
                GUI.Label(new Rect(box.x, y, width, rowHeight), "갈 수 있는 체크포인트가 없음", hintStyle);
            }
            for (int i = 0; i < options.Count; i++)
            {
                var cp = options[i];
                string number = i < NumberKeys.Length ? $"{i + 1}. " : "";
                string mark = cp.Order == activeOrder ? "  (현재 저장 지점)" : "";
                if (GUI.Button(new Rect(box.x + 20f, y, width - 40f, rowHeight), $"{number}{cp.name}{mark}", buttonStyle))
                {
                    TravelTo(cp);
                    return;
                }
                y += rowHeight + gap;
            }
            GUI.Label(new Rect(box.x, box.yMax - 36f, width, 28f), "M / Esc: 닫기", hintStyle);
        }
    }
}
