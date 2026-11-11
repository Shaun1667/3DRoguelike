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
                                IReadOnlyList<StatEffect> effects = null, bool comingLater = false)
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
        }

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
