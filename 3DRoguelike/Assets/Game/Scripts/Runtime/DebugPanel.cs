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
        /// <summary>EnemySpecies 순서(새 종 = 끝에 한 줄).</summary>
        private static readonly string[] SpeciesNames = { "오소리", "멧돼지", "멧토끼", "두꺼비", "너구리", "여우" };

        private static DebugPanel instance;

        private bool open;
        private bool onePunch;
        private PlayerMelee melee;
        private PlayerGun gun;
        private PlayerController controller;
        private PlayerForm form;
        private PlayerUpgrades upgrades;
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
            upgrades = player.GetComponent<PlayerUpgrades>();
            if (melee != null) melee.Hit += OnPlayerHit;
            if (gun != null) gun.Hit += OnPlayerHit;
        }

        private void Unhook()
        {
            if (melee != null) melee.Hit -= OnPlayerHit;
            if (gun != null) gun.Hit -= OnPlayerHit;
            melee = null; gun = null; controller = null; form = null; upgrades = null;
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

        /// <summary>권총 보장 상태(3-4) 한 줄 + [권총 주기 / 빼기].</summary>
        private void PistolRow()
        {
            if (upgrades == null) return;
            if (!upgrades.InRun)
            {
                GUILayout.Label("권총: 시험 방 — 늘 가짐");
                return;
            }
            bool has = upgrades.Has(RewardUnlock.Pistol);
            GUILayout.Label($"권총: {(has ? "있음" : "없음(맨몸)")} · {PistolGuarantee(has)}");
            if (GUILayout.Button(has ? "권총 빼기 (맨몸으로)" : "권총 주기")) SetPistol(!has);
        }

        private string PistolGuarantee(bool has)
        {
            if (floor == null || floor.Rewards == null) return "보장: 첫 던전의 보장 방";
            int room = floor.Rewards.GuaranteedRoom;
            if (room < 0) return "이 던전은 보장 없음";
            string state = floor.HasPickup(room) ? "보상물 놓임" : !floor.Progress.IsCleared(room) ? "아직 안 깸" : has ? "얻음" : "보상물 없음";
            return $"보장 방 {room} ({state})";
        }

        /// <summary>권총을 주거나 뺀다(판 강화 합계에 바로, 보상 기록 · 성향은 그대로).</summary>
        public void SetPistol(bool value)
        {
            if (upgrades == null || !upgrades.InRun) return;
            upgrades.Sheet.SetUnlocked(RewardUnlock.Pistol, value);
            lastAction = value ? "권총 줌" : "권총 뺌 (맨몸)";
        }

        /// <summary>판 중간 저장(3-6) 상태 한 줄 + [지금 저장] · [저장 지우기] · [저장 파일 위치].</summary>
        private void SaveRows()
        {
            RunSaveStore store = CurrentRun.Store;
            string state = CurrentRun.Session == null ? "판 없음(층 씬만 Play — 저장 안 함)" : store.Exists ? "있음" : "없음";
            GUILayout.Label($"저장: {state}{(CurrentRun.LastSaveNote.Length > 0 ? " · 마지막 " + CurrentRun.LastSaveNote : "")}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("지금 저장")) lastAction = SaveNow();
            if (GUILayout.Button("저장 지우기")) lastAction = CurrentRun.DeleteSave("디버그") ? "저장 지움" : "지울 저장 없음";
            if (GUILayout.Button("저장 파일 위치")) lastAction = ShowSaveFile(store);
            GUILayout.EndHorizontal();
        }

        /// <summary>[지금 저장]: 싸우는 중(문이 잠김 · 길 위 무리)이면 저장하지 않는다 — 저장 규칙 그대로.</summary>
        public string SaveNow()
        {
            if (CurrentRun.Session == null) return "판이 없어 저장 안 함";
            return CurrentRun.Save("디버그") ? "지금 저장함" : "저장 안 함(싸우는 중 · 판 끝)";
        }

        private static string ShowSaveFile(RunSaveStore store)
        {
            string path = store.Exists ? store.FilePath : store.Directory;
            Debug.Log($"[DebugPanel] 저장 파일: {store.FilePath}");
            GUIUtility.systemCopyBuffer = store.FilePath;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#else
            Application.OpenURL("file:///" + store.Directory.Replace('\\', '/'));
#endif
            return $"저장 위치(복사함): {store.FilePath}";
        }


        /// <summary>판 밖 성장(3-7) 한 줄 + [재화 +100] · [트리 초기화] · [모두 사기] · [성장 트리 열기].</summary>
        private void MetaRows()
        {
            if (!MetaRun.Ready)
            {
                GUILayout.Label("영구 재화: 트리 표 없음(월드에서 시작하면 생김)");
                return;
            }
            MetaProgress meta = MetaRun.Progress;
            RunSession run = CurrentRun.Session;
            GUILayout.Label($"영구 재화 {meta.Currency} · 노드 {meta.Owned.Count}/{MetaRun.Tree.Count} · 재생 {meta.Runs}" +
                            (run != null ? $" · 이 판 노드 {run.MetaNodes.Count}" + (run.Stats.Outcome == RunOutcome.Playing ? $" · 지금 끝나면 +{MetaRun.Earnings.Earned(run.Stats)}" : "") : ""));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("재화 +100")) { MetaRun.DebugAddCurrency(100); lastAction = $"영구 재화 +100 → {MetaRun.Progress.Currency}"; }
            if (GUILayout.Button("트리 초기화")) { MetaRun.DebugResetTree(); lastAction = "트리 초기화(쓴 재화 돌려받음) — 지금 판에 건 효과는 다음 재생부터 빠짐"; }
            if (GUILayout.Button("모두 사기")) { int now = MetaRun.DebugBuyAll(run); lastAction = now > 0 ? $"모두 삼 · 이번 판에 {now}개 걸림" : "모두 삼 · 다음 재생부터"; }
            GUILayout.EndHorizontal();
            if (world != null && GUILayout.Button("성장 트리 열기"))
            {
                SetOpen(false);
                lastAction = world.OpenMetaTree() != null ? "성장 트리 열림" : "성장 트리를 열지 못함";
            }
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

            GUILayout.BeginArea(new Rect(10, 10, 380, 760), GUI.skin.box);
            GUILayout.Label("디버그 (F1 닫기)");
            if (floor != null && floor.Layout != null)
            {
                GUILayout.Label($"시드 {floor.Seed.ToCode()} · 방 {floor.Progress.CurrentRoom} / 보스 {floor.Layout.BossRoom.Id}");
                GUILayout.Label($"깬 방 {floor.Stats.RoomsCleared} / {floor.Stats.BattleRooms} · 처치 {floor.Stats.Kills}");
            }
            GUILayout.Label($"살아 있는 적 {Enemy.Alive.Count}");
            PistolRow();
            SaveRows();
            MetaRows();
            bool next = GUILayout.Toggle(onePunch, " 원펀맨 모드 (F2): 한 마리 맞히면 전부 처치");
            if (next != onePunch) SetOnePunch(next);
            if (GUILayout.Button("지금 모든 적 처치 (F3)")) KillAll("버튼");
            if (roster != null)
            {
                summonSpecies = GUILayout.SelectionGrid(summonSpecies, SpeciesNames, 3);
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
                // 등장 연출(타임라인)이 있는 보스(까치)는 연출을, 없는 보스(곰)는 이름 + 짧은 등장 동작을 다시
                if (boss != null && !boss.IsDead && !floor.BossIntroPlaying && GUILayout.Button(boss.Tuning.intro != null ? "보스 연출 다시 보기" : "보스 등장 다시 보기"))
                {
                    lastAction = floor.DebugReplayBossIntro() ? "보스 등장 다시 재생" : "등장을 다시 보지 못함";
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
