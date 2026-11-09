using UnityEngine;

namespace Game
{
    /// <summary>시험용 표적 적 수치. 값의 기준은 BALANCE.md 6번.</summary>
    [CreateAssetMenu(menuName = "Game/Target Enemy Tuning")]
    public sealed class TargetEnemyTuning : ScriptableObject
    {
        [Min(1)] public int maxHealth = 60;
        [Min(0)] public int shockwaveDamage = 15;
        [Tooltip("충격파 사이 대기(초)")]
        [Min(0f)] public float attackInterval = 3f;
        [Tooltip("바닥에 범위를 보여 주는 예고 시간(초)")]
        [Min(0f)] public float warningTime = 0.8f;
        [Min(0f)] public float shockwaveRadius = 3.5f;
        [Min(0f)] public float respawnTime = 2f;
        [Min(0f)] public float hitFlashTime = 0.08f;
    }
}