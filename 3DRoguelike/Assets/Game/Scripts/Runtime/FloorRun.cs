using System;
using System.Collections;
using System.Collections.Generic;
using Game.Rules;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// 층(던전) 하나를 진행한다. 시드로 배치와 방마다 적 자리를 정해 세우고, 플레이어가 깨지 않은 방에 들어서면 문을 잠그고 적을 내보낸다.
    /// 그 방의 적을 모두 잡으면 방을 깬 것이 되어 문이 열린다.
    /// 월드에서 들어왔으면(<see cref="CurrentRun.Session"/>) 판 기록을 이어받고, 보스 방을 깨면 월드로 돌아간다(그 뒤 관문 열림) — 다음 지역이 없는 마지막 던전이면 지역 클리어.
    /// 층 씬을 바로 Play하면 던전 하나짜리 판으로, 보스 방을 깨거나 죽으면 결과 화면.
    /// </summary>
    [DefaultExecutionOrder(-100)] // 넘겨받은 체력을 HUD.Start보다 먼저 맞춘다
    public sealed class FloorRun : MonoBehaviour, IRunRestart
    {
        /// <summary>"같은 층으로 다시"를 고르면 씬을 다시 불러오는 동안 시드를 여기에 맡겨 둔다.</summary>
        private static RunSeed? pendingSeed;

        [SerializeField] private FloorTuning tuning;
        [SerializeField] private PlayerVitals player;
        [Tooltip("적 종 · 프리팹 · 던전 순서별 비중(단계 = 던전 순서)")]
        [SerializeField] private EnemyRoster roster;
        [Tooltip("층을 세운 뒤 적이 다닐 길을 굽는다")]
        [SerializeField] private NavMeshSurface navMesh;
        [SerializeField] private RunResultScreen resultScreen;
        [SerializeField] private RewardTable rewardTable;
        [SerializeField] private RewardChoiceScreen rewardScreen;
        [Tooltip("보상물을 줍는 거리(m)")]
        [Min(0.1f)] [SerializeField] private float pickupRadius = 1.4f;
        [Tooltip("비워 두면 플레이마다 새 시드. 'XXXX-XXXX' 코드를 넣으면 그 층을 다시 만든다")]
        [SerializeField] private string fixedSeedCode = "";
        [Tooltip("죽은 뒤 결과 화면까지(초, 쓰러지는 동작을 보여 줄 시간)")]
        [Min(0f)] [SerializeField] private float resultDelayAfterDeath = 1.5f;
        [Tooltip("보스 방을 깬 뒤 결과 화면까지(초)")]
        [Min(0f)] [SerializeField] private float resultDelayAfterClear = 1f;
        [Tooltip("월드에서 들어왔을 때: 마지막 방을 깬 뒤 월드로 돌아가기까지(초)")]
        [Min(0f)] [SerializeField] private float returnDelayAfterClear = 2f;
        [Tooltip("던전 이름을 읽는 곳(월드 판일 때)")]
        [SerializeField] private WorldTuning world;
        [Tooltip("화면 위 가운데 안내")]
        [SerializeField] private TMP_Text objective;

        [Header("재질")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material startFloorMaterial;
        [SerializeField] private Material bossFloorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material lockedDoorMaterial;
        [SerializeField] private Material treasureFloorMaterial;
        [SerializeField] private Material rewardMaterial;
        [SerializeField] private Material rareRewardMaterial;

        [Header("테마 (던전 안 모양)")]
        [Tooltip("테마가 조명 색 · 세기를 바꿀 해")]
        [SerializeField] private Light sun;
        [Tooltip("층 씬만 Play할 때 쓸 테마(비면 회색). 월드 판은 WorldTuning.dungeons의 interiorTheme")]
        [SerializeField] private FloorTheme previewTheme;

        private readonly List<Health> roomEnemies = new List<Health>();
        private GameObject[] doorBarriers;
        private Transform playerTransform;
        private readonly List<(int roomId, GameObject pickup)> pickups = new List<(int, GameObject)>();
        /// <summary>월드에서 들어왔으면 그 판. 층 씬만 Play했으면 null.</summary>
        private RunSession session;
        private int dungeon;

        public RunSeed Seed { get; private set; }
        public FloorLayout Layout { get; private set; }
        public FloorProgress Progress { get; private set; }
        public FloorEncounters Encounters { get; private set; }
        public FloorRewards Rewards { get; private set; }
        /// <summary>보물방 Id. 없으면 -1.</summary>
        public int TreasureRoom { get; private set; } = -1;
        /// <summary>지금 놓여 있는 보상물 수.</summary>
        public int PickupCount => pickups.Count;
        public RunStats Stats { get; private set; }
        /// <summary>이 던전의 안 모양 테마. 없으면(숲) null — 회색 층.</summary>
        public FloorTheme Theme { get; private set; }
        /// <summary>테마 장식 자리. 테마가 없으면 null.</summary>
        public FloorDressing Dressing { get; private set; }
        /// <summary>잠긴 방에 아직 살아 있는 적 수.</summary>
        public int AliveEnemies => roomEnemies.Count;
        public EnemyRoster Roster => roster;

        /// <summary>판이 끝난 순간(죽음 · 층 클리어). 결과 화면은 잠시 뒤에 뜬다.</summary>
        public event Action<RunStats> RunEnded;

        /// <summary>씬을 다시 불러와 판을 새로 시작한다. sameSeed면 지금 층을 그대로 다시 만든다.</summary>
        public void Restart(bool sameSeed)
        {
            if (session != null)
            {
                CurrentRun.RestartFromWorld(sameSeed); // 죽으면 월드 길 시작부터
                return;
            }
            pendingSeed = sameSeed ? Seed : (RunSeed?)null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        /// <summary>판 시작부터 흐른 시간. 월드 판은 씬을 옮겨도 이어진다.</summary>
        private float Elapsed => session != null ? CurrentRun.Elapsed : Time.timeSinceLevelLoad;

        private void Awake()
        {
            Time.timeScale = 1f;
            playerTransform = player.transform;
            session = CurrentRun.Session != null && CurrentRun.Session.InDungeon ? CurrentRun.Session : null;
            dungeon = session != null ? session.CurrentDungeon : 0;
            Seed = session != null ? session.Seed : ChooseSeed();
            // 던전마다 다른 층: 난수의 층 번호 = 길 위 던전 순서
            var random = new RunRandom(Seed);
            Layout = FloorGenerator.Generate(tuning.ToSettings(), random.Create(RandomPurpose.Map, dungeon));
            TreasureRoom = SpecialRoomPlanner.AssignTreasure(Layout, random.Create(RandomPurpose.Decor, dungeon));
            Encounters = EncounterPlanner.Plan(Layout, tuning.ToEncounterSettings(), roster.ToMix(), dungeon, random.Create(RandomPurpose.Enemy, dungeon));
            Rewards = RewardPlanner.Plan(Layout, tuning.ToRewardSettings(), rewardTable.ToDefinitions(), random.Create(RandomPurpose.Reward, dungeon));
            Progress = new FloorProgress(Layout);
            int battleRooms = 0;
            foreach (FloorRoom room in Layout.Rooms) if (room.Kind == RoomKind.Battle || room.Kind == RoomKind.Boss) battleRooms++;
            if (session != null)
            {
                Stats = session.Stats;
                Stats.AddBattleRooms(battleRooms);
            }
            else Stats = new RunStats(Seed, battleRooms);
            player.GetComponent<PlayerUpgrades>().Use(Stats.Upgrades);
            Progress.RoomLocked += OnRoomLocked;
            Progress.RoomCleared += OnRoomCleared;

            Theme = session != null ? (world != null && dungeon < world.dungeons.Length ? world.dungeons[dungeon].interiorTheme : null) : previewTheme;
            var materials = new FloorGeometry.Materials
            {
                floor = floorMaterial,
                startFloor = startFloorMaterial,
                bossFloor = bossFloorMaterial,
                wall = wallMaterial,
                lockedDoor = lockedDoorMaterial,
                treasureFloor = treasureFloorMaterial,
            };
            if (Theme != null) UseTheme(ref materials);
            doorBarriers = FloorGeometry.Build(Layout, tuning, materials, transform);
            if (Theme != null)
            {
                // 장식은 dressing 난수 따로(맵 · 적 · 보상 그대로), NavMesh를 굽기 전에 세운다
                Dressing = RoomDressingPlanner.Plan(Layout, Encounters, Theme.ToDressingSettings(tuning), random.Create(RandomPurpose.Dressing, dungeon));
                FloorGeometry.BuildDressing(Dressing, Theme, transform);
                Theme.ApplyAtmosphere(sun);
            }
            navMesh.BuildNavMesh();
            if (TreasureRoom >= 0) SpawnPickup(TreasureRoom);
            Debug.Log($"[FloorRun] {DungeonName} · 시드 {Seed.ToCode()} · 방 {Layout.Rooms.Count}개 · 보스 방 {Layout.BossRoom.Id} · 보물방 {TreasureRoom}\n{Layout.ToAscii()}");
        }

        private void Start()
        {
            if (objective != null) objective.text = $"{DungeonName}  ·  마지막 방(빨간 바닥)을 깨면 {ClearGoal}";
            if (session == null) return;
            // PlayerVitals.Awake가 체력을 가득으로 만든 뒤, HUD.Start보다 먼저(실행 순서 -100)
            if (session.PlayerHealth != RunSession.FullHealth) player.Health.SetCurrent(session.PlayerHealth);
            player.GetComponent<PlayerForm>().UpdateForm(Stats.Beast, Stats.Human);
        }

        /// <summary>보스 방을 깨면: 밖으로(다음 지역이 열림) · "{지역} 클리어"(다음 지역이 없는 마지막 던전) · 끝(층 씬만).</summary>
        private string ClearGoal
        {
            get
            {
                if (session == null) return "끝";
                bool final = world != null && dungeon == world.dungeons.Length - 1 && !session.LastDungeonOpensNextRegion;
                return final ? $"{world.ClearedRegionName(dungeon)} 클리어" : "밖으로";
            }
        }

        private string DungeonName => session != null && world != null && dungeon < world.dungeons.Length
            ? world.dungeons[dungeon].displayName
            : "시험 던전";

        private void OnEnable()
        {
            player.Health.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            player.Health.Died -= OnPlayerDied;
        }

        private void Update()
        {
            if (Stats.Outcome != RunOutcome.Playing) return;

            Vector3 position = playerTransform.position;
            SpinPickups();
            if (!rewardScreen.IsShown) TryPickUp(position);
            if (!FloorGeometry.TryGetCell(Layout, tuning, position, out int x, out int y)) return;

            int roomId = Layout.RoomIdAt(x, y);
            if (roomId < 0 || roomId == Progress.CurrentRoom) return;
            if (!FloorGeometry.IsWellInside(Layout, tuning, Layout.Rooms[roomId], position, tuning.enterMargin)) return;

            Progress.Enter(roomId);
        }

        private RunSeed ChooseSeed()
        {
            if (pendingSeed.HasValue)
            {
                RunSeed again = pendingSeed.Value;
                pendingSeed = null;
                return again;
            }
            if (!string.IsNullOrWhiteSpace(fixedSeedCode))
            {
                if (RunSeed.TryParse(fixedSeedCode, out RunSeed seed)) return seed;
                Debug.LogWarning($"[FloorRun] 시드 코드 '{fixedSeedCode}'를 읽지 못해 새 시드를 씁니다.");
            }
            return new RunSeed(unchecked((uint)Guid.NewGuid().GetHashCode()));
        }

        /// <summary>테마가 정한 재질로 바꾼다(빈 칸은 회색 기본). 잠긴 문은 어느 테마든 빨간 벽 그대로.</summary>
        private void UseTheme(ref FloorGeometry.Materials materials)
        {
            if (Theme.floor != null) materials.floor = Theme.floor;
            if (Theme.startFloor != null) materials.startFloor = Theme.startFloor;
            if (Theme.bossFloor != null) materials.bossFloor = Theme.bossFloor;
            if (Theme.treasureFloor != null) materials.treasureFloor = Theme.treasureFloor;
            if (Theme.wall != null) materials.wall = Theme.wall;
            materials.floorTile = Theme.floorTile;
            materials.wallTile = Theme.wallTile;
        }


        private void OnRoomLocked(int roomId)
        {
            UpdateDoors();

            IReadOnlyList<SpawnPoint> spawns = Encounters.SpawnsFor(roomId);
            for (int i = 0; i < spawns.Count; i++)
            {
                var position = new Vector3(spawns[i].X, 0f, spawns[i].Z);
                Vector3 toPlayer = playerTransform.position - position;
                toPlayer.y = 0f;
                Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

                Enemy enemy = roster.Spawn(spawns[i].Kind, position, facing, transform);
                if (enemy == null) continue;
                enemy.name = $"{enemy.name}_Room{roomId}_{i}";
                Health health = enemy.Health;
                health.Died += () => OnRoomEnemyDied(health);
                roomEnemies.Add(health);
            }

            if (roomEnemies.Count == 0) Progress.ClearLockedRoom();
        }

        private void OnRoomEnemyDied(Health enemy)
        {
            Stats.RecordKill();
            roomEnemies.Remove(enemy);
            if (roomEnemies.Count == 0) Progress.ClearLockedRoom();
        }

        private void OnRoomCleared(int roomId)
        {
            Stats.RecordRoomCleared();
            // 재생 강화: 방을 깰 때 최대 체력의 일정 비율 회복
            int heal = Mathf.RoundToInt(player.Health.Max * Stats.Upgrades.Bonus(UpgradeStat.HealOnRoomClear));
            if (heal > 0 && !player.Health.IsDead) player.Health.Heal(heal);
            UpdateDoors();
            if (roomId != Layout.BossRoom.Id) SpawnPickup(roomId);
            if (roomId != Layout.BossRoom.Id || Stats.Outcome != RunOutcome.Playing) return;

            if (session == null)
            {
                if (Stats.ClearFloor(Elapsed))
                {
                    player.DisableControls();
                    EndRun(resultDelayAfterClear);
                }
                return;
            }

            if (session.FinishDungeon(player.Health.Current, Elapsed))
            {
                player.DisableControls();
                EndRun(resultDelayAfterClear); // 다음 지역이 없는 마지막 던전: 지역 클리어
                return;
            }
            StartCoroutine(ReturnToWorld());
        }

        /// <summary>던전을 깼다 — 잠깐 알리고 월드의 그 입구 앞으로 돌아간다(뒤 관문이 열려 있다).</summary>
        private IEnumerator ReturnToWorld()
        {
            if (objective != null) objective.text = $"{DungeonName} 클리어!  잠시 뒤 밖으로 나갑니다";
            Debug.Log($"[FloorRun] {DungeonName} 클리어 → 월드로");
            yield return new WaitForSecondsRealtime(returnDelayAfterClear);
            while (rewardScreen.IsShown) yield return null; // 보상을 고르는 중이면 기다린다
            SceneManager.LoadScene(CurrentRun.WorldScene);
        }

        private void OnPlayerDied()
        {
            if (Stats.Die(player.Health.LastDamageSource, Elapsed)) EndRun(resultDelayAfterDeath);
        }

        private void EndRun(float delay)
        {
            Debug.Log($"[FloorRun] 판 끝: {Stats.Outcome} · 시드 {Seed.ToCode()} · 방 {Stats.RoomsCleared}/{Stats.BattleRooms} · 처치 {Stats.Kills} · {RunStats.FormatTime(Stats.ElapsedSeconds)}" +
                      (Stats.DeathCause != null ? $" · 원인 {Stats.DeathCause}" : ""));
            RunEnded?.Invoke(Stats);
            StartCoroutine(ShowResultAfter(delay));
        }

        private IEnumerator ShowResultAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            // 지역 클리어 제목 = 이 던전이 있는 지역 이름(예: "폐허 도시 클리어")
            resultScreen.Show(Stats, this, session != null && world != null ? world.ClearedRegionName(dungeon) : null);
        }

        /// <summary>그 방에 정해 둔 보상이 있으면 방 가운데에 보상물을 놓는다(희귀는 노란색 · 조금 크게).</summary>
        private void SpawnPickup(int roomId)
        {
            IReadOnlyList<RewardDefinition> offers = Rewards.OffersFor(roomId);
            if (offers.Count == 0) return;
            bool rare = offers[0].Rarity == RewardRarity.Rare;
            var pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(pickup.GetComponent<Collider>());
            pickup.name = $"RewardPickup_Room{roomId}";
            pickup.transform.SetParent(transform, false);
            pickup.transform.position = FloorGeometry.RoomCenter(Layout, tuning, Layout.Rooms[roomId]) + Vector3.up * 0.8f;
            pickup.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
            pickup.transform.localScale = Vector3.one * (rare ? 0.6f : 0.45f);
            pickup.GetComponent<Renderer>().sharedMaterial = rare ? rareRewardMaterial : rewardMaterial;
            pickups.Add((roomId, pickup));
        }

        private void TryPickUp(Vector3 position)
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                (int roomId, GameObject pickup) = pickups[i];
                Vector3 offset = pickup.transform.position - position;
                offset.y = 0f;
                if (offset.magnitude > pickupRadius) continue;
                pickups.RemoveAt(i);
                Destroy(pickup);
                OpenRewardChoice(roomId);
                return;
            }
        }

        /// <summary>그 방의 보상 선택 화면을 연다(보상물을 주웠을 때 · 확인용).</summary>
        public void OpenRewardChoice(int roomId)
        {
            IReadOnlyList<RewardDefinition> offers = Rewards.OffersFor(roomId);
            if (offers.Count == 0) return;
            rewardScreen.Show(offers, Stats, offers[0].Rarity == RewardRarity.Rare, reward =>
            {
                Stats.TakeReward(reward);
                Debug.Log($"[FloorRun] 보상 '{reward.Name}' → Beast {Stats.Beast} · Human {Stats.Human}");
                player.GetComponent<PlayerForm>().UpdateForm(Stats.Beast, Stats.Human);
            });
        }

        private void SpinPickups()
        {
            foreach ((int _, GameObject pickup) in pickups) pickup.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        private void UpdateDoors()
        {
            for (int i = 0; i < doorBarriers.Length; i++)
            {
                doorBarriers[i].SetActive(Progress.IsDoorLocked(Layout.Doors[i]));
            }
        }
    }
}
