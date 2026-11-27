using TMPro;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 화면 위 보스 체력 막대와 이름, 등장할 때 가운데 큰 이름(짧게). 보스가 없으면 숨는다. `HudSetup.BuildBossBar`가 만든다.
    /// 체력이 바뀐 순간만 고친다.
    /// </summary>
    public sealed class BossHealthBar : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("등장할 때 가운데에 잠깐 뜨는 큰 이름")]
        [SerializeField] private TMP_Text introText;

        private Boss boss;
        private float introEndTime;
        private float hideTime = float.PositiveInfinity;

        /// <summary>보스가 쓰러진 뒤 막대가 사라지기까지(초, 빈 막대를 잠깐 보여 줌).</summary>
        private const float HideDelayAfterDeath = 1.5f;

        public Boss Target => boss;
        public bool IsShown => root != null && root.activeSelf;
        public bool IntroShown => introText != null && introText.gameObject.activeSelf;
        public string NameShown => nameText != null ? nameText.text : "";
        public float FillShown => fill != null ? fill.anchorMax.x : 0f;

        private void Awake()
        {
            Hide();
        }

        /// <summary>보스를 보여 준다(등장 이름 포함).</summary>
public void Show(Boss target, bool announce = true)
        {
            Unhook();
            boss = target;
            boss.Health.Damaged += OnDamaged;
            boss.Health.Died += OnDied;
            boss.Enraged += OnEnraged;
            root.SetActive(true);
            UpdateName();
            UpdateFill();
            // 등장 연출이 이름 타이틀을 이미 보여 줬으면(announce = false) 가운데 큰 이름은 건너뛴다
            if (introText != null)
            {
                introText.text = boss.DisplayName;
                introText.gameObject.SetActive(announce);
                introEndTime = Time.time + boss.Tuning.introTime;
            }
        }

        public void Hide()
        {
            hideTime = float.PositiveInfinity;
            Unhook();
            boss = null;
            if (root != null) root.SetActive(false);
            if (introText != null) introText.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (introText != null && introText.gameObject.activeSelf && Time.time >= introEndTime) introText.gameObject.SetActive(false);
            if (Time.unscaledTime >= hideTime) Hide(); // 결과 화면이 시간을 멈춰도 숨는다
        }

        private void OnDestroy() => Unhook();

        private void Unhook()
        {
            if (boss == null) return;
            boss.Health.Damaged -= OnDamaged;
            boss.Health.Died -= OnDied;
            boss.Enraged -= OnEnraged;
        }

        private void OnDamaged(int amount) => UpdateFill();
        private void OnEnraged() => UpdateName();

        private void OnDied()
        {
            UpdateFill();
            if (introText != null) introText.gameObject.SetActive(false);
            hideTime = Time.unscaledTime + HideDelayAfterDeath;
        }

        private void UpdateFill()
        {
            float ratio = boss.Health.Max > 0 ? (float)boss.Health.Current / boss.Health.Max : 0f;
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        private void UpdateName()
        {
            nameText.text = boss.IsEnraged ? $"{boss.DisplayName}  ·  {boss.Tuning.enragedLabel}" : boss.DisplayName;
        }
    }
}
