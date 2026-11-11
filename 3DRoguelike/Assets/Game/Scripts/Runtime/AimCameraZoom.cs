using Unity.Cinemachine;
using UnityEngine;

namespace Game
{
    /// <summary>조준하는 동안 3인칭 카메라를 어깨 가까이 당긴다.</summary>
    public sealed class AimCameraZoom : MonoBehaviour
    {
        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private PlayerGun gun;
        [SerializeField] private CinemachineThirdPersonFollow follow;
        [SerializeField] private GameObject crosshair;

        private float blend;

        private void Update()
        {
            blend = Mathf.MoveTowards(blend, gun.IsAiming ? 1f : 0f, tuning.aimBlendSpeed * gun.Upgrades.Scale(Rules.UpgradeStat.AimTransition) * Time.deltaTime);
            follow.CameraDistance = Mathf.Lerp(tuning.cameraDistance, tuning.aimCameraDistance, blend);
            if (crosshair.activeSelf != gun.IsAiming) crosshair.SetActive(gun.IsAiming);
        }
    }
}