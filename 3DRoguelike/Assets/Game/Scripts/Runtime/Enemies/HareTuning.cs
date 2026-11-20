using UnityEngine;

namespace Game
{
    /// <summary>멧토끼(빠른 떼) 수치. 값의 기준은 BALANCE.md 9번. 떼 크기는 `EnemyRoster`.</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Hare Tuning")]
    public sealed class HareTuning : EnemyTuning
    {
        public HareTuning()
        {
            displayName = "멧토끼 Mutant";
            evolvedDisplayName = "멧토끼 E 개체";
            maxHealth = 12;
            moveSpeed = 4.2f;
            windupLeanAngle = 0f;
            strikeLeanAngle = 25f;
        }

        [Header("도약 들이받기")]
        public string attackName = "들이받기";
        [Tooltip("이 거리 안에 들어오면 멈추고 웅크린다(m)")]
        [Min(0f)] public float leapStartRange = 5f;
        [Tooltip("웅크림(예고, 초). 뛸 곳은 웅크리기 시작할 때 정해진다")]
        [Min(0f)] public float crouchTime = 1f;
        [Tooltip("가장 멀리 뛰는 거리(m)")]
        [Min(0f)] public float leapDistance = 6f;
        [Tooltip("공중에 있는 시간(초)")]
        [Min(0.05f)] public float leapTime = 0.4f;
        [Tooltip("도약 높이(m, 보이는 것만)")]
        [Min(0f)] public float leapHeight = 0.8f;
        [Tooltip("뛰는 동안 부딪히는 거리(m)")]
        [Min(0f)] public float hitRadius = 0.9f;
        [Min(0)] public int damage = 6;
        [Tooltip("착지 뒤 빈틈(초)")]
        [Min(0f)] public float recoverTime = 2f;

        [Header("E 개체: 높이 뛰어 내려찍기")]
        public string slamName = "내려찍기";
        [Min(0f)] public float slamCrouchTime = 0.8f;
        [Min(0f)] public float slamLeapDistance = 8f;
        [Min(0.05f)] public float slamLeapTime = 0.8f;
        [Min(0f)] public float slamLeapHeight = 3f;
        [Tooltip("착지할 때 맞는 반지름(m). 착지 자리에 예고가 보인다")]
        [Min(0f)] public float slamRadius = 2f;
        [Min(0)] public int slamDamage = 10;
        [Min(0f)] public float slamRecoverTime = 1f;
    }
}
