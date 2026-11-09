using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>플레이어 체력 연결과 사망. 죽으면 쓰러지고 조작이 멈추며, R로 씬을 다시 시작한다. 결과 화면은 TASKS 2-5.</summary>
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

        private InputAction restartAction;
        private bool dead;

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
            if (dead && restartAction.WasPressedThisFrame())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().path);
            }
        }

        private void OnDied()
        {
            dead = true;
            controller.enabled = false;
            melee.enabled = false;
            gun.enabled = false;
            roll.enabled = false;
            animator.SetTrigger(DieId);
        }
    }
}