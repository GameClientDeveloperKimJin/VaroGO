using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VaroGO.Shared;

namespace VaroGO.Client.UI
{
    /// <summary>
    /// C2-03: 세션이 PartnerLost면 "파트너 재접속 대기 중 (n초)" + 나가기 버튼을 덮어 보여준다.
    /// 보이는 동안 플레이어 입력을 잠근다 (PlayerInputReader.Blocked).
    /// 씬에 따로 놓지 않는다. 게임 시작 시 스스로 하나 만들어 씬이 바뀌어도 유지한다.
    /// </summary>
    public class PartnerLostOverlay : MonoBehaviour
    {
        const string Title = "파트너 재접속 대기 중";

        GameObject root;
        Text messageText;
        float shownSeconds = -1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 서버 끊김 처리(S2-04) 전 확인용: F9로 끊김 상태를 흉내 낸다
        bool fakeLost;
        float fakeLostTime;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("PartnerLostOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<PartnerLostOverlay>();
        }

        void Awake() => Build();

        void OnDestroy() => PlayerInputReader.Blocked = false;

        void Update()
        {
            var session = GameServices.Session;
            bool lost = session != null && session.Phase == SessionPhase.PartnerLost;
            float seconds = lost ? session.PartnerLostSeconds : 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                fakeLost = !fakeLost;
                fakeLostTime = Time.unscaledTime;
            }
            if (fakeLost && !lost)
            {
                lost = true;
                seconds = Time.unscaledTime - fakeLostTime;
            }
#endif
            if (root.activeSelf != lost)
            {
                root.SetActive(lost);
                PlayerInputReader.Blocked = lost;
                if (lost) EnsureEventSystem();
            }
            if (!lost) return;

            // 초가 바뀔 때만 글자를 다시 쓴다
            float whole = Mathf.Floor(seconds);
            if (whole == shownSeconds) return;
            shownSeconds = whole;
            messageText.text = $"{Title} ({whole:0}초)";
        }

        void Leave()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            fakeLost = false;
#endif
            GameServices.Session?.Leave();
        }

        // 코스 씬에는 EventSystem이 없을 수 있다. 없으면 버튼이 눌리지 않으므로 만든다
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem (PartnerLostOverlay)", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 다른 UI보다 위
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = NewChild("Dim", root.transform);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var panel = NewChild("Panel", root.transform);
            panel.sizeDelta = new Vector2(720f, 300f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.16f, 0.95f);

            var message = NewChild("Message", panel);
            message.anchorMin = new Vector2(0f, 0.45f);
            message.anchorMax = new Vector2(1f, 1f);
            message.offsetMin = message.offsetMax = Vector2.zero;
            messageText = message.gameObject.AddComponent<Text>();
            messageText.font = font;
            messageText.fontSize = 40;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            messageText.text = Title;

            var button = NewChild("LeaveButton", panel);
            button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.25f);
            button.sizeDelta = new Vector2(260f, 72f);
            button.gameObject.AddComponent<Image>().color = new Color(0.75f, 0.3f, 0.3f);
            button.gameObject.AddComponent<Button>().onClick.AddListener(Leave);

            var label = NewChild("Label", button);
            Stretch(label);
            var labelText = label.gameObject.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 32;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = "나가기";

            root.SetActive(false);
        }

        static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
