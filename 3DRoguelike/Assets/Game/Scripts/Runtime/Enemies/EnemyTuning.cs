using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 숲 적 수치의 공통 부분. 종마다 이것을 이어받은 에셋 하나에 Mutant와 E 개체 값을 같이 둔다(BALANCE.md 9번).
    /// E 개체 = 체력 × <see cref="evolvedHealthMultiplier"/> + 공격 <see cref="extraAttackEvery"/>번마다 추가 공격 한 번.
    /// </summary>
    public abstract class EnemyTuning : ScriptableObject
    {
        [Tooltip("결과 화면의 사망 원인: '{이름}의 {공격 이름}'")]
        public string displayName = "Mutant";
        public string evolvedDisplayName = "E 개체";

        [Min(1)] public int maxHealth = 30;
        [Tooltip("E 개체 체력 배율(결정 2026-11-12: ×1.8)")]
        [Min(1f)] public float evolvedHealthMultiplier = 1.8f;
        [Min(0f)] public float moveSpeed = 3f;
        [Tooltip("초당 회전 각도")]
        [Min(0f)] public float turnSpeed = 540f;
        [Tooltip("길을 다시 찾는 간격(초)")]
        [Min(0.02f)] public float repathInterval = 0.2f;
        [Tooltip("나타나기 전 바닥 예고(초). 이동 · 공격 · 피격 없음")]
        [Min(0f)] public float spawnTime = 1f;
        [Tooltip("E 개체: 공격을 이만큼 할 때마다 한 번은 추가 공격(2 = 번갈아)")]
        [Min(1)] public int extraAttackEvery = 2;

        [Header("연출")]
        [Tooltip("예고 동안 몸을 뒤로 젖히는 각도")]
        [Range(0f, 60f)] public float windupLeanAngle = 25f;
        [Tooltip("공격할 때 앞으로 숙이는 각도. 빈틈 동안 천천히 선다")]
        [Range(0f, 60f)] public float strikeLeanAngle = 20f;
        [Min(0f)] public float hitFlashTime = 0.08f;
        [Tooltip("죽은 뒤 사라질 때까지(초)")]
        [Min(0f)] public float corpseTime = 1f;

        public int HealthFor(EnemyGrade grade)
        {
            return grade == EnemyGrade.Evolved ? Mathf.RoundToInt(maxHealth * evolvedHealthMultiplier) : maxHealth;
        }

        public string NameFor(EnemyGrade grade) => grade == EnemyGrade.Evolved ? evolvedDisplayName : displayName;
    }
}
