using Game.Rules;
using UnityEngine;
using UnityEngine.Playables;

namespace Game
{
    /// <summary>
    /// 보스 등장 연출 하나의 수치(BALANCE 15번). 보스마다 `BossTuning.intro`로 고른다(비면 연출 없이 이름만 잠깐 뜨고 싸움 시작).
    /// 예전 까치 보스전(main)의 BossIntroCinematic · BossIntroSetup 값을 옮겼다 — 시간 · 컷 순서 · 화각 · 흔들림 세기는 그대로,
    /// 거리 · 위치는 예전 캐릭터 배율 2.75로 나눴다(걷는 거리만 지금 보스 방 크기에 맞춰 더 줄어들 수 있다).
    /// 타임라인 에셋(카메라 컷 · 검은 띠 · 이름 타이틀)은 `BossIntroSetup`이 이 값으로 만든다 — 시간을 고친 뒤 Setup Floor를 다시 실행.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Boss Intro Tuning", fileName = "BossIntroTuning")]
    public sealed class BossIntroTuning : ScriptableObject
    {
        [Tooltip("이 값으로 만든 타임라인(카메라 컷 · 검은 띠 · 이름 타이틀). BossIntroSetup이 만들고 잇는다")]
        public PlayableAsset timeline;

        [Header("이름 타이틀")]
        public string title = "까치 대장";
        public string subtitle = "보물 둥지의 수호자";
        [Min(0f)] public float titleStart = 11f;
        [Min(0f)] public float titleEnd = 12.3f;
        [Tooltip("위아래 검은 띠가 사라지는 때(초). 처음부터 켜져 있다")]
        [Min(0f)] public float letterboxEnd = 12.2f;

        [Header("시간(초) — 카메라")]
        [Tooltip("와이드가 흘러가기 시작 · 끝")]
        [Min(0f)] public float wideDriftStart = 0.3f;
        [Min(0f)] public float wideDriftEnd = 3f;
        [Tooltip("와이드 → 어깨 너머 3인칭(컷)")]
        [Min(0f)] public float thirdPersonCut = 4.5f;
        [Tooltip("3인칭 → 1인칭으로 카메라가 머리 속으로 들어가는 구간")]
        [Min(0f)] public float firstPersonBlendStart = 6f;
        [Min(0f)] public float firstPersonBlendEnd = 7f;
        [Tooltip("1인칭 카메라가 바닥에 다 떨어지는 때(돌진 도착부터)")]
        [Min(0f)] public float fallBlendEnd = 8.9f;
        [Tooltip("게임 카메라로 이어지는 구간(시작할 때 몸을 다시 보임)")]
        [Min(0f)] public float gameplayBlendStart = 11.2f;
        [Min(0f)] public float gameplayBlendEnd = 12.4f;
        [Min(0f)] public float end = 12.6f;

        [Header("시간(초) — 배우")]
        [Tooltip("걸어 들어와 멈추는 때")]
        [Min(0f)] public float walkEnd = 7f;
        [Tooltip("걷다가 마지막 이 시간 동안 천천히 멈춤")]
        [Min(0.01f)] public float walkStopTime = 0.4f;
        [Tooltip("1인칭이 되기 직전에 몸을 숨기는 때")]
        [Min(0f)] public float hideBodyTime = 6.6f;
        [Tooltip("보스가 웅크리는 때")]
        [Min(0f)] public float crouchTime = 7.3f;
        [Min(0f)] public float chargeStart = 7.9f;
        [Tooltip("돌진 도착 = 넘어짐 + 밟기 시작")]
        [Min(0f)] public float chargeEnd = 8.6f;
        [Tooltip("밟기 동작을 시작한 뒤 발이 바닥에 닿기까지(밟기 클립 GGachi_Stomp의 내리찍는 순간)")]
        [Min(0f)] public float stompDelay = 0.68f;
        [Tooltip("할퀴기(오른손 → 왼손)")]
        [Min(0f)] public float claw1 = 10.5f;
        [Min(0f)] public float claw2 = 10.75f;
        [Tooltip("보스가 주춤하며 제자리로 물러나는 구간")]
        [Min(0f)] public float recoilStart = 10.9f;
        [Min(0f)] public float recoilEnd = 11.9f;

        [Header("거리(m) — 예전 값 ÷ 2.75")]
        [Tooltip("방에 들어선 자리에서 보스 쪽으로 걸어 들어오는 거리(예전 18). 보스와의 거리가 아래 값보다 가까워지지 않게 줄어든다")]
        [Min(0f)] public float walkDistance = 6.5f;
        [Tooltip("걸어 들어와 멈췄을 때 보스(방 가운데)와 적어도 이만큼 떨어진다 = 싸움을 시작하는 거리")]
        [Min(0f)] public float minBossDistance = 4.5f;
        [Tooltip("밟을 때 보스 몸이 플레이어 앞 이만큼 떨어진 곳에 선다(예전 2.6). 밟기 클립의 발이 몸 앞에 내려와 누운 가슴 위를 찍는다")]
        [Min(0f)] public float stompDistance = 0.95f;
        [Tooltip("MoveSpeed 1일 때 걷기 동작이 가는 속도(m/s) = PlayerTuning.moveSpeed. 걷는 빠르기에 맞춰 동작을 느리게 재생해 발이 미끄러지지 않게 한다")]
        [Min(0.1f)] public float walkClipSpeed = 4f;
        [Tooltip("걷기 동작 배속에 곱하는 값(예전 0.55 = 18 m ÷ 7 s ÷ 5 m/s × 1.07)")]
        [Min(0.1f)] public float walkAnimScale = 1.07f;

        [Header("카메라 자리 — x 오른쪽 · y 위 · z 앞(플레이어가 보스를 보는 방향), 싸움 시작 자리 기준")]
        [Tooltip("와이드 시작(예전 앞 12 · 왼쪽 20 · 위 16). 들어선 자리를 내려다봄")]
        public Vector3 wideA = new Vector3(-7.27f, 5.82f, 4.36f);
        [Tooltip("와이드 끝(예전 앞 8 · 왼쪽 17 · 위 13). 걷는 길 가운데를 봄")]
        public Vector3 wideB = new Vector3(-6.18f, 4.73f, 2.91f);
        [Tooltip("와이드가 보는 높이(예전 3)")]
        public float wideLookHeight = 1.09f;
        [Tooltip("벽 안쪽 면에서 이만큼 안으로 와이드 카메라를 당긴다(방이 좁을 때 벽 밖으로 나가지 않게)")]
        [Min(0f)] public float wideWallMargin = 0.5f;
        [Min(1f)] public float wideFov = 50f;
        [Tooltip("어깨 너머 3인칭: 플레이어 발밑 기준(예전 뒤 9 · 오른쪽 2.2 · 위 6.5). 보스 머리를 봄")]
        public Vector3 shoulder = new Vector3(0.8f, 2.36f, -3.27f);
        [Min(1f)] public float shoulderFov = 55f;
        [Tooltip("1인칭: 플레이어 머리 뼈에 붙어 보스 머리를 봄")]
        [Min(1f)] public float firstPersonFov = 65f;
        [Tooltip("바닥 1인칭 눈 자리(예전 뒤 1.2 · 위 0.75)")]
        public Vector3 groundEye = new Vector3(0f, 0.27f, -0.44f);
        [Tooltip("바닥 1인칭이 보는 높이: 밟으러 온 보스 발밑에서 위로(예전 5.5)")]
        public float groundLookHeight = 2f;
        [Min(1f)] public float groundFov = 70f;
        [Tooltip("바닥 1인칭 화면 기울기(도)")]
        public float groundDutch = 12f;

        [Header("이펙트 · 흔들림")]
        [Tooltip("물러날 때 상체를 뒤로 젖히는 각도(도)")]
        public float recoilLean = 14f;
        [Tooltip("할퀴기 이펙트(플레이어 근접과 같은 것). 1인칭 화면 앞에 그대로 생성")]
        public GameObject clawEffect;
        [Tooltip("할퀴기 이펙트 크기(예전 1 ÷ 2.75)")]
        [Min(0.01f)] public float clawScale = 0.36f;
        [Tooltip("흔들림 한 번의 기본 속도(아래 방향, m/s — 예전 0.6 ÷ 2.75)와 길이(초)")]
        [Min(0f)] public float shakeVelocity = 0.22f;
        [Min(0.01f)] public float shakeDuration = 0.35f;
        [Tooltip("흔들림 세기: 덮쳐서 넘어짐 · 발로 찍음 · 할퀴기")]
        [Min(0f)] public float pounceShake = 0.7f;
        [Min(0f)] public float stompShake = 1.6f;
        [Min(0f)] public float clawShake = 0.5f;

        [Header("건너뛰기")]
        [Tooltip("아무 키 · 마우스 버튼으로 끝으로 건너뛴다(F1 ~ F12는 뺌)")]
        public bool allowSkip = true;
        [Tooltip("시작하고 이 시간(초) 동안은 건너뛰기를 받지 않는다(방에 들어서며 누르던 키로 넘어가지 않게)")]
        [Min(0f)] public float skipLockTime = 1f;

        public BossIntroTimes ToTimes()
        {
            return new BossIntroTimes
            {
                WideDriftStart = wideDriftStart,
                WideDriftEnd = wideDriftEnd,
                ThirdPersonCut = thirdPersonCut,
                FirstPersonBlendStart = firstPersonBlendStart,
                FirstPersonBlendEnd = firstPersonBlendEnd,
                WalkEnd = walkEnd,
                WalkStopTime = walkStopTime,
                HideBodyTime = hideBodyTime,
                CrouchTime = crouchTime,
                ChargeStart = chargeStart,
                ChargeEnd = chargeEnd,
                FallBlendEnd = fallBlendEnd,
                StompDelay = stompDelay,
                Claw1 = claw1,
                Claw2 = claw2,
                RecoilStart = recoilStart,
                RecoilEnd = recoilEnd,
                GameplayBlendStart = gameplayBlendStart,
                GameplayBlendEnd = gameplayBlendEnd,
                End = end,
                TitleStart = titleStart,
                TitleEnd = titleEnd,
                LetterboxEnd = letterboxEnd,
            };
        }

        public BossIntroScript ToScript() => new BossIntroScript(ToTimes());
    }
}
