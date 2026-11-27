using System;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 판 밖 성장 트리 표(3-7) + 영구 재화 버는 양. 새 노드 = 표 한 줄(id · 이름 · 설명 · 갈래 · 비용 · 앞 노드 · 효과 · 여는 보상).
    /// 효과 구조는 보상 표(<see cref="RewardTable"/>)와 같다(<see cref="StatEffect"/>). 규칙은 <see cref="MetaTree"/> · <see cref="MetaProgress"/>.
    /// 값은 BALANCE 16번.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Meta Tree")]
    public sealed class MetaTreeTable : ScriptableObject
    {
        [Serializable]
        public struct Node
        {
            [Tooltip("바꾸지 않는 이름표(판 밖 저장 · 판 저장에 쓴다)")]
            public string id;
            public string displayName;
            [TextArea] public string description;
            [Tooltip("화면 열(Beast 왼쪽 · Human 오른쪽). 판 안 성향은 올리지 않는다")]
            public Tendency branch;
            [Min(0)] public int cost;
            [Tooltip("먼저 사야 하는 노드 id. 비면 뿌리")]
            public string requires;
            [Tooltip("판을 시작할 때 더하는 능력치 효과(보상 표와 같은 구조, 퍼센트)")]
            public StatEffect[] effects;
            [Tooltip("사면 판의 보상 후보에 넣는 보상 id(RewardTable에서 metaLocked인 줄)")]
            public string opensReward;
        }

        [Header("영구 재화 (판이 끝날 때 판 기록으로 계산)")]
        [Min(0)] public int perKill = 1;
        [Min(0)] public int perRoom = 3;
        [Min(0)] public int perDungeon = 20;
        [Min(0)] public int perRegionClear = 30;

        [Header("노드 (표 순서 = 화면 순서)")]
        public Node[] nodes =
        {
            new Node { id = "meta_beast_claw", displayName = "단단한 발톱", description = "맨손 공격이 조금 세진다", branch = Tendency.Beast, cost = 20,
                       effects = new[] { new StatEffect(UpgradeStat.MeleeDamage, 10f) } },
            new Node { id = "meta_beast_legs", displayName = "튼튼한 다리", description = "조금 더 빨리 움직인다", branch = Tendency.Beast, cost = 40, requires = "meta_beast_claw",
                       effects = new[] { new StatEffect(UpgradeStat.MoveSpeed, 5f) } },
            new Node { id = "meta_beast_regen", displayName = "초재생", description = "방을 깰 때마다 상처가 조금 아문다", branch = Tendency.Beast, cost = 40, requires = "meta_beast_claw",
                       effects = new[] { new StatEffect(UpgradeStat.HealOnRoomClear, 10f) } },
            new Node { id = "meta_beast_frenzy", displayName = "광폭의 기억", description = "보상 후보에 희귀 '광폭'이 나온다", branch = Tendency.Beast, cost = 60, requires = "meta_beast_legs",
                       opensReward = "beast_frenzy" },
            new Node { id = "meta_human_grip", displayName = "익숙한 손", description = "권총이 조금 세게 맞는다", branch = Tendency.Human, cost = 20,
                       effects = new[] { new StatEffect(UpgradeStat.PistolDamage, 10f) } },
            new Node { id = "meta_human_mag", displayName = "여분 탄창", description = "탄창이 조금 커진다", branch = Tendency.Human, cost = 40, requires = "meta_human_grip",
                       effects = new[] { new StatEffect(UpgradeStat.PistolMagazine, 10f) } },
            new Node { id = "meta_human_aim", displayName = "침착한 조준", description = "조준이 조금 빨라진다", branch = Tendency.Human, cost = 40, requires = "meta_human_grip",
                       effects = new[] { new StatEffect(UpgradeStat.AimTransition, 10f), new StatEffect(UpgradeStat.AimMoveSpeed, 10f) } },
            new Node { id = "meta_human_hollow", displayName = "할로포인트 설계도", description = "보상 후보에 희귀 '할로포인트'가 나온다", branch = Tendency.Human, cost = 60, requires = "meta_human_mag",
                       opensReward = "human_hollowpoint" },
        };

        public MetaTree ToTree()
        {
            var list = new List<MetaNode>(nodes.Length);
            foreach (Node n in nodes) list.Add(new MetaNode(n.id, n.displayName, n.description, n.branch, n.cost, n.requires, n.effects, n.opensReward));
            return new MetaTree(list);
        }

        public MetaEarnings ToEarnings() => new MetaEarnings(perKill, perRoom, perDungeon, perRegionClear);
    }
}
