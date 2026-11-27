using System;
using System.Collections.Generic;
using Game.Rules;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Playables;

namespace Game
{
    /// <summary>
    /// 싸움 전 보스 등장 연출(Timeline + Cinemachine) — 예전 까치 보스전(main)의 BossIntroCinematic을 옮긴 것.
    ///
    /// [장면] 시간 · 컷 순서는 <see cref="BossIntroScript"/>(수치는 <see cref="BossIntroTuning"/>)
    ///  1. 방 안쪽 높은 곳에서, 문으로 들어선 플레이어가 천천히 걸어 들어오는 것을 내려다본다 → 어깨 너머 3인칭
    ///  2. 카메라가 머리 속으로 들어가며 1인칭으로 바뀐다
    ///  3. 까치가 웅크렸다가 돌진해 와서 플레이어를 넘어뜨린다(1인칭 카메라가 바닥으로 떨어져 올려다봄)
    ///  4. 까치가 다리를 들어 발로 찍어 누른다
    ///  5. 플레이어의 할퀴기(화면 가득 할퀴기 이펙트 두 번) → 까치가 번쩍이며 주춤하고 제자리로 물러난다
    ///  6. 카메라가 게임 카메라 자리로 이어지고 싸움 시작
    ///
    /// [역할]
    ///  - 카메라 컷 · 검은 띠 · 이름 타이틀: 타임라인(`BossIntroTuning.timeline`)의 Cinemachine · Activation 트랙. 트랙은 이름으로 여기 것에 잇는다.
    ///  - 배우(걷기, 돌진 · 밟기 · 후퇴, 이펙트, 흔들림): 이 스크립트가 타임라인 시간을 읽어 움직인다 → 건너뛰어도 끝에서 모두 싸움 시작 상태로 돌려놓는다.
    ///  - 연출 동안 플레이어 조작 · 피해(양쪽)를 막는다. 보스 행동은 부르는 쪽(FloorRun)이 연출이 끝난 뒤에 시작한다.
    ///
    /// [카메라] 평소 카메라도 Cinemachine(PlayerCamera)이라 Brain은 늘 켜져 있다. 연출용 카메라는 우선순위가 낮아 타임라인이 고를 때만 쓰이고,
    /// 타임라인이 끝나면 Brain이 저절로 PlayerCamera로 돌아간다. 마지막 컷은 PlayerCamera와 같은 자리 · 화각을 따라가므로 화면이 튀지 않는다.
    /// 방 · 들어선 자리는 판마다 달라서 카메라 자리는 재생할 때 계산한다.
    /// </summary>
    public sealed class BossIntroCinematic : MonoBehaviour
    {
        public const string CameraTrack = "Cameras";
        public const string LetterboxTrack = "Letterbox";
        public const string TitleTrack = "Boss Title";
        /// <summary>연출용 카메라 우선순위(게임 카메라 0보다 낮게 — 타임라인이 고를 때만 쓰인다).</summary>
        public const int ShotPriority = -10;

        private static readonly string[] ShotNames = { "CM_01_Wide_A", "CM_02_Wide_B", "CM_03_OverShoulder", "CM_04_POV", "CM_05_Ground", "CM_06_Gameplay" };
        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveYId = Animator.StringToHash("MoveY");
        private static readonly int MoveAmountId = Animator.StringToHash("MoveAmount");
        private static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        private static readonly int MoveStateId = Animator.StringToHash("Move");
        private static readonly string[] PlayerTriggers = { "Attack", "Roll", "Jump" };
        private static readonly Func<bool> Blocked = () => true;
        /// <summary>발밑이 바닥에 묻히지 않게 살짝 띄운다(조작이 돌아오면 중력으로 내려앉는다).</summary>
        private const float FootClearance = 0.05f;

        /// <summary>타임라인의 카메라 컷 이름(= 연출용 카메라 오브젝트 이름).</summary>
        public static string ShotName(BossIntroShot shot) => ShotNames[(int)shot];

        [Header("타임라인 / 카메라")]
        [SerializeField] private PlayableDirector director;
        [Tooltip("Main Camera의 CinemachineBrain(카메라 트랙이 여기에 이어진다)")]
        [SerializeField] private CinemachineBrain brain;
        [Tooltip("게임 카메라. 마지막 컷이 이 카메라의 자리 · 화각을 따라간다")]
        [SerializeField] private CinemachineCamera playerCamera;
        [Tooltip("연출용 카메라들의 부모. 연출 동안만 켠다")]
        [SerializeField] private GameObject cameraRoot;
        [Tooltip("연출용 카메라(BossIntroShot 순서: 와이드 A · 와이드 B · 어깨 너머 · 1인칭 · 바닥 · 게임 카메라 자리)")]
        [SerializeField] private CinemachineCamera[] shots;
        [Tooltip("넘어짐 · 밟기 · 할퀴기 때 화면을 흔든다")]
        [SerializeField] private CinemachineImpulseSource impulse;

        [Header("화면")]
        [SerializeField] private GameObject letterbox;
        [SerializeField] private GameObject title;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [Tooltip("연출 동안 숨길 HUD(체력 · 탄 · 미니맵 · 안내 · 보스 체력 막대)")]
        [SerializeField] private GameObject[] hideDuringIntro;

        private readonly List<BossIntroCue> cues = new List<BossIntroCue>();
        private readonly List<BossIntroCue> fired = new List<BossIntroCue>();
        private BossIntroTuning tuning;
        private BossIntroScript script;
        private Action onFinished;
        private Camera outputCamera;
        private float lastTime;

        private Transform player;
        private Animator playerAnimator;
        private CharacterController playerBody;
        private bool playerBodyWasEnabled;
        private Behaviour[] playerDrivers;
        private bool[] playerDriverDefaults;
        private Renderer[] playerRenderers;
        private bool[] playerRendererDefaults; // 원래 꺼져 있던 렌더러는 다시 켜지 않도록
        private Health playerHealth, bossHealth;
        private Func<bool> playerBlock, bossBlock;
        private Vector3 playerEntrance, playerStart;
        private Quaternion playerRotation;
        private float walkSpeedParameter;

        private Boss boss;
        private Vector3 bossStart, bossStomp;
        private Quaternion bossRotation;
        private bool[] hudDefaults;

        public bool IsPlaying { get; private set; }
        /// <summary>지금까지 재생한 횟수(끝까지 봤든 건너뛰었든).</summary>
        public int PlayCount { get; private set; }
        /// <summary>마지막 재생을 건너뛰었는지.</summary>
        public bool WasSkipped { get; private set; }
        /// <summary>타임라인 시간(초). 재생 중이 아니면 0.</summary>
        public float CurrentTime => IsPlaying ? (float)director.time : 0f;
        /// <summary>지금 들어와 있는 컷(확인용).</summary>
        public BossIntroShot CurrentShot => script != null ? script.ShotAt(CurrentTime) : BossIntroShot.WideA;
        /// <summary>이번 재생에서 일어난 일(순서대로, 확인용).</summary>
        public IReadOnlyList<BossIntroCue> FiredCues => fired;
        public bool LetterboxShown => letterbox != null && letterbox.activeSelf;
        public bool TitleShown => title != null && title.activeSelf;
        /// <summary>싸움을 시작하는 자리(걸어 들어와 멈춘 곳) · 보스가 밟으러 오는 자리. 재생한 뒤에 읽는다.</summary>
        public Vector3 PlayerStart => playerStart;
        public Vector3 BossStomp => bossStomp;

        private void Awake()
        {
            if (cameraRoot != null) cameraRoot.SetActive(false);
            if (letterbox != null) letterbox.SetActive(false);
            if (title != null) title.SetActive(false);
            director.playOnAwake = false;
            director.stopped += OnDirectorStopped;
        }

        private void OnDestroy()
        {
            if (director != null) director.stopped -= OnDirectorStopped;
        }

        /// <summary>
        /// 연출을 처음부터 재생한다. 끝나거나 건너뛰면 finished를 한 번 부른다. 재생하지 못하면(타임라인 없음 · 이미 재생 중) false — 부르는 쪽이 바로 싸움을 시작한다.
        /// entry = 플레이어가 방에 들어선 자리(여기서 보스 쪽으로 걸어 들어온다), arenaCenter = 보스 자리(방 가운데),
        /// roomHalf = 방 가운데에서 벽 안쪽 면까지(와이드 카메라가 벽 밖으로 나가지 않게).
        /// </summary>
        public bool Play(BossIntroTuning introTuning, PlayerVitals target, Boss bossTarget, Vector3 entry, Vector3 arenaCenter, Vector2 roomHalf, Action finished)
        {
            if (IsPlaying || introTuning == null || introTuning.timeline == null || target == null || bossTarget == null) return false;
            tuning = introTuning;
            script = tuning.ToScript();
            onFinished = finished;
            boss = bossTarget;
            outputCamera = brain.GetComponent<Camera>();

            // ── 자리: 들어선 곳에서 보스 쪽으로 걸어 들어와, 보스와 minBossDistance 넘게 떨어진 곳에서 멈춘다 ──
            Vector3 up = Vector3.up;
            float groundY = boss.transform.position.y;
            bossStart = new Vector3(arenaCenter.x, groundY, arenaCenter.z);
            Vector3 ground = new Vector3(entry.x, groundY, entry.z);
            Vector3 toBoss = bossStart - ground;
            float distance = toBoss.magnitude;
            Vector3 forward = distance > 0.01f ? toBoss / distance : -Flat(boss.transform.forward).normalized;
            Vector3 right = Vector3.Cross(up, forward);
            float walk = Mathf.Clamp(distance - tuning.minBossDistance, 0f, tuning.walkDistance);
            Vector3 groundStart = ground + forward * walk;
            playerEntrance = ground + up * FootClearance;
            playerStart = groundStart + up * FootClearance;
            playerRotation = Quaternion.LookRotation(forward);
            bossRotation = Quaternion.LookRotation(-forward);
            float gap = Flat(groundStart - bossStart).magnitude;
            bossStomp = bossStart - forward * Mathf.Max(0f, gap - tuning.stompDistance);
            walkSpeedParameter = Mathf.Max(0.05f, walk / Mathf.Max(0.01f, tuning.walkEnd) / tuning.walkClipSpeed * tuning.walkAnimScale);

            // ── 플레이어: 조작 · 피해를 막고 들어선 자리에 세운다 ──
            player = target.transform;
            playerAnimator = player.GetComponentInChildren<Animator>();
            playerBody = player.GetComponent<CharacterController>();
            playerDrivers = new Behaviour[]
            {
                player.GetComponent<PlayerController>(), player.GetComponent<PlayerMelee>(), player.GetComponent<PlayerGun>(), player.GetComponent<PlayerRoll>(),
            };
            playerDriverDefaults = new bool[playerDrivers.Length];
            for (int i = 0; i < playerDrivers.Length; i++)
            {
                if (playerDrivers[i] == null) continue;
                playerDriverDefaults[i] = playerDrivers[i].enabled;
                playerDrivers[i].enabled = false;
            }
            playerBodyWasEnabled = playerBody != null && playerBody.enabled;
            if (playerBody != null) playerBody.enabled = false; // 위치를 직접 옮기므로 충돌 처리를 잠시 끔
            playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            playerRendererDefaults = new bool[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++) playerRendererDefaults[i] = playerRenderers[i].enabled;
            player.SetPositionAndRotation(playerEntrance, playerRotation);
            foreach (string trigger in PlayerTriggers) playerAnimator.ResetTrigger(trigger);
            if (playerAnimator.HasState(0, MoveStateId)) playerAnimator.Play(MoveStateId, 0, 0f); // 구르기 · 공격 · 점프 중에 들어섰어도 걷기부터

            playerHealth = target.Health;
            playerBlock = playerHealth.DamageBlocked;
            playerHealth.DamageBlocked = Blocked;
            bossHealth = boss.Health;
            bossBlock = bossHealth.DamageBlocked;
            bossHealth.DamageBlocked = Blocked;

            boss.transform.SetPositionAndRotation(bossStart, bossRotation);
            boss.ResetAnimation();

            // ── 카메라 ──
            Transform playerHead = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (playerHead == null) playerHead = player;
            Transform bossHead = boss.Head;
            var room = new Vector2(Mathf.Max(0f, roomHalf.x - tuning.wideWallMargin), Mathf.Max(0f, roomHalf.y - tuning.wideWallMargin));

            Vector3 wideA = ClampToRoom(Offset(groundStart, right, forward, tuning.wideA), arenaCenter, room);
            Vector3 wideB = ClampToRoom(Offset(groundStart, right, forward, tuning.wideB), arenaCenter, room);
            Aim(Shot(BossIntroShot.WideA), wideA, ground + up * tuning.wideLookHeight, tuning.wideFov, 0f);
            Aim(Shot(BossIntroShot.WideB), wideB, (ground + groundStart) * 0.5f + up * tuning.wideLookHeight, tuning.wideFov, 0f);

            CinemachineCamera shoulder = Shot(BossIntroShot.OverShoulder);
            Vector3 shoulderOffset = right * tuning.shoulder.x + up * tuning.shoulder.y + forward * tuning.shoulder.z;
            shoulder.Follow = player;
            shoulder.LookAt = bossHead;
            shoulder.Lens.FieldOfView = tuning.shoulderFov;
            var follow = shoulder.GetComponent<CinemachineFollow>();
            if (follow != null) follow.FollowOffset = shoulderOffset;
            shoulder.transform.SetPositionAndRotation(ground + shoulderOffset, Quaternion.LookRotation(bossHead.position - (ground + shoulderOffset), up));
            shoulder.PreviousStateIsValid = false;

            CinemachineCamera firstPerson = Shot(BossIntroShot.FirstPerson);
            firstPerson.Follow = playerHead;
            firstPerson.LookAt = bossHead;
            firstPerson.Lens.FieldOfView = tuning.firstPersonFov;
            firstPerson.transform.SetPositionAndRotation(playerHead.position, playerRotation);
            firstPerson.PreviousStateIsValid = false;

            Aim(Shot(BossIntroShot.Ground), Offset(groundStart, right, forward, tuning.groundEye), bossStomp + up * tuning.groundLookHeight, tuning.groundFov, tuning.groundDutch);
            SyncGameplayShot();

            if (impulse != null)
            {
                impulse.ImpulseDefinition.ImpulseDuration = tuning.shakeDuration;
                impulse.DefaultVelocity = Vector3.down * tuning.shakeVelocity;
            }

            // ── 화면 ──
            if (titleText != null) titleText.text = tuning.title;
            if (subtitleText != null) subtitleText.text = tuning.subtitle;
            hudDefaults = new bool[hideDuringIntro != null ? hideDuringIntro.Length : 0];
            for (int i = 0; i < hudDefaults.Length; i++)
            {
                if (hideDuringIntro[i] == null) continue;
                hudDefaults[i] = hideDuringIntro[i].activeSelf;
                hideDuringIntro[i].SetActive(false);
            }

            // ── 타임라인: 트랙을 이름으로 잇는다(보스마다 타임라인이 달라도 같은 이름이면 된다) ──
            director.playableAsset = tuning.timeline;
            foreach (PlayableBinding binding in tuning.timeline.outputs)
            {
                if (binding.sourceObject == null) continue;
                if (binding.streamName == CameraTrack) director.SetGenericBinding(binding.sourceObject, brain);
                else if (binding.streamName == LetterboxTrack) director.SetGenericBinding(binding.sourceObject, letterbox);
                else if (binding.streamName == TitleTrack) director.SetGenericBinding(binding.sourceObject, title);
            }
            for (int i = 0; i < shots.Length && i < ShotNames.Length; i++) director.SetReferenceValue(ShotNames[i], shots[i]);
            director.extrapolationMode = DirectorWrapMode.None;

            if (cameraRoot != null) cameraRoot.SetActive(true);
            fired.Clear();
            lastTime = -1f;
            WasSkipped = false;
            IsPlaying = true;
            PlayCount++;
            director.time = 0;
            director.Play();
            UpdateActors(0f);
            return true;
        }

        /// <summary>끝으로 건너뛴다(배우 · 카메라는 싸움 시작 상태가 된다).</summary>
        public void Skip()
        {
            if (!IsPlaying) return;
            WasSkipped = true;
            director.Stop(); // stopped 이벤트 → Finish
        }

        private void Update()
        {
            if (!IsPlaying) return;
            float time = (float)director.time;
            if (tuning.allowSkip && time >= tuning.skipLockTime && SkipPressed())
            {
                Skip();
                return;
            }
            UpdateActors(time);
        }

        private void UpdateActors(float time)
        {
            UpdatePlayer(time);
            UpdateBoss(time);
            cues.Clear();
            script.Cues(lastTime, time, cues);
            foreach (BossIntroCue cue in cues) Fire(cue);
            lastTime = time;
            SyncGameplayShot();
        }

        // ───────────────────────── 배우 ─────────────────────────

        private void UpdatePlayer(float time)
        {
            player.SetPositionAndRotation(Vector3.Lerp(playerEntrance, playerStart, script.WalkProgress(time)), playerRotation);
            // 앞으로 걷기. 걷는 빠르기에 맞춰 느리게 재생해 발이 미끄러지지 않게 하고, 마지막에는 천천히 선다
            playerAnimator.SetFloat(MoveXId, 0f);
            playerAnimator.SetFloat(MoveYId, 1f);
            playerAnimator.SetFloat(MoveAmountId, script.WalkAmount(time));
            playerAnimator.SetFloat(MoveSpeedId, time < tuning.walkEnd ? walkSpeedParameter : 1f);
            SetPlayerVisible(script.BodyVisible(time)); // 1인칭 동안은 몸을 숨긴다(카메라가 머리 속에 있으므로)
        }

        private void UpdateBoss(float time)
        {
            Vector3 position = Vector3.Lerp(bossStart, bossStomp, script.BossAdvance(time));
            // 주춤: 상체를 뒤로 젖혔다가 돌아옴
            Quaternion rotation = bossRotation * Quaternion.Euler(-tuning.recoilLean * script.BossLean(time), 0f, 0f);
            boss.transform.SetPositionAndRotation(position, rotation);
        }

        private void Fire(BossIntroCue cue)
        {
            fired.Add(cue);
            switch (cue)
            {
                case BossIntroCue.Crouch: boss.PlaySignal("ChargeReady"); break;
                case BossIntroCue.Charge: boss.PlaySignal("Charge"); break;
                case BossIntroCue.Pounce: boss.PlaySignal("Stomp"); Shake(tuning.pounceShake); break; // 덮쳐서 넘어짐
                case BossIntroCue.StompImpact: Shake(tuning.stompShake); break;                           // 발로 찍음
                case BossIntroCue.ClawRight: Claw(false); break;
                case BossIntroCue.ClawLeft: Claw(true); break;
                case BossIntroCue.Recoil: boss.PlaySignal("Land"); break;                                // 주춤하며 떠오름 → 호버링
            }
        }

        private void Claw(bool left)
        {
            if (tuning.clawEffect != null && outputCamera != null)
            {
                // 1인칭 화면 앞에 그대로 생성. 왼손은 좌우를 뒤집는다
                Transform view = outputCamera.transform;
                GameObject effect = Instantiate(tuning.clawEffect, view.position, view.rotation);
                float s = tuning.clawScale;
                effect.transform.localScale = new Vector3(left ? -s : s, s, s);
            }
            if (boss.Flash != null) boss.Flash.Flash();
            Shake(tuning.clawShake);
        }

        private void Shake(float force)
        {
            if (impulse != null && force > 0f) impulse.GenerateImpulseWithForce(force);
        }

        // ───────────────────────── 종료 / 복구 ─────────────────────────

        private void OnDirectorStopped(PlayableDirector stopped)
        {
            if (IsPlaying) Finish();
        }

        private void Finish()
        {
            IsPlaying = false;

            // 배우를 싸움 시작 상태로(끝까지 봤든 건너뛰었든 같은 상태)
            if (player != null)
            {
                player.SetPositionAndRotation(playerStart, playerRotation);
                playerAnimator.SetFloat(MoveAmountId, 0f);
                playerAnimator.SetFloat(MoveSpeedId, 1f);
                SetPlayerVisible(true);
                if (playerBody != null) playerBody.enabled = playerBodyWasEnabled;
                for (int i = 0; i < playerDrivers.Length; i++)
                {
                    if (playerDrivers[i] != null) playerDrivers[i].enabled = playerDriverDefaults[i];
                }
            }
            if (playerHealth != null) playerHealth.DamageBlocked = playerBlock;
            if (bossHealth != null) bossHealth.DamageBlocked = bossBlock;
            if (boss != null)
            {
                boss.transform.SetPositionAndRotation(bossStart, bossRotation);
                boss.ResetAnimation();
            }

            // 카메라: 타임라인이 놓았으므로 Brain이 게임 카메라로 돌아간다
            if (cameraRoot != null) cameraRoot.SetActive(false);
            if (letterbox != null) letterbox.SetActive(false);
            if (title != null) title.SetActive(false);
            for (int i = 0; i < hudDefaults.Length; i++)
            {
                if (hideDuringIntro[i] != null) hideDuringIntro[i].SetActive(hudDefaults[i]);
            }

            Action callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }

        // ───────────────────────── 도움 ─────────────────────────

        private CinemachineCamera Shot(BossIntroShot shot) => shots[(int)shot];

        /// <summary>제자리에서 한 점을 바라보는 카메라(움직임은 앞뒤 컷과의 블렌드로 만든다).</summary>
        private static void Aim(CinemachineCamera shot, Vector3 position, Vector3 lookAt, float fov, float dutch)
        {
            shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            shot.Lens.FieldOfView = fov;
            shot.Lens.Dutch = dutch;
            shot.PreviousStateIsValid = false;
        }

        /// <summary>마지막 컷을 게임 카메라와 같은 자리 · 화각으로 맞춘다 → 연출 끝 화면 = 싸움 시작 화면.</summary>
private void SyncGameplayShot()
        {
            CinemachineCamera shot = Shot(BossIntroShot.Gameplay);
            if (playerCamera != null)
            {
                CameraState state = playerCamera.State;
                shot.transform.SetPositionAndRotation(state.GetFinalPosition(), state.GetFinalOrientation());
                shot.Lens.FieldOfView = state.Lens.FieldOfView;
            }
            else if (outputCamera != null && !IsPlaying)
            {
                // 게임 카메라를 모르면 재생 직전의 화면 자리로
                shot.transform.SetPositionAndRotation(outputCamera.transform.position, outputCamera.transform.rotation);
                shot.Lens.FieldOfView = outputCamera.fieldOfView;
            }
        }

        private static Vector3 Offset(Vector3 origin, Vector3 right, Vector3 forward, Vector3 local)
        {
            return origin + right * local.x + Vector3.up * local.y + forward * local.z;
        }

        private static Vector3 ClampToRoom(Vector3 position, Vector3 center, Vector2 half)
        {
            return new Vector3(Mathf.Clamp(position.x, center.x - half.x, center.x + half.x), position.y, Mathf.Clamp(position.z, center.z - half.y, center.z + half.y));
        }

        /// <summary>아무 키 · 마우스 버튼. F1 ~ F12(디버그 창)는 뺀다.</summary>
        private static bool SkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (KeyControl key in keyboard.allKeys)
                {
                    if (key == null || !key.wasPressedThisFrame) continue;
                    if (key.keyCode >= Key.F1 && key.keyCode <= Key.F12) continue;
                    return true;
                }
            }
            Mouse mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        }

        private void SetPlayerVisible(bool visible)
        {
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                Renderer target = playerRenderers[i];
                bool on = visible && playerRendererDefaults[i];
                if (target != null && target.enabled != on) target.enabled = on;
            }
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
