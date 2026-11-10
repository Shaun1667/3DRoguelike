using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// 플레이어 체력 연결과 사망. 죽으면 쓰러지고 조작이 멈춘다.
    /// 층에서는 결과 화면(RunResultScreen)이 다시 시작을 맡고, 결과 화면이 없는 시험 방에서는 R로 씬을 다시 시작한다.
    /// </summary>
    public sealed class PlayerVitals : MonoBehaviour
    {
        private static readonly int DieId = Animator.StringToHash("Die");

        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private Health health;
        [SerializeField] private HitFlash flash;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private PlayerGun gun;
        [SerializeField] private PlayerRoll roll;
        [SerializeField] private Animator animator;
        [Tooltip("죽은 뒤 R로 씬을 다시 시작한다. 결과 화면이 있는 층 씬에서는 끈다")]
        [SerializeField] private bool restartOnR = true;

        private InputAction restartAction;
        private bool dead;

        public Health Health => health;
        public PlayerGun Gun => gun;

        private void Awake()
        {
            restartAction = InputSystem.actions.FindAction("Player/Reload", throwIfNotFound: true);
            health.Configure(tuning.playerMaxHealth, tuning.hitInvulnerableTime);
            health.DamageBlocked = () => roll.IsInvulnerable;
            flash.Configure(tuning.hitFlashTime);
        }

        private void OnEnable()
        {
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (dead && restartOnR && restartAction.WasPressedThisFrame())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().path);
            }
        }

        /// <summary>이동 · 공격 · 구르기를 멈춘다. 죽었을 때와 층을 깼을 때.</summary>
        public void DisableControls()
        {
            controller.enabled = false;
            melee.enabled = false;
            gun.enabled = false;
            roll.enabled = false;
        }

        private void OnDied()
        {
            dead = true;
            DisableControls();
            animator.SetTrigger(DieId);
        }
    }
}
