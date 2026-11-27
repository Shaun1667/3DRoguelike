using System;
using System.Collections.Generic;
using System.Text;
using Game.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 성장 트리 화면(3-7). 결과 화면 [성장 트리] · 월드 시작 지점(G) · 디버그 창에서 연다. 코드로 세운다(씬 · 프리팹에 넣지 않음).
    /// 두 갈래(Beast 왼쪽 · Human 오른쪽), 노드마다 이름 · 설명 · 효과 · 비용 · [사기], 앞 노드를 안 샀으면 잠김.
    /// 떠 있는 동안 게임 시간은 멈추고 커서가 풀린다. 닫으면 연 때 상태로 돌린다. 키: Esc · G 닫기.
    /// </summary>
    public sealed class MetaTreeScreen : MonoBehaviour
    {
        private const float CardWidth = 820f;
        private const float CardHeight = 128f;

        private sealed class Card
        {
            public MetaNode node;
            public Image back;
            public TMP_Text body;
            public Button button;
            public Image buttonImage;
            public TMP_Text buttonLabel;
        }

        private static MetaTreeScreen shown;

        private readonly List<Card> cards = new List<Card>();
        private TMP_Text header;
        private TMP_Text message;
        private TMP_Text note;
        private Func<RunSession> currentRun;
        private Action closed;
        private float previousTimeScale;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private int openedFrame;

        public static MetaTreeScreen Shown => shown != null && shown.gameObject.activeSelf ? shown : null;

        private static int closedFrame = -1;

        /// <summary>떠 있지 않고, 이번 프레임에 G로 닫힌 것도 아니면 true(같은 키로 닫자마자 다시 열리지 않게).</summary>
        public static bool CanOpenThisFrame => Shown == null && Time.frameCount != closedFrame;
        public bool IsShown => gameObject.activeSelf;
        public string HeaderText => header.text;
        public string MessageText => message.text;

        /// <param name="font">한글 글꼴</param>
        /// <param name="current">지금 판(아무것도 안 한 판이면 산 노드를 바로 건다). 결과 화면이면 끝난 판 — 다음 재생부터.</param>
        /// <param name="onClosed">닫힌 뒤</param>
        public static MetaTreeScreen Open(TMP_FontAsset font, Func<RunSession> current, Action onClosed = null)
        {
            if (!MetaRun.Ready) return null;
            if (Shown != null) return shown;
            var root = new GameObject("[MetaTreeScreen]");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var screen = root.AddComponent<MetaTreeScreen>();
            screen.Build(font);
            screen.currentRun = current;
            screen.closed = onClosed;
            screen.previousTimeScale = Time.timeScale;
            screen.previousLock = Cursor.lockState;
            screen.previousVisible = Cursor.visible;
            screen.openedFrame = Time.frameCount;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            shown = screen;
            MetaRun.Changed += screen.Refresh;
            screen.Refresh();
            return screen;
        }

        private void Build(TMP_FontAsset font)
        {
            RectTransform shade = Rect("Shade", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            shade.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.03f, 0.97f);

            Text(font, "Title", transform, "성장 트리", 58, new Vector2(0f, 470f), new Vector2(1400f, 80f), new Color(0.85f, 0.9f, 0.8f));
            header = Text(font, "Header", transform, "", 30, new Vector2(0f, 405f), new Vector2(1700f, 50f), new Color(0.95f, 0.85f, 0.45f));
            Text(font, "BeastHead", transform, "Beast — 야수의 길", 34, new Vector2(-460f, 335f), new Vector2(CardWidth, 50f), new Color(0.85f, 0.55f, 0.4f));
            Text(font, "HumanHead", transform, "Human — 인간의 길", 34, new Vector2(460f, 335f), new Vector2(CardWidth, 50f), new Color(0.5f, 0.7f, 0.9f));

            var rows = new Dictionary<Tendency, int>();
            foreach (MetaNode node in MetaRun.Tree.Nodes)
            {
                rows.TryGetValue(node.Branch, out int row);
                rows[node.Branch] = row + 1;
                float x = node.Branch == Tendency.Beast ? -460f : 460f;
                // 앞 노드가 있는 노드는 조금 들여 써서 갈래가 보이게
                float indent = node.Requires != null ? 30f : 0f;
                cards.Add(BuildCard(font, node, new Vector2(x + indent, 250f - row * (CardHeight + 14f))));
            }

            message = Text(font, "Message", transform, "", 28, new Vector2(0f, -385f), new Vector2(1700f, 44f), new Color(0.9f, 0.9f, 0.85f));
            note = Text(font, "Note", transform, "", 24, new Vector2(0f, -425f), new Vector2(1700f, 40f), new Color(0.65f, 0.65f, 0.6f));
            Button(font, transform, "Close", "닫기  [Esc · G]", new Vector2(0f, -485f), new Vector2(360f, 70f), new Color(0.22f, 0.3f, 0.2f), Close, out _, out _);
        }

        private Card BuildCard(TMP_FontAsset font, MetaNode node, Vector2 position)
        {
            RectTransform rect = Rect($"Node_{node.Id}", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(CardWidth - 30f, CardHeight));
            var card = new Card { node = node, back = rect.gameObject.AddComponent<Image>() };
            card.body = Text(font, "Body", rect, "", 24, new Vector2(-110f, 0f), new Vector2(CardWidth - 280f, CardHeight - 12f), Color.white);
            card.body.alignment = TextAlignmentOptions.MidlineLeft;
            card.body.richText = true;
            string id = node.Id;
            card.button = Button(font, rect, "Buy", "", new Vector2(CardWidth * 0.5f - 130f, 0f), new Vector2(200f, 80f), Color.gray, () => Buy(id),
                                 out card.buttonImage, out card.buttonLabel);
            return card;
        }

        /// <summary>[사기](버튼 · 확인용 코드).</summary>
        public bool Buy(string id)
        {
            bool bought = MetaRun.TryBuy(id, currentRun?.Invoke(), out string result);
            message.text = bought ? result : $"살 수 없음: {result}";
            Refresh();
            return bought;
        }

        public void Close()
        {
            if (!IsShown) return;
            MetaRun.Changed -= Refresh;
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            if (shown == this) shown = null;
            closedFrame = Time.frameCount;
            Action callback = closed;
            closed = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            MetaRun.Changed -= Refresh;
            if (shown == this) shown = null;
        }

        private void Refresh()
        {
            if (header == null) return;
            MetaProgress progress = MetaRun.Progress;
            header.text = $"영구 재화  {progress.Currency}   ·   노드 {progress.Owned.Count} / {MetaRun.Tree.Count}   ·   재생 {progress.Runs}번";
            RunSession run = currentRun?.Invoke();
            note.text = run != null && run.IsUntouched
                ? "아직 아무것도 안 한 판 — 지금 사면 이번 판부터 적용"
                : "산 노드는 다음 재생(새 판)부터 적용 · 판 안 성향(Beast/Human)은 오르지 않음";
            foreach (Card card in cards) RefreshCard(card, progress);
        }

        private static void RefreshCard(Card card, MetaProgress progress)
        {
            MetaNode node = card.node;
            MetaNodeState state = progress.StateOf(node);
            var text = new StringBuilder();
            text.Append($"<size=32><b>{node.Name}</b></size>\n{node.Description}");
            var effects = new List<string>();
            foreach (StatEffect effect in node.Effects) effects.Add(UpgradeStats.Describe(effect));
            if (effects.Count > 0) text.Append($"  <color=#C8E6A0>({string.Join(", ", effects)})</color>");
            if (node.Requires != null && MetaRun.Tree.TryGet(node.Requires, out MetaNode before))
                text.Append($"\n<size=20><color=#{(progress.Owns(before.Id) ? "8A9A80" : "D08060")}>앞 노드: {before.Name}</color></size>");
            card.body.text = text.ToString();

            Color back, button;
            string label;
            switch (state)
            {
                case MetaNodeState.Owned: back = new Color(0.16f, 0.26f, 0.14f, 0.95f); button = new Color(0.25f, 0.42f, 0.22f); label = "보유"; break;
                case MetaNodeState.Locked: back = new Color(0.1f, 0.1f, 0.1f, 0.95f); button = new Color(0.22f, 0.22f, 0.22f); label = $"잠김 · {node.Cost}"; break;
                case MetaNodeState.TooExpensive: back = new Color(0.16f, 0.16f, 0.15f, 0.95f); button = new Color(0.3f, 0.28f, 0.22f); label = $"{node.Cost} · 부족"; break;
                default: back = new Color(0.2f, 0.2f, 0.17f, 0.95f); button = new Color(0.55f, 0.45f, 0.15f); label = $"사기  {node.Cost}"; break;
            }
            card.back.color = back;
            card.buttonImage.color = button;
            card.buttonLabel.text = label;
            card.button.interactable = state == MetaNodeState.Buyable;
        }

        /// <summary>노드 하나의 화면 상태(확인용).</summary>
        public string DescribeNode(string id)
        {
            foreach (Card card in cards)
                if (card.node.Id == id) return $"{card.buttonLabel.text} · {(card.button.interactable ? "누를 수 있음" : "누를 수 없음")}";
            return null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || Time.frameCount == openedFrame) return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.gKey.wasPressedThisFrame) Close();
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

        private static TMP_Text Text(TMP_FontAsset font, string name, Transform parent, string value, float size, Vector2 position, Vector2 box, Color color)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, box);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(TMP_FontAsset font, Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, Action click,
                                     out Image image, out TMP_Text text)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            text = Text(font, "Label", rect, label, 28, Vector2.zero, size, Color.white);
            return button;
        }
    }
}
