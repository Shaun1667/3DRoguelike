using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 판 중간 저장의 규칙 한 곳(3-6, 핵심 설계 규칙 3): 판 상태(<see cref="RunSession"/>)에서 무엇을 담고(<see cref="Capture"/>)
    /// 어떻게 되살리는지(<see cref="TryRestore"/>). 담는 것 = 시드 · 월드 진행(깬 던전 · 잡은 무리 · 돌아올 입구 · 저장한 자리) ·
    /// 던전 안이면 그 층의 선 방 · 깬 방 · 가 본 방 · 보상 받은 방 · 들어설 때 가진 도구 · 체력 · 탄 · 고른 보상 · 가진 도구 · 판 기록 수.
    /// 담지 않는 것 = 맵 · 적 · 보상 선택지(같은 시드로 다시 만든다), 싸우는 중 상태(저장은 문이 잠기지 않았을 때만 — 끄면 그 방 처음부터).
    /// 판이 끝나면(사망 · 클리어) 저장을 지운다 — 같은 판을 되살리지 못하게(<see cref="ShouldKeep"/>).
    /// </summary>
    public static class RunSaveRules
    {
        /// <summary>
        /// 저장 형식 번호. 칸을 바꾸면 올린다. <see cref="MinReadableVersion"/> ~ 이 번호 밖의 파일은 읽지 않는다(깨진 파일처럼 백업하고 새 판).
        /// 2(3-7): 성장 트리 노드 칸 <see cref="RunSaveData.metaNodes"/>.
        /// </summary>
        public const int FormatVersion = 2;

        /// <summary>읽을 수 있는 가장 오래된 형식. 1은 트리 노드 칸만 없고(빈 목록) 나머지가 같다.</summary>
        public const int MinReadableVersion = 1;

        public static bool IsReadable(int version) => version >= MinReadableVersion && version <= FormatVersion;

        /// <summary>판이 진행 중일 때만 저장을 둔다. 끝났으면(사망 · 클리어) 지운다.</summary>
        public static bool ShouldKeep(RunSession session) => session != null && session.Stats.Outcome == RunOutcome.Playing;

        /// <summary>지금 판 상태를 저장 모양으로. 목록은 정렬해 같은 상태 = 같은 글자가 되게 한다.</summary>
        public static RunSaveData Capture(RunSession session, float elapsedSeconds)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunStats stats = session.Stats;
            var data = new RunSaveData
            {
                version = FormatVersion,
                seed = session.Seed.ToCode(),
                dungeonCount = session.World.DungeonCount,
                lastDungeonOpensNextRegion = session.LastDungeonOpensNextRegion,
                elapsedSeconds = Math.Max(0f, elapsedSeconds),
                dungeonsCleared = session.World.ClearedCount,
                encountersCleared = session.World.ClearedEncounters().ToArray(),
                returnDungeon = session.ReturnDungeon,
                hasWorldSpot = session.HasWorldSpot,
                worldX = session.WorldX,
                worldY = session.WorldY,
                worldZ = session.WorldZ,
                worldYaw = session.WorldYaw,
                currentDungeon = session.CurrentDungeon,
                playerHealth = session.PlayerHealth,
                playerAmmo = session.PlayerAmmo,
                unlocks = Names(stats.Upgrades.Unlocked()),
                metaNodes = new List<string>(session.MetaNodes).ToArray(),
                beast = stats.Beast,
                human = stats.Human,
                battleRooms = stats.BattleRooms,
                statsDungeonsCleared = stats.DungeonsCleared,
                roomsCleared = stats.RoomsCleared,
                kills = stats.Kills,
            };
            var taken = new string[stats.RewardsTaken.Count];
            for (int i = 0; i < taken.Length; i++) taken[i] = stats.RewardsTaken[i].Id;
            data.rewardsTaken = taken;

            DungeonVisit visit = session.Visit;
            if (session.InDungeon && visit != null)
            {
                data.battleRoomsCounted = visit.BattleRoomsCounted;
                data.currentRoom = visit.CurrentRoom;
                data.clearedRooms = Sorted(visit.ClearedRooms);
                data.visitedRooms = Sorted(visit.VisitedRooms);
                data.rewardRoomsTaken = Sorted(visit.RewardsTakenRooms);
                data.entryUnlocks = Names(visit.EntryUnlocks);
            }
            return data;
        }

        /// <summary>
        /// 저장 모양에서 판 상태를 되살린다. 형식 번호 · 시드 · 월드 데이터(던전 수)가 맞지 않거나 값이 어긋나면 false와 까닭.
        /// 고른 보상은 표(<paramref name="rewardTable"/>)에서 id로 찾아 다시 반영한다 — 성향 · 강화 합계가 같아진다.
        /// </summary>
        /// <param name="metaTree">성장 트리(3-7). 저장에 트리 노드가 있으면 여기서 id로 찾아 다시 건다(없거나 모르는 id면 읽지 않음).</param>
        public static bool TryRestore(RunSaveData data, IReadOnlyList<RewardDefinition> rewardTable, int dungeonCount, bool lastDungeonOpensNextRegion,
                                      out RunSession session, out float elapsedSeconds, out string error, MetaTree metaTree = null)
        {
            session = null;
            elapsedSeconds = 0f;
            try
            {
                error = Check(data, rewardTable, dungeonCount, lastDungeonOpensNextRegion);
                if (error != null) return false;
                session = Build(data, rewardTable, metaTree, out error);
                if (session == null) return false;
                elapsedSeconds = data.elapsedSeconds;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                session = null;
                error = $"값이 어긋남: {exception.Message}";
                return false;
            }
        }

        static string Check(RunSaveData data, IReadOnlyList<RewardDefinition> rewardTable, int dungeonCount, bool lastOpens)
        {
            if (data == null) return "비어 있음";
            if (rewardTable == null) throw new ArgumentNullException(nameof(rewardTable));
            if (!IsReadable(data.version)) return $"형식 번호 {data.version} (읽는 번호 {MinReadableVersion} ~ {FormatVersion})";
            if (!RunSeed.TryParse(data.seed, out _)) return $"시드 '{data.seed}'를 읽지 못함";
            if (data.dungeonCount != dungeonCount || data.lastDungeonOpensNextRegion != lastOpens)
                return $"월드가 바뀜(저장 던전 {data.dungeonCount}개, 지금 {dungeonCount}개)";
            if (float.IsNaN(data.elapsedSeconds) || float.IsInfinity(data.elapsedSeconds) || data.elapsedSeconds < 0f) return "판 시간이 어긋남";
            if (data.dungeonsCleared < 0 || data.dungeonsCleared > dungeonCount) return "깬 던전 수가 어긋남";
            if (data.returnDungeon < -1 || data.returnDungeon >= dungeonCount) return "돌아올 입구가 어긋남";
            if (data.currentDungeon != RunSession.InWorld && data.currentDungeon != data.dungeonsCleared) return "들어가 있는 던전이 다음 던전이 아님";
            if (data.playerHealth < 0 && data.playerHealth != RunSession.FullHealth) return "체력이 어긋남";
            if (data.playerAmmo < 0 && data.playerAmmo != RunSession.FullAmmo) return "탄이 어긋남";
            if (data.encountersCleared == null || data.rewardsTaken == null || data.unlocks == null) return "목록이 빠짐";
            foreach (int site in data.encountersCleared) if (site < 0) return "길 위 무리 번호가 어긋남";
            if (data.currentDungeon != RunSession.InWorld)
            {
                if (data.clearedRooms == null || data.visitedRooms == null || data.rewardRoomsTaken == null || data.entryUnlocks == null) return "던전 목록이 빠짐";
                if (data.currentRoom >= 0 && Array.IndexOf(data.clearedRooms, data.currentRoom) < 0) return "선 방이 깬 방이 아님";
                foreach (int id in data.clearedRooms) if (id < 0) return "방 번호가 어긋남";
                foreach (int id in data.visitedRooms) if (id < 0) return "방 번호가 어긋남";
                foreach (int id in data.rewardRoomsTaken) if (id < 0) return "방 번호가 어긋남";
                if (data.hasWorldSpot) return "던전 안인데 월드 자리가 있음";
            }
            return null;
        }

        static RunSession Build(RunSaveData data, IReadOnlyList<RewardDefinition> rewardTable, MetaTree metaTree, out string error)
        {
            error = null;
            RunSeed.TryParse(data.seed, out RunSeed seed);
            var session = new RunSession(seed, data.dungeonCount, data.lastDungeonOpensNextRegion);
            for (int i = 0; i < data.dungeonsCleared; i++) session.World.Clear(i);
            foreach (int site in data.encountersCleared) session.World.ClearEncounter(site);

            // 성장 트리 노드(판을 시작할 때 건 것 그대로 — 그 뒤에 산 노드는 다음 재생부터)
            foreach (string id in data.metaNodes ?? Array.Empty<string>())
            {
                if (metaTree == null || !metaTree.TryGet(id, out MetaNode node))
                {
                    error = $"성장 트리 노드 '{id}'을 모름";
                    return null;
                }
                session.ApplyMeta(node);
            }

            var byId = new Dictionary<string, RewardDefinition>();
            foreach (RewardDefinition reward in rewardTable) byId[reward.Id] = reward;
            foreach (string id in data.rewardsTaken)
            {
                if (id == null || !byId.TryGetValue(id, out RewardDefinition reward))
                {
                    error = $"보상 '{id}'이 표에 없음";
                    return null;
                }
                session.Stats.TakeReward(reward);
            }
            if (session.Stats.Beast != data.beast || session.Stats.Human != data.human)
            {
                error = $"성향이 보상과 맞지 않음(저장 Beast {data.beast} · Human {data.human}, 다시 계산 {session.Stats.Beast} · {session.Stats.Human})";
                return null;
            }
            if (!TryParseUnlocks(data.unlocks, out List<RewardUnlock> unlocks, out error)) return null;
            // 가진 도구는 저장한 그대로(디버그로 주고 뺀 것까지): 보상으로 얻은 것도 목록에 없으면 뺀다
            for (int i = 1; i < RewardUnlocks.Count; i++) session.Stats.Upgrades.SetUnlocked((RewardUnlock)i, unlocks.Contains((RewardUnlock)i));
            session.Stats.RestoreCounts(data.battleRooms, data.statsDungeonsCleared, data.roomsCleared, data.kills);

            session.ReturnDungeon = data.returnDungeon;
            session.PlayerHealth = data.playerHealth;
            session.PlayerAmmo = data.playerAmmo;
            if (data.currentDungeon != RunSession.InWorld)
            {
                if (!TryParseUnlocks(data.entryUnlocks, out List<RewardUnlock> entry, out error)) return null;
                session.CurrentDungeon = data.currentDungeon;
                var visit = new DungeonVisit(data.currentDungeon, entry) { BattleRoomsCounted = data.battleRoomsCounted };
                visit.Load(data.currentRoom, data.clearedRooms, data.visitedRooms, data.rewardRoomsTaken);
                session.Visit = visit;
            }
            else if (data.hasWorldSpot)
            {
                session.SetWorldSpot(data.worldX, data.worldY, data.worldZ, data.worldYaw);
            }
            return session;
        }

        static bool TryParseUnlocks(string[] names, out List<RewardUnlock> unlocks, out string error)
        {
            unlocks = new List<RewardUnlock>();
            error = null;
            foreach (string name in names)
            {
                if (!Enum.TryParse(name, false, out RewardUnlock unlock) || !Enum.IsDefined(typeof(RewardUnlock), unlock) || unlock == RewardUnlock.None)
                {
                    error = $"도구 '{name}'을 모름";
                    return false;
                }
                if (!unlocks.Contains(unlock)) unlocks.Add(unlock);
            }
            return true;
        }

        static string[] Names(IEnumerable<RewardUnlock> unlocks)
        {
            var list = new List<RewardUnlock>(unlocks);
            list.Sort();
            var names = new string[list.Count];
            for (int i = 0; i < names.Length; i++) names[i] = list[i].ToString();
            return names;
        }

        static int[] Sorted(IEnumerable<int> values)
        {
            var list = new List<int>(values);
            list.Sort();
            return list.ToArray();
        }
    }
}
