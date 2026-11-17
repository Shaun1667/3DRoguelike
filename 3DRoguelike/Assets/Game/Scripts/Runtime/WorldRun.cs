using System.Collections;
using System.Collections.Generic;
using Game.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// 숲 월드(고정 지형 · 진행 길). 판을 시작하거나 이어받고(<see cref="CurrentRun"/>), 길 위 적 무리를 내보내고,
    /// 다음 던전 입구에 들어서면 층 씬으로 보낸다. 던전을 깨고 돌아오면 그 입구 앞에 서고 뒤 관문이 열려 있다.
    /// 지형 · 나무 · 입구 · 관문은 `WorldSetup`(메뉴 Tools/Game/Setup World)이 씬에 세워 둔다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // 플레이어 자리 · 체력을 HUD · 카메라보다 먼저 맞춘다
    public sealed class WorldRun : MonoBehaviour, IRunRestart
    {
        [SerializeField] private WorldTuning tuning;
        [SerializeField] private PlayerVitals player;
        [Tooltip("적 종 · 프리팹 · 비중. 길 위 무리는 그 앞 던전의 단계를 쓰되 Mutant만")]
        [SerializeField] private EnemyRoster roster;
        [SerializeField] private RunResultScreen resultScreen;
        [Tooltip("화면 위 가운데 안내(다음 던전 · 거리)")]
        [SerializeField] private TMP_Text objective;
        [Tooltip("길 시작(처음 · 죽은 뒤 여기서)")]
        [SerializeField] private Transform startPoint;
        [Tooltip("던전 순서대로. 앞(+z)이 길 쪽 — 돌아오면 그 앞에 선다")]
        [SerializeField] private Transform[] entrances;
        [Tooltip("입구 안쪽 어두운 면. 깬 던전은 회색으로")]
        [SerializeField] private Renderer[] doorways;
        [SerializeField] private Material clearedDoorwayMaterial;
        [Tooltip("던전 순서대로, 그 던전을 깨면 열리는 관문")]
        [SerializeField] private GameObject[] gates;
        [Tooltip("길 위 적 무리. 자식이 적이 나올 자리")]
        [SerializeField] private Transform[] encounterSites;
        [Tooltip("비워 두면 새 판마다 새 시드. 'XXXX-XXXX' 코드를 넣으면 그 던전들을 다시 만든다")]
        [SerializeField] private string fixedSeedCode = "";
        [Min(0f)] [SerializeField] private float resultDelayAfterDeath = 1.5f;
        [Tooltip("돌아올 때 입구 앞으로 떨어져 서는 거리(m)")]
        [Min(0.5f)] [SerializeField] private float returnDistance = 5f;

        private List<Health>[] alive;
        private bool[] started;
        private Transform playerTransform;
        private float nextObjectiveTime;
        private bool leaving;
        private EnemyMix mix;

        public RunSession Session { get; private set; }
        public WorldTuning Tuning => tuning;
        public EnemyRoster Roster => roster;

        private void Awake()
        {
            Time.timeScale = 1f;
            playerTransform = player.transform;
            Session = CurrentRun.EnsureSession(tuning.dungeons.Length, ChooseSeed);
            mix = roster.ToMix();

            int back = Session.ReturnDungeon;
            if (back >= 0 && back < entrances.Length)
            {
                Transform entrance = entrances[back];
                Place(entrance.position + entrance.forward * returnDistance, Quaternion.LookRotation(-entrance.forward));
            }
            else Place(startPoint.position, startPoint.rotation);

            alive = new List<Health>[encounterSites.Length];
            started = new bool[encounterSites.Length];
            for (int i = 0; i < encounterSites.Length; i++)
            {
                alive[i] = new List<Health>();
                started[i] = Session.World.IsEncounterCleared(i);
            }
            RefreshWorld();
            Debug.Log($"[WorldRun] 시드 {Session.Seed.ToCode()} · 깬 던전 {Session.World.ClearedCount}/{Session.World.DungeonCount}" +
                      (back >= 0 ? $" · {tuning.dungeons[back].displayName} 앞에서" : " · 길 시작에서"));
        }

        private void Start()
        {
            // PlayerVitals.Awake가 체력을 가득으로 만든 뒤, HUD.Start보다 먼저(실행 순서 -100)
            if (Session.PlayerHealth != RunSession.FullHealth) player.Health.SetCurrent(Session.PlayerHealth);
            player.GetComponent<PlayerUpgrades>().Use(Session.Stats.Upgrades);
            player.GetComponent<PlayerForm>().UpdateForm(Session.Stats.Beast, Session.Stats.Human);
        }

        private void OnEnable()
        {
            player.Health.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            player.Health.Died -= OnPlayerDied;
        }

        private void Update()
        {
            if (leaving || Session.Stats.Outcome != RunOutcome.Playing) return;
            Vector3 position = playerTransform.position;

            for (int i = 0; i < encounterSites.Length; i++)
            {
                if (!started[i] && Flat(encounterSites[i].position - position).magnitude <= tuning.encounterTriggerRange) SpawnEncounter(i);
            }

            int next = Session.World.NextDungeon;
            if (next >= 0 && next < entrances.Length && Flat(entrances[next].position - position).magnitude <= tuning.entranceRadius)
            {
                EnterDungeon(next);
                return;
            }

            if (objective != null && Time.unscaledTime >= nextObjectiveTime)
            {
                nextObjectiveTime = Time.unscaledTime + 0.25f;
                objective.text = ObjectiveText(position, next);
            }
        }

        public void Restart(bool sameSeed) => CurrentRun.RestartFromWorld(sameSeed);

        private RunSeed ChooseSeed()
        {
            if (!string.IsNullOrWhiteSpace(fixedSeedCode))
            {
                if (RunSeed.TryParse(fixedSeedCode, out RunSeed seed)) return seed;
                Debug.LogWarning($"[WorldRun] 시드 코드 '{fixedSeedCode}'를 읽지 못해 새 시드를 씁니다.");
            }
            return new RunSeed(unchecked((uint)System.Guid.NewGuid().GetHashCode()));
        }

        private void EnterDungeon(int dungeon)
        {
            if (!Session.EnterDungeon(dungeon, player.Health.Current)) return;
            leaving = true;
            Debug.Log($"[WorldRun] {tuning.dungeons[dungeon].displayName} 입장");
            SceneManager.LoadScene(CurrentRun.FloorScene);
        }

        /// <summary>
        /// 무리를 내보낸다. 자리 수(자식 수)를 종으로 나누는 것은 규칙 <see cref="EnemyMixPlanner"/>(길 난수, 무리마다 따로) — 같은 시드면 같은 무리.
        /// 단계는 그 무리 앞 던전, E 개체 없음(결정 2026-11-12). 떼(멧토끼)는 한 자리 둘레에 둘러선다.
        /// </summary>
        private void SpawnEncounter(int site)
        {
            started[site] = true;
            Transform points = encounterSites[site];
            IRandom random = new RunRandom(Session.Seed).Create(RandomPurpose.Road, site);
            List<EnemyPick> picks = EnemyMixPlanner.Pick(mix, tuning.DungeonAhead(site), points.childCount, allowEvolved: false, random);
            for (int i = 0; i < picks.Count && i < points.childCount; i++)
            {
                Vector3 position = points.GetChild(i).position;
                Vector3 toPlayer = Flat(playerTransform.position - position);
                Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
                foreach (Enemy enemy in roster.SpawnGroup(picks[i].Kind, picks[i].GroupSize, position, facing, transform))
                {
                    enemy.name = $"{enemy.name}_Road{site}_{i}";
                    Health health = enemy.Health;
                    health.Died += () => OnRoadEnemyDied(site, health);
                    alive[site].Add(health);
                }
            }
            if (alive[site].Count == 0) Session.World.ClearEncounter(site);
        }

        private void OnRoadEnemyDied(int site, Health enemy)
        {
            Session.Stats.RecordKill();
            alive[site].Remove(enemy);
            if (alive[site].Count == 0) Session.World.ClearEncounter(site);
        }

        /// <summary>깬 던전 입구는 회색, 깬 던전 뒤 관문은 열림.</summary>
        private void RefreshWorld()
        {
            for (int i = 0; i < gates.Length; i++) gates[i].SetActive(!Session.World.IsGateOpen(i));
            for (int i = 0; i < doorways.Length; i++)
            {
                if (Session.World.IsCleared(i) && clearedDoorwayMaterial != null) doorways[i].sharedMaterial = clearedDoorwayMaterial;
            }
        }

        private string ObjectiveText(Vector3 position, int next)
        {
            if (next < 0) return "숲을 모두 지났다";
            float distance = Flat(entrances[next].position - position).magnitude;
            return $"다음: {tuning.dungeons[next].displayName}  ·  {Mathf.RoundToInt(distance)} m  ({Session.World.ClearedCount}/{Session.World.DungeonCount})";
        }

        private void OnPlayerDied()
        {
            if (Session.Stats.Die(player.Health.LastDamageSource, CurrentRun.Elapsed)) StartCoroutine(ShowResultAfter(resultDelayAfterDeath));
        }

        private IEnumerator ShowResultAfter(float delay)
        {
            Debug.Log($"[WorldRun] 판 끝: {Session.Stats.Outcome} · 원인 {Session.Stats.DeathCause}");
            yield return new WaitForSecondsRealtime(delay);
            resultScreen.Show(Session.Stats, this);
        }

        private void Place(Vector3 position, Quaternion rotation)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            Vector3 forward = Flat(rotation * Vector3.forward);
            playerTransform.SetPositionAndRotation(position + Vector3.up * 0.1f, forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity);
            body.enabled = true;
        }

        // --- 디버그 창(DebugPanel)이 쓴다 ---

        /// <summary>다음 던전을 깬 것으로 치고 관문을 연다(던전에 들어가지 않고 길 확인용).</summary>
        public void DebugClearNext()
        {
            int next = Session.World.NextDungeon;
            if (next < 0 || !Session.EnterDungeon(next, player.Health.Current)) return;
            bool regionCleared = Session.FinishDungeon(player.Health.Current, CurrentRun.Elapsed);
            RefreshWorld();
            if (regionCleared) StartCoroutine(ShowResultAfter(0f));
        }

        /// <summary>다음 던전 입구 조금 앞으로 옮긴다.</summary>
        public void DebugGoToNext()
        {
            int next = Session.World.NextDungeon;
            if (next < 0) return;
            Transform entrance = entrances[next];
            Place(entrance.position + entrance.forward * (returnDistance + 3f), Quaternion.LookRotation(-entrance.forward));
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
