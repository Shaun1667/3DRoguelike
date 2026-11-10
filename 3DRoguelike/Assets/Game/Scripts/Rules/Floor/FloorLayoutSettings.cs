using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>방이 격자에서 차지하는 칸 수.</summary>
    public readonly struct RoomShape
    {
        public RoomShape(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public override string ToString() => $"{Width}x{Height}";
    }

    /// <summary>방 모양과 뽑힐 무게.</summary>
    public readonly struct WeightedRoomShape
    {
        public WeightedRoomShape(RoomShape shape, int weight)
        {
            if (weight < 1) throw new ArgumentOutOfRangeException(nameof(weight));
            Shape = shape;
            Weight = weight;
        }

        public RoomShape Shape { get; }
        public int Weight { get; }
    }

    /// <summary>층 생성 수치. 값은 BALANCE.md 7번. 새 방 모양 = 모양 목록에 한 줄.</summary>
    public sealed class FloorLayoutSettings
    {
        public FloorLayoutSettings(int gridWidth, int gridHeight, int minRooms, int maxRooms, IReadOnlyList<WeightedRoomShape> shapes)
        {
            if (gridWidth < 3) throw new ArgumentOutOfRangeException(nameof(gridWidth));
            if (gridHeight < 3) throw new ArgumentOutOfRangeException(nameof(gridHeight));
            if (minRooms < 2) throw new ArgumentOutOfRangeException(nameof(minRooms));
            if (maxRooms < minRooms || maxRooms > gridWidth * gridHeight) throw new ArgumentOutOfRangeException(nameof(maxRooms));
            if (shapes == null || shapes.Count == 0) throw new ArgumentException("At least one room shape is needed.", nameof(shapes));

            int totalWeight = 0;
            foreach (var entry in shapes)
            {
                if (entry.Weight < 1) throw new ArgumentException("Use the WeightedRoomShape constructor.", nameof(shapes));
                if (entry.Shape.Width > gridWidth || entry.Shape.Height > gridHeight) throw new ArgumentException($"Shape {entry.Shape} does not fit the grid.", nameof(shapes));
                totalWeight += entry.Weight;
            }

            GridWidth = gridWidth;
            GridHeight = gridHeight;
            MinRooms = minRooms;
            MaxRooms = maxRooms;
            Shapes = new List<WeightedRoomShape>(shapes).AsReadOnly();
            TotalShapeWeight = totalWeight;
        }

        public int GridWidth { get; }
        public int GridHeight { get; }
        public int MinRooms { get; }
        public int MaxRooms { get; }
        public IReadOnlyList<WeightedRoomShape> Shapes { get; }
        public int TotalShapeWeight { get; }
    }
}
