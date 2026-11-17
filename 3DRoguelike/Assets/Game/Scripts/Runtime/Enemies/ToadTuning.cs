using UnityEngine;

namespace Game
{
    /// <summary>두꺼비(원거리 독액) 수치. 값의 기준은 BALANCE.md 9번.</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Toad Tuning")]
    public sealed class ToadTuning : EnemyTuning
    {
        public ToadTuning()
        {
            displayName = "두꺼비 Mutant";
            evolvedDisplayName = "두꺼비 E 개체";
            maxHealth = 24;
            moveSpeed = 2.2f;
            strikeLeanAngle = 10f;
        }

        [Header("거리 유지")]
        [Tooltip("이보다 가까우면 물러난다(m)")]
        [Min(0f)] public float keepMin = 8f;
        [Tooltip("이보다 멀면 다가간다(m)")]
        [Min(0f)] public float keepMax = 14f;
        [Tooltip("물러날 때 한 번에 가는 거리(m)")]
        [Min(0.5f)] public float retreatStep = 4f;

        [Header("독액")]
        public string attackName = "독액";
        [Tooltip("예고 시간(초). 떨어질 곳은 예고를 시작할 때 정해지고 바닥에 보인다")]
        [Min(0f)] public float windupTime = 1.2f;
        [Tooltip("독액이 날아가는 시간(초)")]
        [Min(0.05f)] public float flightTime = 0.9f;
        [Tooltip("포물선 꼭대기 높이(m)")]
        [Min(0f)] public float arcHeight = 3f;
        [Tooltip("떨어진 곳에서 맞는 반지름(m)")]
        [Min(0f)] public float splashRadius = 1.5f;
        [Min(0)] public int damage = 8;
        [Tooltip("쏜 뒤 빈틈(초)")]
        [Min(0f)] public float recoverTime = 1f;
        [Tooltip("한 번 쏘고 다음 예고까지 최소 간격(초, 빈틈 포함)")]
        [Min(0f)] public float attackInterval = 2.5f;

        [Header("독 웅덩이")]
        public string puddleName = "독 웅덩이";
        [Min(0f)] public float puddleRadius = 2f;
        [Min(0f)] public float puddleTime = 4f;
        [Tooltip("초당 피해")]
        [Min(0)] public int puddleDamagePerSecond = 4;
        [Tooltip("피해 간격(초). 한 번에 초당 피해 × 간격")]
        [Min(0.1f)] public float puddleTickInterval = 1f;

        [Header("E 개체: 독액 3발 부채꼴")]
        public string fanName = "독액 부채꼴";
        [Min(1)] public int fanShots = 3;
        [Tooltip("가운데 발에서 양옆 발까지 각도")]
        [Range(0f, 90f)] public float fanAngle = 25f;
    }
}
