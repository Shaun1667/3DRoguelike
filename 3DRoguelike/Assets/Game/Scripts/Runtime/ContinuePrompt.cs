using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 게임을 켰을 때 저장된 판이 있으면 뜨는 최소 시작 화면(3-6): [이어 하기] / [새 판]. 시작 메뉴가 아직 없어
    /// 월드가 플레이할 때 코드로 세운다(씬 · 프리팹에 넣지 않음). 떠 있는 동안 게임 시간은 멈추고 커서가 풀린다.
    /// 키: Enter · C = 이어 하기, N = 새 판.
    /// </summary>
    public sealed class ContinuePrompt : MonoBehaviour
    {
        private Action onContinue;
        private Action onNewRun;
        private TMP_Text body;

        public bool IsShown => gameObject.activeSelf;
        public string BodyText => body != null ? body.text : "";

        /// <summary>화면을 세워 띄운다. font는 한글 글꼴(월드 안내 글자의 글꼴을 넘긴다).</summary>
        public static ContinuePrompt Show(TMP_FontAsset font, string summary, Action continueRun, Action newRun)
        {
            var root = new GameObject("[ContinuePrompt]");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root.AddComponent<GraphicRaycaster>();

            var shade = Rect("Shade", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            shade.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.03f, 0.88f);

            var prompt = root.AddComponent<ContinuePrompt>();
            Text(font, "Title", root.transform, "저장된 판이 있습니다", 64, new Vector2(0f, 170f), new Color(0.85f, 0.9f, 0.8f));
            prompt.body = Text(font, "Body", root.transform, summary ?? "", 30, new Vector2(0f, 40f), new Color(0.8f, 0.8f, 0.78f));
            Button("Continue", font, root.transform, "이어 하기  [Enter]", new Vector2(-220f, -130f), () => prompt.Choose(true));
            Button("NewRun", font, root.transform, "새 판  [N]", new Vector2(220f, -130f), () => prompt.Choose(false));
            Text(font, "Note", root.transform, "새 판을 고르면 저장된 판은 지워집니다", 24, new Vector2(0f, -240f), new Color(0.65f, 0.6f, 0.55f));

            prompt.onContinue = continueRun;
            prompt.onNewRun = newRun;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return prompt;
        }

        /// <summary>고르기(버튼 · 키 · 확인용 코드).</summary>
        public void Choose(bool continueRun)
        {
            if (!IsShown) return;
            Action callback = continueRun ? onContinue : onNewRun;
            onContinue = onNewRun = null;
            Time.timeScale = 1f;
            if (!continueRun)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            Destroy(gameObject);
            gameObject.SetActive(false);
            callback?.Invoke();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame) Choose(true);
            else if (keyboard.nKey.wasPressedThisFrame) Choose(false);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TMP_Text Text(TMP_FontAsset font, string name, Transform parent, string value, float size, Vector2 position, Color color)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(1400f, 160f));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        private static void Button(string name, TMP_FontAsset font, Transform parent, string label, Vector2 position, Action click)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(380f, 90f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.3f, 0.2f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            TMP_Text text = Text(font, "Label", rect, label, 34, Vector2.zero, Color.white);
            ((RectTransform)text.transform).sizeDelta = new Vector2(380f, 90f);
        }
    }
}
