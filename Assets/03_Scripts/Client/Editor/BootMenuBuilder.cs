using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VaroGO.Client.UI;

namespace VaroGO.Client.EditorTools
{
    // 00_Boot 타이틀 메뉴(uGUI)를 열린 씬에 새로 만든다. 같은 이름 오브젝트가 있으면 지우고 다시 만든다
    static class BootMenuBuilder
    {
        const float X = 260f;
        static readonly Color Orange = Hex("#F2A93B");
        static readonly Color Dark = Hex("#151B2D");
        static readonly Color White = Hex("#F4F1EA");
        static readonly Color Grey = Hex("#7C84A0");
        static readonly Vector2 Left = new(0f, 0.5f);
        static readonly Vector2 Mid = new(0.5f, 0.5f);

        static Font font;
        static Sprite round;
        static Sprite circle;

        [MenuItem("VAROGO/Build Boot Menu UI")]
        public static void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // 아트가 적용된 기존 메뉴를 기본 모양으로 덮어쓰지 않게 확인한다
            if (GameObject.Find("BootMenu") && !EditorUtility.DisplayDialog("Build Boot Menu UI",
                    "이미 BootMenu가 있습니다. 지우고 기본 모양으로 다시 만들면 적용한 아트가 사라집니다.\n계속할까요?", "다시 만들기", "취소"))
                return;

            foreach (var name in new[] { "BootMenu", "EventSystem", "BootCamera" })
            {
                var old = GameObject.Find(name);
                if (old) Object.DestroyImmediate(old);
            }

            var cam = new GameObject("BootCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#0E1322");
            cam.cullingMask = 0;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = new GameObject("BootMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            BuildBackground(root.transform);

            Label(Rect("Tag", root.transform, Left, new Vector2(X, 250), new Vector2(600, 30)), "PROTOTYPE", 22, Orange, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(Rect("Title", root.transform, Left, new Vector2(X, 170), new Vector2(1200, 130)), "[GAME TITLE]", 110, White, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 메인
            var main = Panel("MainPanel", root.transform);
            // 게임 시작은 서버 TitleStartLink(LobbyScene 이동), 옵션은 OptionPopup이 맡는다. 방 만들기·참가·대기는 LobbyScene에 있다
            Button("StartButton", main, new Vector2(X, 40), new Vector2(560, 72), "게임 시작", primary: true, arrow: "›");
            Button("OptionButton", main, new Vector2(X, -48), new Vector2(560, 72), "옵션");
            var quitButton = Button("QuitButton", main, new Vector2(X, -136), new Vector2(560, 72), "종료");

            var ui = root.AddComponent<BootMenuUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 서버 디버그 IMGUI(BootUI)는 끈다 (지우지 않음: 필요하면 다시 켜서 쓴다).
            // 클라는 Net 코드를 참조하지 않으므로 타입 이름으로 찾는다
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour.GetType().Name != "BootUI") continue;
                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        static void BuildBackground(Transform parent)
        {
            var bg = Stretch("Background", parent);
            Image(bg, Hex("#0E1322"));

            var rng = new System.Random(7);
            for (int i = 0; i < 40; i++)
            {
                var anchor = new Vector2((float)rng.NextDouble(), 0.35f + 0.65f * (float)rng.NextDouble());
                var star = Rect("Star", bg, anchor, Vector2.zero, Vector2.one * (rng.Next(2) == 0 ? 3 : 5));
                Image(star, new Color(1f, 1f, 1f, 0.25f + 0.5f * (float)rng.NextDouble()));
            }

            var halo = Rect("MoonHalo", bg, new Vector2(0.86f, 0.78f), Vector2.zero, new Vector2(230, 230));
            Image(halo, Hex("#3B3426"), circle);
            Image(Rect("Moon", halo, Mid, Vector2.zero, new Vector2(160, 160)), Hex("#8A6A36"), circle);

            var back = Hex("#182038");
            var front = Hex("#1E2744");
            Mountain(bg, 0.12f, 620, back);
            Mountain(bg, 0.45f, 760, back);
            Mountain(bg, 0.80f, 680, back);
            Mountain(bg, 0.28f, 520, front);
            Mountain(bg, 0.62f, 560, front);
            Mountain(bg, 0.98f, 480, front);

            var ground = Stretch("Ground", bg);
            ground.anchorMax = new Vector2(1f, 0.18f);
            Image(ground, Hex("#0A0E19"));

            var version = Rect("Version", parent, new Vector2(1f, 0f), new Vector2(-40, 30), new Vector2(400, 30));
            version.pivot = new Vector2(1f, 0f);
            Label(version, "v0.1 · prototype", 20, Grey, TextAnchor.LowerRight);
        }

        // 45도 돌린 사각형의 위쪽 꼭짓점이 산봉우리가 된다
        static void Mountain(Transform parent, float x, float size, Color color)
        {
            var m = Rect("Mountain", parent, new Vector2(x, 0.18f), Vector2.zero, new Vector2(size, size));
            m.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image(m, color);
        }

        static Button Button(string name, Transform parent, Vector2 pos, Vector2 size, string label,
            bool primary = false, bool outline = false, string arrow = null)
        {
            var rt = Rect(name, parent, Left, pos, size);
            rt.pivot = Left;
            Image(rt, primary ? Orange : Dark, round, sliced: true).raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = Orange;
                o.effectDistance = new Vector2(2, -2);
            }

            var textColor = primary ? Hex("#1B1408") : outline ? Orange : White;
            var labelRect = Stretch("Label", rt);
            labelRect.offsetMin = new Vector2(outline ? 0 : 28, 0);
            Label(labelRect, label, 26, textColor, outline ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, FontStyle.Bold);

            if (arrow != null)
            {
                var arrowRect = Stretch("Arrow", rt);
                arrowRect.offsetMax = new Vector2(-24, 0);
                Label(arrowRect, arrow, 30, textColor, TextAnchor.MiddleRight, FontStyle.Bold);
            }
            return button;
        }

        static RectTransform Panel(string name, Transform parent) => Stretch(name, parent);

        static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Mid, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            return rt;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            // 왼쪽 정렬 요소는 X가 왼쪽 가장자리가 되도록 pivot도 왼쪽
            rt.pivot = anchor == Left ? Left : Mid;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Image(RectTransform rt, Color color, Sprite sprite = null, bool sliced = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sliced) image.type = UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        static Text Label(RectTransform rt, string text, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
