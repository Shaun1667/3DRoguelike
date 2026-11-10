using Game.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 판이 끝나면 뜨는 결과 화면. 사망 원인(또는 층 클리어) · 시드 · 깬 방 · 처치 · 시간을 보여 주고,
    /// R(새 층) · T(같은 층) 또는 버튼으로 다시 시작한다. 떠 있는 동안 게임 시간은 멈춘다.
    /// </summary>
    public sealed class RunResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Button newFloorButton;
        [SerializeField] private Button sameFloorButton;

        private FloorRun run;

        public bool IsShown => panel.activeSelf;
        public string TitleText => title.text;
        public string BodyText => body.text;

        private void Awake()
        {
            panel.SetActive(false);
            newFloorButton.onClick.AddListener(() => run.Restart(sameSeed: false));
            sameFloorButton.onClick.AddListener(() => run.Restart(sameSeed: true));
        }

        public void Show(RunStats stats, FloorRun floorRun)
        {
            run = floorRun;
            bool died = stats.Outcome == RunOutcome.Died;
            title.text = died ? "쓰러졌다" : "층 클리어";
            title.color = died ? new Color(0.9f, 0.35f, 0.3f) : new Color(0.55f, 0.85f, 0.5f);

            string cause = died ? $"사망 원인  {stats.DeathCause}\n" : "";
            body.text = cause +
                        $"시드  {stats.Seed.ToCode()}\n" +
                        $"깬 방  {stats.RoomsCleared} / {stats.BattleRooms}\n" +
                        $"처치  {stats.Kills}\n" +
                        $"시간  {RunStats.FormatTime(stats.ElapsedSeconds)}";

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
