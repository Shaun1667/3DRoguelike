using UnityEngine;

namespace Game
{
    /// <summary>너구리(작고 약한 떼, 도시) 수치. 값의 기준은 BALANCE.md 9-1번. 떼 크기는 `EnemyRoster`.</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Raccoon Dog Tuning")]
    public sealed class RaccoonDogTuning : EnemyTuning
    {
        public RaccoonDogTuning()
        {
            displayName = "너구리 Mutant";
            evolvedDisplayName = "너구리 E 개체";
            maxHealth = 14;
            moveSpeed = 3f;
            windupLeanAngle = 20f;
            strikeLeanAngle = 20f;
        }

        [Header("할퀴어 물기")]
        public string attackName = "할퀴어 물기";
        [Tooltip("이 거리 안에 들어오면 멈추고 예고를 시작한다(m)")]
        [Min(0f)] public float attackStartRange = 1.3f;
        [Tooltip("예고 시간(초). 방향은 예고를 시작할 때 고정된다")]
        [Min(0f)] public float windupTime = 0.7f;
        [Tooltip("무는 순간 맞는 거리(m)")]
        [Min(0f)] public float hitRange = 1.6f;
        [Tooltip("정면 기준 전체 각도")]
        [Range(0f, 360f)] public float hitAngle = 90f;
        [Min(0)] public int damage = 5;
        [Tooltip("문 뒤 멈춰 있는 빈틈(초)")]
        [Min(0f)] public float recoverTime = 0.8f;

        [Header("E 개체: 연속 할퀴기")]
        public string comboName = "연속 할퀴기";
        [Min(1)] public int comboHits = 2;
        [Tooltip("할퀴기 사이 간격(초). 플레이어 맞은 뒤 무적(0.5초)보다 길어야 모두 맞는다")]
        [Min(0f)] public float comboInterval = 0.55f;
        [Min(0)] public int comboDamage = 5;
        [Tooltip("할퀼 때마다 앞으로 내딛는 거리(m)")]
        [Min(0f)] public float comboStep = 0.4f;
        [Tooltip("마지막 할퀴기 뒤 빈틈(초)")]
        [Min(0f)] public float comboRecoverTime = 1.2f;
    }
}
