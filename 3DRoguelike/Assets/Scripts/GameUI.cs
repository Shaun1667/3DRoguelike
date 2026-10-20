using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 UI 표시만 담당한다 (게임 규칙은 GameManager).
///
/// - 체력바: Health.HealthChanged 이벤트를 구독해서 "바뀔 때만" 목표값을 갱신하고,
///   화면의 막대는 매 프레임 목표값으로 부드럽게 따라간다.
/// - 체력바 채움은 Image.fillAmount 대신 RectTransform의 anchorMax.x를 줄이는 방식이라
///   스프라이트 없이도 동작한다.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("체력 연결")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Health bossHealth;

    [Header("HUD")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private RectTransform bossBarFill;
    [SerializeField] private RectTransform playerBarFill;
    [SerializeField] private Text bossHpText;
    [SerializeField] private Text playerHpText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text bannerText;

    [Header("가운데 패널 (시작 / 결과)")]
    [SerializeField] private GameObject centerPanel;
    [SerializeField] private Text centerTitle;
    [SerializeField] private Text centerBody;

    [SerializeField] private float barFollowSpeed = 3f;
    [SerializeField] private float bannerDuration = 2.5f;

    private float bossTarget = 1f, bossShown = 1f;
    private float playerTarget = 1f, playerShown = 1f;
    private float bannerHideTime;

    private void Start()
    {
        playerHealth.HealthChanged += OnPlayerHealthChanged;
        bossHealth.HealthChanged += OnBossHealthChanged;
        OnPlayerHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        OnBossHealthChanged(bossHealth.CurrentHealth, bossHealth.MaxHealth);
        bannerText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.HealthChanged -= OnPlayerHealthChanged;
        if (bossHealth != null) bossHealth.HealthChanged -= OnBossHealthChanged;
    }

    private void Update()
    {
        bossShown = Mathf.MoveTowards(bossShown, bossTarget, barFollowSpeed * Time.unscaledDeltaTime);
        playerShown = Mathf.MoveTowards(playerShown, playerTarget, barFollowSpeed * Time.unscaledDeltaTime);
        SetFill(bossBarFill, bossShown);
        SetFill(playerBarFill, playerShown);

        if (bannerText.gameObject.activeSelf && Time.unscaledTime >= bannerHideTime)
        {
            bannerText.gameObject.SetActive(false);
        }
    }

    public void ShowReady()
    {
        hudRoot.SetActive(false);
        centerPanel.SetActive(true);
        centerTitle.text = "까치 대장의 둥지";
        centerBody.text = "보물을 지키는 까치 대장을 쓰러뜨리세요!\n\n"
                        + "WASD 이동   마우스 시점   좌클릭 공격\n"
                        + "Space 점프   Shift 대시(무적)\n\n"
                        + "클릭 또는 Enter로 시작";
    }

    public void ShowHud()
    {
        centerPanel.SetActive(false);
        hudRoot.SetActive(true);
    }

    public void SetTimer(float seconds)
    {
        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        timerText.text = $"{m:00}:{s:00}";
    }

    public void ShowBanner(string message)
    {
        bannerText.text = message;
        bannerText.gameObject.SetActive(true);
        bannerHideTime = Time.unscaledTime + bannerDuration;
    }

    public void ShowResult(bool victory, string reason, float seconds, int hitsTaken, int score)
    {
        centerPanel.SetActive(true);
        centerTitle.text = victory ? "승리!" : "패배...";
        centerTitle.color = victory ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.45f);

        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        centerBody.text = $"{reason}\n\n"
                        + $"전투 시간  {m:00}:{s:00}\n"
                        + $"맞은 횟수  {hitsTaken}\n"
                        + $"점수  {score:N0}\n\n"
                        + "R 키로 다시 시작";
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        playerTarget = max > 0 ? (float)current / max : 0f;
        playerHpText.text = $"HP {current} / {max}";
    }

    private void OnBossHealthChanged(int current, int max)
    {
        bossTarget = max > 0 ? (float)current / max : 0f;
        bossHpText.text = $"{current} / {max}";
    }

    private static void SetFill(RectTransform fill, float ratio)
    {
        Vector2 max = fill.anchorMax;
        max.x = Mathf.Clamp01(ratio);
        fill.anchorMax = max;
    }
}
