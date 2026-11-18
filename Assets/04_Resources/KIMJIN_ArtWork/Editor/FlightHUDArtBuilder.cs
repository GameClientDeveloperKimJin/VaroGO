using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VaroGO.Art.EditorTools
{
    // 비행 화면(PlayScene_Flight) HUD 아트 프리팹을 만든다. 아트 UI 세션 제작.
    // 메뉴 한 번 → 스프라이트 임포트 설정 + UI_FlightHUD.prefab 생성(있으면 덮어씀).
    // 화면 배치·모양만 있다. 값 갱신(진행도·거리·키 눌림)은 개발 세션이 FlightHUD에서 연결한다.
    static class FlightHUDArtBuilder
    {
        const string Dir = "Assets/04_Resources/KIMJIN_ArtWork/UI/Flight/";
        const string PrefabPath = Dir + "UI_FlightHUD.prefab";
        const string GaugeFrame = "Assets/04_Resources/UI/InGame/UI_ThrowGauge_Frame.png";
        const string GaugeFill = "Assets/04_Resources/UI/InGame/UI_ThrowGauge_Fill.png";
        const string FontPath = "Assets/04_Resources/UI/ONE Mobile POP SDF.asset";

        static readonly Color Cream = new Color(1f, 0.965f, 0.88f);
        static readonly Color KeyLetter = new Color(0.43f, 0.24f, 0.12f);
        static readonly Color FillGreen = new Color(0.45f, 0.85f, 0.35f);

        // 내구도 상태 4단계 색 (Border 색 / PercentText 색). 에디터 전용이라 런타임 코드는 값을 복사해서 씀
        // 안전 61~100% · 주의 31~60% · 위험 1~30% · 파괴 0% (KJ: 파괴 시 기존대로 3초 뒤 처음부터, 시안의 입력 잠금·체크포인트는 안 씀)
        public static readonly Color DurSafeBorder = new Color(1f, 1f, 1f, 0.35f);
        public static readonly Color DurSafeText = Color.white;
        public static readonly Color DurWarnBorder = new Color(0.95f, 0.65f, 0.2f);
        public static readonly Color DurWarnText = new Color(1f, 0.8f, 0.45f);
        public static readonly Color DurDangerBorder = new Color(0.9f, 0.3f, 0.25f);
        public static readonly Color DurDangerText = new Color(1f, 0.5f, 0.45f);
        public static readonly Color DurBrokenBorder = new Color(0.35f, 0.38f, 0.45f);
        public static readonly Color DurBrokenText = new Color(0.85f, 0.55f, 0.55f);
        public static readonly Color DurEmpty = new Color(1f, 1f, 1f, 0.35f);     // 빈 칸 점선
        public static readonly Color DurVignette = new Color(1f, 0.15f, 0.1f, 0.55f); // 위험 비네트 최대

        [MenuItem("VAROGO/Art/비행 HUD 프리팹 만들기 (UI_FlightHUD)")]
        public static void Build()
        {
            SetupSprite(Dir + "Icon_FlightPlane.png", Vector4.zero);
            SetupSprite(Dir + "UI_FlightKey.png", Vector4.zero);
            SetupSprite(Dir + "UI_FlightKeyArrow.png", Vector4.zero);
            SetupSprite(Dir + "UI_DurTray_Bg.png", new Vector4(24, 24, 24, 24));
            SetupSprite(Dir + "UI_DurTray_Border.png", new Vector4(24, 24, 24, 24));
            SetupSprite(Dir + "UI_DurPlank.png", Vector4.zero);
            SetupSprite(Dir + "UI_DurPlank_Empty.png", Vector4.zero);
            SetupSprite(Dir + "UI_DurPlank_Crack.png", Vector4.zero);
            SetupSprite(Dir + "UI_ScreenVignette.png", Vector4.zero);

            var trayBg = Load<Sprite>(Dir + "UI_DurTray_Bg.png");
            var trayBorder = Load<Sprite>(Dir + "UI_DurTray_Border.png");
            var plank = Load<Sprite>(Dir + "UI_DurPlank.png");
            var plankEmpty = Load<Sprite>(Dir + "UI_DurPlank_Empty.png");
            var plankCrack = Load<Sprite>(Dir + "UI_DurPlank_Crack.png");
            var vignette = Load<Sprite>(Dir + "UI_ScreenVignette.png");
            if (!trayBg || !trayBorder || !plank || !plankEmpty || !plankCrack || !vignette)
            {
                EditorUtility.DisplayDialog("비행 HUD", "내구도 리소스를 못 찾았습니다. Console 로그를 확인해 주세요.", "확인");
                return;
            }

            var plane = Load<Sprite>(Dir + "Icon_FlightPlane.png");
            var key = Load<Sprite>(Dir + "UI_FlightKey.png");
            var arrow = Load<Sprite>(Dir + "UI_FlightKeyArrow.png");
            var frame = Load<Sprite>(GaugeFrame);
            var fill = Load<Sprite>(GaugeFill);
            var font = Load<TMP_FontAsset>(FontPath);
            if (!plane || !key || !arrow || !frame || !fill || !font)
            {
                EditorUtility.DisplayDialog("비행 HUD", "리소스를 못 찾았습니다. Console 로그를 확인해 주세요.", "확인");
                return;
            }

            // 루트 캔버스 (기준 1920x1080)
            var root = new GameObject("UI_FlightHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 위험 단계 화면 가장자리 붉은 비네트 (맨 뒤). 기본 꺼짐(알파 0). 코드: color = DurVignette, 알파로 세기 조절
            var vig = Stretch("DangerVignette", root.transform, 0, 0, 0, 0);
            Img(vig, vignette, Image.Type.Simple).color = new Color(DurVignette.r, DurVignette.g, DurVignette.b, 0f);

            // ── 상단 진행 판 ──
            // KJ 결정(11-19): 비행기 아이콘·남은 거리 숫자 뺌, 이어서 TopBar 돌 판도 지움 → 글자 + 진행 바만 루트 바로 아래
            var label = Rect("GoalLabel", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -37), new Vector2(560, 30));
            Text(label, font, "목표까지 남은 거리", 24, Cream, TextAlignmentOptions.Center);

            var bar = Rect("ProgressBar", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(560, 54));
            var barImg = Img(bar, frame, Image.Type.Sliced);
            barImg.pixelsPerUnitMultiplier = 2f;

            var fillRt = Stretch("Fill", bar, 27, 26, 21, 19);
            var fillImg = Img(fillRt, fill, Image.Type.Filled);
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0.62f;
            fillImg.color = FillGreen;

            // 바 위를 따라가는 작은 비행기. MarkerTrack = 채움 영역과 같은 크기.
            // 코드: Marker의 anchorMin.x = anchorMax.x = 진행도(0~1)
            var track = Stretch("MarkerTrack", bar, 27, 26, 21, 19);
            var marker = Rect("Marker", track, new Vector2(0.62f, 0.5f), new Vector2(0.62f, 0.5f), Vector2.zero, new Vector2(67, 44));
            marker.pivot = new Vector2(0.5f, 0.5f);
            Img(marker, plane, Image.Type.Simple).preserveAspect = true;

            // ── 하단 중앙 내구도 v4 (KJ 11-19, 상태 4단계 시안) ──
            // 판자 10칸 + 아래 % 숫자. 판자 칸 수 = 내구도 비율 x 10 (올림). 상태별 색은 아래 Dur* 상수 참고.
            // Plank_N: Empty(점선, 항상 보임) 위에 Wood(판자). 잃은 칸 = Wood 끔. 위험 단계 = 마지막 남은 칸의 Wood/Crack 켬.
            var dur = Rect("DurabilityBar", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(320, 116));
            dur.pivot = new Vector2(0.5f, 0f);

            var tray = Rect("Tray", dur, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(320, 60));
            Img(tray, trayBg, Image.Type.Sliced);
            var border = Stretch("Border", tray, 0, 0, 0, 0);
            Img(border, trayBorder, Image.Type.Sliced).color = DurSafeBorder;

            var planks = Stretch("Planks", tray, 0, 0, 0, 0);
            var layout = planks.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            for (int i = 1; i <= 10; i++)
            {
                var p = Rect("Plank_" + i, planks, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(23, 35));
                p.pivot = new Vector2(0.5f, 0.5f);
                Img(Stretch("Empty", p, 0, 0, 0, 0), plankEmpty, Image.Type.Simple).color = DurEmpty;
                var wood = Stretch("Wood", p, 0, 0, 0, 0);
                Img(wood, plank, Image.Type.Simple);
                var crack = Stretch("Crack", wood, 0, 0, 0, 0);
                Img(crack, plankCrack, Image.Type.Simple);
                crack.gameObject.SetActive(false);
            }

            var pct = Rect("PercentText", dur, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(320, 52));
            pct.pivot = new Vector2(0.5f, 0f);
            Text(pct, font, "100%", 44, DurSafeText, TextAlignmentOptions.Center);

            // ── 카운트다운 3·2·1 / 출발! (KJ 11-19 승인) ──
            // 글자 = TMP + 전용 머티리얼 M_FlightTitle(두꺼운 갈색 테두리 + 아래 그림자). 색은 위→아래 그라데이션(정점 색).
            // 둘 다 기본 꺼짐. 코드: 카운트다운 단계에 CountdownText 켜고 text = "3"/"2"/"1", 비행 시작 직후 GoText 켬.
            var titleMat = TitleMaterial(font);
            var cd = Rect("CountdownText", root.transform, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(600, 280));
            cd.pivot = new Vector2(0.5f, 0.5f);
            Title(cd, font, titleMat, "3", 220, new Color(1f, 0.98f, 0.84f), new Color(1f, 0.75f, 0.27f));
            cd.gameObject.SetActive(false);

            var go = Rect("GoText", root.transform, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(800, 240));
            go.pivot = new Vector2(0.5f, 0.5f);
            Title(go, font, titleMat, "출발!", 170, new Color(1f, 0.92f, 0.47f), new Color(1f, 0.49f, 0.16f));
            go.gameObject.SetActive(false);

            // ── 키 안내 (왼쪽 화면 A◀, 오른쪽 화면 D▶) ──
            KeyPrompt(root.transform, "KeyPrompt_A", new Vector2(0.14f, 0.55f), "A", true, key, arrow, font);
            KeyPrompt(root.transform, "KeyPrompt_D", new Vector2(0.86f, 0.55f), "D", false, key, arrow, font);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            Object.DestroyImmediate(root);
            Debug.Log(ok ? $"[비행 HUD] 저장: {PrefabPath}" : "[비행 HUD] 프리팹 저장 실패");
            if (ok) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        static void KeyPrompt(Transform parent, string name, Vector2 anchor, string letter, bool arrowLeft, Sprite key, Sprite arrow, TMP_FontAsset font)
        {
            var root = Rect(name, parent, anchor, anchor, Vector2.zero, new Vector2(95, 96));
            root.pivot = new Vector2(0.5f, 0.5f);

            var keyRt = Stretch("Key", root, 0, 0, 0, 0);
            Img(keyRt, key, Image.Type.Simple);

            // 키 윗면 중앙(아랫단 두께만큼 위로)
            var letterRt = Stretch("Letter", keyRt, 0, 0, 0, 8);
            Text(letterRt, font, letter, 58, KeyLetter, TextAlignmentOptions.Center);

            float x = arrowLeft ? -(95 / 2f + 10 + 37 / 2f) : 95 / 2f + 10 + 37 / 2f;
            var arrowRt = Rect("Arrow", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(37, 40));
            arrowRt.pivot = new Vector2(0.5f, 0.5f);
            if (arrowLeft) arrowRt.localScale = new Vector3(-1f, 1f, 1f);
            Img(arrowRt, arrow, Image.Type.Simple);
        }

        // 큰 제목 글자용 머티리얼 (폰트 기본 머티리얼 복사 + 테두리·그림자). 있으면 값만 다시 씀
        static Material TitleMaterial(TMP_FontAsset font)
        {
            string path = Dir + "M_FlightTitle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.CopyPropertiesFromMaterial(font.material);

            mat.SetColor("_FaceColor", Color.white);
            mat.SetFloat("_FaceDilate", 0.15f);
            mat.SetColor("_OutlineColor", new Color(0.35f, 0.18f, 0.08f, 1f));
            mat.SetFloat("_OutlineWidth", 0.3f);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0.16f, 0.08f, 0.04f, 0.6f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.5f);
            mat.SetFloat("_UnderlayDilate", 0.3f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void Title(RectTransform rt, TMP_FontAsset font, Material mat, string s, float size, Color top, Color bottom)
        {
            var t = Text(rt, font, s, size, Color.white, TextAlignmentOptions.Center);
            t.fontSharedMaterial = mat;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        static void SetupSprite(string path, Vector4 border)
        {
            var t = AssetImporter.GetAtPath(path) as TextureImporter;
            if (t == null) { Debug.LogError("[비행 HUD] 텍스처 없음: " + path); return; }
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = 100;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.wrapMode = TextureWrapMode.Clamp;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.spriteBorder = border;
            t.SaveAndReimport();
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogError("[비행 HUD] 없음: " + path);
            return a;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = aMin;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(string name, Transform parent, float l, float r, float t, float b)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        static Image Img(RectTransform rt, Sprite sprite, Image.Type type)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset font, string s, float size, Color color, TextAlignmentOptions align)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = s;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
