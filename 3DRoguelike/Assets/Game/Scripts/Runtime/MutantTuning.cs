using UnityEngine;

namespace Game
{
    /// <summary>숲의 Mutant 수치. 값의 기준은 BALANCE.md 9번.</summary>
    [CreateAssetMenu(menuName = "Game/Mutant Tuning")]
    public sealed class MutantTuning : ScriptableObject
    {
        [Tooltip("결과 화면의 사망 원인: '{이름}의 {공격 이름}'")]
        public string displayName = "Mutant";
        public string attackName = "할퀴기";

        [Min(1)] public int maxHealth = 30;
        [Min(0f)] public float moveSpeed = 3f;
        [Tooltip("초당 회전 각도")]
        [Min(0f)] public float turnSpeed = 540f;
        [Tooltip("길을 다시 찾는 간격(초)")]
        [Min(0.02f)] public float repathInterval = 0.2f;
        [Tooltip("나타나기 전 바닥 예고(초). 이동 · 공격 · 피격 없음")]
        [Min(0f)] public float spawnTime = 1f;

        [Header("할퀴기")]
        [Tooltip("이 거리 안에 들어오면 멈추고 예고를 시작한다(m)")]
        [Min(0f)] public float attackStartRange = 1.8f;
        [Tooltip("예고 시간(초). 방향은 예고를 시작할 때 고정된다")]
        [Min(0f)] public float windupTime = 0.6f;
        [Tooltip("할퀴는 순간 맞는 거리(m)")]
        [Min(0f)] public float hitRange = 2.2f;
        [Tooltip("정면 기준 전체 각도")]
        [Range(0f, 360f)] public float hitAngle = 100f;
        [Min(0)] public int damage = 10;
        [Tooltip("할퀸 뒤 멈춰 있는 빈틈(초)")]
        [Min(0f)] public float recoverTime = 0.9f;

        [Min(0f)] public float hitFlashTime = 0.08f;
        [Tooltip("죽은 뒤 사라질 때까지(초)")]
        [Min(0f)] public float corpseTime = 1f;
    }
}
