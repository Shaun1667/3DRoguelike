namespace Game.Rules
{
    /// <summary>
    /// 보상이 주는 도구(무기 · 기술). 수치 강화(<see cref="UpgradeStat"/>)와 달리 한 번 얻으면 판 끝까지 가지고, 다시 나오지 않는다.
    /// 가진 것은 <see cref="UpgradeSheet.Has"/>로 묻는다. 끝에만 추가한다.
    /// </summary>
    public enum RewardUnlock
    {
        None,
        /// <summary>권총(조준 · 사격). 첫 던전에서 반드시 얻는다(GDD 시작 전투, 3-4).</summary>
        Pistol,
    }

    public static class RewardUnlocks
    {
        public const int Count = (int)RewardUnlock.Pistol + 1;

        public static string Label(RewardUnlock unlock)
        {
            switch (unlock)
            {
                case RewardUnlock.None: return "없음";
                case RewardUnlock.Pistol: return "권총";
                default: return unlock.ToString();
            }
        }
    }
}
