using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VaroGO.Client.UI;

namespace VaroGO.Client.EditorTools
{
    // UI_Lobby.prefab 방 목록(RoomListPanel)에 아트_UI 리소스(KIMJIN_ArtWork/UI/Lobby, KJ 승인 2026-11-28)를 입힌다.
    // 서버 LobbyUI가 경로로 찾는 오브젝트 이름·계층은 바꾸지 않는다(이미지·크기·위치·글자 모양만). 새 오브젝트는 기존 오브젝트의 자식/형제로만 추가.
    // 판 기준: 화면 가운데 1180×820 (UI_Lobby_Panel). 줄 1000×80.
    static class LobbyRoomListSkin
    {
        const string PrefabPath = "Assets/02_Prefabs/Client/UI/Lobby/UI_Lobby.prefab";
        const string Art = "Assets/04_Resources/KIMJIN_ArtWork/UI/Lobby/";
        const string FontPath = "Assets/04_Resources/UI/ONE Mobile POP SDF.asset";
        public const string OutlineMatPath = "Assets/04_Resources/UI/ONE Mobile POP SDF - Lobby Outline.mat";

        static readonly Color Cream = new(1f, 0.97f, 0.9f);
        static readonly Color Brown = new(0.35f, 0.19f, 0.07f);

        static TMP_FontAsset font;
        static Material outline;

        [MenuItem("VAROGO/Lobby/Apply Room List Skin")]
        public static void Apply()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            outline = EnsureOutlineMaterial();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.transform.Find("RoomListPanel");
                Skin(panel);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Material EnsureOutlineMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "ONE Mobile POP SDF - Lobby Outline" };
                AssetDatabase.CreateAsset(mat, OutlineMatPath);
            }
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetColor("_OutlineColor", Brown);
            mat.SetFloat("_OutlineWidth", 0.25f);
            mat.SetFloat("_FaceDilate", 0.1f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void Skin(Transform p)
        {
            // 전체 화면 Background: 뒤 숲 배경이 보이게 옅은 어둠만 (2026-11-28 KJ). 빈 무대 슬롯은 RoomStageListHider가 숨긴다
            var bg = (RectTransform)p.Find("Background");
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = null; bgImg.type = Image.Type.Simple; bgImg.color = new Color(0.02f, 0.04f, 0.03f, 0.35f);

            // 판
            var frame = Child(p, "Frame", 1);
            SetRect(frame, Center, Vector2.zero, new Vector2(1180f, 820f));
            Sliced(frame, "UI_Lobby_Panel", 1.27f, false);

            // 위쪽 제목 바
            var top = (RectTransform)p.Find("TopBar");
            SetRect(top, Center, new Vector2(0f, 345f), new Vector2(1080f, 140f));
            Sliced(Child(top, "BarBg", 0), "UI_Lobby_TitleBar", 2.2f, false);
            var title = top.Find("TitleText").GetComponent<TMP_Text>();
            Text(title, 44f, Cream, true);
            title.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            IconButton((RectTransform)top.Find("BackButton"), new Vector2(0f, 0.5f), new Vector2(110f, 4f), "UI_Lobby_Icon_Back_Dark");
            IconButton((RectTransform)top.Find("RefreshButton"), new Vector2(1f, 0.5f), new Vector2(-110f, 4f), "UI_Lobby_Icon_Refresh_Dark");

            // 열 제목 줄 (줄 1000폭 기준 열 위치에 +20)
            var header = (RectTransform)p.Find("ColumnHeader");
            SetRect(header, Center, new Vector2(0f, 236f), new Vector2(1040f, 52f));
            Sliced(header, "UI_Lobby_ColumnHeader", 2f, false);
            Column(header.Find("NameHeader"), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(400f, 50f), TextAlignmentOptions.Left);
            Column(header.Find("CountHeader"), new Vector2(0f, 0.5f), new Vector2(620f, 0f), new Vector2(140f, 50f), TextAlignmentOptions.Center);
            Column(header.Find("StateHeader"), new Vector2(0f, 0.5f), new Vector2(780f, 0f), new Vector2(160f, 50f), TextAlignmentOptions.Center);

            // 목록
            var list = (RectTransform)p.Find("RoomList");
            SetRect(list, Center, new Vector2(0f, -32f), new Vector2(1060f, 470f));
            var listImg = list.GetComponent<Image>();
            listImg.color = new Color(1f, 1f, 1f, 0f); // 스크롤 입력만 받는다
            var viewport = (RectTransform)list.Find("Viewport");
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = new Vector2(10f, 0f); viewport.offsetMax = new Vector2(-40f, 0f);
            var content = list.Find("Viewport/Content");
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f; vlg.padding = new RectOffset(0, 0, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            Row((RectTransform)content.Find("RoomItem_Template"));

            var bar = (RectTransform)list.Find("Scrollbar Vertical");
            bar.sizeDelta = new Vector2(28f, 0f);
            var barImg = bar.GetComponent<Image>();
            barImg.sprite = null; barImg.color = new Color(0.35f, 0.2f, 0.08f, 0.35f);
            var handle = (RectTransform)bar.Find("Sliding Area/Handle");
            Sliced(handle, "UI_Lobby_ScrollHandle", 2f, false);
            ((RectTransform)bar.Find("Sliding Area")).offsetMin = Vector2.zero;
            ((RectTransform)bar.Find("Sliding Area")).offsetMax = Vector2.zero;

            // 빈 목록: 문구 위에 작은 섬 그림 (EmptyText 자식이라 LobbyUI가 켜고 끌 때 같이 움직인다)
            var empty = (RectTransform)list.Find("EmptyText");
            SetRect(empty, Center, new Vector2(0f, -110f), new Vector2(900f, 60f));
            Text(empty.GetComponent<TMP_Text>(), 30f, Brown, false);
            var island = Child(empty, "EmptyIsland", 0);
            SetRect(island, Center, new Vector2(0f, 170f), new Vector2(280f, 282f));
            var islandImg = Img(island, "UI_Lobby_EmptyIsland");
            islandImg.type = Image.Type.Simple; islandImg.preserveAspect = true;

            // 아래 버튼: 방 만들기(주) 왼쪽, 코드로 참가(보조) 오른쪽
            var footer = (RectTransform)p.Find("Footer");
            SetRect(footer, Center, new Vector2(0f, -335f), new Vector2(900f, 110f));
            var h = footer.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 40f; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var create = (RectTransform)footer.Find("CreateRoomButton");
            var joinCode = (RectTransform)footer.Find("JoinCodeButton");
            create.SetSiblingIndex(0);
            create.sizeDelta = new Vector2(380f, 104f);
            PrimaryButton(create.GetComponent<Button>(), 2f);
            LabelOf(create, 36f);
            joinCode.sizeDelta = new Vector2(380f, 104f);
            var jcImg = joinCode.GetComponent<Image>();
            jcImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/04_Resources/UI/InGame/BT-02.png");
            jcImg.type = Image.Type.Sliced; jcImg.pixelsPerUnitMultiplier = 0.6f; jcImg.color = Color.white; // BT-02 9-slice L60 R60 T24 B24 (KJ 승인 2026-11-28). 64px 원본을 104 높이로 → 배율 0.6
            var jc = joinCode.GetComponent<Button>();
            jc.transition = Selectable.Transition.ColorTint;
            var cb = jc.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); cb.selectedColor = Color.white; jc.colors = cb;
            LabelOf(joinCode, 34f);
        }

        static void Row(RectTransform row)
        {
            row.sizeDelta = new Vector2(1000f, 80f);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = 80f; le.minHeight = 80f; le.preferredWidth = 1000f;
            Sliced(row, "UI_Lobby_RowBg", 2f, true);
            var btn = row.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.targetGraphic = row.GetComponent<Image>();
            btn.spriteState = new SpriteState
            {
                highlightedSprite = Sprite("UI_Lobby_RowBg_Hover"),
                selectedSprite = Sprite("UI_Lobby_RowBg_Selected"),
                pressedSprite = Sprite("UI_Lobby_RowBg_Selected"),
            };
            var nav = btn.navigation; nav.mode = Navigation.Mode.Automatic; btn.navigation = nav;

            var name = row.Find("RoomNameText").GetComponent<TMP_Text>();
            Column(name.transform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(500f, 70f), TextAlignmentOptions.Left);
            Text(name, 32f, Brown, false);

            // 인원칸
            var slot = Child(row, "CountSlot", row.Find("PlayerCountText").GetSiblingIndex());
            SetRect(slot, new Vector2(0f, 0.5f), new Vector2(600f, 0f), new Vector2(110f, 52f));
            Sliced(slot, "UI_Lobby_CountSlot", 2f, false);
            var count = row.Find("PlayerCountText").GetComponent<TMP_Text>();
            Column(count.transform, new Vector2(0f, 0.5f), new Vector2(600f, 0f), new Vector2(110f, 52f), TextAlignmentOptions.Center);
            Text(count, 28f, Cream, true);

            // 상태 태그 (색은 RoomStatusTagTint가 글자를 보고 칠한다)
            var tag = Child(row, "StatusTag", row.Find("StateText").GetSiblingIndex());
            SetRect(tag, new Vector2(0f, 0.5f), new Vector2(760f, 0f), new Vector2(130f, 44f));
            var tagImg = Sliced(tag, "UI_Lobby_StatusTag", 2f, false);
            tagImg.color = new Color(0.55f, 0.95f, 0.45f);
            var state = row.Find("StateText").GetComponent<TMP_Text>();
            Column(state.transform, new Vector2(0f, 0.5f), new Vector2(760f, 0f), new Vector2(130f, 44f), TextAlignmentOptions.Center);
            Text(state, 22f, Cream, true);
            var tint = row.GetComponent<RoomStatusTagTint>() ?? row.gameObject.AddComponent<RoomStatusTagTint>();
            var so = new SerializedObject(tint);
            so.FindProperty("stateText").objectReferenceValue = state;
            so.FindProperty("tag").objectReferenceValue = tagImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 참가 버튼 (높이 64라 배율 3: 2로 하면 테두리가 눌린다)
            var join = (RectTransform)row.Find("JoinButton");
            SetRect(join, new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(150f, 64f));
            PrimaryButton(join.GetComponent<Button>(), 3f);
            LabelOf(join, 28f);
        }

        // ---- 도우미 ----

        static readonly Vector2 Center = new(0.5f, 0.5f);

        static RectTransform Child(Transform parent, string name, int index)
        {
            var t = parent.Find(name) as RectTransform;
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = parent.gameObject.layer;
                t = (RectTransform)go.transform;
                t.SetParent(parent, false);
            }
            t.SetSiblingIndex(Mathf.Clamp(index, 0, parent.childCount - 1));
            if (name == "BarBg") { t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero; }
            return t;
        }

        static void SetRect(RectTransform t, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            t.anchorMin = t.anchorMax = anchor;
            t.pivot = new Vector2(anchor.x == 0f ? 0.5f : anchor.x == 1f ? 0.5f : 0.5f, 0.5f);
            t.sizeDelta = size;
            t.anchoredPosition = pos;
        }

        static Sprite Sprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + file + ".png");

        static Image Img(Transform t, string file)
        {
            var img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.sprite = Sprite(file);
            img.color = Color.white;
            if (img.sprite == null) Debug.LogWarning("[LobbySkin] 스프라이트 없음: " + file);
            return img;
        }

        static Image Sliced(Transform t, string file, float ppum, bool raycast)
        {
            var img = Img(t, file);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppum;
            img.fillCenter = true;
            img.raycastTarget = raycast;
            return img;
        }

        static void IconButton(RectTransform t, Vector2 anchor, Vector2 pos, string icon)
        {
            SetRect(t, anchor, pos, new Vector2(60f, 60f));
            var img = Img(t, icon);
            img.type = Image.Type.Simple; img.preserveAspect = true; img.raycastTarget = true;
            var b = t.GetComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f); cb.pressedColor = new Color(0.75f, 0.75f, 0.75f); cb.selectedColor = Color.white; b.colors = cb;
            var label = t.Find("Label");
            if (label != null) label.gameObject.SetActive(false); // 아이콘만 쓴다
        }

        static void PrimaryButton(Button b, float ppum, string baseName = "UI_Lobby_BtnPrimary", bool sliced = true)
        {
            var img = sliced ? Sliced(b.transform, baseName, ppum, true) : Img(b.transform, baseName);
            if (!sliced) { img.type = Image.Type.Simple; img.raycastTarget = true; }
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = Sprite(baseName + "_Hover"),
                selectedSprite = Sprite(baseName + "_Hover"),
                pressedSprite = Sprite(baseName + "_Pressed"),
                disabledSprite = Sprite(baseName + "_Disabled"),
            };
        }

        static LobbySkinState.SpriteSet Set(string baseName) => new()
        {
            normal = Sprite(baseName), highlighted = Sprite(baseName + "_Hover"),
            pressed = Sprite(baseName + "_Pressed"), disabled = Sprite(baseName + "_Disabled"),
        };

        // ================= ② 팝업·대기실 (KJ 승인 2026-11-28) =================

        static readonly Color Ink = new(0.37f, 0.22f, 0.1f);   // 판 위 라벨·안내문
        static readonly Color ErrorRed = new(0.8f, 0.24f, 0.16f);

        [MenuItem("VAROGO/Lobby/Apply Popups and Room Skin")]
        public static void ApplyPopupsAndRoom()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            outline = EnsureOutlineMaterial();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var state = root.GetComponent<LobbySkinState>() ?? root.AddComponent<LobbySkinState>();
                var so = new SerializedObject(state);
                CreatePopup(root.transform.Find("CreateRoomPopup"));
                JoinPopup(root.transform.Find("JoinCodePopup"), so);
                RoomPanel(root.transform.Find("RoomPanel"), so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void PopupWindow(Transform popup, string title, float height)
        {
            var win = (RectTransform)popup.Find("Window");
            SetRect(win, Center, new Vector2(0f, -20f), new Vector2(800f, height));
            Sliced(win, "UI_Lobby_Panel", 2f, true);
            var header = (RectTransform)win.Find("Header");
            header.anchorMin = header.anchorMax = new Vector2(0.5f, 1f);
            header.pivot = new Vector2(0.5f, 0.5f);
            header.sizeDelta = new Vector2(720f, 120f);
            header.anchoredPosition = new Vector2(0f, -36f);
            Sliced(header, "UI_Lobby_TitleBar", 2.6f, false);
            var t = header.Find("TitleText").GetComponent<TMP_Text>();
            Text(t, 40f, Cream, true);
            t.alignment = TextAlignmentOptions.Center;
            ((RectTransform)t.transform).offsetMin = new Vector2(60f, 0f);
            ((RectTransform)t.transform).offsetMax = new Vector2(-60f, 6f);
        }

        static void PopupFooter(Transform win, string confirmName)
        {
            var footer = (RectTransform)win.Find("Footer");
            footer.anchorMin = new Vector2(0f, 0f); footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, 96f);
            footer.anchoredPosition = new Vector2(0f, 40f);
            var h = footer.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 40f; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var confirm = (RectTransform)footer.Find(confirmName);
            var cancel = (RectTransform)footer.Find("CancelButton");
            confirm.SetSiblingIndex(0);
            confirm.sizeDelta = cancel.sizeDelta = new Vector2(290f, 90f);
            PrimaryButton(confirm.GetComponent<Button>(), 2f);
            LabelOf(confirm, 34f);
            PrimaryButton(cancel.GetComponent<Button>(), 2f, "UI_Lobby_BtnSecondary");
            LabelOf(cancel, 34f);
            var cl = cancel.Find("Label").GetComponent<TMP_Text>();
            Text(cl, 34f, Ink, false);
        }

        static void CreatePopup(Transform popup)
        {
            PopupWindow(popup, "방 만들기", 560f);
            var win = popup.Find("Window");
            var rows = (RectTransform)win.Find("Rows");
            rows.anchorMin = rows.anchorMax = new Vector2(0.5f, 1f); rows.pivot = new Vector2(0.5f, 1f);
            rows.sizeDelta = new Vector2(680f, 300f); rows.anchoredPosition = new Vector2(0f, -112f);
            var v = rows.GetComponent<VerticalLayoutGroup>();
            v.spacing = 14f; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;

            // 방 이름: 라벨 위, 입력칸 아래
            var nameRow = (RectTransform)rows.Find("RoomNameRow");
            nameRow.GetComponent<LayoutElement>().preferredHeight = 128f;
            var nl = (RectTransform)nameRow.Find("Label");
            nl.anchorMin = nl.anchorMax = new Vector2(0f, 1f); nl.pivot = new Vector2(0f, 1f);
            nl.sizeDelta = new Vector2(400f, 40f); nl.anchoredPosition = new Vector2(10f, 0f);
            Text(nl.GetComponent<TMP_Text>(), 28f, Ink, false);
            nl.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Left;
            var input = (RectTransform)nameRow.Find("RoomNameInput");
            input.anchorMin = input.anchorMax = new Vector2(0.5f, 0f); input.pivot = new Vector2(0.5f, 0f);
            input.sizeDelta = new Vector2(680f, 80f); input.anchoredPosition = Vector2.zero;
            Sliced(input, "UI_Lobby_InputField", 2f, true);
            var tmpIn = input.GetComponent<TMP_InputField>();
            if (tmpIn.textComponent != null) { Text(tmpIn.textComponent, 30f, Ink, false); tmpIn.textComponent.alignment = TextAlignmentOptions.MidlineLeft; }
            if (tmpIn.placeholder is TMP_Text ph) { Text(ph, 30f, new Color(Ink.r, Ink.g, Ink.b, 0.45f), false); ph.alignment = TextAlignmentOptions.MidlineLeft; ph.fontStyle = FontStyles.Normal; }
            var area = (RectTransform)input.Find("Text Area");
            area.offsetMin = new Vector2(28f, 8f); area.offsetMax = new Vector2(-28f, -8f);

            // 공개 설정: 라디오 두 개
            var visRow = (RectTransform)rows.Find("VisibilityRow");
            visRow.GetComponent<LayoutElement>().preferredHeight = 76f;
            var vl = visRow.Find("Label").GetComponent<TMP_Text>();
            Text(vl, 28f, Ink, false); vl.alignment = TextAlignmentOptions.Left;
            ((RectTransform)vl.transform).anchoredPosition = new Vector2(10f, 0f);
            ((RectTransform)vl.transform).pivot = new Vector2(0f, 0.5f);
            ((RectTransform)vl.transform).sizeDelta = new Vector2(220f, 64f);
            var toggles = (RectTransform)visRow.Find("VisibilityToggles");
            toggles.sizeDelta = new Vector2(440f, 70f); toggles.pivot = new Vector2(1f, 0.5f); toggles.anchoredPosition = Vector2.zero;
            Radio((RectTransform)toggles.Find("PublicToggle"), 0f);
            Radio((RectTransform)toggles.Find("PrivateToggle"), 230f);

            var notice = (RectTransform)rows.Find("PrivateNoticeText");
            var nt = notice.GetComponent<TMP_Text>();
            Text(nt, 24f, Ink, false); nt.alignment = TextAlignmentOptions.Left;
            nt.margin = new Vector4(46f, 0f, 0f, 0f);
            var lockIcon = Child(notice, "LockIcon", 0);
            lockIcon.anchorMin = lockIcon.anchorMax = new Vector2(0f, 0.5f); lockIcon.pivot = new Vector2(0f, 0.5f);
            lockIcon.sizeDelta = new Vector2(36f, 36f); lockIcon.anchoredPosition = new Vector2(0f, 0f);
            var li = Img(lockIcon, "UI_Lobby_Icon_Lock_Dark"); li.preserveAspect = true; li.raycastTarget = false;

            PopupFooter(win, "CreateButton");
        }

        static void Radio(RectTransform toggleRt, float x)
        {
            toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(0f, 0.5f); toggleRt.pivot = new Vector2(0f, 0.5f);
            toggleRt.sizeDelta = new Vector2(210f, 70f); toggleRt.anchoredPosition = new Vector2(x, 0f);
            var hit = toggleRt.GetComponent<Image>();
            hit.sprite = null; hit.color = new Color(1f, 1f, 1f, 0f); hit.raycastTarget = true;
            var bgRt = Child(toggleRt, "RadioBg", 0);
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 0.5f); bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.sizeDelta = new Vector2(56f, 65f); bgRt.anchoredPosition = new Vector2(4f, 0f);
            var bg = Img(bgRt, "UI_Lobby_Toggle_Off"); bg.raycastTarget = false; bg.preserveAspect = true;
            var sel = (RectTransform)toggleRt.Find("Selected");
            sel.SetSiblingIndex(1);
            sel.anchorMin = sel.anchorMax = new Vector2(0f, 0.5f); sel.pivot = new Vector2(0f, 0.5f);
            sel.sizeDelta = new Vector2(56f, 65f); sel.anchoredPosition = new Vector2(4f, 0f);
            var si = Img(sel, "UI_Lobby_Toggle_On"); si.raycastTarget = false; si.preserveAspect = true;
            var tg = toggleRt.GetComponent<Toggle>();
            tg.targetGraphic = bg; tg.graphic = si;
            tg.transition = Selectable.Transition.ColorTint;
            var cb = tg.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.selectedColor = Color.white; tg.colors = cb;
            var label = (RectTransform)toggleRt.Find("Label");
            label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one; label.offsetMin = new Vector2(72f, 0f); label.offsetMax = Vector2.zero;
            var lt = label.GetComponent<TMP_Text>(); Text(lt, 30f, Ink, false); lt.alignment = TextAlignmentOptions.Left;
        }

        static void JoinPopup(Transform popup, SerializedObject state)
        {
            PopupWindow(popup, "코드로 참가", 560f);
            var win = popup.Find("Window");
            var guide = Child(win, "GuideText", 1);
            var gImg = guide.GetComponent<Image>(); if (gImg != null) Object.DestroyImmediate(gImg);
            var gt = guide.GetComponent<TextMeshProUGUI>() ?? guide.gameObject.AddComponent<TextMeshProUGUI>();
            guide.anchorMin = guide.anchorMax = new Vector2(0.5f, 1f); guide.pivot = new Vector2(0.5f, 1f);
            guide.sizeDelta = new Vector2(700f, 44f); guide.anchoredPosition = new Vector2(0f, -110f);
            gt.text = "친구에게 받은 6자리 코드를 넣어 주세요";
            Text(gt, 26f, Ink, false); gt.alignment = TextAlignmentOptions.Center; gt.raycastTarget = false;

            var slots = (RectTransform)win.Find("CodeSlots");
            slots.anchorMin = slots.anchorMax = new Vector2(0.5f, 1f); slots.pivot = new Vector2(0.5f, 1f);
            slots.sizeDelta = new Vector2(612f, 92f); slots.anchoredPosition = new Vector2(-50f, -172f);
            var h = slots.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12f; h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false; h.childAlignment = TextAnchor.MiddleCenter;
            var images = state.FindProperty("codeSlots"); var texts = state.FindProperty("codeSlotTexts");
            images.arraySize = 6; texts.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var s = (RectTransform)slots.Find($"CodeSlot_{i + 1}");
                s.sizeDelta = new Vector2(92f, 92f);
                var img = Img(s, "UI_Lobby_CodeSlot"); img.type = Image.Type.Simple; img.raycastTarget = false;
                var st = s.Find("SlotText").GetComponent<TMP_Text>();
                Text(st, 50f, Ink, false); st.alignment = TextAlignmentOptions.Center; st.fontStyle = FontStyles.Bold;
                images.GetArrayElementAtIndex(i).objectReferenceValue = img;
                texts.GetArrayElementAtIndex(i).objectReferenceValue = st;
            }
            state.FindProperty("slotEmpty").objectReferenceValue = Sprite("UI_Lobby_CodeSlot");
            state.FindProperty("slotFilled").objectReferenceValue = Sprite("UI_Lobby_CodeSlot_Filled");
            state.FindProperty("slotError").objectReferenceValue = Sprite("UI_Lobby_CodeSlot_Error");

            // 입력칸은 6칸 위에 겹쳐 키 입력만 받는다 (LobbyUI가 글자를 숨김). 바탕은 투명
            var input = (RectTransform)win.Find("CodeInput");
            input.anchorMin = input.anchorMax = slots.anchorMin; input.pivot = slots.pivot;
            input.sizeDelta = slots.sizeDelta; input.anchoredPosition = slots.anchoredPosition;
            var ii = input.GetComponent<Image>(); ii.sprite = null; ii.color = new Color(1f, 1f, 1f, 0f);
            var ph = input.GetComponent<TMP_InputField>().placeholder; if (ph != null) ph.gameObject.SetActive(false);

            var paste = (RectTransform)win.Find("PasteButton");
            paste.anchorMin = paste.anchorMax = new Vector2(0.5f, 1f); paste.pivot = new Vector2(0.5f, 0.5f);
            paste.sizeDelta = new Vector2(84f, 84f); paste.anchoredPosition = new Vector2(316f, -218f);
            PrimaryButton(paste.GetComponent<Button>(), 1f, "UI_Lobby_RoundBtn", false);
            paste.GetComponent<Image>().preserveAspect = true;
            Icon(paste, "UI_Lobby_Icon_Paste_Dark", 50f, Vector2.zero);

            var err = (RectTransform)win.Find("ErrorText");
            err.anchorMin = err.anchorMax = new Vector2(0.5f, 1f); err.pivot = new Vector2(0.5f, 1f);
            err.sizeDelta = new Vector2(700f, 44f); err.anchoredPosition = new Vector2(0f, -290f);
            var et = err.GetComponent<TMP_Text>(); Text(et, 26f, ErrorRed, false); et.alignment = TextAlignmentOptions.Center;
            state.FindProperty("errorObject").objectReferenceValue = err.gameObject;

            PopupFooter(win, "JoinButton");
        }

        static void Icon(Transform parent, string file, float size, Vector2 pos)
        {
            var label = parent.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
            var ic = Child(parent, "Icon", parent.childCount);
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f); ic.pivot = new Vector2(0.5f, 0.5f);
            ic.sizeDelta = new Vector2(size, size); ic.anchoredPosition = pos;
            var img = Img(ic, file); img.preserveAspect = true; img.raycastTarget = false;
        }

        static void RoomPanel(Transform room, SerializedObject state)
        {
            // 나가기 (왼쪽 위)
            var leave = (RectTransform)room.Find("LeaveButton");
            leave.anchorMin = leave.anchorMax = new Vector2(0f, 1f); leave.pivot = new Vector2(0f, 1f);
            leave.sizeDelta = new Vector2(200f, 80f); leave.anchoredPosition = new Vector2(30f, -30f);
            PrimaryButton(leave.GetComponent<Button>(), 2.6f, "UI_Lobby_BtnSecondary");
            LabelOf(leave, 30f);
            Text(leave.Find("Label").GetComponent<TMP_Text>(), 30f, Ink, false);

            // 방 이름 바 (가운데 위) + 공개 태그
            var info = (RectTransform)room.Find("RoomInfo");
            info.anchorMin = info.anchorMax = new Vector2(0.5f, 1f); info.pivot = new Vector2(0.5f, 1f);
            info.sizeDelta = new Vector2(900f, 130f); info.anchoredPosition = new Vector2(0f, -24f);
            Sliced(info, "UI_Lobby_TitleBar", 2.4f, false);
            var rn = (RectTransform)info.Find("RoomNameText");
            rn.anchorMin = rn.anchorMax = new Vector2(0.5f, 0.5f); rn.pivot = new Vector2(0.5f, 0.5f);
            rn.sizeDelta = new Vector2(560f, 70f); rn.anchoredPosition = new Vector2(-40f, 6f);
            var rnt = rn.GetComponent<TMP_Text>(); Text(rnt, 40f, Cream, true); rnt.alignment = TextAlignmentOptions.Center;
            var tag = (RectTransform)info.Find("VisibilityTag");
            tag.anchorMin = tag.anchorMax = new Vector2(1f, 0.5f); tag.pivot = new Vector2(1f, 0.5f);
            tag.sizeDelta = new Vector2(130f, 44f); tag.anchoredPosition = new Vector2(-110f, 6f);
            var tagImg = Sliced(tag, "UI_Lobby_StatusTag", 2f, false);
            var lockRt = Child(tag, "LockIcon", 0);
            lockRt.anchorMin = lockRt.anchorMax = new Vector2(0f, 0.5f); lockRt.pivot = new Vector2(0f, 0.5f);
            lockRt.sizeDelta = new Vector2(30f, 30f); lockRt.anchoredPosition = new Vector2(12f, 0f);
            var lockImg = Img(lockRt, "UI_Lobby_Icon_Lock_Dark"); lockImg.preserveAspect = true; lockImg.raycastTarget = false;
            var tt = tag.Find("TagText").GetComponent<TMP_Text>();
            Text(tt, 22f, Ink, false); tt.alignment = TextAlignmentOptions.Center; tt.margin = new Vector4(28f, 0f, 0f, 0f);
            state.FindProperty("visibilityTag").objectReferenceValue = tagImg;
            state.FindProperty("lockIcon").objectReferenceValue = lockRt.gameObject;

            // 방 코드 바 + 복사 + 친구 초대
            var bar = (RectTransform)room.Find("CodeBar");
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f); bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(520f, 80f); bar.anchoredPosition = new Vector2(-190f, -170f);
            Sliced(bar, "UI_Lobby_InputField", 2f, false);
            var cl = Child(bar, "CodeLabel", 0);
            var clImg = cl.GetComponent<Image>(); if (clImg != null) Object.DestroyImmediate(clImg);
            var clt = cl.GetComponent<TextMeshProUGUI>() ?? cl.gameObject.AddComponent<TextMeshProUGUI>();
            cl.anchorMin = cl.anchorMax = new Vector2(0f, 0.5f); cl.pivot = new Vector2(0f, 0.5f);
            cl.sizeDelta = new Vector2(130f, 60f); cl.anchoredPosition = new Vector2(28f, 0f);
            clt.text = "방 코드"; Text(clt, 28f, Ink, false); clt.alignment = TextAlignmentOptions.Left; clt.raycastTarget = false;
            var code = (RectTransform)bar.Find("CodeText");
            code.anchorMin = code.anchorMax = new Vector2(0f, 0.5f); code.pivot = new Vector2(0f, 0.5f);
            code.sizeDelta = new Vector2(320f, 70f); code.anchoredPosition = new Vector2(150f, 0f);
            var ct = code.GetComponent<TMP_Text>(); Text(ct, 36f, Ink, false); ct.alignment = TextAlignmentOptions.Left; ct.fontStyle = FontStyles.Bold; ct.characterSpacing = 6f;
            var copy = (RectTransform)bar.Find("CopyCodeButton");
            copy.anchorMin = copy.anchorMax = new Vector2(1f, 0.5f); copy.pivot = new Vector2(0f, 0.5f);
            copy.sizeDelta = new Vector2(76f, 76f); copy.anchoredPosition = new Vector2(16f, 0f);
            PrimaryButton(copy.GetComponent<Button>(), 1f, "UI_Lobby_RoundBtn", false);
            copy.GetComponent<Image>().preserveAspect = true;
            Icon(copy, "UI_Lobby_Icon_Copy_Dark", 44f, Vector2.zero);
            var steam = (RectTransform)bar.Find("SteamInviteButton");
            steam.anchorMin = steam.anchorMax = new Vector2(1f, 0.5f); steam.pivot = new Vector2(0f, 0.5f);
            steam.sizeDelta = new Vector2(260f, 80f); steam.anchoredPosition = new Vector2(108f, 0f);
            PrimaryButton(steam.GetComponent<Button>(), 2.6f, "UI_Lobby_BtnPrimaryBlue");
            LabelOf(steam, 28f);
            ((RectTransform)steam.Find("Label")).offsetMin = new Vector2(70f, 4f);
            var inv = Child(steam, "InviteIcon", steam.childCount);
            inv.anchorMin = inv.anchorMax = new Vector2(0f, 0.5f); inv.pivot = new Vector2(0f, 0.5f);
            inv.sizeDelta = new Vector2(44f, 44f); inv.anchoredPosition = new Vector2(26f, 2f);
            var invImg = Img(inv, "UI_Lobby_Icon_Invite"); invImg.preserveAspect = true; invImg.raycastTarget = false;

            // 팀·준비 (아래 가운데). 판 바탕은 없앤다 (3D 무대가 보이게)
            var panel = (RectTransform)room.Find("TeamReadyPanel");
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f); panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(900f, 300f); panel.anchoredPosition = new Vector2(0f, 30f);
            var pImg = panel.GetComponent<Image>(); pImg.sprite = null; pImg.color = new Color(1f, 1f, 1f, 0f); pImg.raycastTarget = false;
            var teamLabel = (RectTransform)panel.Find("TeamLabel");
            teamLabel.anchorMin = teamLabel.anchorMax = new Vector2(0.5f, 1f); teamLabel.pivot = new Vector2(0.5f, 1f);
            teamLabel.sizeDelta = new Vector2(400f, 44f); teamLabel.anchoredPosition = Vector2.zero;
            var tl = teamLabel.GetComponent<TMP_Text>(); Text(tl, 30f, Cream, true); tl.alignment = TextAlignmentOptions.Center;
            Team((RectTransform)panel.Find("RedTeamButton"), new Vector2(0f, 1f), "UI_Lobby_TeamRed", state, "redButton", "redSprites");
            Team((RectTransform)panel.Find("BlueTeamButton"), new Vector2(1f, 1f), "UI_Lobby_TeamBlue", state, "blueButton", "blueSprites");

            var ready = (RectTransform)panel.Find("ReadyButton");
            ready.anchorMin = ready.anchorMax = new Vector2(0f, 0f); ready.pivot = new Vector2(0.5f, 0f);
            ready.sizeDelta = new Vector2(360f, 90f); ready.anchoredPosition = new Vector2(210f, 0f);
            PrimaryButton(ready.GetComponent<Button>(), 2f);
            LabelOf(ready, 36f);
            WriteSet(state.FindProperty("readySet"), Set("UI_Lobby_BtnPrimary"));
            WriteSet(state.FindProperty("cancelSet"), Set("UI_Lobby_BtnPrimaryOrange"));
            state.FindProperty("readyButton").objectReferenceValue = ready.GetComponent<Button>();

            var start = (RectTransform)room.Find("StartGameButton");
            start.anchorMin = start.anchorMax = new Vector2(0.5f, 0f); start.pivot = new Vector2(0.5f, 0f);
            start.sizeDelta = new Vector2(360f, 90f); start.anchoredPosition = new Vector2(240f, 30f);
            PrimaryButton(start.GetComponent<Button>(), 2f, "UI_Lobby_BtnPrimaryGold");
            LabelOf(start, 36f);
        }

        static void Team(RectTransform b, Vector2 anchor, string baseName, SerializedObject state, string imgProp, string setProp)
        {
            b.anchorMin = b.anchorMax = anchor; b.pivot = anchor;
            b.sizeDelta = new Vector2(420f, 150f); b.anchoredPosition = new Vector2(0f, -52f);
            var img = Sliced(b, baseName, 2f, true);
            var btn = b.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint; // 그림은 LobbySkinState가 상태로 바꾼다. 비활성(이미 고름)도 색은 그대로
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.selectedColor = Color.white; cb.disabledColor = Color.white; btn.colors = cb;
            LabelOf(b, 48f);
            ((RectTransform)b.Find("Label")).offsetMin = new Vector2(90f, 6f);
            state.FindProperty(imgProp).objectReferenceValue = img;
            var arr = state.FindProperty(setProp); arr.arraySize = 4;
            string[] suffix = { "", "_Mine", "_Other", "_Both" };
            for (int i = 0; i < 4; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = Sprite(baseName + suffix[i]);
        }

        static void WriteSet(SerializedProperty p, LobbySkinState.SpriteSet s)
        {
            p.FindPropertyRelative("normal").objectReferenceValue = s.normal;
            p.FindPropertyRelative("highlighted").objectReferenceValue = s.highlighted;
            p.FindPropertyRelative("pressed").objectReferenceValue = s.pressed;
            p.FindPropertyRelative("disabled").objectReferenceValue = s.disabled;
        }

        static void LabelOf(Transform button, float size)
        {
            var label = button.Find("Label");
            if (label == null) return;
            var rt = (RectTransform)label;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(10f, 4f); rt.offsetMax = new Vector2(-10f, -4f);
            var t = label.GetComponent<TMP_Text>();
            Text(t, size, Cream, true);
            t.alignment = TextAlignmentOptions.Center;
        }

        static void Column(Transform t, Vector2 anchor, Vector2 pos, Vector2 size, TextAlignmentOptions align)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = align == TextAlignmentOptions.Left ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp == null) return;
            tmp.alignment = align;
            if (t.name.EndsWith("Header")) Text(tmp, 26f, Cream, true);
        }

        static void Text(TMP_Text t, float size, Color color, bool withOutline)
        {
            t.font = font;
            t.fontSharedMaterial = withOutline ? outline : font.material;
            t.fontSize = size;
            t.enableAutoSizing = false;
            t.color = color;
        }
    }
}
