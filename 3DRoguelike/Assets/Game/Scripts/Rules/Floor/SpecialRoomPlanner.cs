namespace Game.Rules
{
    /// <summary>
    /// 맵 모양이 정해진 뒤 특수 방을 정한다. 난수는 꾸미기 용도(<see cref="RandomPurpose.Decor"/>)로 받아
    /// 특수 방을 더해도 같은 시드의 맵 모양은 바뀌지 않는다.
    /// </summary>
    public static class SpecialRoomPlanner
    {
        /// <summary>보스 방이 아닌 막다른 방 하나를 보물방으로 바꾼다.</summary>
        /// <returns>보물방 Id. 후보가 없으면 -1</returns>
        public static int AssignTreasure(FloorLayout layout, IRandom random)
        {
            var candidates = new System.Collections.Generic.List<FloorRoom>();
            foreach (FloorRoom room in layout.DeadEnds)
            {
                if (room.Kind == RoomKind.Battle) candidates.Add(room);
            }
            if (candidates.Count == 0) return -1;
            FloorRoom treasure = candidates[random.Next(candidates.Count)];
            treasure.Kind = RoomKind.Treasure;
            return treasure.Id;
        }
    }
}
