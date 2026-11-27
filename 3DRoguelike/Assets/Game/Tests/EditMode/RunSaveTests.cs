using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// 3-6 판 중간 저장 · 이어 하기: 담는 것 · 되살리는 것(규칙 RunSaveRules), 왕복(저장 → 되살림 → 저장 = 같은 글자),
    /// 깬 방 · 보상 · 체력 · 탄 · 도구 · 성향, 싸우는 중은 안 적음, 판 끝 = 지움, 깨진 파일 · 형식 번호, 파일(임시 폴더).
    /// </summary>
    public class RunSaveTests
    {
        static readonly RunSeed Seed = new RunSeed(0xC0FFEEu);

        static RewardDefinition Pistol() =>
            new RewardDefinition("weapon_pistol", "권총", "", Tendency.Human, 0, RewardRarity.Common, unlock: RewardUnlock.Pistol, guaranteeFromDungeon: 0);

        static List<RewardDefinition> Table()
        {
            var list = new List<RewardDefinition>();
            for (int i = 0; i < 6; i++)
                list.Add(new RewardDefinition($"c{i}", $"common {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 1, RewardRarity.Common,
                                              new[] { new StatEffect(UpgradeStat.MeleeDamage, 20f) }));
            for (int i = 0; i < 4; i++) list.Add(new RewardDefinition($"r{i}", $"rare {i}", "", i % 2 == 0 ? Tendency.Beast : Tendency.Human, 2, RewardRarity.Rare));
            list.Add(Pistol());
            return list;
        }

        static string Json(RunSaveData data) => JsonUtility.ToJson(data);

        static RunSession Restore(RunSaveData data, int dungeons = 2, bool opens = false)
        {
            // 글자로 한 번 옮긴 뒤 되살린다(파일과 같은 길)
            RunSaveData read = JsonUtility.FromJson<RunSaveData>(Json(data));
            Assert.That(RunSaveRules.TryRestore(read, Table(), dungeons, opens, out RunSession session, out _, out string error), Is.True, error);
            return session;
        }

        /// <summary>던전 1에 들어가 시작 옆 방 하나를 깨고, 그 방의 보상(권총)을 받은 판.</summary>
        static (RunSession session, FloorProgress progress, int room) SessionInDungeon()
        {
            var session = new RunSession(Seed, 2);
            session.World.ClearEncounter(0);
            session.World.ClearEncounter(2);
            Assert.That(session.EnterDungeon(0, 80), Is.True);
            FloorLayout layout = FloorGeneratorTests.Generate(Seed.Value, 0);
            var progress = new FloorProgress(layout);
            int room = layout.Doors.Select(d => d.RoomA == layout.StartRoom.Id ? d.RoomB : d.RoomB == layout.StartRoom.Id ? d.RoomA : -1)
                .First(id => id >= 0 && layout.Rooms[id].Kind == RoomKind.Battle); // 시작 옆 전투 방
            progress.Enter(room);
            progress.ClearLockedRoom();
            session.Stats.AddBattleRooms(5);
            session.Visit.BattleRoomsCounted = true;
            session.Stats.RecordKill();
            session.Stats.RecordKill();
            session.Stats.RecordRoomCleared();
            session.Stats.TakeReward(Pistol());
            session.Visit.MarkRewardTaken(room);
            Assert.That(session.Visit.Record(progress), Is.True);
            session.UpdatePlayer(64, 7);
            return (session, progress, room);
        }

        [Test]
        public void Capture_NewRun_HoldsSeedAndDefaults()
        {
            var session = new RunSession(Seed, 2, lastDungeonOpensNextRegion: true);
            RunSaveData data = RunSaveRules.Capture(session, 12.5f);

            Assert.That(data.version, Is.EqualTo(RunSaveRules.FormatVersion));
            Assert.That(data.seed, Is.EqualTo(Seed.ToCode()));
            Assert.That(data.dungeonCount, Is.EqualTo(2));
            Assert.That(data.lastDungeonOpensNextRegion, Is.True);
            Assert.That(data.elapsedSeconds, Is.EqualTo(12.5f));
            Assert.That(data.currentDungeon, Is.EqualTo(RunSession.InWorld));
            Assert.That(data.playerHealth, Is.EqualTo(RunSession.FullHealth));
            Assert.That(data.playerAmmo, Is.EqualTo(RunSession.FullAmmo));
            Assert.That(data.rewardsTaken, Is.Empty);
            Assert.That(data.unlocks, Is.Empty, "판은 맨몸으로 시작");
        }

        [Test]
        public void RoundTrip_World_SameTextAndState()
        {
            var session = new RunSession(Seed, 2);
            Assert.That(session.EnterDungeon(0, 90), Is.True);
            session.Stats.TakeReward(Table()[0]);
            session.Stats.TakeReward(Table()[0]);
            session.Stats.TakeReward(Table()[1]);
            session.Stats.TakeReward(Pistol());
            Assert.That(session.FinishDungeon(70, 100f), Is.False);
            session.World.ClearEncounter(3);
            session.UpdatePlayer(55, 4);
            session.SetWorldSpot(10.5f, 2f, 300.25f, 90f);

            RunSaveData first = RunSaveRules.Capture(session, 321f);
            RunSession back = Restore(first);
            RunSaveData second = RunSaveRules.Capture(back, 321f);

            Assert.That(Json(second), Is.EqualTo(Json(first)), "저장 → 되살림 → 저장 = 같은 글자");
            Assert.That(back.Seed, Is.EqualTo(Seed));
            Assert.That(back.World.ClearedCount, Is.EqualTo(1));
            Assert.That(back.World.IsGateOpen(0), Is.True);
            Assert.That(back.World.IsEncounterCleared(3), Is.True);
            Assert.That(back.World.IsEncounterCleared(0), Is.False);
            Assert.That(back.ReturnDungeon, Is.EqualTo(0));
            Assert.That(back.InDungeon, Is.False);
            Assert.That(back.PlayerHealth, Is.EqualTo(55));
            Assert.That(back.PlayerAmmo, Is.EqualTo(4));
            Assert.That(back.HasWorldSpot, Is.True);
            Assert.That(back.WorldZ, Is.EqualTo(300.25f));
            Assert.That(back.Stats.Beast, Is.EqualTo(2));
            Assert.That(back.Stats.Human, Is.EqualTo(1));
            Assert.That(back.Stats.Upgrades.Bonus(UpgradeStat.MeleeDamage), Is.EqualTo(0.6f).Within(1e-5f), "강화 합계를 다시 계산");
            Assert.That(back.Stats.Upgrades.Has(RewardUnlock.Pistol), Is.True, "권총 그대로");
            Assert.That(back.Stats.RewardsTaken.Select(r => r.Id), Is.EqualTo(new[] { "c0", "c0", "c1", "weapon_pistol" }));
            Assert.That(back.Stats.DungeonsCleared, Is.EqualTo(1));
        }

        [Test]
        public void RoundTrip_InDungeon_ClearedRoomsStayCleared()
        {
            var (session, progress, room) = SessionInDungeon();
            RunSaveData first = RunSaveRules.Capture(session, 50f);
            RunSession back = Restore(first);

            Assert.That(Json(RunSaveRules.Capture(back, 50f)), Is.EqualTo(Json(first)));
            Assert.That(back.InDungeon, Is.True);
            Assert.That(back.CurrentDungeon, Is.EqualTo(0));
            Assert.That(back.Visit.BattleRoomsCounted, Is.True, "깰 방 수를 두 번 더하지 않게");
            Assert.That(back.Visit.IsRewardTaken(room), Is.True);
            Assert.That(back.PlayerHealth, Is.EqualTo(64));
            Assert.That(back.PlayerAmmo, Is.EqualTo(7));
            Assert.That(back.Stats.Kills, Is.EqualTo(2));
            Assert.That(back.Stats.RoomsCleared, Is.EqualTo(1));
            Assert.That(back.Stats.BattleRooms, Is.EqualTo(5));
            Assert.That(back.World.IsEncounterCleared(2), Is.True);

            // 같은 시드로 층을 다시 만들고 저장한 진행을 덮는다
            var rebuilt = new FloorProgress(FloorGeneratorTests.Generate(Seed.Value, 0));
            back.Visit.ApplyTo(rebuilt);
            for (int id = 0; id < rebuilt.Layout.Rooms.Count; id++)
            {
                Assert.That(rebuilt.IsCleared(id), Is.EqualTo(progress.IsCleared(id)), $"방 {id} 깸");
                Assert.That(rebuilt.IsVisited(id), Is.EqualTo(progress.IsVisited(id)), $"방 {id} 가 봄");
            }
            Assert.That(rebuilt.CurrentRoom, Is.EqualTo(room));
            Assert.That(rebuilt.LockedRoom, Is.EqualTo(-1));
            Assert.That(rebuilt.Enter(room), Is.False, "깬 방은 다시 잠기지 않음");
        }

        [Test]
        public void Visit_EntryUnlocks_KeepRewardPlanAfterPistolTaken()
        {
            var (session, _, _) = SessionInDungeon();
            Assert.That(session.Stats.Upgrades.Has(RewardUnlock.Pistol), Is.True, "던전 안에서 권총을 얻음");
            RunSession back = Restore(RunSaveRules.Capture(session, 0f));
            Assert.That(back.Visit.EntryUnlocks, Is.Empty, "들어설 때는 맨몸");

            FloorLayout layout = FloorGeneratorTests.Generate(Seed.Value, 0);
            var run = new RunRandom(Seed);
            FloorRewards atEntry = RewardPlanner.Plan(layout, new RewardSettings(0.5f, 3), Table(), run.Create(RandomPurpose.Reward, 0), 0, new UpgradeSheet());
            FloorRewards resumed = RewardPlanner.Plan(layout, new RewardSettings(0.5f, 3), Table(), run.Create(RandomPurpose.Reward, 0), 0, back.Visit.EntrySheet());
            Assert.That(resumed.GuaranteedRoom, Is.EqualTo(atEntry.GuaranteedRoom));
            for (int id = 0; id < layout.Rooms.Count; id++)
                Assert.That(resumed.OffersFor(id).Select(r => r.Id), Is.EqualTo(atEntry.OffersFor(id).Select(r => r.Id)), $"방 {id}");
        }

        [Test]
        public void Visit_DoesNotRecordWhileARoomIsLocked()
        {
            var (session, progress, room) = SessionInDungeon();
            int next = progress.Layout.Doors.Select(d => d.RoomA == room ? d.RoomB : d.RoomB == room ? d.RoomA : -1)
                .First(id => id >= 0 && !progress.IsCleared(id));
            Assert.That(progress.Enter(next), Is.True, "싸우는 방에 들어감");

            Assert.That(session.Visit.Record(progress), Is.False);
            Assert.That(session.Visit.CurrentRoom, Is.EqualTo(room), "앞 저장 그대로 — 끄면 그 방 처음부터");
            Assert.That(session.Visit.ClearedRooms.Contains(next), Is.False);
        }

        [Test]
        public void Restore_DebugRemovedPistol_StaysRemoved()
        {
            var session = new RunSession(Seed, 2);
            session.Stats.TakeReward(Pistol());
            session.Stats.Upgrades.SetUnlocked(RewardUnlock.Pistol, false);
            RunSession back = Restore(RunSaveRules.Capture(session, 0f));
            Assert.That(back.Stats.Upgrades.Has(RewardUnlock.Pistol), Is.False);
            Assert.That(back.Stats.RewardsTaken.Count, Is.EqualTo(1));
        }

        [Test]
        public void ShouldKeep_OnlyWhilePlaying()
        {
            var session = new RunSession(Seed, 1);
            Assert.That(RunSaveRules.ShouldKeep(session), Is.True);
            Assert.That(RunSaveRules.ShouldKeep(null), Is.False);
            session.Stats.Die("곰의 앞발", 10f);
            Assert.That(RunSaveRules.ShouldKeep(session), Is.False, "죽으면 저장을 지운다");

            var cleared = new RunSession(Seed, 1);
            cleared.EnterDungeon(0, 50);
            Assert.That(cleared.FinishDungeon(50, 10f), Is.True);
            Assert.That(RunSaveRules.ShouldKeep(cleared), Is.False, "클리어해도 지운다");
        }

        [Test]
        public void Session_EnterAndFinish_ResetAmmoSpotAndVisit()
        {
            var session = new RunSession(Seed, 2);
            session.Stats.TakeReward(Pistol());
            session.UpdatePlayer(40, 3);
            session.SetWorldSpot(1f, 2f, 3f, 4f);
            Assert.That(session.EnterDungeon(0, 40), Is.True);
            Assert.That(session.PlayerAmmo, Is.EqualTo(RunSession.FullAmmo), "씬을 옮기면 탄창 가득(예전 그대로)");
            Assert.That(session.HasWorldSpot, Is.False);
            Assert.That(session.Visit.EntryUnlocks, Is.EqualTo(new[] { RewardUnlock.Pistol }));
            Assert.That(() => session.SetWorldSpot(0f, 0f, 0f, 0f), Throws.InvalidOperationException);
            session.FinishDungeon(30, 1f);
            Assert.That(session.Visit, Is.Null);
            Assert.That(session.PlayerHealth, Is.EqualTo(30));
        }

        [Test]
        public void Restore_RejectsBadSaves()
        {
            RunSaveData Good() => RunSaveRules.Capture(SessionInDungeon().session, 5f);
            void Rejected(Action<RunSaveData> breakIt, string why, int dungeons = 2)
            {
                RunSaveData data = JsonUtility.FromJson<RunSaveData>(Json(Good()));
                breakIt(data);
                Assert.That(RunSaveRules.TryRestore(data, Table(), dungeons, false, out RunSession session, out _, out string error), Is.False, why);
                Assert.That(session, Is.Null);
                Assert.That(error, Is.Not.Empty);
            }

            Assert.That(RunSaveRules.TryRestore(null, Table(), 2, false, out _, out _, out _), Is.False);
            Rejected(d => d.version = RunSaveRules.FormatVersion + 1, "형식 번호");
            Rejected(d => d.seed = "NOPE-NOPE", "시드");
            Rejected(d => { }, "던전 수가 바뀐 월드", dungeons: 3);
            Rejected(d => d.rewardsTaken = new[] { "no_such_reward" }, "표에 없는 보상");
            Rejected(d => d.beast = 99, "성향이 보상과 다름");
            Rejected(d => d.unlocks = new[] { "Laser" }, "모르는 도구");
            Rejected(d => d.currentRoom = 999, "선 방이 깬 방이 아님");
            Rejected(d => d.currentDungeon = 1, "다음 던전이 아닌 곳");
            Rejected(d => d.playerHealth = -5, "체력");
            Rejected(d => d.hasWorldSpot = true, "던전 안인데 월드 자리");
            Rejected(d => d.kills = -1, "판 기록 수");
        }

        [Test]
        public void FloorRestore_RejectsUnclearedCurrentRoomAndBadIds()
        {
            FloorLayout layout = FloorGeneratorTests.Generate(Seed.Value, 0);
            int other = layout.Rooms.First(r => r.Kind == RoomKind.Battle).Id;
            Assert.That(() => new FloorProgress(layout).Restore(new int[0], new int[0], other), Throws.ArgumentException);
            Assert.That(() => new FloorProgress(layout).Restore(new[] { 999 }, new int[0], layout.StartRoom.Id), Throws.InstanceOf<ArgumentOutOfRangeException>());
            var ok = new FloorProgress(layout);
            ok.Restore(new[] { other }, new[] { other }, other);
            Assert.That(ok.IsCleared(layout.StartRoom.Id), Is.True, "시작 방은 늘 깬 방");
            Assert.That(ok.CurrentRoom, Is.EqualTo(other));
        }

        [Test]
        public void Store_WriteReadDeleteAndBackUpBroken_InTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gorani_save_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new RunSaveStore(folder);
                Assert.That(store.TryRead(out _, out _), Is.EqualTo(RunSaveStore.ReadResult.None));

                RunSaveData data = RunSaveRules.Capture(SessionInDungeon().session, 9f);
                store.Write(data);
                store.Write(data); // 덮어쓰기(바꿔 끼우기)
                Assert.That(store.TryRead(out RunSaveData read, out string error), Is.EqualTo(RunSaveStore.ReadResult.Ok), error);
                Assert.That(Json(read), Is.EqualTo(Json(data)));
                Assert.That(File.ReadAllText(store.FilePath), Does.Contain("\"version\": " + RunSaveRules.FormatVersion));
                Assert.That(File.Exists(store.FilePath + ".tmp"), Is.False);

                Assert.That(store.Delete(), Is.True);
                Assert.That(store.Exists, Is.False);
                Assert.That(store.Delete(), Is.False);

                File.WriteAllText(store.FilePath, "{ not json");
                Assert.That(store.TryRead(out _, out _), Is.EqualTo(RunSaveStore.ReadResult.Broken));
                string backup = store.BackUpBroken(new DateTime(2026, 11, 27, 1, 2, 3, DateTimeKind.Utc));
                Assert.That(Path.GetFileName(backup), Is.EqualTo("run_save.broken-20261127-010203.json"));
                Assert.That(File.Exists(backup), Is.True, "깨진 파일은 지우지 않고 백업");
                Assert.That(store.Exists, Is.False, "그리고 새 판으로");

                File.WriteAllText(store.FilePath, "{\"version\": 999}");
                Assert.That(store.TryRead(out _, out string versionError), Is.EqualTo(RunSaveStore.ReadResult.Broken));
                Assert.That(versionError, Does.Contain("999"));
            }
            finally
            {
                if (Directory.Exists(folder) && folder.Contains("gorani_save_test_")) Directory.Delete(folder, recursive: true);
            }
        }
    }
}
