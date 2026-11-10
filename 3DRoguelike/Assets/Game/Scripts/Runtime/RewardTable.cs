using System;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>보상 목록. 새 보상 = 표에 한 줄. 효과(피해 · 속도 등)는 3-2 강화 표가 Id로 붙인다 — 3-1에서는 성향만 오른다.</summary>
    [CreateAssetMenu(menuName = "Game/Reward Table")]
    public sealed class RewardTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("바꾸지 않는 이름표(저장 · 3-2 효과 연결에 쓴다)")]
            public string id;
            public string displayName;
            [TextArea] public string description;
            public Tendency tendency;
            [Min(0)] public int amount;
            public RewardRarity rarity;
        }

        public Entry[] entries =
        {
            new Entry { id = "beast_strength", displayName = "근력", description = "맨손 공격이 묵직해진다", tendency = Tendency.Beast, amount = 1, rarity = RewardRarity.Common },
            new Entry { id = "beast_move", displayName = "이동", description = "더 빨리 움직인다", tendency = Tendency.Beast, amount = 1, rarity = RewardRarity.Common },
            new Entry { id = "beast_regen", displayName = "재생", description = "상처가 빨리 아문다", tendency = Tendency.Beast, amount = 1, rarity = RewardRarity.Common },
            new Entry { id = "human_pistol", displayName = "권총 손질", description = "권총이 더 세게 맞는다", tendency = Tendency.Human, amount = 1, rarity = RewardRarity.Common },
            new Entry { id = "human_magazine", displayName = "탄창", description = "한 번에 더 많이 쏜다", tendency = Tendency.Human, amount = 1, rarity = RewardRarity.Common },
            new Entry { id = "human_aim", displayName = "조준", description = "조준이 빨라진다", tendency = Tendency.Human, amount = 1, rarity = RewardRarity.Common },
            new Entry { id = "beast_charge", displayName = "돌진", description = "적에게 몸을 던진다", tendency = Tendency.Beast, amount = 2, rarity = RewardRarity.Rare },
            new Entry { id = "beast_dodge", displayName = "회피", description = "구르기가 길고 빨라진다", tendency = Tendency.Beast, amount = 2, rarity = RewardRarity.Rare },
            new Entry { id = "human_shotgun", displayName = "산탄총", description = "가까운 적 여럿을 한 번에", tendency = Tendency.Human, amount = 2, rarity = RewardRarity.Rare },
            new Entry { id = "human_explosive", displayName = "폭발물", description = "모여 있는 적을 날린다", tendency = Tendency.Human, amount = 2, rarity = RewardRarity.Rare },
        };

        public List<RewardDefinition> ToDefinitions()
        {
            var list = new List<RewardDefinition>(entries.Length);
            foreach (Entry e in entries) list.Add(new RewardDefinition(e.id, e.displayName, e.description, e.tendency, e.amount, e.rarity));
            return list;
        }
    }
}
