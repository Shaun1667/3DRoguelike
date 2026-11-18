using UnityEngine;

namespace Game
{
    /// <summary>
    /// 두꺼비 독액 한 발(임시 모양: 초록 공). 포물선으로 정해진 곳에 떨어져 둘레에 피해를 주고 독 웅덩이를 남긴다.
    /// 쏜 두꺼비가 죽어도 날아가던 독액은 그대로 떨어진다(필요한 값은 쏠 때 다 받는다).
    /// </summary>
    public sealed class ToadSpit : MonoBehaviour
    {
        /// <summary>한 발에 필요한 값. 두꺼비가 <see cref="ToadTuning"/>에서 채운다.</summary>
        public struct Shot
        {
            public Vector3 from;
            public Vector3 to;
            public float flightTime;
            public float arcHeight;
            public Health target;
            public int damage;
            public float splashRadius;
            public string source;
            public PoisonPuddle puddlePrefab;
            public float puddleRadius;
            public float puddleTime;
            public int puddleTickDamage;
            public float puddleTickInterval;
            public string puddleSource;
        }

        private Shot shot;
        private float startTime;

        public void Launch(Shot value)
        {
            shot = value;
            startTime = Time.time;
            transform.position = shot.from;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - startTime) / shot.flightTime);
            transform.position = Vector3.Lerp(shot.from, shot.to, t) + Vector3.up * (4f * shot.arcHeight * t * (1f - t));
            if (t >= 1f) Land();
        }

        private void Land()
        {
            Health target = shot.target;
            // 안전 지대(마을) 안의 플레이어는 맞지 않는다
            if (target != null && !target.IsDead && !SafeZone.Protects(target.transform.position))
            {
                Vector3 offset = target.transform.position - shot.to;
                bool near = Mathf.Abs(offset.y) < 1.5f;
                offset.y = 0f;
                if (near && offset.sqrMagnitude <= shot.splashRadius * shot.splashRadius) target.TakeDamage(shot.damage, shot.source);
            }
            if (shot.puddlePrefab != null && shot.puddleTime > 0f)
            {
                PoisonPuddle puddle = Instantiate(shot.puddlePrefab, shot.to + Vector3.up * 0.03f, Quaternion.identity);
                puddle.Begin(shot.target, shot.puddleRadius, shot.puddleTime, shot.puddleTickDamage, shot.puddleTickInterval, shot.puddleSource);
            }
            Destroy(gameObject);
        }
    }
}
