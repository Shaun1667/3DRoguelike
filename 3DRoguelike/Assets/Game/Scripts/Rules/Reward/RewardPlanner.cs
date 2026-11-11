using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>보상 수치. 값은 BALANCE.md 11번.</summary>
    public sealed class RewardSettings
    {
        public RewardSettings(float battleRoomChance, int choices)
        {
            if (battleRoomChance < 0f || battleRoomChance > 1f) throw new ArgumentOutOfRangeException(nameof(battleRoomChance));
            if (choices < 1) throw new ArgumentOutOfRangeException(nameof(choices));
            BattleRoomChance = battleRoomChance;
            Choices = choices;
        }

        /// <summary>전투 방을 깼을 때 보상물이 나올 확률(0~1).</summary>
        public float BattleRoomChance { get; }
        /// <summary>보상물을 주우면 고르는 선택지 수.</summary>
        public int Choices { get; }
    }

    /// <summary>층의 방마다 정해 둔 보상 선택지. 비어 있으면 그 방에는 보상물이 없다.</summary>
    public sealed class FloorRewards
    {
        readonly IReadOnlyList<RewardDefinition>[] offers;

        internal FloorRewards(IReadOnlyList<RewardDefinition>[] offers)
        {
            this.offers = offers;
        }

        public int RoomCount => offers.Length;

        public IReadOnlyList<RewardDefinition> OffersFor(int roomId)
        {
            if (roomId < 0 || roomId >= offers.Length) throw new ArgumentOutOfRangeException(nameof(roomId));
            return offers[roomId];
        }
    }

    /// <summary>
    /// 층을 시작할 때 방마다 보상물이 나올지와 그 선택지를 정한다. 난수는 보상 용도(<see cref="RandomPurpose.Reward"/>).
    /// 방 Id 순서로 정하므로 같은 시드면 들어가는 순서와 무관하게 같은 보상이다.
    /// 전투 방: 확률로 일반 보상 선택지. 보물방: 반드시 희귀 보상 선택지. 시작 · 보스 방: 없음(보스 방을 깨면 층이 끝난다).
    /// 선택지는 표에서 겹치지 않게 무작위로 뽑고, 표가 모자라면 있는 만큼만.
    /// 준비 중(<see cref="RewardDefinition.ComingLater"/>)인 보상은 내지 않는다. 보물방은 희귀가 선택지 수보다 적으면
    /// 희귀를 모두 앞에 놓고 남은 자리를 일반으로 채운다.
    /// </summary>
    public static class RewardPlanner
    {
        public static FloorRewards Plan(FloorLayout layout, RewardSettings settings, IReadOnlyList<RewardDefinition> table, IRandom random)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var common = new List<RewardDefinition>(); var rare = new List<RewardDefinition>();
            foreach (RewardDefinition reward in table)
            {
                if (reward.ComingLater) continue;
                (reward.Rarity == RewardRarity.Rare ? rare : common).Add(reward);
            }

            var offers = new IReadOnlyList<RewardDefinition>[layout.Rooms.Count];
            foreach (FloorRoom room in layout.Rooms)
            {
                List<RewardDefinition> picked = null;
                if (room.Kind == RoomKind.Treasure)
                {
                    picked = Pick(rare, settings.Choices, random);
                    if (picked.Count < settings.Choices) picked.AddRange(Pick(common, settings.Choices - picked.Count, random));
                }
                else if (room.Kind == RoomKind.Battle && random.NextFloat() < settings.BattleRoomChance)
                {
                    picked = Pick(common, settings.Choices, random);
                }
                offers[room.Id] = picked == null ? (IReadOnlyList<RewardDefinition>)Array.Empty<RewardDefinition>() : picked.AsReadOnly();
            }
            return new FloorRewards(offers);
        }

        static List<RewardDefinition> Pick(List<RewardDefinition> pool, int count, IRandom random)
        {
            var bag = new List<RewardDefinition>(pool);
            var picked = new List<RewardDefinition>(Math.Min(count, bag.Count));
            while (picked.Count < count && bag.Count > 0)
            {
                int i = random.Next(bag.Count);
                picked.Add(bag[i]);
                bag.RemoveAt(i);
            }
            return picked;
        }
    }
}
