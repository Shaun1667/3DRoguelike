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
    public sealed class FloorRun : MonoBehaviour, IRunRestart, IRunSavePoint
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

        [Header("보스")]
        [Tooltip("층 씬만 Play할 때 보스 방에 나올 보스(비면 일반 적). 월드 판은 WorldTuning.dungeons의 boss")]
        [SerializeField] private BossTuning previewBoss;
        [Tooltip("화면 위 보스 체력 막대 · 이름")]
        [SerializeField] private BossHealthBar bossBar;
        [Tooltip("보스 등장 연출(Timeline + Cinemachine). 보스 수치에 intro가 있을 때만 재생한다")]
        [SerializeField] private BossIntroCinematic bossIntro;


        private readonly List<Health> roomEnemies = new List<Health>();
        private GameObject[] doorBarriers;
        private Transform playerTransform;
        private readonly List<(int roomId, GameObject pickup)> pickups = new List<(int, GameObject)>();
        /// <summary>월드에서 들어왔으면 그 판. 층 씬만 Play했으면 null.</summary>
        private RunSession session;
        private Vector3 bossArenaCenter;
        private Vector2 bossArenaHalf;
        /// <summary>보스 방 가운데에서 벽 안쪽 면까지(등장 연출 카메라가 벽 밖으로 나가지 않게).</summary>
        private Vector2 bossRoomHalf;
        /// <summary>플레이어가 보스 방에 들어선 자리(등장 연출에서 여기서부터 걸어 들어온다).</summary>
        private Vector3 bossIntroEntry;

        private int dungeon;
        /// <summary>들어가 있는 던전의 판 상태(저장할 층 진행). 층 씬만 Play했으면 null.</summary>
        private DungeonVisit visit;

        /// <summary>저장한 층 진행에서 이어 하는 중인가(깬 방을 되살렸다).</summary>
        public bool Resumed { get; private set; }

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
        /// <summary>이 던전의 안 모양 테마. 없으면 null — 회색 층.</summary>
        public FloorTheme Theme { get; private set; }
        /// <summary>테마 장식 자리. 테마가 없으면 null.</summary>
        public FloorDressing Dressing { get; private set; }
        /// <summary>잠긴 방에 아직 살아 있는 적 수.</summary>
        public int AliveEnemies => roomEnemies.Count;
        public EnemyRoster Roster => roster;
        /// <summary>이 던전 보스 방의 보스. 없으면(null) 보스 방도 일반 적.</summary>
        public BossTuning BossTuning { get; private set; }
        /// <summary>보스 방에 나온 보스. 아직 안 나왔거나 없으면 null(죽은 뒤에도 몸이 남아 있는 동안은 그대로).</summary>
        public Boss Boss { get; private set; }
        public BossHealthBar BossBar => bossBar;
        public BossIntroCinematic BossIntro => bossIntro;
        /// <summary>보스 등장 연출이 재생 중인지(조작 · 보스 행동 · 피해가 막혀 있다).</summary>
        public bool BossIntroPlaying => bossIntro != null && bossIntro.IsPlaying;


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
            if (session != null && world != null) MetaRun.Use(world.metaTree); // 판 밖 성장(3-7): 판이 끝나면 영구 재화를 더한다
            dungeon = session != null ? session.CurrentDungeon : 0;
            Seed = session != null ? session.Seed : ChooseSeed();
            // 던전마다 다른 층: 난수의 층 번호 = 길 위 던전 순서
            var random = new RunRandom(Seed);
            Layout = FloorGenerator.Generate(tuning.ToSettings(), random.Create(RandomPurpose.Map, dungeon));
            TreasureRoom = SpecialRoomPlanner.AssignTreasure(Layout, random.Create(RandomPurpose.Decor, dungeon));
            Encounters = EncounterPlanner.Plan(Layout, tuning.ToEncounterSettings(), roster.ToMix(), dungeon, random.Create(RandomPurpose.Enemy, dungeon));
            Progress = new FloorProgress(Layout);
            int battleRooms = 0;
            foreach (FloorRoom room in Layout.Rooms) if (room.Kind == RoomKind.Battle || room.Kind == RoomKind.Boss) battleRooms++;
            visit = session?.Visit;
            if (session != null)
            {
                Stats = session.Stats;
                // 이어 할 때는 이미 더했다(저장에 들어 있음)
                if (visit == null || !visit.BattleRoomsCounted) Stats.AddBattleRooms(battleRooms);
                if (visit != null) visit.BattleRoomsCounted = true;
            }
            else Stats = new RunStats(Seed, battleRooms);
            // 보상은 가진 도구를 알아야 해서(권총 보장 · 중복 없음) 판 기록 뒤에 정한다 — 난수는 따로라 순서가 바뀌어도 값은 같다
            Rewards = RewardPlanner.Plan(Layout, tuning.ToRewardSettings(), rewardTable.ToDefinitions(), random.Create(RandomPurpose.Reward, dungeon), dungeon,
                                         visit != null ? visit.EntrySheet(Stats.Upgrades) : Stats.Upgrades); // 들어설 때 가진 도구 + 트리가 연 보상 후보 기준 — 이어 해도 같은 보상
            ResumeProgress();
            player.GetComponent<PlayerUpgrades>().Use(Stats.Upgrades);
            Progress.RoomLocked += OnRoomLocked;
            Progress.RoomCleared += OnRoomCleared;

            Theme = session != null ? (world != null && dungeon < world.dungeons.Length ? world.dungeons[dungeon].interiorTheme : null) : previewTheme;
            BossTuning = session != null ? (world != null && dungeon < world.dungeons.Length ? world.dungeons[dungeon].boss : null) : previewBoss;
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
            if (TreasureRoom >= 0 && !RewardTaken(TreasureRoom)) SpawnPickup(TreasureRoom);
            if (Rewards.GuaranteedRoom == Layout.StartRoom.Id && !RewardTaken(Layout.StartRoom.Id)) SpawnPickup(Layout.StartRoom.Id); // 보스 방이 시작 바로 옆인 층: 권총은 시작 방에
            if (Resumed) PlaceResumedPickupsAndPlayer();
            if (Rewards.Guaranteed != null) Debug.Log($"[FloorRun] 보장 보상 '{Rewards.Guaranteed.Name}' → 방 {Rewards.GuaranteedRoom}");
            Debug.Log($"[FloorRun] {DungeonName} · 시드 {Seed.ToCode()} · 방 {Layout.Rooms.Count}개 · 보스 방 {Layout.BossRoom.Id} · 보물방 {TreasureRoom}\n{Layout.ToAscii()}");
        }

        private void Start()
        {
            if (objective != null) objective.text = $"{DungeonName}  ·  마지막 방(빨간 바닥)을 깨면 {ClearGoal}";
            if (session == null) return;
            // PlayerVitals.Awake가 체력을 가득으로 만든 뒤, HUD.Start보다 먼저(실행 순서 -100)
            if (session.PlayerHealth != RunSession.FullHealth) player.Health.SetCurrent(session.PlayerHealth);
            if (session.PlayerAmmo != RunSession.FullAmmo && player.Gun != null) player.Gun.SetAmmo(session.PlayerAmmo); // 이어 할 때만(던전에 들어오면 가득)
            player.GetComponent<PlayerForm>().UpdateForm(Stats.Beast, Stats.Human);
        }

        /// <summary>이어 하기(3-6): 저장해 둔 깬 방 · 가 본 방 · 선 방을 새로 만든 층에 덮는다. 층과 맞지 않으면 백업하고 층 처음부터.</summary>
        private void ResumeProgress()
        {
            if (visit == null || !visit.HasProgress) return;
            try
            {
                visit.ApplyTo(Progress);
                Resumed = true;
                Debug.Log($"[FloorRun] 이어 하기 · 방 {Progress.CurrentRoom} · 깬 방 {visit.ClearedRooms.Count} · 보상 받은 방 {visit.RewardsTakenRooms.Count}");
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[FloorRun] 저장한 층 진행이 이 층과 맞지 않아 층 처음부터 합니다: {exception.Message}");
                CurrentRun.ReportBrokenSave(exception.Message);
                Progress = new FloorProgress(Layout);
            }
        }

        private bool RewardTaken(int roomId) => visit != null && visit.IsRewardTaken(roomId);

        /// <summary>
        /// 이어 하기: 깬 방(시작 · 보스 방 빼고)에 아직 안 받은 보상물을 다시 놓고(방을 깬 때와 같은 자리),
        /// 플레이어를 저장한 방 가운데에서 조금 비켜 세운다(보상물을 바로 줍지 않게).
        /// </summary>
        private void PlaceResumedPickupsAndPlayer()
        {
            foreach (FloorRoom room in Layout.Rooms)
            {
                if (room.Id == Layout.StartRoom.Id || room.Id == Layout.BossRoom.Id || room.Id == TreasureRoom) continue;
                if (Progress.IsCleared(room.Id) && !RewardTaken(room.Id)) SpawnPickup(room.Id);
            }
            if (Progress.CurrentRoom == Layout.StartRoom.Id) return;
            FloorRoom current = Layout.Rooms[Progress.CurrentRoom];
            Vector3 center = FloorGeometry.RoomCenter(Layout, tuning, current);
            Vector3 spot = center + new Vector3(0f, 0f, -Mathf.Min(4f, current.Height * tuning.cellSize * 0.25f));
            if (UnityEngine.AI.NavMesh.SamplePosition(spot, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) spot = hit.position;
            var body = player.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            Vector3 look = center - spot;
            look.y = 0f;
            playerTransform.SetPositionAndRotation(spot + Vector3.up * 0.1f, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            if (body != null) body.enabled = true;
        }

        /// <summary>
        /// 저장 지점(3-6): 층 진행 · 체력 · 탄을 판 상태에 적는다. 문이 잠긴 동안(싸우는 중 · 보스 등장)은 false —
        /// 그때 끄면 앞 저장에서 이어 그 방을 처음부터(GDD 저장). 던전을 깨고 나가는 중이면 판 상태 그대로.
        /// </summary>
        public bool SyncForSave(RunSession run)
        {
            if (run == null || run != session || Stats.Outcome != RunOutcome.Playing || player.Health.IsDead) return false;
            if (!run.InDungeon) return true;
            if (Progress.LockedRoom >= 0 || BossIntroPlaying) return false;
            visit?.Record(Progress);
            PlayerGun gun = player.Gun;
            run.UpdatePlayer(player.Health.Current, gun == null || gun.Ammo >= gun.Magazine ? RunSession.FullAmmo : gun.Ammo);
            return true;
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
            CurrentRun.UseSavePoint(this);
        }

        private void OnDisable()
        {
            player.Health.Died -= OnPlayerDied;
            CurrentRun.ReleaseSavePoint(this);
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

            // 보스가 정해진 던전: 보스 방에는 보스 한 마리만(계획해 둔 일반 적 자리는 쓰지 않는다 — 계획은 그대로라 다른 방 · 장식이 바뀌지 않음)
            if (roomId == Layout.BossRoom.Id && BossTuning != null && BossTuning.prefab != null)
            {
                SpawnBoss(roomId);
                return;
            }

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

        /// <summary>보스 방 가운데에 보스를 놓고 싸움을 시작한다. 패턴 난수는 boss 용도(같은 시드 = 같은 패턴 순서).</summary>
private void SpawnBoss(int roomId)
        {
            FloorRoom room = Layout.Rooms[roomId];
            Vector3 center = FloorGeometry.RoomCenter(Layout, tuning, room);
            Vector3 toPlayer = playerTransform.position - center;
            toPlayer.y = 0f;
            Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
            Boss = Instantiate(BossTuning.prefab, center, facing, transform);
            Boss.name = $"{BossTuning.prefab.name}_Room{roomId}";
            Health health = Boss.Health;
            health.Died += () => OnRoomEnemyDied(health);
            roomEnemies.Add(health);

            float margin = tuning.wallThickness * 0.5f + BossTuning.bodyRadius + 0.5f;
            bossArenaCenter = center;
            bossArenaHalf = new Vector2(room.Width * tuning.cellSize * 0.5f - margin, room.Height * tuning.cellSize * 0.5f - margin);
            bossRoomHalf = new Vector2(room.Width * tuning.cellSize * 0.5f - tuning.wallThickness * 0.5f, room.Height * tuning.cellSize * 0.5f - tuning.wallThickness * 0.5f);
            bossIntroEntry = playerTransform.position;
            Debug.Log($"[FloorRun] 보스 {BossTuning.displayName} 등장 · 방 {roomId} · 경기장 반폭 {bossArenaHalf.x:0.0} × {bossArenaHalf.y:0.0} m");

            // 등장 연출이 있으면 끝난(건너뛴) 뒤에 싸움을 시작한다 — 그동안 조작 · 보스 행동 · 피해는 막힌다. 없으면 바로
            if (!PlayBossIntro(() => BeginBossFight(announce: false))) BeginBossFight(announce: true);
        }

        /// <summary>보스 패턴을 시작하고 체력 막대를 띄운다. 패턴 난수는 boss 용도(같은 시드 = 같은 패턴 순서) — 연출은 난수를 쓰지 않는다.</summary>
        private void BeginBossFight(bool announce)
        {
            if (Boss == null || Boss.IsDead) return;
            Boss.Begin(player.Health, bossArenaCenter, bossArenaHalf, new RunRandom(Seed).Create(RandomPurpose.Boss, dungeon));
            if (bossBar != null) bossBar.Show(Boss, announce);
            Debug.Log($"[FloorRun] 보스 {BossTuning.displayName} 싸움 시작");
        }

        private bool PlayBossIntro(Action finished)
        {
            if (bossIntro == null || BossTuning == null || BossTuning.intro == null) return false;
            return bossIntro.Play(BossTuning.intro, player, Boss, bossIntroEntry, bossArenaCenter, bossRoomHalf, finished);
        }

        /// <summary>디버그 창: 보스 등장 연출을 처음 들어선 자리에서 다시 본다. 보스는 하던 패턴을 멈추고, 끝나면 첫 대기부터 이어서 싸운다(체력 · 패턴 순서 그대로).</summary>
        public bool DebugReplayBossIntro()
        {
            if (Stats.Outcome != RunOutcome.Playing || Boss == null || Boss.IsDead || BossIntroPlaying || rewardScreen.IsShown) return false;
            Boss.Hold();
            if (PlayBossIntro(Boss.Release)) return true;
            if (bossBar != null) bossBar.Show(Boss, announce: true);
            Boss.Release();
            return true;
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
            if (roomId != Layout.BossRoom.Id || Stats.Outcome != RunOutcome.Playing)
            {
                if (roomId != Layout.BossRoom.Id && session != null) CurrentRun.Save("방 깸"); // 안전한 지점: 문이 열린 뒤
                return;
            }

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
            visit = null;
            CurrentRun.Save("던전 나옴", syncFromScene: false); // 돌아갈 입구 앞(체력은 FinishDungeon이 적음)
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
            if (session != null) CurrentRun.RunEnded(); // 로그라이크: 판이 끝나면(사망 · 클리어) 저장을 지운다
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
            IReadOnlyList<RewardDefinition> offers = AvailableOffers(roomId);
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
            IReadOnlyList<RewardDefinition> offers = AvailableOffers(roomId);
            if (offers.Count == 0) return;
            rewardScreen.Show(offers, Stats, offers[0].Rarity == RewardRarity.Rare, reward =>
            {
                Stats.TakeReward(reward);
                Debug.Log($"[FloorRun] 보상 '{reward.Name}' → Beast {Stats.Beast} · Human {Stats.Human}" +
                          (reward.Unlock != RewardUnlock.None ? $" · {RewardUnlocks.Label(reward.Unlock)} 얻음" : ""));
                player.GetComponent<PlayerForm>().UpdateForm(Stats.Beast, Stats.Human);
                visit?.MarkRewardTaken(roomId);
                if (session != null) CurrentRun.Save("보상 받음");
            });
        }

        /// <summary>그 방에 정해 둔 선택지에서 이미 가진 도구를 주는 보상을 뺀 것(층을 정한 뒤 디버그로 얻었을 때 중복 없게).</summary>
        private IReadOnlyList<RewardDefinition> AvailableOffers(int roomId)
        {
            IReadOnlyList<RewardDefinition> offers = Rewards.OffersFor(roomId);
            bool any = false;
            foreach (RewardDefinition reward in offers) if (reward.Unlock != RewardUnlock.None && Stats.Upgrades.Has(reward.Unlock)) any = true;
            if (!any) return offers;
            var left = new List<RewardDefinition>(offers.Count);
            foreach (RewardDefinition reward in offers) if (reward.Unlock == RewardUnlock.None || !Stats.Upgrades.Has(reward.Unlock)) left.Add(reward);
            return left;
        }

        /// <summary>디버그 창: 그 방에 보상물이 놓여 있는가.</summary>
        public bool HasPickup(int roomId)
        {
            foreach ((int id, GameObject _) in pickups) if (id == roomId) return true;
            return false;
        }

        private void SpinPickups()
        {
            foreach ((int _, GameObject pickup) in pickups) pickup.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        // --- 디버그 창(DebugPanel)이 쓴다 ---

        /// <summary>보스 방 안(가장 가까운 문 쪽, 벽에서 4 m)으로 순간이동. 다른 방이 잠겨 있으면 그 방 적을 먼저 모두 처치해 연다.</summary>
        public bool DebugGoToBossRoom()
        {
            if (Stats.Outcome != RunOutcome.Playing) return false;
            int boss = Layout.BossRoom.Id;
            if (Progress.LockedRoom >= 0 && Progress.LockedRoom != boss)
            {
                foreach (Health enemy in new List<Health>(roomEnemies)) if (!enemy.IsDead) enemy.TakeDamage(enemy.Current, "디버그");
                if (Progress.LockedRoom >= 0) Progress.ClearLockedRoom();
            }
            FloorRoom room = Layout.BossRoom;
            Vector3 center = FloorGeometry.RoomCenter(Layout, tuning, room);
            Vector3 entry = center + new Vector3(0f, 0f, -(room.Height * tuning.cellSize * 0.5f - 4f));
            foreach (FloorDoor door in Layout.Doors)
            {
                if (door.RoomA != boss && door.RoomB != boss) continue;
                Vector3 doorPosition = FloorGeometry.CellCenter(Layout, tuning, (door.X + door.OtherX) * 0.5f, (door.Y + door.OtherY) * 0.5f); // 두 칸 사이 = 문
                Vector3 toDoor = doorPosition - center;
                toDoor.y = 0f;
                float halfDepth = (Mathf.Abs(toDoor.x) > Mathf.Abs(toDoor.z) ? room.Width : room.Height) * tuning.cellSize * 0.5f;
                entry = center + (Mathf.Abs(toDoor.x) > Mathf.Abs(toDoor.z) ? new Vector3(Mathf.Sign(toDoor.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toDoor.z))) * (halfDepth - 4f);
                break;
            }
            var body = player.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            Vector3 look = center - entry;
            playerTransform.SetPositionAndRotation(entry + Vector3.up * 0.1f, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            if (body != null) body.enabled = true;
            return true;
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
