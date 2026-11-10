using System;

namespace Game.Rules
{
    /// <summary>미니맵에 방이 어떻게 보이는가. 끝에만 추가한다.</summary>
    public enum RoomVisibility
    {
        /// <summary>아직 모름. 그리지 않는다.</summary>
        Hidden,
        /// <summary>가 본 방과 문으로 이어진 방. 테두리만 보인다.</summary>
        Known,
        /// <summary>들어가 본 방.</summary>
        Visited,
    }

    /// <summary>
    /// 층 안에서 어느 방에 가 봤고 어느 방을 깼고 지금 어느 방이 잠겼는지. 저장할 판 상태의 일부(3-6).
    /// 깨지 않은 방에 들어가면 그 방의 문이 모두 잠기고, 방을 깨면 열린다. 시작 방은 처음부터 가 본 방 · 깬 방이고, 보물방은 처음부터 깬 방이다.
    /// </summary>
    public sealed class FloorProgress
    {
        readonly FloorLayout layout;
        readonly bool[] cleared;
        readonly bool[] visited;

        public FloorProgress(FloorLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            cleared = new bool[layout.Rooms.Count];
            visited = new bool[layout.Rooms.Count];
            cleared[layout.StartRoom.Id] = true;
            foreach (FloorRoom room in layout.Rooms)
            {
                if (room.Kind == RoomKind.Treasure) cleared[room.Id] = true; // 보물방은 적이 없어 잠기지 않는다
            }
            visited[layout.StartRoom.Id] = true;
            CurrentRoom = layout.StartRoom.Id;
            LockedRoom = -1;
        }

        /// <summary>다른 방에 들어선 순간(잠기기 전). 값은 방 Id.</summary>
        public event Action<int> RoomEntered;

        /// <summary>방이 잠긴 순간. 값은 방 Id.</summary>
        public event Action<int> RoomLocked;

        /// <summary>잠긴 방을 깬 순간. 값은 방 Id.</summary>
        public event Action<int> RoomCleared;

        public FloorLayout Layout => layout;
        public int CurrentRoom { get; private set; }
        /// <summary>문이 잠긴 방. 없으면 -1.</summary>
        public int LockedRoom { get; private set; }

        public bool IsCleared(int roomId)
        {
            CheckRoom(roomId);
            return cleared[roomId];
        }

        public bool IsVisited(int roomId)
        {
            CheckRoom(roomId);
            return visited[roomId];
        }

        /// <summary>가 본 방은 Visited, 가 본 방과 문으로 이어진 방은 Known, 나머지는 Hidden.</summary>
        public RoomVisibility Visibility(int roomId)
        {
            CheckRoom(roomId);
            if (visited[roomId]) return RoomVisibility.Visited;
            foreach (FloorDoor door in layout.Doors)
            {
                if (door.RoomA == roomId && visited[door.RoomB] || door.RoomB == roomId && visited[door.RoomA]) return RoomVisibility.Known;
            }
            return RoomVisibility.Hidden;
        }

        public bool IsDoorLocked(FloorDoor door)
        {
            return LockedRoom >= 0 && (door.RoomA == LockedRoom || door.RoomB == LockedRoom);
        }

        /// <summary>플레이어가 방 안으로 들어섰다. 잠긴 방이 있는 동안에는 다른 방으로 갈 수 없으니 무시한다.</summary>
        /// <returns>이번에 그 방이 잠겼으면 true</returns>
        public bool Enter(int roomId)
        {
            CheckRoom(roomId);
            if (LockedRoom >= 0 || roomId == CurrentRoom) return false;

            CurrentRoom = roomId;
            visited[roomId] = true;
            RoomEntered?.Invoke(roomId);
            if (cleared[roomId]) return false;

            LockedRoom = roomId;
            RoomLocked?.Invoke(roomId);
            return true;
        }

        /// <summary>잠긴 방의 적을 모두 잡았다.</summary>
        /// <returns>잠긴 방이 있어서 깼으면 true</returns>
        public bool ClearLockedRoom()
        {
            if (LockedRoom < 0) return false;

            int roomId = LockedRoom;
            cleared[roomId] = true;
            LockedRoom = -1;
            RoomCleared?.Invoke(roomId);
            return true;
        }

        void CheckRoom(int roomId)
        {
            if (roomId < 0 || roomId >= cleared.Length) throw new ArgumentOutOfRangeException(nameof(roomId));
        }
    }
}
