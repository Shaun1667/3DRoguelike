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

        internal FloorRewards(IReadOnlyList<RewardDefinition>[] offers, int guaranteedRoom, RewardDefinition guaranteed)
        {
            this.offers = offers;
            GuaranteedRoom = guaranteedRoom;
            Guaranteed = guaranteed;
        }

        public int RoomCount => offers.Length;
        /// <summary>보장 보상(권총)을 둔 방. 이 층에 보장이 없으면 -1.</summary>
        public int GuaranteedRoom { get; }
        /// <summary>그 방에 둔 보장 보상. 없으면 null.</summary>
        public RewardDefinition Guaranteed { get; }

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
    /// 보장(3-4): 보장 보상(<see cref="RewardDefinition.IsGuaranteed"/>)은 무작위 선택지에 없고, 그 던전 순서부터 아직 없으면
    /// 보장 방(<see cref="GuaranteeRoom"/>)의 선택지를 그 하나로 바꾼다. 다른 방을 다 정한 뒤에 바꾸므로 보상 난수를 더 쓰지 않고
    /// 다른 방의 보상은 보장이 없을 때와 같다. 층마다 보장은 하나(표 순서로 첫 줄), 나머지는 다음 층으로.
    /// 이미 가진 도구(<paramref name="owned"/>)를 주는 보상은 어디에도 내지 않는다.
    /// </summary>
    public static class RewardPlanner
    {
        public static FloorRewards Plan(FloorLayout layout, RewardSettings settings, IReadOnlyList<RewardDefinition> table, IRandom random)
        {
            return Plan(layout, settings, table, random, 0, null);
        }

        /// <param name="dungeon">길 위 던전 순서(층 씬만 Play하면 0). 보장 보상의 시작 던전과 비교한다.</param>
        /// <param name="owned">이 판의 강화 합계(가진 도구). null이면 아무것도 없는 것으로.</param>
        public static FloorRewards Plan(FloorLayout layout, RewardSettings settings, IReadOnlyList<RewardDefinition> table, IRandom random, int dungeon, UpgradeSheet owned)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var common = new List<RewardDefinition>(); var rare = new List<RewardDefinition>();
            foreach (RewardDefinition reward in table)
            {
                if (reward.ComingLater || reward.IsGuaranteed || Owned(reward, owned) || Closed(reward, owned)) continue;
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
            int guaranteedRoom = -1;
            RewardDefinition guaranteed = null;
            foreach (RewardDefinition reward in table)
            {
                if (!reward.IsGuaranteed || reward.ComingLater || dungeon < reward.GuaranteeFromDungeon || Owned(reward, owned) || Closed(reward, owned)) continue;
                guaranteed = reward;
                guaranteedRoom = GuaranteeRoom(layout);
                offers[guaranteedRoom] = new List<RewardDefinition> { reward }.AsReadOnly();
                break;
            }
            return new FloorRewards(offers, guaranteedRoom, guaranteed);
        }

        /// <summary>성장 트리(3-7)가 아직 열지 않은 보상 후보. 빼고 뽑으므로 아무것도 안 산 판은 그 줄이 없던 때와 같은 보상.</summary>
        static bool Closed(RewardDefinition reward, UpgradeSheet owned) =>
            reward.MetaLocked && (owned == null || !owned.IsRewardOpen(reward.Id));

        static bool Owned(RewardDefinition reward, UpgradeSheet owned) =>
            reward.Unlock != RewardUnlock.None && owned != null && owned.Has(reward.Unlock);

        /// <summary>
        /// 보장 보상을 둘 방: 시작 방에서 보스 방으로 가는 길의 첫 방. 방이 나무 모양이라 보스에 가려면 반드시 지나고,
        /// 들어서면 문이 잠겨 깨야 지날 수 있다(막다른 방이 아니라 보물방일 수 없다). 보스 방이 시작 바로 옆이면 시작 방.
        /// 맵만 보고 난수는 쓰지 않는다.
        /// </summary>
        public static int GuaranteeRoom(FloorLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            int start = layout.StartRoom.Id, boss = layout.BossRoom.Id;
            var parent = new int[layout.Rooms.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            parent[start] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                foreach (FloorDoor door in layout.Doors)
                {
                    int other = door.RoomA == room ? door.RoomB : door.RoomB == room ? door.RoomA : -1;
                    if (other < 0 || parent[other] != -2) continue;
                    parent[other] = room;
                    queue.Enqueue(other);
                }
            }
            if (boss == start || parent[boss] == -2) return start;
            int step = boss;
            while (parent[step] != start) step = parent[step];
            return step == boss ? start : step;
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
