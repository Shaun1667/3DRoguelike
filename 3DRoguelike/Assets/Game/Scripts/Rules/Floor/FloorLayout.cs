using System.Collections.Generic;
using System.Text;

namespace Game.Rules
{
    /// <summary>층의 방 하나. (X, Y)는 왼쪽 아래 칸.</summary>
    public sealed class FloorRoom
    {
        internal FloorRoom(int id, int x, int y, RoomShape shape)
        {
            Id = id;
            X = x;
            Y = y;
            Width = shape.Width;
            Height = shape.Height;
        }

        /// <summary>0이 시작 방. 놓인 순서대로 붙는다.</summary>
        public int Id { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public RoomKind Kind { get; internal set; }
        /// <summary>시작 방에서 지나야 하는 문 수.</summary>
        public int DistanceFromStart { get; internal set; }
        public int DoorCount { get; internal set; }

        public bool Contains(int x, int y)
        {
            return x >= X && x < X + Width && y >= Y && y < Y + Height;
        }
    }

    /// <summary>두 방을 잇는 문. RoomA의 (X, Y) 칸에서 Direction 쪽으로 나가면 RoomB.</summary>
    public readonly struct FloorDoor
    {
        public FloorDoor(int roomA, int roomB, int x, int y, GridDirection direction)
        {
            RoomA = roomA;
            RoomB = roomB;
            X = x;
            Y = y;
            Direction = direction;
        }

        public int RoomA { get; }
        public int RoomB { get; }
        public int X { get; }
        public int Y { get; }
        public GridDirection Direction { get; }
        /// <summary>문 건너편, RoomB 쪽 칸.</summary>
        public int OtherX => X + Direction.Dx();
        public int OtherY => Y + Direction.Dy();

        public override string ToString() => $"{RoomA}-{RoomB}@{X},{Y}{Direction.ToString()[0]}";
    }

    /// <summary>
    /// 층 하나의 방 배치. 방은 나무 모양으로 이어져 두 방 사이 길은 하나뿐이고, 붙어 있는 두 방 사이에는 항상 문이 있다.
    /// </summary>
    public sealed class FloorLayout
    {
        readonly int[] roomIds;

        internal FloorLayout(int width, int height, int[] roomIds, List<FloorRoom> rooms, List<FloorDoor> doors, List<FloorRoom> deadEnds, FloorRoom boss)
        {
            Width = width;
            Height = height;
            this.roomIds = roomIds;
            Rooms = rooms.AsReadOnly();
            Doors = doors.AsReadOnly();
            DeadEnds = deadEnds.AsReadOnly();
            BossRoom = boss;
        }

        public int Width { get; }
        public int Height { get; }
        /// <summary>Id 순서. Rooms[0]이 시작 방.</summary>
        public IReadOnlyList<FloorRoom> Rooms { get; }
        public IReadOnlyList<FloorDoor> Doors { get; }
        /// <summary>문이 하나뿐인 방(시작 방 제외), Id 순서. 보스 방도 들어 있다. 특수 방 후보.</summary>
        public IReadOnlyList<FloorRoom> DeadEnds { get; }
        public FloorRoom StartRoom => Rooms[0];
        /// <summary>시작에서 가장 먼 막다른 방. 같으면 Id가 작은 방.</summary>
        public FloorRoom BossRoom { get; }

        /// <summary>그 칸의 방 Id. 격자 밖이거나 빈칸이면 -1.</summary>
        public int RoomIdAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
            return roomIds[y * Width + x];
        }

        /// <summary>디버그 · 테스트용 그림. 위가 북쪽, 칸마다 방 Id(0-9, a-z), 빈칸은 '.'.</summary>
        public string ToAscii()
        {
            const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            var text = new StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    int id = RoomIdAt(x, y);
                    text.Append(id < 0 ? '.' : id < Digits.Length ? Digits[id] : '?');
                }
                if (y > 0) text.Append('\n');
            }
            return text.ToString();
        }
    }
}
