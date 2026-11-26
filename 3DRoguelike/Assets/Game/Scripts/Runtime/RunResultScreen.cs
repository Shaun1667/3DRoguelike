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

        public bool IsShown => panel.activeSelf;
        public string TitleText => title.text;
        public string BodyText => body.text;

        private void Awake()
        {
            panel.SetActive(false);
            newFloorButton.onClick.AddListener(() => run.Restart(sameSeed: false));
            sameFloorButton.onClick.AddListener(() => run.Restart(sameSeed: true));
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

            string cause = died ? $"사망 원인  {stats.DeathCause}\n" : "";
            string dungeons = stats.Dungeons > 0 ? $"깬 던전  {stats.DungeonsCleared} / {stats.Dungeons}\n" : "";
            body.text = cause +
                        $"시드  {stats.Seed.ToCode()}\n" +
                        dungeons +
                        $"깬 방  {stats.RoomsCleared} / {stats.BattleRooms}\n" +
                        $"처치  {stats.Kills}\n" +
                        $"시간  {RunStats.FormatTime(stats.ElapsedSeconds)}\n" +
                        $"성향  Beast {stats.Beast} · Human {stats.Human}  (보상 {stats.RewardsTaken.Count}개)";

            panel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (!IsShown || run == null || Keyboard.current == null) return;
            if (Keyboard.current.rKey.wasPressedThisFrame) run.Restart(sameSeed: false);
            else if (Keyboard.current.tKey.wasPressedThisFrame) run.Restart(sameSeed: true);
        }
    }
}
