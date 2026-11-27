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
    public sealed class WorldRun : MonoBehaviour, IRunRestart, IRunSavePoint
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
        [Tooltip("지형 타일(지역 순서, 남 → 북). 플레이 때 이웃으로 잇는다")]
        [SerializeField] private Terrain[] terrains;
        [Tooltip("비워 두면 새 판마다 새 시드. 'XXXX-XXXX' 코드를 넣으면 그 던전들을 다시 만든다")]
        [SerializeField] private string fixedSeedCode = "";
        [Min(0f)] [SerializeField] private float resultDelayAfterDeath = 1.5f;
        [Tooltip("돌아올 때 입구 앞으로 떨어져 서는 거리(m)")]
        [Min(0.5f)] [SerializeField] private float returnDistance = 5f;
        [Tooltip("길 시작에서 이 거리(m) 안이면 G로 성장 트리를 연다(3-7 재생 자리)")]
        [Min(1f)] [SerializeField] private float metaTreeRadius = 8f;

        private List<Health>[] alive;
        private bool[] started;
        private Transform playerTransform;
        private float nextObjectiveTime;
        private bool leaving;
        private EnemyMix mix;
        private WorldRoute route;
        private int lastRegion = -1;
        private float atmosphereDistance = float.NaN;

        public RunSession Session { get; private set; }
        public WorldTuning Tuning => tuning;
        public EnemyRoster Roster => roster;

        /// <summary>이 씬에서 새 판을 만들었는가(그러면 Start에서 이어 하기를 묻거나 바로 저장).</summary>
        private bool createdRun;

        private void Awake()
        {
            Time.timeScale = 1f;
            playerTransform = player.transform;
            MetaRun.Use(tuning.metaTree); // 판 밖 성장(3-7): 새 판을 만들 때 산 노드를 건다
            Session = CurrentRun.EnsureSession(tuning.dungeons.Length, tuning.LastDungeonOpensRegion, ChooseSeed, out createdRun);
            mix = roster.ToMix();
            route = tuning.ToRoute();
            ConnectTerrains();

            int back = Session.ReturnDungeon;
            if (Session.HasWorldSpot)
            {
                // 이어 하기: 월드에서 저장한 자리(길 위 무리를 깬 곳 · 끈 곳)
                Place(new Vector3(Session.WorldX, Session.WorldY, Session.WorldZ), Quaternion.Euler(0f, Session.WorldYaw, 0f));
            }
            else if (back >= 0 && back < entrances.Length)
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
            UpdateAtmosphere(playerTransform.position, force: true);
            Debug.Log($"[WorldRun] 시드 {Session.Seed.ToCode()} · 깬 던전 {Session.World.ClearedCount}/{Session.World.DungeonCount}" +
                      (back >= 0 ? $" · {tuning.dungeons[back].displayName} 앞에서" : " · 길 시작에서"));
        }

        private void Start()
        {
            // PlayerVitals.Awake가 체력을 가득으로 만든 뒤, HUD.Start보다 먼저(실행 순서 -100)
            if (Session.PlayerHealth != RunSession.FullHealth) player.Health.SetCurrent(Session.PlayerHealth);
            player.GetComponent<PlayerUpgrades>().Use(Session.Stats.Upgrades);
            if (Session.PlayerAmmo != RunSession.FullAmmo && player.Gun != null) player.Gun.SetAmmo(Session.PlayerAmmo); // 이어 할 때만(씬을 옮기면 가득)
            player.GetComponent<PlayerForm>().UpdateForm(Session.Stats.Beast, Session.Stats.Human);
            if (createdRun) OfferContinueOrSave();
        }

        /// <summary>
        /// 새 판을 시작한 참(3-6): 게임을 켜고 처음이고 이어 할 저장이 있으면 [이어 하기] / [새 판]을 묻는다.
        /// 없으면(또는 새 판을 고르면) 새 판을 바로 저장한다.
        /// </summary>
        private void OfferContinueOrSave()
        {
            var table = tuning.rewardTable != null ? tuning.rewardTable.ToDefinitions() : null;
            if (!CurrentRun.TryTakeSavedRun(tuning.dungeons.Length, tuning.LastDungeonOpensRegion, table, out RunSession saved, out float elapsed, out string summary))
            {
                CurrentRun.Save("판 시작");
                return;
            }
            Prompt = ContinuePrompt.Show(objective != null ? objective.font : null, summary,
                () => CurrentRun.Resume(saved, elapsed),
                () =>
                {
                    CurrentRun.DeleteSave("새 판 고름");
                    CurrentRun.Save("판 시작");
                });
        }

        /// <summary>떠 있는 이어 하기 화면. 없으면 null.</summary>
        public ContinuePrompt Prompt { get; private set; }

        private void OnEnable()
        {
            player.Health.Died += OnPlayerDied;
            CurrentRun.UseSavePoint(this);
        }

        private void OnDisable()
        {
            player.Health.Died -= OnPlayerDied;
            CurrentRun.ReleaseSavePoint(this);
        }

        private void Update()
        {
            if (leaving || Session.Stats.Outcome != RunOutcome.Playing) return;
            Vector3 position = playerTransform.position;
            UpdateAtmosphere(position, force: false);
            SafeZone safeZone = SafeZone.Protecting(position);

            // 안전 지대(마을) 안에서는 길 위 무리가 나타나지 않는다 — 나오면 평소처럼 나타남 거리로 본다
            for (int i = 0; safeZone == null && i < encounterSites.Length; i++)
            {
                if (!started[i] && Flat(encounterSites[i].position - position).magnitude <= tuning.encounterTriggerRange) SpawnEncounter(i);
            }

            bool atStart = MetaRun.Ready && Flat(startPoint.position - position).magnitude <= metaTreeRadius;
            if (atStart && Prompt == null && MetaTreeScreen.CanOpenThisFrame && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.gKey.wasPressedThisFrame) OpenMetaTree();

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
                if (safeZone != null) objective.text = $"{safeZone.DisplayName} · 안전 지대\n{objective.text}";
                if (atStart) objective.text = $"{RebirthText()}  ·  G: 성장 트리 (영구 재화 {MetaRun.Progress.Currency})\n{objective.text}";
            }
        }

        public void Restart(bool sameSeed) => CurrentRun.RestartFromWorld(sameSeed);

        /// <summary>길 시작(재생 자리) 안내: 몇 번째 깨어났나(끝난 판 수 + 1).</summary>
        private static string RebirthText()
        {
            int runs = MetaRun.Progress.Runs;
            return runs > 0 ? $"초재생 · {runs + 1}번째 깨어남" : "숲에서 깨어났다";
        }

        /// <summary>성장 트리 화면(3-7)을 연다. 아직 아무것도 안 한 판이면 산 노드가 이 판에도 바로 걸린다.</summary>
        public MetaTreeScreen OpenMetaTree()
        {
            if (Session == null || Session.Stats.Outcome != RunOutcome.Playing) return null;
            return MetaTreeScreen.Open(objective != null ? objective.font : null, () => Session,
                () => player.GetComponent<PlayerForm>().UpdateForm(Session.Stats.Beast, Session.Stats.Human));
        }

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
            CurrentRun.Save("던전 입장", syncFromScene: false); // 입장한 판 상태 그대로(체력은 EnterDungeon이 적음)
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
            List<EnemyPick> picks = EnemyMixPlanner.Pick(mix, tuning.DungeonAhead(site), EnemyPlace.Road, points.childCount, random);
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
            if (alive[site].Count == 0 && Session.World.ClearEncounter(site)) CurrentRun.Save("길 위 무리 깸");
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
            if (next < 0) return OpenRoadText(position);
            string count = $"({Session.World.ClearedCount}/{Session.World.DungeonCount})";
            // 다음 던전이 아직 들어서지 않은 지역에 있으면 먼저 그 지역으로(숲 던전 1 → 폐허 도시 → 도시 던전)
            int region = tuning.RegionOfDungeon(next);
            route.DistanceFrom(position.x, position.z, out float along);
            if (region > 0 && along < tuning.regions[region].startDistance)
                return $"다음: {tuning.regions[region].displayName}  ·  {Mathf.RoundToInt(tuning.regions[region].startDistance - along)} m  {count}";
            float distance = Flat(entrances[next].position - position).magnitude;
            return $"다음: {tuning.dungeons[next].displayName}  ·  {Mathf.RoundToInt(distance)} m  {count}";
        }

        /// <summary>
        /// 던전을 모두 깼는데 판이 이어질 때(마지막 던전이 다음 지역을 열었을 때): 열린 관문 너머 지역이 아직 앞이면 그리로,
        /// 그 지역에 들어섰으면 지역 이름 · 앞길은 아직 막힘(그 지역 던전 · 다음 지역은 준비 중).
        /// </summary>
        private string OpenRoadText(Vector3 position)
        {
            route.DistanceFrom(position.x, position.z, out float along);
            for (int r = 1; r < tuning.regions.Length; r++)
            {
                int gate = tuning.GateBefore(r);
                if (gate < 0 || !Session.World.IsGateOpen(gate) || along >= tuning.regions[r].startDistance) continue;
                return $"다음: {tuning.regions[r].displayName}  ·  {Mathf.RoundToInt(tuning.regions[r].startDistance - along)} m";
            }
            return $"{tuning.regions[tuning.RegionAt(along)].displayName}  ·  앞길은 아직 막혀 있다(준비 중)";
        }

        private void OnPlayerDied()
        {
            if (!Session.Stats.Die(player.Health.LastDamageSource, CurrentRun.Elapsed)) return;
            CurrentRun.RunEnded(); // 로그라이크: 죽으면 저장을 지운다
            StartCoroutine(ShowResultAfter(resultDelayAfterDeath));
        }

        /// <summary>
        /// 저장 지점(3-6): 체력 · 탄 · 지금 자리를 판 상태에 적는다. 길 위 무리와 싸우는 중(나온 무리에 살아 있는 적)이면 false.
        /// 던전으로 떠나는 중이면 판 상태가 이미 던전 입장이라 그대로 둔다.
        /// </summary>
        public bool SyncForSave(RunSession session)
        {
            if (session != Session || session.Stats.Outcome != RunOutcome.Playing || player.Health.IsDead) return false;
            if (leaving || session.InDungeon) return true;
            for (int i = 0; i < alive.Length; i++) if (alive[i].Count > 0) return false;
            PlayerGun gun = player.Gun;
            session.UpdatePlayer(player.Health.Current, gun == null || gun.Ammo >= gun.Magazine ? RunSession.FullAmmo : gun.Ammo);
            Vector3 position = playerTransform.position;
            session.SetWorldSpot(position.x, position.y, position.z, playerTransform.eulerAngles.y);
            return true;
        }

        private IEnumerator ShowResultAfter(float delay)
        {
            Debug.Log($"[WorldRun] 판 끝: {Session.Stats.Outcome} · 원인 {Session.Stats.DeathCause}");
            yield return new WaitForSecondsRealtime(delay);
            resultScreen.Show(Session.Stats, this, tuning.ClearedRegionName(Session.ReturnDungeon));
        }

        /// <summary>지형 타일을 남 → 북 이웃으로 잇는다(이음매 LOD 틈이 없게). 이웃은 저장되지 않아 판마다 잇는다.</summary>
        private void ConnectTerrains()
        {
            if (terrains == null) return;
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue;
                Terrain south = i > 0 ? terrains[i - 1] : null;
                Terrain north = i + 1 < terrains.Length ? terrains[i + 1] : null;
                terrains[i].SetNeighbors(null, north, null, south);
            }
        }

        /// <summary>
        /// 플레이어가 있는 길 거리로 지역 분위기(앰비언트 · 안개 색 · 거리)를 섞는다(<see cref="WorldTuning.AtmosphereAt"/>).
        /// 길 거리가 0.5 m 넘게 바뀔 때만 고친다.
        /// </summary>
        private void UpdateAtmosphere(Vector3 position, bool force)
        {
            route.DistanceFrom(position.x, position.z, out float along);
            if (!force && Mathf.Abs(along - atmosphereDistance) < 0.5f) return;
            atmosphereDistance = along;
            WorldTuning.Atmosphere air = tuning.AtmosphereAt(along);
            RenderSettings.ambientLight = air.ambientColor;
            RenderSettings.fogColor = air.fogColor;
            RenderSettings.fogStartDistance = air.fogStart;
            RenderSettings.fogEndDistance = air.fogEnd;
            int region = tuning.RegionAt(along);
            if (region != lastRegion)
            {
                if (lastRegion >= 0) Debug.Log($"[WorldRun] 지역: {tuning.regions[region].displayName} (길 {along:0} m)");
                lastRegion = region;
            }
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
            if (regionCleared)
            {
                CurrentRun.RunEnded();
                StartCoroutine(ShowResultAfter(0f));
            }
            else CurrentRun.Save("던전 깬 것으로(디버그)");
        }

        /// <summary>다음 던전 입구 조금 앞으로 옮긴다.</summary>
        public void DebugGoToNext()
        {
            int next = Session.World.NextDungeon;
            if (next < 0) return;
            Transform entrance = entrances[next];
            Place(entrance.position + entrance.forward * (returnDistance + 3f), Quaternion.LookRotation(-entrance.forward));
        }

        /// <summary>지역 수 · 이름(디버그 창).</summary>
        public int RegionCount => tuning.regions.Length;
        public string RegionName(int region) => tuning.regions[region].displayName;

        /// <summary>
        /// 길 거리 distance의 길 위로 옮긴다(앞을 보고). openGates면 그 앞 관문을 모두 열어 둔다 — 보기만 하는 것이라 판 상태(깬 던전)는 그대로다.
        /// </summary>
        public void DebugGoToRouteDistance(float distance, bool openGates)
        {
            if (openGates)
            {
                for (int i = 0; i < gates.Length; i++) if (tuning.GateDistance(i) <= distance) gates[i].SetActive(false);
            }
            (float x, float z) = route.PointAt(distance);
            (float dx, float dz) = route.DirectionAt(distance);
            var ground = new Vector3(x, 500f, z);
            if (Physics.Raycast(ground, Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point;
            else ground.y = 0f;
            Place(ground, Quaternion.LookRotation(new Vector3(dx, 0f, dz)));
            UpdateAtmosphere(ground, force: true);
        }

        /// <summary>지역 i 시작(섞임이 끝난 자리)으로 옮기고 그 앞 관문을 연다.</summary>
        public void DebugGoToRegion(int region)
        {
            WorldTuning.Region data = tuning.regions[region];
            DebugGoToRouteDistance(data.startDistance + data.blendDistance + 5f, openGates: true);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
