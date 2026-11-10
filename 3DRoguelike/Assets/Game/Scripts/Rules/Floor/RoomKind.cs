namespace Game.Rules
{
    /// <summary>방 종류. 에셋 · 저장에 정수로 남을 수 있으니 끝에만 추가한다. 보상 · 상점 방은 그 기능을 만들 때 더한다.</summary>
    public enum RoomKind
    {
        Battle,
        Start,
        Boss,
    }
}
