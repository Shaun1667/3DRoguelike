namespace Game.Rules
{
    /// <summary>
    /// 격자 좌표와 층 좌표(m)의 변환. 시작 방 가운데가 원점이고, 격자 x → 층 x, 격자 y → 층 z(북쪽).
    /// 화면(FloorGeometry)과 적 배치(EncounterPlanner)가 같은 좌표를 쓰도록 여기 한 곳에 둔다.
    /// </summary>
    public static class FloorSpace
    {
        /// <summary>칸 가운데. 칸 번호에 0.5를 더하면 칸 사이 경계.</summary>
        public static (float x, float z) CellCenter(FloorLayout layout, float cellSize, float cellX, float cellY)
        {
            return ((cellX - layout.Width / 2) * cellSize, (cellY - layout.Height / 2) * cellSize);
        }

        public static (float x, float z) RoomCenter(FloorLayout layout, float cellSize, FloorRoom room)
        {
            return CellCenter(layout, cellSize, room.X + (room.Width - 1) * 0.5f, room.Y + (room.Height - 1) * 0.5f);
        }

        /// <summary>문 가운데(두 칸 사이 경계).</summary>
        public static (float x, float z) DoorCenter(FloorLayout layout, float cellSize, FloorDoor door)
        {
            return CellCenter(layout, cellSize, door.X + door.Direction.Dx() * 0.5f, door.Y + door.Direction.Dy() * 0.5f);
        }
    }
}
