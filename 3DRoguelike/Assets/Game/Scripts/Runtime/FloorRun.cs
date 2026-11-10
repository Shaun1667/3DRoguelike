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

        private readonly List<Health> roomEnemies = new List<Health>();
        private GameObject[] doorBarriers;
        private Transform playerTransform;

        public RunSeed Seed { get; private set; }
        public FloorLayout Layout { get; private set; }
        public FloorProgress Progress { get; private set; }
        public FloorEncounters Encounters { get; private set; }
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
            Encounters = EncounterPlanner.Plan(Layout, tuning.ToEncounterSettings(), random.Create(RandomPurpose.Enemy, 0));
            Progress = new FloorProgress(Layout);
            Stats = new RunStats(Seed, Layout.Rooms.Count - 1);
            Progress.RoomLocked += OnRoomLocked;
            Progress.RoomCleared += OnRoomCleared;

            var materials = new FloorGeometry.Materials
            {
                floor = floorMaterial,
                startFloor = startFloorMaterial,
                bossFloor = bossFloorMaterial,
                wall = wallMaterial,
                lockedDoor = lockedDoorMaterial,
            };
            doorBarriers = FloorGeometry.Build(Layout, tuning, materials, transform);
            navMesh.BuildNavMesh();
            Debug.Log($"[FloorRun] 시드 {Seed.ToCode()} · 방 {Layout.Rooms.Count}개 · 보스 방 {Layout.BossRoom.Id}\n{Layout.ToAscii()}");
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

        private void UpdateDoors()
        {
            for (int i = 0; i < doorBarriers.Length; i++)
            {
                doorBarriers[i].SetActive(Progress.IsDoorLocked(Layout.Doors[i]));
            }
        }
    }
}
