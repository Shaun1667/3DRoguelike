using System;

namespace Game.Rules
{
    /// <summary>
    /// 강화가 올리는 능력치. 모두 "클수록 좋다" 방향이다(재사용 대기는 회복 속도로 센다). 끝에만 추가한다.
    /// 새 능력치 = 여기 한 줄 + <see cref="UpgradeStats.Label"/> 한 줄 + 그 값을 쓰는 곳 한 군데.
    /// </summary>
    public enum UpgradeStat
    {
        /// <summary>맨몸 근접 피해</summary>
        MeleeDamage,
        /// <summary>이동 속도(걷기 · 달리기 모두)</summary>
        MoveSpeed,
        /// <summary>방을 깰 때 최대 체력의 이 비율만큼 회복(기본 0)</summary>
        HealOnRoomClear,
        /// <summary>권총 피해</summary>
        PistolDamage,
        /// <summary>권총 탄창 크기</summary>
        PistolMagazine,
        /// <summary>조준 전환 속도</summary>
        AimTransition,
        /// <summary>조준 중 이동 속도</summary>
        AimMoveSpeed,
        /// <summary>구르기 속도(같은 시간에 더 멀리)</summary>
        RollSpeed,
        /// <summary>구르기 재사용 대기가 줄어드는 정도(대기 ÷ 배율)</summary>
        RollRecovery,
    }

    /// <summary>보상 하나가 능력치 하나를 올리는 양. 퍼센트는 기본값 기준이고 여러 번 고르면 더해진다.</summary>
    [Serializable]
    public struct StatEffect
    {
        public UpgradeStat stat;
        public float percent;

        public StatEffect(UpgradeStat stat, float percent)
        {
            this.stat = stat;
            this.percent = percent;
        }
    }

    public static class UpgradeStats
    {
        public static readonly int Count = Enum.GetValues(typeof(UpgradeStat)).Length;

        /// <summary>카드에 보여 줄 이름.</summary>
        public static string Label(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.MeleeDamage: return "근접 피해";
                case UpgradeStat.MoveSpeed: return "이동 속도";
                case UpgradeStat.HealOnRoomClear: return "방을 깰 때 체력 회복";
                case UpgradeStat.PistolDamage: return "권총 피해";
                case UpgradeStat.PistolMagazine: return "탄창";
                case UpgradeStat.AimTransition: return "조준 전환";
                case UpgradeStat.AimMoveSpeed: return "조준 중 이동";
                case UpgradeStat.RollSpeed: return "구르기 거리";
                case UpgradeStat.RollRecovery: return "구르기 재사용";
                default: return stat.ToString();
            }
        }

        /// <summary>"근접 피해 +20%"</summary>
        public static string Describe(StatEffect effect)
        {
            return $"{Label(effect.stat)} {(effect.percent >= 0 ? "+" : "")}{effect.percent:0.#}%";
        }
    }
}
