using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// 권총. 우클릭을 누르는 동안 조준하고, 조준 중 좌클릭으로 쏜다. 총알은 화면 중앙 방향으로 즉시 명중한다.
    /// 탄은 무한이고 탄창만 갈아 끼운다(R, 또는 빈 탄창으로 쏠 때 자동).
    /// </summary>
    public sealed class PlayerGun : MonoBehaviour
    {
        private const int MaxRayHits = 16;

        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private PlayerMelee melee;
        [SerializeField] private PlayerRoll roll;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform pistol;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LineRenderer tracer;

        private readonly RaycastHit[] rayHits = new RaycastHit[MaxRayHits];
        private InputAction attackAction;
        private InputAction aimAction;
        private InputAction reloadAction;
        private Camera mainCamera;
        private Transform hand;
        private float nextShotTime;
        private float reloadEndTime;
        private float tracerEndTime;
        private bool reloading;

        /// <summary>남은 탄이 바뀔 때.</summary>
        public event Action<int> AmmoChanged;

        /// <summary>총알이 대상을 맞힌 순간.</summary>
        public event Action<IDamageable> Hit;

        public int Ammo { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsReloading => reloading;
        public Vector3 AimPoint { get; private set; }

        /// <summary>디버그: 입력 없이 조준 상태를 유지한다.</summary>
        public bool DebugForceAim { get; set; }

        private void Awake()
        {
            attackAction = InputSystem.actions.FindAction("Player/Attack", throwIfNotFound: true);
            aimAction = InputSystem.actions.FindAction("Player/Aim", throwIfNotFound: true);
            reloadAction = InputSystem.actions.FindAction("Player/Reload", throwIfNotFound: true);
            mainCamera = Camera.main;
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Ammo = tuning.pistolMagazine;
            tracer.enabled = false;
            pistol.gameObject.SetActive(false);
        }

        private void Update()
        {
            SetAiming((aimAction.IsPressed() || DebugForceAim) && !melee.IsAttacking && !roll.IsRolling);
            UpdateAimPoint();

            if (reloadAction.WasPressedThisFrame()) StartReload();
            if (reloading && Time.time >= reloadEndTime) FinishReload();
            if (IsAiming && attackAction.WasPressedThisFrame()) TryFire();
            if (tracer.enabled && Time.time >= tracerEndTime) tracer.enabled = false;
        }

        private void LateUpdate()
        {
            if (!IsAiming) return;
            pistol.SetPositionAndRotation(hand.position, Quaternion.LookRotation(AimPoint - hand.position));
        }

        private void SetAiming(bool aiming)
        {
            if (IsAiming == aiming) return;
            IsAiming = aiming;
            pistol.gameObject.SetActive(aiming);
        }

        public bool TryFire()
        {
            if (reloading) return false;
            if (Ammo == 0)
            {
                StartReload();
                return false;
            }
            if (Time.time < nextShotTime) return false;

            nextShotTime = Time.time + tuning.pistolFireInterval;
            Ammo--;
            AmmoChanged?.Invoke(Ammo);

            Vector3 end = CastAim(out IDamageable target);
            ShowTracer(end);
            if (target != null)
            {
                target.TakeDamage(tuning.pistolDamage);
                Hit?.Invoke(target);
            }
            return true;
        }

        public void StartReload()
        {
            if (reloading || Ammo == tuning.pistolMagazine) return;
            reloading = true;
            reloadEndTime = Time.time + tuning.pistolReloadTime;
        }

        private void FinishReload()
        {
            reloading = false;
            Ammo = tuning.pistolMagazine;
            AmmoChanged?.Invoke(Ammo);
        }

        private void UpdateAimPoint()
        {
            AimPoint = CastAim(out _);
        }

        /// <summary>화면 중앙 방향에서 자기 몸을 뺀 가장 가까운 지점.</summary>
        private Vector3 CastAim(out IDamageable target)
        {
            target = null;
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, rayHits, tuning.pistolRange, Physics.AllLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = rayHits[i];
                if (hit.distance < nearest && !hit.collider.transform.IsChildOf(transform))
                {
                    nearest = hit.distance;
                    nearestCollider = hit.collider;
                }
            }

            if (nearestCollider == null) return ray.GetPoint(tuning.pistolRange);
            target = nearestCollider.GetComponentInParent<IDamageable>();
            return ray.GetPoint(nearest);
        }

        private void ShowTracer(Vector3 end)
        {
            tracer.SetPosition(0, muzzle.position);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerEndTime = Time.time + tuning.tracerTime;
        }
    }
}