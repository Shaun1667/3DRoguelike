using System;

namespace Game.Rules
{
    /// <summary>보스 패턴. 새 패턴 = 끝에 한 줄(순서가 시드 결과를 정한다).</summary>
    public enum BossPattern
    {
        /// <summary>예고선 뒤 곧장 돌진.</summary>
        Charge,
        /// <summary>부채꼴로 깃털 날리기.</summary>
        FeatherVolley,
        /// <summary>떠올라 플레이어 위를 따라다니다 고정 → 급강하 내려찍기.</summary>
        Slam,
    }

    /// <summary>보스 패턴을 고르는 값(BALANCE 15번). 강화 = 체력 비율 이하가 된 뒤.</summary>
    public readonly struct BossPatternSettings
    {
        public BossPatternSettings(float idleMin, float idleMax, float enragedIdleMin, float enragedIdleMax, float enrageHealthRatio)
        {
            IdleMin = idleMin;
            IdleMax = idleMax;
            EnragedIdleMin = enragedIdleMin;
            EnragedIdleMax = enragedIdleMax;
            EnrageHealthRatio = enrageHealthRatio;
        }

        public float IdleMin { get; }
        public float IdleMax { get; }
        public float EnragedIdleMin { get; }
        public float EnragedIdleMax { get; }
        /// <summary>체력이 최대의 이 비율 이하가 되면 강화(0 ~ 1).</summary>
        public float EnrageHealthRatio { get; }

        public void Validate()
        {
            if (IdleMin < 0f || IdleMax < IdleMin) throw new ArgumentException("idle range");
            if (EnragedIdleMin < 0f || EnragedIdleMax < EnragedIdleMin) throw new ArgumentException("enraged idle range");
            if (EnrageHealthRatio < 0f || EnrageHealthRatio > 1f) throw new ArgumentException("enrage ratio");
        }
    }

    /// <summary>패턴 하나 차례: 먼저 대기, 그다음 패턴.</summary>
    public readonly struct BossStep
    {
        public BossStep(float idleSeconds, BossPattern pattern)
        {
            IdleSeconds = idleSeconds;
            Pattern = pattern;
        }

        public float IdleSeconds { get; }
        public BossPattern Pattern { get; }
    }

    /// <summary>
    /// 보스가 다음에 무엇을 할지 정한다(예전 까치 보스전 규칙): "대기 → 패턴" 되풀이, 같은 패턴은 두 번 연달아 나오지 않는다.
    /// 첫 패턴은 셋 중 하나, 그 뒤는 직전 패턴을 뺀 둘 중 하나. 체력이 비율 이하가 되면 강화(대기가 짧아짐 — 패턴 세기는 쓰는 쪽이 <see cref="Enraged"/>로).
    /// 차례마다 난수를 늘 두 번(대기 1 · 패턴 1) 쓰므로, 강화가 언제 되든 같은 시드면 패턴 순서가 같다.
    /// </summary>
    public sealed class BossPatternPlanner
    {
        public const int PatternCount = 3;

        readonly BossPatternSettings settings;
        readonly IRandom random;
        int last = -1;

        public BossPatternPlanner(BossPatternSettings settings, IRandom random)
        {
            settings.Validate();
            this.settings = settings;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool Enraged { get; private set; }

        /// <summary>체력이 바뀌었을 때. 이번에 강화가 됐으면 true(한 번만). 죽었으면(0) 강화하지 않는다.</summary>
        public bool UpdateHealth(int current, int max)
        {
            if (Enraged || current <= 0 || max <= 0) return false;
            if (current > max * settings.EnrageHealthRatio) return false;
            Enraged = true;
            return true;
        }

        public BossStep Next()
        {
            float min = Enraged ? settings.EnragedIdleMin : settings.IdleMin;
            float max = Enraged ? settings.EnragedIdleMax : settings.IdleMax;
            float idle = min + (max - min) * random.NextFloat();

            int pattern;
            if (last < 0) pattern = random.Next(PatternCount);
            else
            {
                pattern = random.Next(PatternCount - 1);
                if (pattern >= last) pattern++;
            }
            last = pattern;
            return new BossStep(idle, (BossPattern)pattern);
        }
    }

    /// <summary>보스 패턴의 모양 계산(엔진 없음, 수평 x · z 평면).</summary>
    public static class BossShapes
    {
        /// <summary>
        /// 부채꼴 발마다 조준선에서 돌린 각도(도). 가운데가 조준선, 양끝이 ±spread/2.
        /// offsetHalf면 반 칸씩 엇갈려 두 번째 연사가 첫 번째의 틈을 메운다(예전 까치 보스전).
        /// </summary>
        public static float[] FanAngles(int count, float spread, bool offsetHalf)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var angles = new float[count];
            float step = count > 1 ? spread / (count - 1) : 0f;
            float start = count > 1 ? -spread * 0.5f : 0f;
            if (offsetHalf) start += step * 0.5f;
            for (int i = 0; i < count; i++) angles[i] = start + step * i;
            return angles;
        }

        /// <summary>
        /// 사각 방(가운데에서 반폭 halfX · halfZ) 안의 점에서 방향(dx, dz, 단위 벡터)으로 곧장 갈 때 벽까지 거리, 최대 maxDistance.
        /// 점은 방 가운데 기준. 벽 밖에 있으면 벽 위로 옮겨 잰다(벽에 붙은 보스가 안쪽으로 돌진할 수 있게).
        /// </summary>
        public static float DistanceToEdge(float x, float z, float dx, float dz, float halfX, float halfZ, float maxDistance)
        {
            x = Math.Max(-halfX, Math.Min(halfX, x));
            z = Math.Max(-halfZ, Math.Min(halfZ, z));
            float distance = maxDistance;
            if (dx > 1e-6f) distance = Math.Min(distance, (halfX - x) / dx);
            else if (dx < -1e-6f) distance = Math.Min(distance, (-halfX - x) / dx);
            if (dz > 1e-6f) distance = Math.Min(distance, (halfZ - z) / dz);
            else if (dz < -1e-6f) distance = Math.Min(distance, (-halfZ - z) / dz);
            return Math.Max(0f, distance);
        }
    }
}
