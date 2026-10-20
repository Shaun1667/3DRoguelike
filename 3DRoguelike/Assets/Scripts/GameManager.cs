using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState
{
    Ready,
    Playing,
    Victory,
    Defeat
}

/// <summary>
/// 게임 전체 흐름(시작 → 전투 → 승리/패배 → 재시작)을 관리한다.
///
/// - 승리: 보스 Health.Died
/// - 패배: 플레이어 Health.Died 또는 경기장 밖으로 추락(fallDeathY 아래)
/// - 게임 중이 아닐 때는 플레이어 조작 컴포넌트를 꺼서 입력을 막는다.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Health bossHealth;
    [SerializeField] private BossController boss;
    [SerializeField] private GameUI ui;

    [Tooltip("게임 중에만 켜둘 플레이어 조작 컴포넌트 (이동, 공격, 카메라)")]
    [SerializeField] private Behaviour[] playerControls;

    [Header("규칙")]
    [Tooltip("플레이어가 이 높이 아래로 떨어지면 패배")]
    [SerializeField] private float fallDeathY = -15f;

    [Header("점수")]
    [SerializeField] private int clearBaseScore = 10000;
    [SerializeField] private int scorePenaltyPerSecond = 20;
    [SerializeField] private int scorePenaltyPerHit = 500;
    [SerializeField] private int scorePerBossDamage = 10;

    public GameState State { get; private set; } = GameState.Ready;

    private float elapsedTime;
    private int hitsTaken;
    private int damageDealt;

    private void Start()
    {
        playerHealth.Died += OnPlayerDied;
        playerHealth.Damaged += OnPlayerDamaged;
        bossHealth.Died += OnBossDied;
        bossHealth.Damaged += OnBossDamaged;
        boss.PhaseChanged += OnBossPhaseChanged;

        SetControls(false);
        ui.ShowReady();
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
            playerHealth.Damaged -= OnPlayerDamaged;
        }
        if (bossHealth != null)
        {
            bossHealth.Died -= OnBossDied;
            bossHealth.Damaged -= OnBossDamaged;
        }
        if (boss != null) boss.PhaseChanged -= OnBossPhaseChanged;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        switch (State)
        {
            case GameState.Ready:
                bool startPressed = (kb != null && kb.enterKey.wasPressedThisFrame)
                                    || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                if (startPressed) StartGame();
                break;

            case GameState.Playing:
                elapsedTime += Time.deltaTime;
                ui.SetTimer(elapsedTime);

                if (playerHealth.transform.position.y < fallDeathY)
                {
                    EndGame(false, "경기장 밖으로 떨어졌습니다");
                }
                break;

            default: // 승리 / 패배 화면
                if (kb != null && kb.rKey.wasPressedThisFrame)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                break;
        }
    }

    private void StartGame()
    {
        State = GameState.Playing;
        SetControls(true);
        boss.StartFight();
        ui.ShowHud();
    }

    private void EndGame(bool victory, string reason)
    {
        if (State != GameState.Playing) return; // 이미 끝났으면 중복 처리 안 함

        State = victory ? GameState.Victory : GameState.Defeat;
        SetControls(false);
        if (!victory) boss.StopFight();

        int score = victory
            ? Mathf.Max(0, clearBaseScore - Mathf.RoundToInt(elapsedTime) * scorePenaltyPerSecond - hitsTaken * scorePenaltyPerHit)
            : damageDealt * scorePerBossDamage;

        ui.ShowResult(victory, reason, elapsedTime, hitsTaken, score);
    }

    private void SetControls(bool enabled)
    {
        foreach (Behaviour b in playerControls)
        {
            if (b != null) b.enabled = enabled;
        }
    }

    private void OnPlayerDied() => EndGame(false, "토끼가 쓰러졌습니다");
    private void OnBossDied() => EndGame(true, "까치 대장을 물리쳤습니다!");
    private void OnPlayerDamaged(int amount) => hitsTaken++;
    private void OnBossDamaged(int amount) => damageDealt += amount;
    private void OnBossPhaseChanged() => ui.ShowBanner("까치 대장이 분노했다! 공격이 빨라집니다");
}
