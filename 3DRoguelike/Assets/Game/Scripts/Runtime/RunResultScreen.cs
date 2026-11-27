using Game.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 판이 끝나면 뜨는 결과 화면. 사망 원인(또는 클리어) · 시드 · 깬 던전 · 깬 방 · 처치 · 시간을 보여 주고,
    /// R(새 판) · T(같은 시드) 또는 버튼으로 다시 시작한다 — 월드 판이면 월드 길 시작부터. 떠 있는 동안 게임 시간은 멈춘다.
    /// </summary>
    public sealed class RunResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Button newFloorButton;
        [SerializeField] private Button sameFloorButton;

        private IRunRestart run;
        private Button treeButton;
        private RunStats shownStats;

        /// <summary>이 판의 영구 재화를 더했고(월드 판) 트리 표가 있어 [성장 트리]를 열 수 있는가.</summary>
        public bool CanOpenTree => IsShown && MetaRun.Ready && MetaRun.TryGetBank(shownStats, out _, out _);

        public bool IsShown => panel.activeSelf;
        public string TitleText => title.text;
        public string BodyText => body.text;

        private void Awake()
        {
            panel.SetActive(false);
            newFloorButton.onClick.AddListener(() => run.Restart(sameSeed: false));
            sameFloorButton.onClick.AddListener(() => run.Restart(sameSeed: true));
            // [성장 트리](3-7): 씬 · 프리팩을 다시 만들지 않도록 같은 버튼을 복제해 아래에 둔다
            treeButton = Instantiate(sameFloorButton, sameFloorButton.transform.parent);
            treeButton.name = "MetaTreeButton";
            treeButton.onClick.RemoveAllListeners();
            treeButton.onClick.AddListener(() => OpenTree());
            var rect = (RectTransform)treeButton.transform;
            var source = (RectTransform)sameFloorButton.transform;
            rect.anchoredPosition = new Vector2(0f, source.anchoredPosition.y - source.sizeDelta.y - 24f);
            SetLabel(treeButton, "성장 트리  (G)");
            treeButton.gameObject.SetActive(false);
        }

        private static void SetLabel(Button button, string text)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        /// <summary>성장 트리 화면을 연다(끝난 판 — 산 노드는 다음 재생부터). 버튼 · G · 확인용 코드.</summary>
        public MetaTreeScreen OpenTree()
        {
            if (!CanOpenTree || !MetaTreeScreen.CanOpenThisFrame) return null;
            return MetaTreeScreen.Open(title.font, null, Refresh);
        }

        /// <summary>트리에서 돌아왔을 때 합계를 다시 쓴다.</summary>
        private void Refresh()
        {
            if (IsShown && shownStats != null) body.text = BodyFor(shownStats);
        }

        /// <summary>결과 화면 제목. 지역 클리어는 그 지역 이름(예: "폐허 도시 클리어"), 이름을 모르면 "지역 클리어".</summary>
        public static string TitleFor(RunOutcome outcome, string clearedRegion)
        {
            switch (outcome)
            {
                case RunOutcome.Died: return "쓰러졌다";
                case RunOutcome.RegionCleared: return $"{(string.IsNullOrEmpty(clearedRegion) ? "지역" : clearedRegion)} 클리어";
                default: return "층 클리어";
            }
        }

        /// <param name="clearedRegion">지역을 깨서 끝났을 때 그 지역 이름(<see cref="WorldTuning.ClearedRegionName"/>)</param>
        public void Show(RunStats stats, IRunRestart restart, string clearedRegion = null)
        {
            run = restart;
            bool died = stats.Outcome == RunOutcome.Died;
            title.text = TitleFor(stats.Outcome, clearedRegion);
            title.color = died ? new Color(0.9f, 0.35f, 0.3f) : new Color(0.55f, 0.85f, 0.5f);

            shownStats = stats;
            body.text = BodyFor(stats);
            bool meta = MetaRun.Ready && MetaRun.TryGetBank(stats, out _, out _);
            treeButton.gameObject.SetActive(meta);
            if (meta) SetLabel(newFloorButton, "숲에서 재생  (R)"); // 죽으면 숲(길 시작)에서 다시 깨어난다(GDD)

            panel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>결과 본문. 월드 판이면 끝에 "영구 재화  이번 판 +N · 합계 M"(3-7).</summary>
        public static string BodyFor(RunStats stats)
        {
            bool died = stats.Outcome == RunOutcome.Died;
            string cause = died ? $"사망 원인  {stats.DeathCause}\n" : "";
            string dungeons = stats.Dungeons > 0 ? $"깬 던전  {stats.DungeonsCleared} / {stats.Dungeons}\n" : "";
            string meta = MetaRun.TryGetBank(stats, out int earned, out _) ? $"\n영구 재화  이번 판 +{earned} · 합계 {MetaRun.Progress.Currency}" : "";
            return cause +
                        $"시드  {stats.Seed.ToCode()}\n" +
                        dungeons +
                        $"깬 방  {stats.RoomsCleared} / {stats.BattleRooms}\n" +
                        $"처치  {stats.Kills}\n" +
                        $"시간  {RunStats.FormatTime(stats.ElapsedSeconds)}\n" +
                        $"성향  Beast {stats.Beast} · Human {stats.Human}  (보상 {stats.RewardsTaken.Count}개)" + meta;
        }

        private void Update()
        {
            if (!IsShown || run == null || Keyboard.current == null || MetaTreeScreen.Shown != null) return;
            if (Keyboard.current.gKey.wasPressedThisFrame) { OpenTree(); return; }
            if (Keyboard.current.rKey.wasPressedThisFrame) run.Restart(sameSeed: false);
            else if (Keyboard.current.tKey.wasPressedThisFrame) run.Restart(sameSeed: true);
        }
    }
}
