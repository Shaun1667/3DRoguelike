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
        RaccoonDog, // 너구리: 작고 약한 떼, 할퀴고 문다(도시, 2026-11-27)
        Fox,        // 여우: 달려들어 물고 빠지는 히트앤런(도시, 2026-11-27)
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

    /// <summary>적이 나오는 곳. 단계마다 곳에 따라 다른 비중을 쓸 수 있다(비우면 던전 방 비중).</summary>
    public enum EnemyPlace
    {
        Room,     // 던전 방(시작 방에서 먼 방 · 보스 방)
        NearRoom, // 던전 앞 방(시작 방에서 NearRoomDistance 이하) — 첫 방이 너무 세지 않게
        Road,     // 길 위 무리(E 개체 없음)
    }

    /// <summary>진행 단계(던전 순서) 하나의 종 비중과 E 개체 비율. 앞 방 · 길 위 비중을 따로 줄 수 있다.</summary>
    public sealed class EnemyStage
    {
        readonly List<(EnemySpecies species, int weight)> weights;
        readonly List<(EnemySpecies species, int weight)> nearWeights;
        readonly List<(EnemySpecies species, int weight)> roadWeights;

        /// <param name="nearWeights">시작 방에서 nearRoomDistance 이하인 방의 비중. null이면 weights</param>
        /// <param name="nearRoomDistance">앞 방으로 치는 시작 방에서의 거리(문 수). 0이면 앞 방 없음</param>
        /// <param name="roadWeights">길 위 무리 비중. null이면 weights</param>
        public EnemyStage(IEnumerable<(EnemySpecies species, int weight)> weights, float evolvedChance,
            IEnumerable<(EnemySpecies species, int weight)> nearWeights, int nearRoomDistance,
            IEnumerable<(EnemySpecies species, int weight)> roadWeights)
            : this(weights, evolvedChance)
        {
            if (nearRoomDistance < 0) throw new ArgumentOutOfRangeException(nameof(nearRoomDistance));
            if (nearWeights != null) this.nearWeights = ReadWeights(nearWeights, nameof(nearWeights));
            if (roadWeights != null) this.roadWeights = ReadWeights(roadWeights, nameof(roadWeights));
            NearRoomDistance = nearRoomDistance;
        }

        public EnemyStage(IEnumerable<(EnemySpecies species, int weight)> weights, float evolvedChance)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (evolvedChance < 0f || evolvedChance > 1f) throw new ArgumentOutOfRangeException(nameof(evolvedChance));

            this.weights = ReadWeights(weights, nameof(weights));
            EvolvedChance = evolvedChance;
        }

        static List<(EnemySpecies, int)> ReadWeights(IEnumerable<(EnemySpecies species, int weight)> source, string name)
        {
            var list = new List<(EnemySpecies, int)>();
            int total = 0;
            foreach ((EnemySpecies species, int weight) in source)
            {
                if (weight < 0) throw new ArgumentOutOfRangeException(name, "Weights must be 0 or more.");
                if (weight == 0) continue;
                list.Add((species, weight));
                total += weight;
            }
            if (total == 0) throw new ArgumentException("A stage needs at least one species with weight.", name);
            return list;
        }

        /// <summary>던전 방 비중. 무게가 0보다 큰 종만.</summary>
        public IReadOnlyList<(EnemySpecies species, int weight)> Weights => weights;
        /// <summary>앞 방 비중(따로 없으면 <see cref="Weights"/>).</summary>
        public IReadOnlyList<(EnemySpecies species, int weight)> NearWeights => nearWeights ?? weights;
        /// <summary>길 위 무리 비중(따로 없으면 <see cref="Weights"/>).</summary>
        public IReadOnlyList<(EnemySpecies species, int weight)> RoadWeights => roadWeights ?? weights;
        /// <summary>시작 방에서 이 거리(문 수) 이하인 방이 앞 방. 0이면 앞 방 없음.</summary>
        public int NearRoomDistance { get; }

        public IReadOnlyList<(EnemySpecies species, int weight)> WeightsAt(EnemyPlace place) =>
            place == EnemyPlace.Road ? RoadWeights : place == EnemyPlace.NearRoom ? NearWeights : Weights;

        /// <summary>시작 방에서 distance만큼 떨어진 던전 방은 앞 방인가 · 먼 방인가.</summary>
        public EnemyPlace RoomPlace(int distanceFromStart) =>
            distanceFromStart <= NearRoomDistance ? EnemyPlace.NearRoom : EnemyPlace.Room;
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
                foreach ((EnemySpecies kind, int _) in AllWeights(stage))
                {
                    if (!rules.ContainsKey(kind)) throw new ArgumentException($"Stage uses species {kind} that has no rule.", nameof(stages));
                }
            }
        }

        static IEnumerable<(EnemySpecies, int)> AllWeights(EnemyStage stage)
        {
            foreach ((EnemySpecies, int) w in stage.Weights) yield return w;
            foreach ((EnemySpecies, int) w in stage.NearWeights) yield return w;
            foreach ((EnemySpecies, int) w in stage.RoadWeights) yield return w;
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
        /// <summary>던전 방 비중으로 뽑는다.</summary>
        /// <param name="slots">쓸 자리 수. 남은 자리로 살 수 있는 종이 없으면 거기서 멈춘다.</param>
        /// <param name="allowEvolved">false면 E 개체 없이.</param>
        public static List<EnemyPick> Pick(EnemyMix mix, int stage, int slots, bool allowEvolved, IRandom random)
        {
            return Pick(mix, stage, EnemyPlace.Room, slots, allowEvolved, random);
        }

        /// <summary>곳(던전 방 · 앞 방 · 길 위)의 비중으로 뽑는다. 길 위는 E 개체 없음.</summary>
        public static List<EnemyPick> Pick(EnemyMix mix, int stage, EnemyPlace place, int slots, IRandom random)
        {
            return Pick(mix, stage, place, slots, place != EnemyPlace.Road, random);
        }

        static List<EnemyPick> Pick(EnemyMix mix, int stage, EnemyPlace place, int slots, bool allowEvolved, IRandom random)
        {
            if (mix == null) throw new ArgumentNullException(nameof(mix));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (slots < 0) throw new ArgumentOutOfRangeException(nameof(slots));

            EnemyStage table = mix.Stage(stage);
            var picks = new List<EnemyPick>();
            IReadOnlyList<(EnemySpecies species, int weight)> weights = table.WeightsAt(place);
            var affordable = new List<(EnemySpecies species, int weight)>(weights.Count);
            int remaining = slots;
            while (remaining > 0)
            {
                affordable.Clear();
                int total = 0;
                foreach ((EnemySpecies species, int weight) in weights)
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
