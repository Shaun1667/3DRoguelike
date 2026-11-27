#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Game.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// 디버그 창(에디터 · 개발 빌드 전용). 씬마다 저절로 생기므로 씬 · 프리팹에 넣지 않는다.
    /// F1 창 열고 닫기 · F2 원펀맨 모드(적 하나를 맞히면 살아 있는 적 전부 처치) · F3 지금 모든 적 처치 · 종 · 모습을 골라 앞에 소환.
    /// 창이 열려 있는 동안은 커서를 풀어 버튼을 누를 수 있게 한다.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        private const string KillSource = "원펀맨";
        private const float SummonDistance = 7f;
        private static readonly string[] SpeciesNames = { "오소리", "멧돼지", "멧토끼", "두꺼비" };

        private static DebugPanel instance;

        private bool open;
        private bool onePunch;
        private PlayerMelee melee;
        private PlayerGun gun;
        private PlayerController controller;
        private PlayerForm form;
        private FloorRun floor;
        private WorldRun world;
        private string lastAction = "";
        private int summonSpecies;
        private bool summonEvolved;
        private Transform playerTransform;
        private EnemyRoster roster;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("[DebugPanel]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DebugPanel>();
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            FindPlayer();
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Unhook();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            Unhook();
            GameObject player = GameObject.FindWithTag("Player");
            floor = FindAnyObjectByType<FloorRun>();
            world = FindAnyObjectByType<WorldRun>();
            EnemyRosterSource source = FindAnyObjectByType<EnemyRosterSource>();
            roster = floor != null ? floor.Roster : world != null ? world.Roster : source != null ? source.Roster : null;
            if (player == null) return;
            playerTransform = player.transform;
            melee = player.GetComponent<PlayerMelee>();
            gun = player.GetComponent<PlayerGun>();
            controller = player.GetComponent<PlayerController>();
            form = player.GetComponent<PlayerForm>();
            if (melee != null) melee.Hit += OnPlayerHit;
            if (gun != null) gun.Hit += OnPlayerHit;
        }

        private void Unhook()
        {
            if (melee != null) melee.Hit -= OnPlayerHit;
            if (gun != null) gun.Hit -= OnPlayerHit;
            melee = null; gun = null; controller = null; form = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) SetOpen(!open);
            if (keyboard.f2Key.wasPressedThisFrame) SetOnePunch(!onePunch);
            if (keyboard.f3Key.wasPressedThisFrame) KillAll("F3");
        }

        public bool OnePunch => onePunch;

        public void SetOnePunch(bool value)
        {
            onePunch = value;
            lastAction = value ? "원펀맨 모드 켬" : "원펀맨 모드 끔";
        }

        /// <returns>쓰러뜨린 적 수</returns>
        public int KillAll(string reason)
        {
            // 처치하면 목록에서 빠지므로 복사본으로 돈다
            var targets = new List<Enemy>(Enemy.Alive);
            foreach (Enemy enemy in targets)
            {
                if (!enemy.IsDead) enemy.Health.TakeDamage(enemy.Health.Current, KillSource);
            }
            lastAction = $"{reason}: 적 {targets.Count}마리 처치";
            return targets.Count;
        }

        /// <summary>플레이어 앞(약 7 m)에 그 종 · 모습을 소환한다. 멧토끼는 떼(가장 작은 떼). 방 클리어 판정에는 들어가지 않는다.</summary>
        /// <returns>소환한 수</returns>
        public int Summon(EnemySpecies species, EnemyGrade grade)
        {
            if (roster == null || playerTransform == null)
            {
                lastAction = "소환: 적 구성 표를 찾지 못함(Setup 다시 실행)";
                return 0;
            }
            Vector3 forward = playerTransform.forward;
            forward.y = 0f;
            Vector3 position = playerTransform.position + forward.normalized * SummonDistance;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) position = hit.position;
            int size = roster.TryGetEntry(species, out EnemyRoster.SpeciesEntry entry) ? entry.groupMin : 1;
            List<Enemy> spawned = roster.SpawnGroup(new EnemyKind(species, grade), size, position, Quaternion.LookRotation(-forward.normalized), null);
            lastAction = $"소환: {SpeciesNames[(int)species]} {(grade == EnemyGrade.Evolved ? "E 개체" : "Mutant")} {spawned.Count}마리";
            return spawned.Count;
        }

        /// <summary>보스 체력을 최대의 절반으로(이미 절반 아래면 지금의 절반으로). 강화 비율이 0.5면 강화가 된다.</summary>
        public void HalveBoss(Boss boss)
        {
            int target = boss.Health.Current > boss.Health.Max / 2 ? boss.Health.Max / 2 : boss.Health.Current / 2;
            boss.Health.TakeDamage(Mathf.Max(1, boss.Health.Current - Mathf.Max(1, target)), KillSource);
            lastAction = $"보스 체력 {boss.Health.Current}/{boss.Health.Max}";
        }

        public void KillBoss(Boss boss)
        {
            boss.Health.TakeDamage(boss.Health.Current, KillSource);
            lastAction = "보스 처치";
        }

        private void OnPlayerHit(IDamageable target)
        {
            if (onePunch && target is Health health && health.GetComponent<Enemy>() != null) KillAll("원펀맨");
        }

        private void SetOpen(bool value)
        {
            open = value;
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (controller != null && controller.enabled && Time.timeScale > 0f)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnGUI()
        {
            if (!open)
            {
                if (onePunch) GUI.Label(new Rect(10, 10, 300, 22), "원펀맨 모드 (F2로 끔)");
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 340, 600), GUI.skin.box);
            GUILayout.Label("디버그 (F1 닫기)");
            if (floor != null && floor.Layout != null)
            {
                GUILayout.Label($"시드 {floor.Seed.ToCode()} · 방 {floor.Progress.CurrentRoom} / 보스 {floor.Layout.BossRoom.Id}");
                GUILayout.Label($"깬 방 {floor.Stats.RoomsCleared} / {floor.Stats.BattleRooms} · 처치 {floor.Stats.Kills}");
            }
            GUILayout.Label($"살아 있는 적 {Enemy.Alive.Count}");
            bool next = GUILayout.Toggle(onePunch, " 원펀맨 모드 (F2): 한 마리 맞히면 전부 처치");
            if (next != onePunch) SetOnePunch(next);
            if (GUILayout.Button("지금 모든 적 처치 (F3)")) KillAll("버튼");
            if (roster != null)
            {
                summonSpecies = GUILayout.Toolbar(summonSpecies, SpeciesNames);
                GUILayout.BeginHorizontal();
                summonEvolved = GUILayout.Toggle(summonEvolved, " E 개체");
                if (GUILayout.Button("앞에 소환")) Summon((EnemySpecies)summonSpecies, summonEvolved ? EnemyGrade.Evolved : EnemyGrade.Mutant);
                GUILayout.EndHorizontal();
            }
            if (floor != null && floor.BossTuning != null)
            {
                Boss boss = floor.Boss;
                if (boss != null) GUILayout.Label($"보스 {boss.DisplayName} {boss.Health.Current}/{boss.Health.Max}{(boss.IsEnraged ? " · " + boss.Tuning.enragedLabel : "")} · {boss.Step}");
                if (GUILayout.Button("보스 방으로 이동")) lastAction = floor.DebugGoToBossRoom() ? "보스 방 앞으로" : "판이 끝나 이동 못 함";
                if (boss != null && !boss.IsDead && boss.Tuning.intro != null && !floor.BossIntroPlaying && GUILayout.Button("보스 연출 다시 보기"))
                {
                    lastAction = floor.DebugReplayBossIntro() ? "보스 등장 연출 다시 재생" : "연출을 재생하지 못함";
                }

                if (boss != null && !boss.IsDead)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("보스 체력 반으로")) HalveBoss(boss);
                    if (GUILayout.Button("보스 처치")) KillBoss(boss);
                    GUILayout.EndHorizontal();
                }
            }
            if (form != null && !form.IsMutant && GUILayout.Button("Mutant 동작으로 (Beast 높을 때 모습)"))
            {
                form.BecomeMutant();
                lastAction = form.IsMutant ? "Mutant 동작으로 바꿈" : "Mutant 컨트롤러 없음 — Setup Test Room 다시 실행";
            }
            if (world != null && world.Session != null && world.Session.World.NextDungeon >= 0)
            {
                if (GUILayout.Button("다음 던전 입구로 이동")) { world.DebugGoToNext(); lastAction = "다음 던전 입구 앞으로"; }
                if (GUILayout.Button("다음 던전 깬 것으로 (관문 열기)")) { world.DebugClearNext(); lastAction = "던전을 깬 것으로 침"; }
            }
            if (world != null && world.Session != null)
            {
                // 지역마다 한 줄: 그 지역 시작으로 순간이동(앞 관문은 보기만 열림, 깬 던전은 그대로)
                for (int i = 1; i < world.RegionCount; i++)
                {
                    if (GUILayout.Button($"{world.RegionName(i)} 시작으로 이동 (관문 열기)")) { world.DebugGoToRegion(i); lastAction = $"{world.RegionName(i)} 시작으로"; }
                }
            }
            if (lastAction.Length > 0) GUILayout.Label(lastAction);
            GUILayout.EndArea();
        }
    }
}
#endif
