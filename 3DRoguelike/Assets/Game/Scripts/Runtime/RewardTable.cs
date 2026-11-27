using System;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>보상 목록 겸 강화 표. 새 보상 = 표에 한 줄(성향 + 능력치 효과). 합계 · 조회는 규칙 <see cref="UpgradeSheet"/> 한 곳.</summary>
    [CreateAssetMenu(menuName = "Game/Reward Table")]
    public sealed class RewardTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("바꾸지 않는 이름표(저장에 쓴다)")]
            public string id;
            public string displayName;
            [TextArea] public string description;
            public Tendency tendency;
            [Min(0)] public int amount;
            public RewardRarity rarity;
            [Tooltip("능력치 강화. 퍼센트는 기본값 기준, 같은 보상을 다시 고르면 더해진다(BALANCE 12번)")]
            public StatEffect[] effects;
            [Tooltip("기술이 아직 없어 선택지에 내지 않음")]
            public bool comingLater;
            [Tooltip("주는 도구(권총 등). 한 번 얻으면 판 끝까지, 다시 나오지 않음")]
            public RewardUnlock unlock;
            [Tooltip("보장: 아래 던전 순서부터 아직 없으면 층마다 보장 방(시작 → 보스 길의 첫 방)에 반드시. 무작위 선택지에는 없음. unlock이 있어야 함")]
            public bool guaranteed;
            [Tooltip("보장이 시작되는 던전 순서(0 = 첫 던전)")]
            [Min(0)] public int guaranteeFromDungeon;
            [Tooltip("판 밖 성장 트리(3-7)의 노드를 사야 보상 후보에 나옴(MetaTreeTable의 opensReward). 열지 않은 판은 이 줄이 없는 것과 같다")]
            public bool metaLocked;
        }

        /// <summary>새로 만들 때의 값. 에셋이 이미 있으면 에셋 값이 기준이다(BALANCE 11 · 12번).</summary>
        public Entry[] entries =
        {
            new Entry { id = "beast_strength", displayName = "근력", description = "맨손 공격이 묵직해진다", tendency = Tendency.Beast, amount = 1, rarity = RewardRarity.Common,
                        effects = new[] { new StatEffect(UpgradeStat.MeleeDamage, 20f) } },
            new Entry { id = "beast_move", displayName = "이동", description = "더 빨리 움직인다", tendency = Tendency.Beast, amount = 1, rarity = RewardRarity.Common,
                        effects = new[] { new StatEffect(UpgradeStat.MoveSpeed, 10f) } },
            new Entry { id = "beast_regen", displayName = "재생", description = "방을 깰 때마다 상처가 아문다", tendency = Tendency.Beast, amount = 1, rarity = RewardRarity.Common,
                        effects = new[] { new StatEffect(UpgradeStat.HealOnRoomClear, 20f) } },
            new Entry { id = "human_pistol", displayName = "권총 손질", description = "권총이 더 세게 맞는다", tendency = Tendency.Human, amount = 1, rarity = RewardRarity.Common,
                        effects = new[] { new StatEffect(UpgradeStat.PistolDamage, 20f) } },
            new Entry { id = "human_magazine", displayName = "탄창", description = "한 번에 더 많이 쏜다", tendency = Tendency.Human, amount = 1, rarity = RewardRarity.Common,
                        effects = new[] { new StatEffect(UpgradeStat.PistolMagazine, 20f) } },
            new Entry { id = "human_aim", displayName = "조준", description = "조준이 빨라진다", tendency = Tendency.Human, amount = 1, rarity = RewardRarity.Common,
                        effects = new[] { new StatEffect(UpgradeStat.AimTransition, 20f), new StatEffect(UpgradeStat.AimMoveSpeed, 20f) } },
            new Entry { id = "beast_charge", displayName = "돌진", description = "적에게 몸을 던진다", tendency = Tendency.Beast, amount = 2, rarity = RewardRarity.Rare,
                        comingLater = true },
            new Entry { id = "beast_dodge", displayName = "회피", description = "구르기가 길고 빨라진다", tendency = Tendency.Beast, amount = 2, rarity = RewardRarity.Rare,
                        effects = new[] { new StatEffect(UpgradeStat.RollSpeed, 40f), new StatEffect(UpgradeStat.RollRecovery, 40f) } },
            new Entry { id = "human_shotgun", displayName = "산탄총", description = "가까운 적 여럿을 한 번에", tendency = Tendency.Human, amount = 2, rarity = RewardRarity.Rare,
                        comingLater = true },
            new Entry { id = "human_explosive", displayName = "폭발물", description = "모여 있는 적을 날린다", tendency = Tendency.Human, amount = 2, rarity = RewardRarity.Rare,
                        comingLater = true },
            new Entry { id = "weapon_pistol", displayName = "권총", description = "우클릭으로 조준하고 좌클릭으로 쏜다", tendency = Tendency.Human, amount = 0, rarity = RewardRarity.Common,
                        unlock = RewardUnlock.Pistol, guaranteed = true, guaranteeFromDungeon = 0 },
            new Entry { id = "beast_frenzy", displayName = "광폭", description = "본능이 터져 맨손이 훨씬 세진다", tendency = Tendency.Beast, amount = 2, rarity = RewardRarity.Rare,
                        effects = new[] { new StatEffect(UpgradeStat.MeleeDamage, 40f) }, metaLocked = true },
            new Entry { id = "human_hollowpoint", displayName = "할로포인트", description = "권총 탄이 맞으면 퍼진다", tendency = Tendency.Human, amount = 2, rarity = RewardRarity.Rare,
                        effects = new[] { new StatEffect(UpgradeStat.PistolDamage, 40f) }, metaLocked = true },
        };

        public List<RewardDefinition> ToDefinitions()
        {
            var list = new List<RewardDefinition>(entries.Length);
            foreach (Entry e in entries) list.Add(new RewardDefinition(e.id, e.displayName, e.description, e.tendency, e.amount, e.rarity, e.effects, e.comingLater,
                                                               e.unlock, e.guaranteed ? e.guaranteeFromDungeon : -1, e.metaLocked));
            return list;
        }
    }
}
