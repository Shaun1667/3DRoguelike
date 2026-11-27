using UnityEngine;

namespace Game
{
    /// <summary>여우(히트앤런, 도시) 수치. 값의 기준은 BALANCE.md 9-1번.</summary>
    [CreateAssetMenu(menuName = "Game/Enemies/Fox Tuning")]
    public sealed class FoxTuning : EnemyTuning
    {
        public FoxTuning()
        {
            displayName = "여우 Mutant";
            evolvedDisplayName = "여우 E 개체";
            maxHealth = 22;
            moveSpeed = 4.4f;
            windupLeanAngle = 15f;
            strikeLeanAngle = 20f;
        }

        [Header("달려들어 물기")]
        public string attackName = "달려들어 물기";
        [Tooltip("이 거리 안이고 앞이 트였으면 멈추고 웅크린다(m). 가장 멀리 달려드는 거리 - 더 달려드는 거리 이하라야 닿는다")]
        [Min(0f)] public float lungeStartRange = 4f;
        [Tooltip("웅크림(예고, 초). 달려들 곳은 웅크리기 시작할 때 정해져 바닥에 보인다")]
        [Min(0f)] public float windupTime = 0.6f;
        [Tooltip("달려드는 시간(초)")]
        [Min(0.05f)] public float lungeTime = 0.3f;
        [Tooltip("가장 멀리 달려드는 거리(m)")]
        [Min(0f)] public float lungeDistance = 4.5f;
        [Tooltip("플레이어가 있던 곳보다 이만큼 더 달려든다(m)")]
        [Min(0f)] public float lungeOvershoot = 0.5f;
        [Tooltip("달려드는 동안 무는 거리(m)")]
        [Min(0f)] public float hitRadius = 0.9f;
        [Min(0)] public int damage = 9;

        [Header("뒤로 빠졌다 다시 노리기")]
        [Tooltip("문 뒤 플레이어 반대쪽으로 물러나는 거리(m)")]
        [Min(0f)] public float retreatDistance = 3.5f;
        [Tooltip("물러나는 시간(초) = 빈틈")]
        [Min(0.05f)] public float retreatTime = 1f;
        [Tooltip("물러난 뒤 옆걸음으로 노리는 시간(초). 이 동안은 달려들지 않는다")]
        [Min(0f)] public float stalkTime = 0.8f;
        [Tooltip("노릴 때 옆걸음 빠르기(m/s, 걷기 동작 빠르기에 맞춤)")]
        [Min(0f)] public float stalkSpeed = 0.55f;

        [Header("E 개체: 연속 달려들기")]
        public string chainName = "연속 달려들기";
        [Tooltip("첫 번째 달려들기 뒤 다시 웅크리는 시간(초)")]
        [Min(0f)] public float chainWindupTime = 0.35f;
        [Min(0)] public int chainDamage = 7;
    }
}
