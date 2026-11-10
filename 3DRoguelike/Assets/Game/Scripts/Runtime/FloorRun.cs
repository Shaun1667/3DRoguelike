using System;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 층 하나를 진행한다. 시드로 배치를 만들어 세우고, 플레이어가 깨지 않은 방에 들어서면 문을 잠그고 표적 적을 내보낸다.
    /// 적을 잡으면 방을 깬 것이 되어 문이 열린다. 진짜 적과 적 수는 TASKS 2-4.
    /// </summary>
    public sealed class FloorRun : MonoBehaviour
    {
        [SerializeField] private FloorTuning tuning;
        [SerializeField] private Transform player;
        [SerializeField] private GameObject enemyPrefab;
        [Tooltip("비워 두면 플레이마다 새 시드. 'XXXX-XXXX' 코드를 넣으면 그 층을 다시 만든다")]
        [SerializeField] private string fixedSeedCode = "";

        [Header("재질")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material startFloorMaterial;
        [SerializeField] private Material bossFloorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material lockedDoorMaterial;

        private GameObject[] doorBarriers;
        private Health roomEnemy;

        public RunSeed Seed { get; private set; }
        public FloorLayout Layout { get; private set; }
        public FloorProgress Progress { get; private set; }

        private void Awake()
        {
            Seed = ChooseSeed();
            var random = new RunRandom(Seed);
            Layout = FloorGenerator.Generate(tuning.ToSettings(), random.Create(RandomPurpose.Map, 0));
            Progress = new FloorProgress(Layout);
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
            Debug.Log($"[FloorRun] 시드 {Seed.ToCode()} · 방 {Layout.Rooms.Count}개 · 보스 방 {Layout.BossRoom.Id}\n{Layout.ToAscii()}");
        }

        private void Update()
        {
            Vector3 position = player.position;
            if (!FloorGeometry.TryGetCell(Layout, tuning, position, out int x, out int y)) return;

            int roomId = Layout.RoomIdAt(x, y);
            if (roomId < 0 || roomId == Progress.CurrentRoom) return;
            if (!FloorGeometry.IsWellInside(Layout, tuning, Layout.Rooms[roomId], position, tuning.enterMargin)) return;

            Progress.Enter(roomId);
        }

        private RunSeed ChooseSeed()
        {
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

            Vector3 position = FloorGeometry.RoomCenter(Layout, tuning, Layout.Rooms[roomId]) + Vector3.up;
            GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity, transform);
            enemy.name = $"TargetEnemy_Room{roomId}";
            enemy.GetComponent<TargetEnemy>().Respawns = false;
            roomEnemy = enemy.GetComponent<Health>();
            roomEnemy.Died += OnRoomEnemyDied;
        }

        private void OnRoomEnemyDied()
        {
            roomEnemy.Died -= OnRoomEnemyDied;
            roomEnemy = null;
            Progress.ClearLockedRoom();
        }

        private void OnRoomCleared(int roomId)
        {
            UpdateDoors();
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
