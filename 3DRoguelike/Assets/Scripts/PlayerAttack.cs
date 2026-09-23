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

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // 좌클릭이 "눌리는 순간"에만 한 번 실행됨 (누르고 있어도 매 프레임 실행 안 됨)
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Attack();
        }
    }

    private void Attack()
    {
        Debug.Log("공격!");
        StartCoroutine(SpawnHitbox());
    }

    /// <summary>
    /// 히트박스를 생성하고, hitboxDuration만큼 기다린 뒤 자동으로 삭제한다.
    /// </summary>
    private IEnumerator SpawnHitbox()
    {
        if (attack1 == null)
        {
            Debug.LogWarning("attack1(히트박스 프리팹)이 연결되어 있지 않습니다.");
            yield break;
        }

        // attackPoint가 있으면 그 위치/방향에, 없으면 캐릭터 앞쪽에 생성
        Vector3 spawnPos = attackPoint != null ? attackPoint.position : transform.position + transform.forward;
        Quaternion spawnRot = attackPoint != null ? attackPoint.rotation : transform.rotation;

        GameObject hitboxInstance = Instantiate(attack1, spawnPos, spawnRot);

        // 잠깐 유지
        yield return new WaitForSeconds(hitboxDuration);

        // 시간이 지나면 제거
        if (hitboxInstance != null)
        {
            Destroy(hitboxInstance);
        }
    }
}
