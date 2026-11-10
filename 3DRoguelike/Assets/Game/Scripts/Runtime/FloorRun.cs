using System;
using System.Collections;
using System.Collections.Generic;
using Game.Rules;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// 층 하나를 진행한다. 시드로 배치와 방마다 적 자리를 정해 세우고, 플레이어가 깨지 않은 방에 들어서면 문을 잠그고 적을 내보낸다.
    /// 그 방의 적을 모두 잡으면 방을 깬 것이 되어 문이 열린다. 보스 방을 깨거나 플레이어가 죽으면 판이 끝나 결과 화면을 띄운다.
    /// </summary>
    public sealed class FloorRun : MonoBehaviour
    {
        /// <summary>"같은 층으로 다시"를 고르면 씬을 다시 불러오는 동안 시드를 여기에 맡겨 둔다.</summary>
        private static RunSeed? pendingSeed;

        [SerializeField] private FloorTuning tuning;
        [SerializeField] private PlayerVitals player;
        [SerializeField] private GameObject enemyPrefab;
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

        [Header("재질")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material startFloorMaterial;
        [SerializeField] private Material bossFloorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material lockedDoorMaterial;
        [SerializeField] private Material treasureFloorMaterial;
        [SerializeField] private Material rewardMaterial;
        [SerializeField] private Material rareRewardMaterial;

        private readonly List<Health> roomEnemies = new List<Health>();
        private GameObject[] doorBarriers;
        private Transform playerTransform;
        private readonly List<(int roomId, GameObject pickup)> pickups = new List<(int, GameObject)>();

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
        /// <summary>잠긴 방에 아직 살아 있는 적 수.</summary>
        public int AliveEnemies => roomEnemies.Count;

        /// <summary>판이 끝난 순간(죽음 · 층 클리어). 결과 화면은 잠시 뒤에 뜬다.</summary>
        public event Action<RunStats> RunEnded;

        /// <summary>씬을 다시 불러와 판을 새로 시작한다. sameSeed면 지금 층을 그대로 다시 만든다.</summary>
        public void Restart(bool sameSeed)
        {
            pendingSeed = sameSeed ? Seed : (RunSeed?)null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            playerTransform = player.transform;
            Seed = ChooseSeed();
            var random = new RunRandom(Seed);
            Layout = FloorGenerator.Generate(tuning.ToSettings(), random.Create(RandomPurpose.Map, 0));
            TreasureRoom = SpecialRoomPlanner.AssignTreasure(Layout, random.Create(RandomPurpose.Decor, 0));
            Encounters = EncounterPlanner.Plan(Layout, tuning.ToEncounterSettings(), random.Create(RandomPurpose.Enemy, 0));
            Rewards = RewardPlanner.Plan(Layout, tuning.ToRewardSettings(), rewardTable.ToDefinitions(), random.Create(RandomPurpose.Reward, 0));
            Progress = new FloorProgress(Layout);
            int battleRooms = 0;
            foreach (FloorRoom room in Layout.Rooms) if (room.Kind == RoomKind.Battle || room.Kind == RoomKind.Boss) battleRooms++;
            Stats = new RunStats(Seed, battleRooms);
            Progress.RoomLocked += OnRoomLocked;
            Progress.RoomCleared += OnRoomCleared;

            var materials = new FloorGeometry.Materials
            {
                floor = floorMaterial,
                startFloor = startFloorMaterial,
                bossFloor = bossFloorMaterial,
                wall = wallMaterial,
                lockedDoor = lockedDoorMaterial,
                treasureFloor = treasureFloorMaterial,
            };
            doorBarriers = FloorGeometry.Build(Layout, tuning, materials, transform);
            navMesh.BuildNavMesh();
            if (TreasureRoom >= 0) SpawnPickup(TreasureRoom);
            Debug.Log($"[FloorRun] 시드 {Seed.ToCode()} · 방 {Layout.Rooms.Count}개 · 보스 방 {Layout.BossRoom.Id} · 보물방 {TreasureRoom}\n{Layout.ToAscii()}");
        }

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

                GameObject enemy = Instantiate(enemyPrefab, position, facing, transform);
                enemy.name = $"Mutant_Room{roomId}_{i}";
                var health = enemy.GetComponent<Health>();
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
            UpdateDoors();
            if (roomId != Layout.BossRoom.Id) SpawnPickup(roomId);
            if (roomId == Layout.BossRoom.Id && Stats.ClearFloor(Time.timeSinceLevelLoad))
            {
                player.DisableControls();
                EndRun(resultDelayAfterClear);
            }
        }

        private void OnPlayerDied()
        {
            if (Stats.Die(player.Health.LastDamageSource, Time.timeSinceLevelLoad)) EndRun(resultDelayAfterDeath);
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
            resultScreen.Show(Stats, this);
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
