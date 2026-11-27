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

        readonly bool[] unlocked = new bool[RewardUnlocks.Count];

        /// <summary>보상의 효과를 더하고 주는 도구를 갖는다. 바뀐 것이 있을 때만 한 번 알린다.</summary>
        public void Add(RewardDefinition reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            bool changed = false;
            foreach (StatEffect effect in reward.Effects) { percent[(int)effect.stat] += effect.percent; changed = true; }
            if (reward.Unlock != RewardUnlock.None && !unlocked[(int)reward.Unlock]) { unlocked[(int)reward.Unlock] = true; changed = true; }
            if (changed) Changed?.Invoke();
        }

        readonly System.Collections.Generic.HashSet<string> openedRewards = new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// 판 밖 성장 트리(3-7) 노드의 능력치 효과를 더한다(판을 시작할 때). 보상과 같은 합계에 들어가고, 성향은 올리지 않는다.
        /// </summary>
        public void AddEffects(System.Collections.Generic.IEnumerable<StatEffect> effects)
        {
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            bool changed = false;
            foreach (StatEffect effect in effects) { percent[(int)effect.stat] += effect.percent; changed = true; }
            if (changed) Changed?.Invoke();
        }

        /// <summary>트리가 연 보상 후보(<see cref="RewardDefinition.MetaLocked"/>)를 이 판의 보상 계획에 넣는다.</summary>
        public void OpenReward(string rewardId)
        {
            if (string.IsNullOrEmpty(rewardId)) throw new ArgumentException("Reward id is required.", nameof(rewardId));
            openedRewards.Add(rewardId);
        }

        public bool IsRewardOpen(string rewardId) => rewardId != null && openedRewards.Contains(rewardId);

        /// <summary>열린 보상 후보(글자 순서).</summary>
        public System.Collections.Generic.List<string> OpenedRewards()
        {
            var list = new System.Collections.Generic.List<string>(openedRewards);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>그 도구를 가졌는가. None은 늘 true.</summary>
        public bool Has(RewardUnlock unlock) => unlock == RewardUnlock.None || unlocked[(int)unlock];

        /// <summary>도구를 주거나 뺀다(디버그 창 · 이어 하기). 보상으로 얻는 것은 <see cref="Add"/>.</summary>
        public void SetUnlocked(RewardUnlock unlock, bool value)
        {
            if (unlock == RewardUnlock.None || unlocked[(int)unlock] == value) return;
            unlocked[(int)unlock] = value;
            Changed?.Invoke();
        }

        /// <summary>가진 도구 목록(enum 순서). 저장 · 던전에 들어설 때 쓴다.</summary>
        public System.Collections.Generic.List<RewardUnlock> Unlocked()
        {
            var list = new System.Collections.Generic.List<RewardUnlock>();
            for (int i = 1; i < unlocked.Length; i++) if (unlocked[i]) list.Add((RewardUnlock)i);
            return list;
        }


        /// <summary>더해진 퍼센트 합 ÷ 100. 근력 두 번(+20% × 2)이면 0.4.</summary>
        public float Bonus(UpgradeStat stat) => percent[(int)stat] / 100f;

        /// <summary>기본값에 곱할 배율 = 1 + Bonus. 음수 합이어도 0.1 아래로 내려가지 않는다.</summary>
        public float Scale(UpgradeStat stat) => Math.Max(0.1f, 1f + Bonus(stat));

        /// <summary>정수 기본값(피해 · 탄창)에 배율을 곱해 반올림. 1 아래로 내려가지 않는다.</summary>
        public int ScaleInt(UpgradeStat stat, int baseValue) => Math.Max(1, (int)Math.Round(baseValue * Scale(stat), MidpointRounding.AwayFromZero));
    }
}
