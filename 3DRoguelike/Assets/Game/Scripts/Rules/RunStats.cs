using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>판이 어떻게 끝났나. 끝에만 추가한다.</summary>
    public enum RunOutcome
    {
        /// <summary>아직 진행 중.</summary>
        Playing,
        Died,
        /// <summary>보스 방을 깼다. 던전 하나만 시험하는 층 씬(Floor를 바로 Play)에서만 쓴다.</summary>
        FloorCleared,
        /// <summary>월드 길의 마지막 던전을 깨서 지역을 끝냈다.</summary>
        RegionCleared,
    }

    /// <summary>결과 화면에 보여 줄 판 기록. 시간은 화면 쪽이 재서 넘긴다(규칙은 시계를 모른다).</summary>
    public sealed class RunStats
    {
        public RunStats(RunSeed seed, int battleRooms)
        {
            if (battleRooms < 0) throw new ArgumentOutOfRangeException(nameof(battleRooms));
            Seed = seed;
            BattleRooms = battleRooms;
        }

        public RunSeed Seed { get; }
        /// <summary>깰 방 수(적이 나오는 방: 전투 · 보스). 월드에서는 들어간 던전마다 더한다.</summary>
        public int BattleRooms { get; private set; }
        /// <summary>월드 길의 던전 수. 층 씬만 시험할 때는 0.</summary>
        public int Dungeons { get; private set; }
        public int DungeonsCleared { get; private set; }
        public int RoomsCleared { get; private set; }
        public int Kills { get; private set; }
        public RunOutcome Outcome { get; private set; }
        /// <summary>죽게 한 공격. 죽지 않았으면 null.</summary>
        public string DeathCause { get; private set; }
        public float ElapsedSeconds { get; private set; }
        /// <summary>판 안 성향(핵심 설계 규칙 4: 고른 보상으로만 오르고 판마다 0에서 시작).</summary>
        public int Beast { get; private set; }
        public int Human { get; private set; }
        public IReadOnlyList<RewardDefinition> RewardsTaken => rewardsTaken;
        /// <summary>고른 보상의 능력치 강화 합계(판마다 0에서 시작).</summary>
        public UpgradeSheet Upgrades { get; } = new UpgradeSheet();

        readonly List<RewardDefinition> rewardsTaken = new List<RewardDefinition>();

        /// <summary>보상을 골랐다. 그 성향이 보상의 양만큼 오르고, 효과가 강화 합계에 더해진다.</summary>
        /// <returns>판이 진행 중이라 반영했으면 true</returns>
        public bool TakeReward(RewardDefinition reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            if (Outcome != RunOutcome.Playing) return false;
            if (reward.Tendency == Tendency.Beast) Beast += reward.Amount; else Human += reward.Amount;
            rewardsTaken.Add(reward);
            Upgrades.Add(reward);
            return true;
        }

        /// <summary>월드 판으로 쓴다(판 처음에 한 번).</summary>
        public void UseWorld(int dungeons)
        {
            if (dungeons < 0) throw new ArgumentOutOfRangeException(nameof(dungeons));
            Dungeons = dungeons;
        }

        /// <summary>던전에 들어가 그 층의 깰 방 수를 더한다.</summary>
        public void AddBattleRooms(int rooms)
        {
            if (rooms < 0) throw new ArgumentOutOfRangeException(nameof(rooms));
            BattleRooms += rooms;
        }

        public void RecordDungeonCleared()
        {
            if (Outcome == RunOutcome.Playing && DungeonsCleared < Dungeons) DungeonsCleared++;
        }

        public void RecordKill()
        {
            if (Outcome == RunOutcome.Playing) Kills++;
        }

        public void RecordRoomCleared()
        {
            if (Outcome == RunOutcome.Playing) RoomsCleared++;
        }

        /// <summary>저장 파일에서 되살릴 때만(<see cref="RunSaveRules"/>). 성향 · 강화는 고른 보상을 다시 반영해 얻는다.</summary>
        internal void RestoreCounts(int battleRooms, int dungeonsCleared, int roomsCleared, int kills)
        {
            if (battleRooms < 0 || dungeonsCleared < 0 || dungeonsCleared > Dungeons || roomsCleared < 0 || kills < 0)
                throw new ArgumentOutOfRangeException(nameof(battleRooms), "Saved run counts are out of range.");
            BattleRooms = battleRooms;
            DungeonsCleared = dungeonsCleared;
            RoomsCleared = roomsCleared;
            Kills = kills;
        }


        /// <returns>이번에 판이 끝났으면 true. 이미 끝났으면 false.</returns>
        public bool Die(string cause, float elapsedSeconds)
        {
            if (Outcome != RunOutcome.Playing) return false;
            Outcome = RunOutcome.Died;
            DeathCause = string.IsNullOrEmpty(cause) ? "알 수 없음" : cause;
            ElapsedSeconds = elapsedSeconds;
            return true;
        }

        /// <returns>이번에 판이 끝났으면 true. 이미 끝났으면 false.</returns>
        public bool ClearFloor(float elapsedSeconds)
        {
            if (Outcome != RunOutcome.Playing) return false;
            Outcome = RunOutcome.FloorCleared;
            ElapsedSeconds = elapsedSeconds;
            return true;
        }

        /// <returns>이번에 판이 끝났으면 true. 이미 끝났으면 false.</returns>
        public bool ClearRegion(float elapsedSeconds)
        {
            if (Outcome != RunOutcome.Playing) return false;
            Outcome = RunOutcome.RegionCleared;
            ElapsedSeconds = elapsedSeconds;
            return true;
        }

        /// <summary>"m:ss". 한 시간이 넘으면 "h:mm:ss".</summary>
        public static string FormatTime(float seconds)
        {
            int total = Math.Max(0, (int)Math.Floor(seconds));
            int hours = total / 3600;
            int minutes = total / 60 % 60;
            int secs = total % 60;
            return hours > 0 ? $"{hours}:{minutes:00}:{secs:00}" : $"{minutes}:{secs:00}";
        }
    }
}
