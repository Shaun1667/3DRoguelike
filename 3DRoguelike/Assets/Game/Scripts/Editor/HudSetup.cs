using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// HUD(체력 · 탄 · 미니맵)와 결과 화면을 씬의 HUD 캔버스에 만든다. TestRoomSetup · FloorSetup이 부른다.
    /// 한글은 Noto Sans KR(SIL OFL, Art/Fonts) 동적 글꼴 에셋으로 쓴다 — 쓰인 글자만 아틀라스에 채워진다.
    /// </summary>
    public static class HudSetup
    {
        private const string FontPath = "Assets/Game/Art/Fonts/NotoSansKR-Regular.otf";
        private const string FontAssetPath = "Assets/Game/Art/Fonts/NotoSansKR SDF.asset";
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>floor가 null이면 미니맵을 숨긴다(시험 방).</summary>
        public static GameHud BuildHud(Canvas canvas, PlayerVitals player, FloorRun floor)
        {
            TMP_FontAsset font = LoadOrCreateFontAsset();
            ConfigureScaler(canvas);

            // 왼쪽 아래 체력 막대
            RectTransform healthBar = Rect("HealthBar", canvas.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(320f, 26f), new Vector2(0f, 0f));
            AddImage(healthBar, PanelColor);
            RectTransform fill = Rect("Fill", healthBar, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            AddImage(fill, new Color(0.8f, 0.25f, 0.2f, 0.9f));
            TMP_Text healthText = Text("HealthText", healthBar, font, 18f, TextAlignmentOptions.Center);
            Stretch(healthText.rectTransform);

            // 오른쪽 아래 탄
            TMP_Text ammoText = Text("AmmoText", canvas.transform, font, 30f, TextAlignmentOptions.BottomRight);
            Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(240f, 44f), new Vector2(1f, 0f));

            // 오른쪽 위 미니맵(가운데 = 시작 방)
            RectTransform minimap = Rect("Minimap", canvas.transform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(160f, 160f), new Vector2(1f, 1f));
            AddImage(minimap, new Color(0f, 0f, 0f, 0.25f));

            var hud = canvas.gameObject.AddComponent<GameHud>();
            var fields = new SerializedObject(hud);
            fields.FindProperty("player").objectReferenceValue = player;
            fields.FindProperty("floor").objectReferenceValue = floor;
            fields.FindProperty("healthFill").objectReferenceValue = fill;
            fields.FindProperty("healthText").objectReferenceValue = healthText;
            fields.FindProperty("ammoText").objectReferenceValue = ammoText;
            fields.FindProperty("minimap").objectReferenceValue = minimap;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        /// <summary>화면 전체를 덮는 결과 화면(처음에는 꺼짐)과 버튼 입력용 EventSystem.</summary>
        public static RunResultScreen BuildResultScreen(Canvas canvas)
        {
            TMP_FontAsset font = LoadOrCreateFontAsset();
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            RectTransform panel = Rect("ResultPanel", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(panel);
            AddImage(panel, new Color(0f, 0f, 0f, 0.7f)).raycastTarget = true;

            TMP_Text title = Text("Title", panel, font, 64f, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(800f, 90f), new Vector2(0.5f, 0.5f));
            TMP_Text body = Text("Body", panel, font, 30f, TextAlignmentOptions.Center);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 260f), new Vector2(0.5f, 0.5f));

            Button newFloor = Button("NewFloorButton", panel, font, "새 층  (R)", new Vector2(-150f, -200f));
            Button sameFloor = Button("SameFloorButton", panel, font, "같은 층  (T)", new Vector2(150f, -200f));

            var screen = canvas.gameObject.AddComponent<RunResultScreen>();
            var fields = new SerializedObject(screen);
            fields.FindProperty("panel").objectReferenceValue = panel.gameObject;
            fields.FindProperty("title").objectReferenceValue = title;
            fields.FindProperty("body").objectReferenceValue = body;
            fields.FindProperty("newFloorButton").objectReferenceValue = newFloor;
            fields.FindProperty("sameFloorButton").objectReferenceValue = sameFloor;
            fields.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return screen;
        }

        private static TMP_FontAsset LoadOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            if (Shader.Find("TextMeshPro/Distance Field") == null)
            {
                throw new InvalidOperationException("TextMeshPro 셰이더가 없습니다. Window > TextMeshPro > Import TMP Essential Resources를 먼저 실행하세요.");
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (source == null) throw new InvalidOperationException("글꼴 파일이 없습니다: " + FontPath);

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "NotoSansKR SDF";
            AssetDatabase.CreateAsset(font, FontAssetPath);
            font.atlasTextures[0].name = "NotoSansKR SDF Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            font.material.name = "NotoSansKR SDF Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        private static void ConfigureScaler(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            Place(rect, anchor, position, size, pivot);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, Transform parent, TMP_FontAsset font, string label, Vector2 position)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, new Vector2(240f, 64f), new Vector2(0.5f, 0.5f));
            Image background = AddImage(rect, new Color(0.25f, 0.25f, 0.28f, 0.95f));
            background.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            TMP_Text text = Text("Label", rect, font, 28f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }
    }
}
