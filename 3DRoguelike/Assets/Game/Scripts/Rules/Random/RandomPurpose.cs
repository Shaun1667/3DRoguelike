namespace Game.Rules
{
    /// <summary>
    /// 난수 용도. 새 용도 = 아래에 한 줄. 키 글자가 시드 결과를 정하므로 한 번 쓴 키는 바꾸지 않는다.
    /// </summary>
    public readonly struct RandomPurpose
    {
        /// <summary>층의 방 격자 모양.</summary>
        public static readonly RandomPurpose Map = new RandomPurpose("map");
        /// <summary>모양이 정해진 뒤 덧칠하는 것(방 종류 · 장식).</summary>
        public static readonly RandomPurpose Decor = new RandomPurpose("decor");
        /// <summary>보상 후보.</summary>
        public static readonly RandomPurpose Reward = new RandomPurpose("reward");
        /// <summary>적 종류 · 배치.</summary>
        public static readonly RandomPurpose Enemy = new RandomPurpose("enemy");
        /// <summary>전투 중 판정. 같은 시드라도 전투 결과까지 같을 필요는 없다(핵심 규칙 2).</summary>
        public static readonly RandomPurpose Combat = new RandomPurpose("combat");
        /// <summary>연출. 결과에 영향을 주지 않는 것만.</summary>
        public static readonly RandomPurpose Effects = new RandomPurpose("effects");
        /// <summary>월드 길 위 적 무리의 종류(층 번호 자리 = 무리 번호). 던전 안 적(enemy)과 수열이 겹치지 않게 따로.</summary>
        public static readonly RandomPurpose Road = new RandomPurpose("road");
        /// <summary>던전 테마 방 꾸미기(잔해 · 차 같은 장식 자리). 맵 · 적 · 보상과 수열이 겹치지 않게 따로 — 테마 없는 던전은 쓰지 않는다.</summary>
        public static readonly RandomPurpose Dressing = new RandomPurpose("dressing");

        RandomPurpose(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public override string ToString() => Key;
    }
}
