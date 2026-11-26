using System;
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 숲 적 구성 표(`Data/EnemyRoster.asset`, BALANCE.md 14번): 종마다 프리팹(Mutant · E 개체) · 떼 크기 · 차지하는 자리,
    /// 단계(던전 순서)마다 종 비중 · E 개체 비율. 결정 2026-11-16(사용자): 단계별로 늘리기 — 던전 1까지 오소리 위주 + 멧토끼 떼, 던전 2부터 멧돼지 · 두꺼비.
    /// 결정 2026-11-25(사용자): 던전 1 방에도 멧돼지 · 두꺼비(시작 방 바로 옆 앞 방 · 길 위 무리는 오소리 · 멧토끼 그대로).
    /// 길 위 무리는 그 앞 던전의 단계를 쓰되 Mutant만(roadWeights가 있으면 그 비중). 새 종 = species에 한 줄 + 단계마다 무게. 프리팹 칸은 `Tools/Game/Setup Test Room`이 채운다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Enemy Roster")]
    public sealed class EnemyRoster : ScriptableObject
    {
        [Serializable]
        public struct SpeciesEntry
        {
            public EnemySpecies species;
            public GameObject mutantPrefab;
            public GameObject evolvedPrefab;
            [Tooltip("한 번 뽑히면 방 적 수(자리)에서 이만큼 쓴다")]
            [Min(1)] public int slots;
            [Tooltip("한 번 뽑히면 나오는 마릿수(떼) 최소 · 최대")]
            [Min(1)] public int groupMin;
            [Min(1)] public int groupMax;
            [Tooltip("떼가 둘러서는 반지름(m)")]
            [Min(0f)] public float groupSpread;
        }

        [Serializable]
        public struct Weight
        {
            public EnemySpecies species;
            [Min(0)] public int weight;
        }

        [Serializable]
        public struct Stage
        {
            public string name;
            [Tooltip("한 번 뽑을 때 E 개체일 확률(떼는 다 같이). 길 위 무리는 늘 0")]
            [Range(0f, 1f)] public float evolvedChance;
            [Tooltip("던전 방 비중(앞 방 · 길 위 비중이 비면 이것)")]
            public Weight[] weights;
            [Tooltip("시작 방에서 이 거리(문 수) 이하인 방은 nearWeights. 0이면 없음 — 첫 방이 너무 세지 않게")]
            [Min(0)] public int nearRoomDistance;
            [Tooltip("앞 방 비중. 비면 weights")]
            public Weight[] nearWeights;
            [Tooltip("이 던전 앞 길 위 무리 비중(E 개체 없음). 비면 weights")]
            public Weight[] roadWeights;
        }

        public SpeciesEntry[] species =
        {
            new SpeciesEntry { species = EnemySpecies.Badger, slots = 1, groupMin = 1, groupMax = 1 },
            new SpeciesEntry { species = EnemySpecies.Boar, slots = 2, groupMin = 1, groupMax = 1 },
            new SpeciesEntry { species = EnemySpecies.Hare, slots = 2, groupMin = 3, groupMax = 5, groupSpread = 1.5f },
            new SpeciesEntry { species = EnemySpecies.Toad, slots = 1, groupMin = 1, groupMax = 1 },
        };

        [Tooltip("던전 순서대로(0 = 숲 던전 1). 표보다 뒤 던전은 마지막 단계")]
        public Stage[] stages =
        {
            new Stage
            {
                name = "숲 던전 1 (+ 그 앞 길)", evolvedChance = 0f,
                // 결정 2026-11-25(사용자) "멧돼지 두꺼비를 던전 1에 섞어줘" — 비중 · 앞 방은 정한 것(Claude, 확인 필요)
                weights = new[] { W(EnemySpecies.Badger, 3), W(EnemySpecies.Hare, 1), W(EnemySpecies.Boar, 1), W(EnemySpecies.Toad, 1) },
                nearRoomDistance = 1,
                nearWeights = new[] { W(EnemySpecies.Badger, 3), W(EnemySpecies.Hare, 1) },
                roadWeights = new[] { W(EnemySpecies.Badger, 3), W(EnemySpecies.Hare, 1) },
            },
            new Stage
            {
                name = "도시 던전 (+ 그 앞 길) — 전 숲 던전 2 단계", evolvedChance = 0.25f,
                weights = new[] { W(EnemySpecies.Badger, 3), W(EnemySpecies.Hare, 1), W(EnemySpecies.Boar, 1), W(EnemySpecies.Toad, 1) },
            },
            new Stage
            {
                name = "세 번째 던전 (+ 그 앞 길) — 아직 없음(전 숲 던전 3 단계)", evolvedChance = 0.5f,
                weights = new[] { W(EnemySpecies.Badger, 2), W(EnemySpecies.Hare, 1), W(EnemySpecies.Boar, 2), W(EnemySpecies.Toad, 2) },
            },
        };

        private static Weight W(EnemySpecies kind, int weight) => new Weight { species = kind, weight = weight };

        public EnemyMix ToMix()
        {
            var rules = new List<EnemySpeciesRule>(species.Length);
            foreach (SpeciesEntry entry in species)
            {
                rules.Add(new EnemySpeciesRule(entry.species, entry.slots, entry.groupMin, entry.groupMax, entry.groupSpread));
            }
            var tables = new List<EnemyStage>(stages.Length);
            foreach (Stage stage in stages)
            {
                tables.Add(new EnemyStage(Pairs(stage.weights), stage.evolvedChance,
                    Pairs(stage.nearWeights), stage.nearRoomDistance, Pairs(stage.roadWeights)));
            }
            return new EnemyMix(rules, tables);
        }

        /// <summary>표의 무게 줄. 비었으면 null(던전 방 비중을 씀).</summary>
        private static List<(EnemySpecies, int)> Pairs(Weight[] weights)
        {
            if (weights == null || weights.Length == 0) return null;
            var pairs = new List<(EnemySpecies, int)>(weights.Length);
            foreach (Weight weight in weights) pairs.Add((weight.species, weight.weight));
            return pairs;
        }

        public bool TryGetEntry(EnemySpecies kind, out SpeciesEntry entry)
        {
            foreach (SpeciesEntry candidate in species)
            {
                if (candidate.species != kind) continue;
                entry = candidate;
                return true;
            }
            entry = default;
            return false;
        }

        public GameObject PrefabFor(EnemyKind kind)
        {
            if (!TryGetEntry(kind.Species, out SpeciesEntry entry)) return null;
            GameObject prefab = kind.Grade == EnemyGrade.Evolved ? entry.evolvedPrefab : entry.mutantPrefab;
            return prefab != null ? prefab : entry.mutantPrefab;
        }

        /// <summary>한 마리를 내보낸다. 프리팹이 없으면 null(경고).</summary>
        public Enemy Spawn(EnemyKind kind, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject prefab = PrefabFor(kind);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemyRoster] {kind} 프리팹이 없습니다. Tools/Game/Setup Test Room을 다시 실행하세요.");
                return null;
            }
            GameObject instance = Instantiate(prefab, position, rotation, parent);
            instance.name = $"{kind.Species}_{(kind.Grade == EnemyGrade.Evolved ? "E" : "Mutant")}";
            return instance.GetComponent<Enemy>();
        }

        /// <summary>떼 하나를 가운데 둘레에 내보낸다(길 위 무리 · 디버그 소환). 모두 가운데의 플레이어 쪽을 본다.</summary>
        public List<Enemy> SpawnGroup(EnemyKind kind, int size, Vector3 center, Quaternion rotation, Transform parent)
        {
            float spread = TryGetEntry(kind.Species, out SpeciesEntry entry) ? entry.groupSpread : 0f;
            var spawned = new List<Enemy>(size);
            foreach ((float x, float z) in EnemyMixPlanner.GroupOffsets(Mathf.Max(1, size), spread, 0f))
            {
                Enemy enemy = Spawn(kind, center + new Vector3(x, 0f, z), rotation, parent);
                if (enemy != null) spawned.Add(enemy);
            }
            return spawned;
        }
    }
}
