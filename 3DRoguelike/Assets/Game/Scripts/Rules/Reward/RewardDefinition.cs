using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>보상이 올리는 성향. 끝에만 추가한다.</summary>
    public enum Tendency
    {
        Beast,
        Human,
    }

    /// <summary>보상 희귀도. 일반은 방을 깰 때, 희귀는 보물방에서 나온다. 끝에만 추가한다.</summary>
    public enum RewardRarity
    {
        Common,
        Rare,
    }

    /// <summary>보상 하나. 고르면 성향이 오르고, 효과(<see cref="Effects"/>)가 <see cref="UpgradeSheet"/>에 더해진다.</summary>
    public sealed class RewardDefinition
    {
        public RewardDefinition(string id, string name, string description, Tendency tendency, int amount, RewardRarity rarity,
                                IReadOnlyList<StatEffect> effects = null, bool comingLater = false,
                                RewardUnlock unlock = RewardUnlock.None, int guaranteeFromDungeon = -1, bool metaLocked = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Reward id is required.", nameof(id));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Id = id;
            Name = name ?? id;
            Description = description ?? "";
            Tendency = tendency;
            Amount = amount;
            Rarity = rarity;
            Effects = effects == null ? (IReadOnlyList<StatEffect>)Array.Empty<StatEffect>() : new List<StatEffect>(effects).AsReadOnly();
            ComingLater = comingLater;
            if (guaranteeFromDungeon >= 0 && unlock == RewardUnlock.None)
                throw new ArgumentException("Only a reward that unlocks something can be guaranteed (it must end once owned).", nameof(guaranteeFromDungeon));
            Unlock = unlock;
            GuaranteeFromDungeon = guaranteeFromDungeon < 0 ? -1 : guaranteeFromDungeon;
            MetaLocked = metaLocked;
        }

        /// <summary>
        /// 판 밖 성장 트리(3-7)의 노드를 사야 보상 후보에 들어간다. 열지 않았으면 선택지에 없다 — 그래서 아무것도 안 산 판은
        /// 이 줄이 없던 때와 같은 보상이 나온다. 열렸는지는 판의 강화 합계(<see cref="UpgradeSheet.IsRewardOpen"/>).
        /// </summary>
        public bool MetaLocked { get; }

        /// <summary>주는 도구(권총 등). 없으면 None. 가지고 있으면 선택지 · 보장에 다시 나오지 않는다.</summary>
        public RewardUnlock Unlock { get; }
        /// <summary>
        /// 이 던전 순서(0 = 첫 던전)부터, 아직 없으면 층마다 보장 방에 반드시 놓는다(3-4 첫 던전 권총). -1이면 보장 없음.
        /// 보장 보상은 무작위 선택지에는 나오지 않는다.
        /// </summary>
        public int GuaranteeFromDungeon { get; }
        public bool IsGuaranteed => GuaranteeFromDungeon >= 0;

        /// <summary>능력치 강화. 같은 보상을 다시 고르면 또 더해진다.</summary>
        public IReadOnlyList<StatEffect> Effects { get; }
        /// <summary>기술(돌진 · 산탄총 등)이 아직 없어 선택지에 내지 않는 보상(결정 2026-11-11, 기술은 나중 항목).</summary>
        public bool ComingLater { get; }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Tendency Tendency { get; }
        /// <summary>고르면 그 성향이 이만큼 오른다.</summary>
        public int Amount { get; }
        public RewardRarity Rarity { get; }
    }
}
