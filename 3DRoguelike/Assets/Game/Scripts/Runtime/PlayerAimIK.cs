using UnityEngine;

namespace Game
{
    /// <summary>조준하는 동안 오른팔을 조준 방향으로 뻗는다. Animator가 있는 오브젝트에 붙인다.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAimIK : MonoBehaviour
    {
        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private PlayerGun gun;

        private Animator animator;
        private Transform shoulder;
        private float weight;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        private void Update()
        {
            weight = Mathf.MoveTowards(weight, gun.IsAiming ? 1f : 0f, tuning.aimBlendSpeed * gun.Upgrades.Scale(Rules.UpgradeStat.AimTransition) * Time.deltaTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
            animator.SetLookAtWeight(weight * 0.5f);
            if (weight <= 0f) return;

            Vector3 direction = (gun.AimPoint - shoulder.position).normalized;
            animator.SetIKPosition(AvatarIKGoal.RightHand, shoulder.position + direction * tuning.aimArmLength);
            animator.SetIKRotation(AvatarIKGoal.RightHand, Quaternion.LookRotation(direction));
            animator.SetLookAtPosition(gun.AimPoint);
        }
    }
}