using System;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>층 생성과 방 크기 수치. 값의 기준은 BALANCE.md 7 · 8번. 새 방 모양 = roomShapes에 한 줄.</summary>
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
