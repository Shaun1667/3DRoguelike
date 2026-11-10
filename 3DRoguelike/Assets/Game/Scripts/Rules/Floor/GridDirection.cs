namespace Game.Rules
{
    /// <summary>격자 방향. y가 커지는 쪽이 북쪽. 끝에만 추가한다.</summary>
    public enum GridDirection
    {
        North,
        East,
        South,
        West,
    }

    public static class GridDirections
    {
        public static int Dx(this GridDirection direction)
        {
            return direction == GridDirection.East ? 1 : direction == GridDirection.West ? -1 : 0;
        }

        public static int Dy(this GridDirection direction)
        {
            return direction == GridDirection.North ? 1 : direction == GridDirection.South ? -1 : 0;
        }

        public static GridDirection Opposite(this GridDirection direction)
        {
            return (GridDirection)(((int)direction + 2) % 4);
        }
    }
}
