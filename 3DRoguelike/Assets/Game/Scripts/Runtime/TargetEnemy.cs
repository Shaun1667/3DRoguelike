using UnityEngine;

namespace Game
{
    /// <summary>
    /// 시험용 표적. 움직이지 않고, 주기마다 바닥에 범위를 예고한 뒤 안에 있는 플레이어를 때린다.
    /// 죽으면 잠시 뒤 다시 나타난다(층에서는 다시 나타나지 않게 끈다). 진짜 적 AI는 TASKS 2-4.
    /// </summary>
    public sealed class TargetEnemy : MonoBehaviour
    {
        [SerializeField] private TargetEnemyTuning tuning;
        [SerializeField] private Health health;
        [SerializeField] private HitFlash flash;
        [SerializeField] private GameObject warning;
        [SerializeField] private GameObject body;
        [Tooltip("죽은 뒤 다시 나타나는가. 시험 방은 켜고, 층의 방마다 두는 표적은 끈다")]
        [SerializeField] private bool respawns = true;

        private Collider bodyCollider;
        private Health player;
        private float nextEventTime;
        private bool warningOn;
        private bool dead;

        public bool Respawns
        {
            get => respawns;
            set => respawns = value;
        }

        private void Awake()
        {
            health.Configure(tuning.maxHealth, 0f);
            flash.Configure(tuning.hitFlashTime);
            bodyCollider = GetComponent<Collider>();
            warning.SetActive(false);
            nextEventTime = Time.time + tuning.attackInterval;
        }

        private void Start()
        {
            player = GameObject.FindWithTag("Player").GetComponent<Health>();
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
            if (Time.time < nextEventTime) return;

            if (dead)
            {
                Respawn();
            }
            else if (!warningOn)
            {
                warningOn = true;
                warning.SetActive(true);
                nextEventTime = Time.time + tuning.warningTime;
            }
            else
            {
                Shockwave();
            }
        }

        private void Shockwave()
        {
            warningOn = false;
            warning.SetActive(false);
            nextEventTime = Time.time + tuning.attackInterval;

            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= tuning.shockwaveRadius) player.TakeDamage(tuning.shockwaveDamage);
        }

        private void OnDied()
        {
            dead = true;
            warningOn = false;
            warning.SetActive(false);
            body.SetActive(false);
            bodyCollider.enabled = false;
            nextEventTime = respawns ? Time.time + tuning.respawnTime : float.PositiveInfinity;
        }

        private void Respawn()
        {
            dead = false;
            health.Restore();
            body.SetActive(true);
            bodyCollider.enabled = true;
            nextEventTime = Time.time + tuning.attackInterval;
        }
    }
}