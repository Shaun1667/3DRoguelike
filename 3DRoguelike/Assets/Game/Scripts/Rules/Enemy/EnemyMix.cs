using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>적 종. 새 종 = 끝에 한 줄(값이 데이터 · 시드 결과에 남는다).</summary>
    public enum EnemySpecies
    {
        Badger, // 오소리: 근접 할퀴기
        Boar,   // 멧돼지: 돌진 탱커
        Hare,   // 멧토끼: 빠른 떼
        Toad,   // 두꺼비: 원거리 독액
    }

    /// <summary>같은 종의 두 모습(결정 2026-11-12, 사용자). E 개체는 체력이 더 많고 공격이 하나 더 있다.</summary>
    public enum EnemyGrade
    {
        Mutant,  // 네 발, 본능
        Evolved, // E 개체, 두 발
    }

    /// <summary>나올 적 하나의 종류: 종 + 모습.</summary>
    public readonly struct EnemyKind : IEquatable<EnemyKind>
    {
        public EnemyKind(EnemySpecies species, EnemyGrade grade)
        {
            Species = species;
            Grade = grade;
        }

        public EnemySpecies Species { get; }
        public EnemyGrade Grade { get; }

        public bool Equals(EnemyKind other) => Species == other.Species && Grade == other.Grade;
        public override bool Equals(object obj) => obj is EnemyKind other && Equals(other);
        public override int GetHashCode() => (int)Species * 8 + (int)Grade;
        public override string ToString() => $"{Species}/{Grade}";
    }

    /// <summary>종마다 나오는 방식: 방 적 수(자리)를 몇 개 차지하고, 한 번에 몇 마리가 떼로 나오나.</summary>
    public sealed class EnemySpeciesRule
    {
        public EnemySpeciesRule(EnemySpecies species, int slots, int groupMin, int groupMax, float groupSpread)
        {
            if (slots < 1) throw new ArgumentOutOfRangeException(nameof(slots));
            if (groupMin < 1) throw new ArgumentOutOfRangeException(nameof(groupMin));
            if (groupMax < groupMin) throw new ArgumentOutOfRangeException(nameof(groupMax));
            if (groupSpread < 0f) throw new ArgumentOutOfRangeException(nameof(groupSpread));

            Species = species;
            Slots = slots;
            GroupMin = groupMin;
            GroupMax = groupMax;
            GroupSpread = groupSpread;
        }

        public EnemySpecies Species { get; }
        /// <summary>한 번 뽑히면 방 적 수에서 이만큼 쓴다.</summary>
        public int Slots { get; }
        /// <summary>한 번 뽑히면 나오는 마릿수 범위(멧토끼 떼 3~5).</summary>
        public int GroupMin { get; }
        public int GroupMax { get; }
        /// <summary>떼가 둘러서는 반지름(m).</summary>
        public float GroupSpread { get; }
    }

    /// <summary>진행 단계(던전 순서) 하나의 종 비중과 E 개체 비율.</summary>
    public sealed class EnemyStage
    {
        readonly List<(EnemySpecies species, int weight)> weights;

        public EnemyStage(IEnumerable<(EnemySpecies species, int weight)> weights, float evolvedChance)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (evolvedChance < 0f || evolvedChance > 1f) throw new ArgumentOutOfRangeException(nameof(evolvedChance));

            this.weights = new List<(EnemySpecies, int)>();
            int total = 0;
            foreach ((EnemySpecies species, int weight) in weights)
            {
                if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be 0 or more.");
                if (weight == 0) continue;
                this.weights.Add((species, weight));
                total += weight;
            }
            if (total == 0) throw new ArgumentException("A stage needs at least one species with weight.", nameof(weights));
            EvolvedChance = evolvedChance;
        }

        /// <summary>무게가 0보다 큰 종만.</summary>
        public IReadOnlyList<(EnemySpecies species, int weight)> Weights => weights;
        /// <summary>한 번 뽑을 때 E 개체로 나올 확률(떼는 다 같이).</summary>
        public float EvolvedChance { get; }
    }

    /// <summary>적 구성 표: 종마다 나오는 방식 + 단계마다 비중. 값은 BALANCE.md 14번(`Data/EnemyRoster.asset`).</summary>
    public sealed class EnemyMix
    {
        readonly Dictionary<EnemySpecies, EnemySpeciesRule> rules = new Dictionary<EnemySpecies, EnemySpeciesRule>();
        readonly List<EnemyStage> stages;

        public EnemyMix(IEnumerable<EnemySpeciesRule> species, IEnumerable<EnemyStage> stages)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            if (stages == null) throw new ArgumentNullException(nameof(stages));

            foreach (EnemySpeciesRule rule in species)
            {
                if (rule == null) throw new ArgumentNullException(nameof(species));
                if (rules.ContainsKey(rule.Species)) throw new ArgumentException($"Species {rule.Species} is listed twice.", nameof(species));
                rules.Add(rule.Species, rule);
            }
            this.stages = new List<EnemyStage>(stages);
            if (this.stages.Count == 0) throw new ArgumentException("At least one stage is needed.", nameof(stages));
            foreach (EnemyStage stage in this.stages)
            {
                if (stage == null) throw new ArgumentNullException(nameof(stages));
                foreach ((EnemySpecies kind, int _) in stage.Weights)
                {
                    if (!rules.ContainsKey(kind)) throw new ArgumentException($"Stage uses species {kind} that has no rule.", nameof(stages));
                }
            }
        }

        public int StageCount => stages.Count;

        /// <summary>단계 번호의 비중. 표보다 뒤 단계는 마지막 단계를 쓴다.</summary>
        public EnemyStage Stage(int index) => stages[Math.Max(0, Math.Min(index, stages.Count - 1))];

        public EnemySpeciesRule Rule(EnemySpecies species) => rules[species];

        /// <summary>한 종 · Mutant만 나오는 표(종 구성 이전의 2-4 Mutant와 같은 결과).</summary>
        public static EnemyMix Single(EnemySpecies species)
        {
            return new EnemyMix(
                new[] { new EnemySpeciesRule(species, 1, 1, 1, 0f) },
                new[] { new EnemyStage(new[] { (species, 1) }, 0f) });
        }
    }

    /// <summary>한 번 뽑은 적: 종류와 마릿수(떼).</summary>
    public readonly struct EnemyPick
    {
        public EnemyPick(EnemyKind kind, int groupSize)
        {
            Kind = kind;
            GroupSize = groupSize;
        }

        public EnemyKind Kind { get; }
        public int GroupSize { get; }

        public override string ToString() => GroupSize == 1 ? Kind.ToString() : $"{Kind}x{GroupSize}";
    }

    /// <summary>
    /// 적 수(자리)를 종 · 모습 · 떼로 나눈다. 난수는 적 용도로 받고, 고를 것이 하나뿐인 경우에는 뽑지 않는다
    /// (그래서 한 종 · Mutant만인 표는 종 구성 이전과 같은 수열을 쓴다).
    /// </summary>
    public static class EnemyMixPlanner
    {
        /// <param name="slots">쓸 자리 수. 남은 자리로 살 수 있는 종이 없으면 거기서 멈춘다.</param>
        /// <param name="allowEvolved">false면 E 개체 없이(길 위 무리).</param>
        public static List<EnemyPick> Pick(EnemyMix mix, int stage, int slots, bool allowEvolved, IRandom random)
        {
            if (mix == null) throw new ArgumentNullException(nameof(mix));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (slots < 0) throw new ArgumentOutOfRangeException(nameof(slots));

            EnemyStage table = mix.Stage(stage);
            var picks = new List<EnemyPick>();
            var affordable = new List<(EnemySpecies species, int weight)>(table.Weights.Count);
            int remaining = slots;
            while (remaining > 0)
            {
                affordable.Clear();
                int total = 0;
                foreach ((EnemySpecies species, int weight) in table.Weights)
                {
                    if (mix.Rule(species).Slots > remaining) continue;
                    affordable.Add((species, weight));
                    total += weight;
                }
                if (affordable.Count == 0) break;

                EnemySpecies chosen = affordable[0].species;
                if (affordable.Count > 1)
                {
                    int roll = random.Next(total);
                    foreach ((EnemySpecies species, int weight) in affordable)
                    {
                        if (roll < weight)
                        {
                            chosen = species;
                            break;
                        }
                        roll -= weight;
                    }
                }

                float chance = allowEvolved ? table.EvolvedChance : 0f;
                bool evolved = chance >= 1f || (chance > 0f && random.NextFloat() < chance);

                EnemySpeciesRule rule = mix.Rule(chosen);
                int size = rule.GroupMin == rule.GroupMax ? rule.GroupMin : random.Range(rule.GroupMin, rule.GroupMax + 1);
                picks.Add(new EnemyPick(new EnemyKind(chosen, evolved ? EnemyGrade.Evolved : EnemyGrade.Mutant), size));
                remaining -= rule.Slots;
            }
            return picks;
        }

        /// <summary>떼가 가운데를 둘러서는 자리(가운데 기준 x, z). 한 마리면 가운데.</summary>
        /// <param name="startAngle">첫 자리의 각도(라디안).</param>
        public static (float x, float z)[] GroupOffsets(int size, float spread, float startAngle)
        {
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));
            var offsets = new (float x, float z)[size];
            if (size == 1) return offsets;
            for (int i = 0; i < size; i++)
            {
                double angle = startAngle + i * Math.PI * 2.0 / size;
                offsets[i] = ((float)(Math.Cos(angle) * spread), (float)(Math.Sin(angle) * spread));
            }
            return offsets;
        }
    }
}
