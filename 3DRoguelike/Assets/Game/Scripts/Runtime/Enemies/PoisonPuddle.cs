using UnityEngine;

namespace Game
{
    /// <summary>두꺼비 독액이 남기는 웅덩이(임시 모양: 초록 원판). 정해진 시간 동안 안에 선 플레이어에게 간격마다 피해.</summary>
    public sealed class PoisonPuddle : MonoBehaviour
    {
        private Health target;
        private float radius;
        private float endTime;
        private int damagePerTick;
        private float tickInterval;
        private float nextTickTime;
        private string source;

        public void Begin(Health player, float puddleRadius, float duration, int tickDamage, float interval, string damageSource)
        {
            target = player;
            radius = puddleRadius;
            endTime = Time.time + duration;
            damagePerTick = tickDamage;
            tickInterval = interval;
            source = damageSource;
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(radius * 2f, scale.y, radius * 2f);
        }

        private void Update()
        {
            if (Time.time >= endTime)
            {
                Destroy(gameObject);
                return;
            }
            if (target == null || target.IsDead || Time.time < nextTickTime) return;

            Vector3 offset = target.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > 1.5f) return; // 뛰어넘는 중
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius) return;

            int before = target.Current;
            target.TakeDamage(damagePerTick, source);
            // 구르기 · 맞은 뒤 무적으로 막히면 다음 프레임에 다시 본다
            if (target.Current < before) nextTickTime = Time.time + tickInterval;
        }
    }
}
