using System;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>층 생성 · 방 크기 · 방마다 적 · 보상 수치. 값의 기준은 BALANCE.md 7 · 8 · 10 · 11번. 새 방 모양 = roomShapes에 한 줄.</summary>
    [CreateAssetMenu(menuName = "Game/Floor Tuning")]
    public sealed class FloorTuning : ScriptableObject
    {
        [Serializable]
        public struct ShapeEntry
        {
            [Min(1)] public int width;
            [Min(1)] public int height;
            [Min(1)] public int weight;
        }

        [Header("방 배치")]
        [Min(3)] public int gridWidth = 9;
        [Min(3)] public int gridHeight = 9;
        [Min(2)] public int minRooms = 8;
        [Min(2)] public int maxRooms = 12;
        public ShapeEntry[] roomShapes =
        {
            new ShapeEntry { width = 1, height = 1, weight = 7 },
            new ShapeEntry { width = 2, height = 1, weight = 1 },
            new ShapeEntry { width = 1, height = 2, weight = 1 },
            new ShapeEntry { width = 2, height = 2, weight = 1 },
        };

        [Header("방 크기")]
        [Tooltip("격자 한 칸의 한 변(m)")]
        [Min(1f)] public float cellSize = 24f;
        [Min(0.1f)] public float wallHeight = 4f;
        [Min(0.1f)] public float wallThickness = 1f;
        [Min(0.5f)] public float doorWidth = 4f;
        [Tooltip("벽에서 이만큼 들어서야 방에 들어간 것으로 본다(m). 문틀에서 잠기지 않게")]
        [Min(0f)] public float enterMargin = 1.5f;

        [Header("방마다 적")]
        [Tooltip("한 칸 방의 적 수 최소 · 최대")]
        [Min(1)] public int minEnemies = 2;
        [Min(1)] public int maxEnemies = 4;
        [Tooltip("큰 방은 칸이 하나 늘 때마다 이만큼 더")]
        [Min(0)] public int extraEnemiesPerCell = 1;
        [Tooltip("적이 나타날 자리가 벽에서 떨어질 거리(m)")]
        [Min(0f)] public float spawnWallMargin = 4f;
        [Tooltip("적끼리 떨어질 거리(m)")]
        [Min(0f)] public float spawnSpacing = 3f;
        [Tooltip("문에서 떨어질 거리(m). 들어서자마자 붙어 있지 않게")]
        [Min(0f)] public float spawnDoorClearance = 6f;

        [Header("보상")]
        [Tooltip("전투 방을 깼을 때 보상물이 나올 확률")]
        [Range(0f, 1f)] public float rewardChance = 0.5f;
        [Tooltip("보상물을 주우면 고르는 선택지 수")]
        [Min(1)] public int rewardChoices = 3;

        public RewardSettings ToRewardSettings()
        {
            return new RewardSettings(rewardChance, rewardChoices);
        }

        public EncounterSettings ToEncounterSettings()
        {
            return new EncounterSettings(cellSize, minEnemies, maxEnemies, extraEnemiesPerCell, spawnWallMargin, spawnSpacing, spawnDoorClearance);
        }

        public FloorLayoutSettings ToSettings()
        {
            var shapes = new List<WeightedRoomShape>(roomShapes.Length);
            foreach (ShapeEntry entry in roomShapes)
            {
                shapes.Add(new WeightedRoomShape(new RoomShape(entry.width, entry.height), entry.weight));
            }
            return new FloorLayoutSettings(gridWidth, gridHeight, minRooms, maxRooms, shapes);
        }
    }
}
