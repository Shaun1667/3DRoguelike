using System;

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

    /// <summary>보상 하나. 실제 효과(피해 · 속도 등)는 3-2 강화 표에서 이 Id로 찾는다.</summary>
    public sealed class RewardDefinition
    {
        public RewardDefinition(string id, string name, string description, Tendency tendency, int amount, RewardRarity rarity)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Reward id is required.", nameof(id));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Id = id;
            Name = name ?? id;
            Description = description ?? "";
            Tendency = tendency;
            Amount = amount;
            Rarity = rarity;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Tendency Tendency { get; }
        /// <summary>고르면 그 성향이 이만큼 오른다.</summary>
        public int Amount { get; }
        public RewardRarity Rarity { get; }
    }
}
