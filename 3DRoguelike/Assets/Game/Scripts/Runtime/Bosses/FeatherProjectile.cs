using UnityEngine;

namespace Game
{
    /// <summary>
    /// 보스 깃털. 앞으로 곧장 날아가고, 플레이어 몸 가운데와 거리로 맞았는지 본다(콜라이더 없음).
    /// 구르기 무적 · 맞은 뒤 무적이면 체력이 줄지 않아 그대로 지나간다.
    /// </summary>
    public sealed class FeatherProjectile : MonoBehaviour
    {
        private Health target;
        private float speed;
        private int damage;
        private float hitRadius;
        private float centerHeight;
        private string source;
        private float dieTime;

        public void Launch(Health player, BossTuning tuning)
        {
            target = player;
            speed = tuning.featherSpeed;
            damage = tuning.featherDamage;
            hitRadius = tuning.featherHitRadius;
            centerHeight = tuning.playerCenterHeight;
            source = $"{tuning.displayName}의 {tuning.featherName}";
            dieTime = Time.time + tuning.featherLifetime;
        }

        private void Update()
        {
            transform.position += transform.forward * (speed * Time.deltaTime);
            if (target != null && !target.IsDead)
            {
                Vector3 center = target.transform.position + Vector3.up * centerHeight;
                if ((center - transform.position).sqrMagnitude <= hitRadius * hitRadius)
                {
                    int before = target.Current;
                    target.TakeDamage(damage, source);
                    if (target.Current < before)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }
            }
            if (Time.time >= dieTime) Destroy(gameObject);
        }
    }
}
