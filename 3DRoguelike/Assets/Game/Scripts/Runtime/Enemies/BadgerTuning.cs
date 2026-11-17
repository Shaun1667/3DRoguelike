using UnityEngine;

namespace Game
{
    /// <summary>오소리(기본 근접) 수치. 값의 기준은 BALANCE.md 9번. Mutant 값은 2-4 Mutant 그대로.</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Badger Tuning")]
    public sealed class BadgerTuning : EnemyTuning
    {
        public BadgerTuning()
        {
            displayName = "오소리 Mutant";
            evolvedDisplayName = "오소리 E 개체";
            maxHealth = 30;
            moveSpeed = 3f;
        }

        [Header("할퀴기")]
        public string attackName = "할퀴기";
        [Tooltip("이 거리 안에 들어오면 멈추고 예고를 시작한다(m)")]
        [Min(0f)] public float attackStartRange = 1.8f;
        [Tooltip("예고 시간(초). 방향은 예고를 시작할 때 고정된다")]
        [Min(0f)] public float windupTime = 1f;
        [Tooltip("할퀴는 순간 맞는 거리(m)")]
        [Min(0f)] public float hitRange = 2.2f;
        [Tooltip("정면 기준 전체 각도")]
        [Range(0f, 360f)] public float hitAngle = 100f;
        [Min(0)] public int damage = 10;
        [Tooltip("할퀸 뒤 멈춰 있는 빈틈(초)")]
        [Min(0f)] public float recoverTime = 1.3f;

        [Header("E 개체: 3연속 할퀴기")]
        public string comboName = "3연속 할퀴기";
        [Min(1)] public int comboHits = 3;
        [Tooltip("할퀴기 사이 간격(초). 플레이어 맞은 뒤 무적(0.5초)보다 길어야 모두 맞는다")]
        [Min(0f)] public float comboInterval = 0.55f;
        [Min(0)] public int comboDamage = 8;
        [Tooltip("할퀼 때마다 앞으로 내딛는 거리(m)")]
        [Min(0f)] public float comboStep = 0.6f;
        [Tooltip("마지막 할퀴기 뒤 빈틈(초)")]
        [Min(0f)] public float comboRecoverTime = 1.6f;
    }
}
