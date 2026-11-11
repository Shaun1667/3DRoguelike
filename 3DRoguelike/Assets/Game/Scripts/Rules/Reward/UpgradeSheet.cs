using System;

namespace Game.Rules
{
    /// <summary>
    /// 판 안 강화 합계. 효과 조회는 여기 한 곳이다 — 플레이어 쪽은 <see cref="Scale"/> · <see cref="Bonus"/>만 묻는다.
    /// 같은 보상을 여러 번 고르면 퍼센트가 더해진다(상한 없음, 결정 2026-11-11). 판마다 새로 만든다(핵심 설계 규칙 4).
    /// </summary>
    public sealed class UpgradeSheet
    {
        readonly float[] percent = new float[UpgradeStats.Count];

        /// <summary>보상을 반영한 뒤. 화면 쪽이 바뀐 값을 다시 읽을 때 쓴다.</summary>
        public event Action Changed;

        public void Add(RewardDefinition reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            if (reward.Effects.Count == 0) return;
            foreach (StatEffect effect in reward.Effects) percent[(int)effect.stat] += effect.percent;
            Changed?.Invoke();
        }

        /// <summary>더해진 퍼센트 합 ÷ 100. 근력 두 번(+20% × 2)이면 0.4.</summary>
        public float Bonus(UpgradeStat stat) => percent[(int)stat] / 100f;

        /// <summary>기본값에 곱할 배율 = 1 + Bonus. 음수 합이어도 0.1 아래로 내려가지 않는다.</summary>
        public float Scale(UpgradeStat stat) => Math.Max(0.1f, 1f + Bonus(stat));

        /// <summary>정수 기본값(피해 · 탄창)에 배율을 곱해 반올림. 1 아래로 내려가지 않는다.</summary>
        public int ScaleInt(UpgradeStat stat, int baseValue) => Math.Max(1, (int)Math.Round(baseValue * Scale(stat), MidpointRounding.AwayFromZero));
    }
}
