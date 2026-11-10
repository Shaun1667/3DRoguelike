namespace Game.Rules
{
    /// <summary>방 종류. 에셋 · 저장에 정수로 남을 수 있으니 끝에만 추가한다. 상점 방은 그 기능을 만들 때 더한다.</summary>
    public enum RoomKind
    {
        Battle,
        Start,
        Boss,
        /// <summary>보물방(3-1): 적이 없고 희귀 보상이 반드시 있다. 꾸미기 난수로 막다른 방 하나에 정한다.</summary>
        Treasure,
    }
}
