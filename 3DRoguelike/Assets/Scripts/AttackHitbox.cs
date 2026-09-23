using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerAttack이 잠깐 생성했다가 지우는 히트박스에 붙는 스크립트.
/// Collider가 Is Trigger로 설정되어 있어야 하고, 이 오브젝트(혹은 부모)에 Rigidbody가 있어야
/// 트리거 이벤트가 정상적으로 발생한다 (플레이어 쪽 Rigidbody/CharacterController로 충분).
/// </summary>
[RequireComponent(typeof(Collider))]
public class AttackHitbox : MonoBehaviour
{
    [Tooltip("이 태그를 가진 대상만 공격 판정 (예: Enemy). 비워두면 모든 대상에 반응")]
    [SerializeField] private string targetTag = "Enemy";

    // 한 번의 공격(히트박스 생존 시간) 동안 같은 대상이 중복으로 맞지 않도록 기록
    private readonly HashSet<Collider> hitTargets = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        // 태그 필터링 (targetTag를 비워두면 전부 통과)
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            return;
        }

        // 이미 이번 공격에서 맞은 대상이면 무시 (한 번 휘두르는 동안 중복 히트 방지)
        if (hitTargets.Contains(other))
        {
            return;
        }
        hitTargets.Add(other);

        Debug.Log($"공격 성공! 대상: {other.name}");

        // 실제 데미지 처리는 여기에 구현하면 됩니다. 예:
        // if (other.TryGetComponent<Enemy>(out var enemy))
        // {
        //     enemy.TakeDamage(10);
        // }
    }
}
