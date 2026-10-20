using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("히트박스 설정")]
    [Tooltip("공격 시 생성할 히트박스 프리팹. Collider(Is Trigger 체크)와 AttackHitbox 스크립트가 붙어있어야 함")]
    public GameObject attack1;

    [Tooltip("히트박스가 생성될 위치/방향. 보통 캐릭터 앞쪽(무기 위치)에 빈 오브젝트를 만들어서 연결")]
    public Transform attackPoint;

    [Tooltip("히트박스가 살아있는 시간 (초). 이 시간이 지나면 자동으로 삭제됨")]
    public float hitboxDuration = 0.2f;

    [Tooltip("클릭부터 히트박스가 생기기까지의 시간 (초). 공격 동작에서 손이 가장 앞으로 나오는 순간에 맞춤 (클립 1.05초 ÷ 재생 속도 3)")]
    public float hitDelay = 0.35f;

    [Header("공격 수치")]
    [Tooltip("한 번 맞힐 때 보스에게 주는 데미지")]
    public int damage = 10;

    [Tooltip("다음 공격까지 기다려야 하는 시간 (초). 연타로 순식간에 잡지 못하게 막는다")]
    public float attackCooldown = 0.4f;

    [Tooltip("공격 동작 동안 이동·대시·점프를 막는 시간 (초). 공격 동작이 끝나 이동으로 돌아가는 시점에 맞춤 (클립 2.63초 ÷ 재생 속도 3 × 0.85)")]
    public float actionLockTime = 0.75f;

    /// <summary>공격이 나간 순간 (공격 애니메이션 재생용). 인자: 왼손 공격이면 true</summary>
    public event System.Action<bool> Attacked;

    /// <summary>공격 동작 중인지. 이 동안 PlayerMovement는 이동·대시·점프를 받지 않는다</summary>
    public bool IsAttacking => Time.time < lockEndTime;

    private float nextAttackTime;
    private float lockEndTime;
    private bool nextIsLeft; // 오른손 → 왼손 → 오른손 … 번갈아 나감

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // 좌클릭이 "눌리는 순간"에만 한 번 실행됨 (누르고 있어도 매 프레임 실행 안 됨)
        if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            Attack();
        }
    }

    private void Attack()
    {
        lockEndTime = Time.time + actionLockTime;
        Attacked?.Invoke(nextIsLeft);
        nextIsLeft = !nextIsLeft;
        StartCoroutine(SpawnHitbox());
    }

    /// <summary>
    /// 손이 앞으로 뻗는 순간(hitDelay)에 히트박스를 생성하고, hitboxDuration만큼 기다린 뒤 자동으로 삭제한다.
    /// </summary>
    private IEnumerator SpawnHitbox()
    {
        if (hitDelay > 0f) yield return new WaitForSeconds(hitDelay);

        if (attack1 == null)
        {
            Debug.LogWarning("attack1(히트박스 프리팹)이 연결되어 있지 않습니다.");
            yield break;
        }

        // attackPoint가 있으면 그 위치/방향에, 없으면 캐릭터 앞쪽에 생성
        Vector3 spawnPos = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
        Quaternion spawnRot = attackPoint != null ? attackPoint.rotation : transform.rotation;

        GameObject hitboxInstance = Instantiate(attack1, spawnPos, spawnRot);

        // 캐릭터 크기를 키우면 공격 범위도 같이 커지도록 플레이어 스케일을 따라간다
        hitboxInstance.transform.localScale = Vector3.Scale(hitboxInstance.transform.localScale, transform.lossyScale);

        // 데미지 수치는 프리팹이 아니라 PlayerAttack 한 곳에서 관리한다
        if (hitboxInstance.TryGetComponent(out AttackHitbox hitbox))
        {
            hitbox.SetDamage(damage);
        }

        // 잠깐 유지
        yield return new WaitForSeconds(hitboxDuration);

        // 시간이 지나면 제거
        if (hitboxInstance != null)
        {
            Destroy(hitboxInstance);
        }
    }
}
