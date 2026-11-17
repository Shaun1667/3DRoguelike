using UnityEngine;

namespace Game
{
    /// <summary>멧돼지(돌진 탱커) 수치. 값의 기준은 BALANCE.md 9번.</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Boar Tuning")]
    public sealed class BoarTuning : EnemyTuning
    {
        public BoarTuning()
        {
            displayName = "멧돼지 Mutant";
            evolvedDisplayName = "멧돼지 E 개체";
            maxHealth = 70;
            moveSpeed = 2.6f;
            turnSpeed = 360f;
        }

        [Header("돌진")]
        public string attackName = "돌진";
        [Tooltip("이 거리 안에서 앞이 트여 있으면 멈추고 예고를 시작한다(m)")]
        [Min(0f)] public float chargeStartRange = 12f;
        [Tooltip("예고 시간(초). 방향은 예고를 시작할 때 고정된다")]
        [Min(0f)] public float windupTime = 1.2f;
        [Min(0.1f)] public float chargeSpeed = 9f;
        [Tooltip("돌진 거리(m)")]
        [Min(0f)] public float chargeDistance = 15f;
        [Tooltip("돌진하며 부딪히는 거리(m). 바닥 예고 폭은 이것의 두 배")]
        [Min(0f)] public float hitRadius = 1.2f;
        [Min(0)] public int damage = 20;
        [Tooltip("맞은 플레이어를 미는 처음 속도(m/s). 줄어드는 빠르기는 PlayerTuning.pushDecay")]
        [Min(0f)] public float knockbackSpeed = 12f;
        [Tooltip("벽 · 나무에 박으면 기절(초). 이 동안 무방비")]
        [Min(0f)] public float stunTime = 2f;
        [Tooltip("벽에 박지 않고 돌진을 마친 뒤 빈틈(초)")]
        [Min(0f)] public float recoverTime = 1f;

        [Header("E 개체: 돌진 끝 내려찍기")]
        public string slamName = "내려찍기";
        [Tooltip("돌진을 마치고 내려찍기까지 예고(초)")]
        [Min(0f)] public float slamWindupTime = 0.5f;
        [Min(0f)] public float slamRadius = 3f;
        [Min(0)] public int slamDamage = 15;
        [Min(0f)] public float slamRecoverTime = 1.2f;
    }
}
