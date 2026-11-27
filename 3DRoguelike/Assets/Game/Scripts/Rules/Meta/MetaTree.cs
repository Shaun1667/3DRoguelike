using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 판 밖 성장 트리(3-7)의 노드 하나 = 표 한 줄: id · 이름 · 설명 · 갈래(Beast/Human, 화면 열) · 비용 · 앞 노드 · 효과.
    /// 효과는 보상과 같은 능력치 강화(<see cref="StatEffect"/>)와, 잠긴 보상 후보를 여는 보상 id(<see cref="OpensReward"/>).
    /// 갈래는 화면을 나누는 데만 쓴다 — 판 안 성향(Beast/Human 수치)은 올리지 않는다(핵심 설계 규칙 4).
    /// </summary>
    public sealed class MetaNode
    {
        public MetaNode(string id, string name, string description, Tendency branch, int cost, string requires,
                        IReadOnlyList<StatEffect> effects = null, string opensReward = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Meta node id is required.", nameof(id));
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            Id = id;
            Name = string.IsNullOrEmpty(name) ? id : name;
            Description = description ?? "";
            Branch = branch;
            Cost = cost;
            Requires = string.IsNullOrEmpty(requires) ? null : requires;
            Effects = effects == null ? (IReadOnlyList<StatEffect>)Array.Empty<StatEffect>() : new List<StatEffect>(effects).AsReadOnly();
            OpensReward = string.IsNullOrEmpty(opensReward) ? null : opensReward;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>화면 열(Beast 왼쪽 · Human 오른쪽).</summary>
        public Tendency Branch { get; }
        /// <summary>영구 재화 비용.</summary>
        public int Cost { get; }
        /// <summary>먼저 사야 하는 노드 id. 없으면 null(뿌리).</summary>
        public string Requires { get; }
        /// <summary>판을 시작할 때 강화 합계에 더하는 능력치 효과.</summary>
        public IReadOnlyList<StatEffect> Effects { get; }
        /// <summary>사면 판의 보상 후보에 들어가는 보상 id(표에서 잠긴 줄, <see cref="RewardDefinition.MetaLocked"/>). 없으면 null.</summary>
        public string OpensReward { get; }
    }

    /// <summary>성장 트리 전체. id가 겹치거나 앞 노드가 없거나 고리가 있으면 만들 때 막는다.</summary>
    public sealed class MetaTree
    {
        readonly List<MetaNode> nodes;
        readonly Dictionary<string, MetaNode> byId = new Dictionary<string, MetaNode>();

        public MetaTree(IEnumerable<MetaNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            this.nodes = new List<MetaNode>();
            foreach (MetaNode node in nodes)
            {
                if (node == null) throw new ArgumentException("Null meta node.", nameof(nodes));
                if (byId.ContainsKey(node.Id)) throw new ArgumentException($"Meta node id '{node.Id}' is used twice.", nameof(nodes));
                byId.Add(node.Id, node);
                this.nodes.Add(node);
            }
            foreach (MetaNode node in this.nodes)
            {
                if (node.Requires != null && !byId.ContainsKey(node.Requires))
                    throw new ArgumentException($"Meta node '{node.Id}' requires unknown node '{node.Requires}'.", nameof(nodes));
                // 고리: 앞 노드를 따라가다 자기로 돌아오면 안 된다
                var seen = new HashSet<string> { node.Id };
                for (string step = node.Requires; step != null; step = byId[step].Requires)
                {
                    if (!seen.Add(step)) throw new ArgumentException($"Meta node '{node.Id}' has a requirement loop.", nameof(nodes));
                }
            }
        }

        /// <summary>표 순서.</summary>
        public IReadOnlyList<MetaNode> Nodes => nodes;
        public int Count => nodes.Count;

        public bool TryGet(string id, out MetaNode node)
        {
            node = null;
            return id != null && byId.TryGetValue(id, out node);
        }

        /// <summary>모든 노드 비용 합.</summary>
        public int TotalCost
        {
            get
            {
                int sum = 0;
                foreach (MetaNode node in nodes) sum += node.Cost;
                return sum;
            }
        }
    }
}
