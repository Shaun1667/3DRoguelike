using System.Collections.Generic;
using Game.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 화면 HUD: 왼쪽 아래 체력 막대, 오른쪽 아래 남은 탄, 오른쪽 위 미니맵(층이 있을 때만).
    /// 바뀐 것만 고친다 — 체력은 맞을 때, 탄은 쏘거나 재장전할 때, 미니맵은 방에 들어서거나 방을 깰 때.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private PlayerVitals player;
        [Tooltip("층 씬에서만. 비어 있으면 미니맵을 숨긴다(시험 방)")]
        [SerializeField] private FloorRun floor;
        [Tooltip("체력 막대 안쪽. 오른쪽 끝(anchorMax.x)을 남은 체력 비율로 줄인다")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private RectTransform minimap;

        [Header("미니맵")]
        [Tooltip("격자 한 칸의 화면 크기(px)")]
        [SerializeField] private float minimapCell = 16f;
        [SerializeField] private float minimapGap = 2f;
        [SerializeField] private Color currentRoomColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color visitedRoomColor = new Color(0.65f, 0.65f, 0.65f, 0.85f);
        [SerializeField] private Color knownRoomColor = new Color(0.4f, 0.4f, 0.4f, 0.45f);
        [SerializeField] private Color bossMarkColor = new Color(0.9f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color treasureMarkColor = new Color(0.95f, 0.8f, 0.2f, 1f);

        private readonly List<Image> roomImages = new List<Image>();
        private readonly List<Image> bossMarks = new List<Image>();
        private Health health;
        private PlayerGun gun;
        private bool shownReloading;

        private void Start()
        {
            health = player.Health;
            gun = player.Gun;
            health.Damaged += OnDamaged;
            gun.AmmoChanged += OnAmmoChanged;
            RefreshHealth();
            RefreshAmmo();

            minimap.gameObject.SetActive(floor != null);
            if (floor == null) return;
            BuildMinimap();
            floor.Progress.RoomEntered += OnRoomChanged;
            floor.Progress.RoomCleared += OnRoomChanged;
            RefreshMinimap();
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (gun != null) gun.AmmoChanged -= OnAmmoChanged;
            if (floor != null && floor.Progress != null)
            {
                floor.Progress.RoomEntered -= OnRoomChanged;
                floor.Progress.RoomCleared -= OnRoomChanged;
            }
        }

        private void Update()
        {
            // 재장전 시작 · 끝은 이벤트가 따로 없어 상태가 바뀔 때만 글자를 고친다
            if (gun.IsReloading != shownReloading) RefreshAmmo();
        }

        private void OnDamaged(int amount) => RefreshHealth();
        private void OnAmmoChanged(int ammo) => RefreshAmmo();
        private void OnRoomChanged(int roomId) => RefreshMinimap();

        private void RefreshHealth()
        {
            float ratio = health.Max > 0 ? (float)health.Current / health.Max : 0f;
            healthFill.anchorMax = new Vector2(ratio, 1f);
            healthText.text = $"{health.Current} / {health.Max}";
        }

        private void RefreshAmmo()
        {
            shownReloading = gun.IsReloading;
            ammoText.text = shownReloading ? "재장전…" : $"{gun.Ammo} / {gun.Magazine}";
        }

        /// <summary>방마다 사각형 하나. 큰 방은 칸 수만큼 크게. 시작 방이 미니맵 가운데.</summary>
        private void BuildMinimap()
        {
            FloorLayout layout = floor.Layout;
            foreach (FloorRoom room in layout.Rooms)
            {
                var image = NewImage($"Room_{room.Id}", minimap);
                var rect = image.rectTransform;
                rect.sizeDelta = new Vector2(room.Width * minimapCell - minimapGap, room.Height * minimapCell - minimapGap);
                rect.anchoredPosition = new Vector2(
                    (room.X + room.Width * 0.5f - (layout.Width / 2 + 0.5f)) * minimapCell,
                    (room.Y + room.Height * 0.5f - (layout.Height / 2 + 0.5f)) * minimapCell);
                roomImages.Add(image);

                var mark = NewImage("Boss", rect);
                mark.rectTransform.sizeDelta = new Vector2(minimapCell * 0.4f, minimapCell * 0.4f);
                mark.color = room.Kind == RoomKind.Treasure ? treasureMarkColor : bossMarkColor;
                mark.gameObject.SetActive(false);
                bool marked = room.Kind == RoomKind.Boss || room.Kind == RoomKind.Treasure;
                bossMarks.Add(marked ? mark : null);
                if (!marked) Destroy(mark.gameObject);
            }
        }

        private void RefreshMinimap()
        {
            FloorProgress progress = floor.Progress;
            for (int id = 0; id < roomImages.Count; id++)
            {
                RoomVisibility visibility = progress.Visibility(id);
                Image image = roomImages[id];
                image.gameObject.SetActive(visibility != RoomVisibility.Hidden);
                image.color = id == progress.CurrentRoom ? currentRoomColor
                    : visibility == RoomVisibility.Visited ? visitedRoomColor
                    : knownRoomColor;
                if (bossMarks[id] != null) bossMarks[id].gameObject.SetActive(visibility != RoomVisibility.Hidden);
            }
        }

        private static Image NewImage(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
