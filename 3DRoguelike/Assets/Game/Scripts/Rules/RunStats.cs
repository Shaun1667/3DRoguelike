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
        /// <summary>보스 방을 깼다. 층 이동(3-4)이 생기면 판 끝이 아니라 다음 층이 된다.</summary>
        FloorCleared,
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
        /// <summary>층의 깰 방 수(적이 나오는 방: 전투 · 보스).</summary>
        public int BattleRooms { get; }
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

        readonly List<RewardDefinition> rewardsTaken = new List<RewardDefinition>();

        /// <summary>보상을 골랐다. 그 성향이 보상의 양만큼 오른다.</summary>
        /// <returns>판이 진행 중이라 반영했으면 true</returns>
        public bool TakeReward(RewardDefinition reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            if (Outcome != RunOutcome.Playing) return false;
            if (reward.Tendency == Tendency.Beast) Beast += reward.Amount; else Human += reward.Amount;
            rewardsTaken.Add(reward);
            return true;
        }

        public void RecordKill()
        {
            if (Outcome == RunOutcome.Playing) Kills++;
        }

        public void RecordRoomCleared()
        {
            if (Outcome == RunOutcome.Playing) RoomsCleared++;
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
