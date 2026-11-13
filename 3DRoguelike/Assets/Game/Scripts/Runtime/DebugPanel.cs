#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// 디버그 창(에디터 · 개발 빌드 전용). 씬마다 저절로 생기므로 씬 · 프리팹에 넣지 않는다.
    /// F1 창 열고 닫기 · F2 원펀맨 모드(적 하나를 맞히면 살아 있는 적 전부 처치) · F3 지금 모든 적 처치.
    /// 창이 열려 있는 동안은 커서를 풀어 버튼을 누를 수 있게 한다.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        private const string KillSource = "원펀맨";

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
            if (player == null) return;
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
            var targets = new List<Mutant>(Mutant.Alive);
            foreach (Mutant enemy in targets)
            {
                if (!enemy.IsDead) enemy.Health.TakeDamage(enemy.Health.Current, KillSource);
            }
            lastAction = $"{reason}: 적 {targets.Count}마리 처치";
            return targets.Count;
        }

        private void OnPlayerHit(IDamageable target)
        {
            if (onePunch && target is Health health && health.GetComponent<Mutant>() != null) KillAll("원펀맨");
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

            GUILayout.BeginArea(new Rect(10, 10, 320, 330), GUI.skin.box);
            GUILayout.Label("디버그 (F1 닫기)");
            if (floor != null && floor.Layout != null)
            {
                GUILayout.Label($"시드 {floor.Seed.ToCode()} · 방 {floor.Progress.CurrentRoom} / 보스 {floor.Layout.BossRoom.Id}");
                GUILayout.Label($"깬 방 {floor.Stats.RoomsCleared} / {floor.Stats.BattleRooms} · 처치 {floor.Stats.Kills}");
            }
            GUILayout.Label($"살아 있는 적 {Mutant.Alive.Count}");
            bool next = GUILayout.Toggle(onePunch, " 원펀맨 모드 (F2): 한 마리 맞히면 전부 처치");
            if (next != onePunch) SetOnePunch(next);
            if (GUILayout.Button("지금 모든 적 처치 (F3)")) KillAll("버튼");
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
            if (lastAction.Length > 0) GUILayout.Label(lastAction);
            GUILayout.EndArea();
        }
    }
}
#endif
