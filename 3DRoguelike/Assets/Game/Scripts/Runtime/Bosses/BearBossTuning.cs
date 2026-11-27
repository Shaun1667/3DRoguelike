using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 반달가슴곰 보스 수치(BALANCE 15-2, `Data/Bosses/BearBossTuning.asset`). 공통 칸은 <see cref="BossTuning"/>, 패턴은 <see cref="BearBoss"/>.
    /// 땅을 울리는 무거운 근접형: 앞발 휘두르기(앞 부채꼴) · 일어서서 내려찍기(예고 원 · 크게 밀림) · 땅 긁고 돌진(예고 길, 벽에 박으면 기절).
    /// 강화(격노) = 포효 한 번 + 예고 · 빈틈이 짧아지고(tempo) 돌진이 빨라짐 · 휘두르기 두 번 · 내려찍기 범위 넓어짐.
    /// 거리는 모델 길이(<see cref="BossTuning.modelScale"/> = 앞뒤 길이 m, 모델은 길이 1 기준) 기준.
    /// 동작 재생 빠르기는 BossSetup이 이 시간들로 계산해 컨트롤러에 굽는다 — 시간을 바꾸면 Setup Floor를 다시 실행한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Boss Tuning (Bear)", fileName = "BearBossTuning")]
    public sealed class BearBossTuning : BossTuning
    {
        public override BossPattern[] Patterns => BossPatternSets.Bear;

        public BearBossTuning()
        {
            // 공통 칸의 곰 값(새 에셋을 만들 때만 쓰인다 — 에셋 값이 우선)
            displayName = "반달가슴곰";
            enragedLabel = "격노";
            maxHealth = 350;
            introTime = 2.5f;
            fightDelay = 0.8f;
            turnSpeed = 90f;
            idleTime = new Vector2(1.3f, 2.1f);
            enragedIdleTime = new Vector2(0.7f, 1.2f);
            enrageHealthRatio = 0.5f;
            bodyRadius = 1.8f;
            contactDamage = 10;
            contactName = "몸통 부딪치기";
            modelScale = 4.5f;
            corpseTime = 4f;
        }

        [Header("움직임")]
        [Tooltip("대기 중 플레이어가 이보다 멀면 걸어서 다가간다(m, 몸 가운데끼리)")]
        [Min(0f)] public float idleWalkDistance = 7f;
        [Tooltip("걷기 빠르기(m/s)")]
        [Min(0.1f)] public float walkSpeed = 1.0f;
        [Tooltip("휘두르기 전에 달려 다가가는 빠르기(m/s)")]
        [Min(0.1f)] public float runSpeed = 5.0f;
        [Tooltip("달리면서 도는 각도(도/초)")]
        [Min(0f)] public float runTurnSpeed = 220f;
        [Tooltip("몸통 피해 높이(m) — 플레이어 발이 이보다 높으면 몸통에 안 맞음")]
        [Min(0f)] public float contactHeight = 2.5f;
        [Tooltip("몸통에 부딪히면 곰 바깥으로 밀려나는 빠르기(m/s) — 몸 안에 갇혀 계속 맞지 않게")]
        [Min(0f)] public float contactPush = 8f;

        [Tooltip("강화 뒤 빠르기 배율: 예고 · 빈틈 시간은 이것으로 나누고 돌진 빠르기에는 곱한다(동작도 같은 배율로 빨라짐)")]
        [Min(1f)] public float enragedTempo = 1.25f;

        [Header("패턴 1: 앞발 휘두르기")]
        [Tooltip("이 거리 안이면 휘두르기 시작(m, 몸 가운데끼리)")]
        [Min(0f)] public float swipeStartRange = 4.5f;
        [Tooltip("다가가는 최대 시간(초) — 넘으면 그 자리에서 휘두름")]
        [Min(0f)] public float swipeApproachTime = 2f;
        [Tooltip("예고(바닥 부채꼴 + 앞발을 드는 동작) 시간(초) — 동작의 맞는 순간(swipeHitFrame)이 이 끝에 온다")]
        [Min(0.05f)] public float swipeWindup = 0.75f;
        [Tooltip("휘두르기 동작에서 앞발이 닿는 순간(초, 원래 빠르기)")]
        [Min(0.01f)] public float swipeHitTime = 0.56f;
        [Tooltip("부채꼴 반지름(m, 몸 가운데부터)")]
        [Min(0.1f)] public float swipeRadius = 4.6f;
        [Tooltip("부채꼴 전체 각도(도)")]
        [Range(10f, 360f)] public float swipeAngle = 120f;
        [Min(0)] public int swipeDamage = 20;
        [Tooltip("맞으면 밀려나는 빠르기(m/s, 곰 앞쪽으로)")]
        [Min(0f)] public float swipePush = 6f;
        [Tooltip("휘두른 뒤 빈틈(초)")]
        [Min(0f)] public float swipeRecover = 0.7f;
        [Tooltip("강화 뒤 휘두르는 횟수")]
        [Min(1)] public int enragedSwipes = 2;
        public string swipeName = "앞발 휘두르기";

        [Header("패턴 2: 일어서서 내려찍기")]
        [Tooltip("일어서는 예고(바닥 원) 시간(초)")]
        [Min(0.05f)] public float slamWindup = 1.1f;
        [Tooltip("일어선 자세에서 앞발이 땅에 닿기까지(초, 원래 빠르기)")]
        [Min(0.01f)] public float slamDropTime = 0.3f;
        [Tooltip("원 가운데: 몸 가운데에서 앞으로(m)")]
        [Min(0f)] public float slamReach = 2.1f;
        [Min(0.1f)] public float slamRadius = 3.6f;
        [Tooltip("강화 뒤 원 반지름 배율")]
        [Min(1f)] public float enragedSlamRadiusMultiplier = 1.3f;
        [Min(0)] public int slamDamage = 30;
        [Tooltip("맞으면 원 가운데에서 바깥으로 밀려나는 빠르기(m/s) — 크게 밀림(줄어드는 빠르기 PlayerTuning.pushDecay 5 → 약 이 값 ÷ 5 m)")]
        [Min(0f)] public float slamPush = 20f;
        [Tooltip("플레이어 발이 바닥에서 이만큼 넘게 떠 있으면(점프) 맞지 않음(m)")]
        [Min(0f)] public float slamJumpClearance = 0.4f;
        [Tooltip("내려찍은 뒤 빈틈(초)")]
        [Min(0f)] public float slamRecover = 1.0f;
        [Tooltip("땅에 닿을 때 화면 흔들기 세기")]
        [Min(0f)] public float slamShake = 0.7f;
        public string slamName = "내려찍기";

        [Header("패턴 3: 땅 긁고 돌진")]
        [Tooltip("땅을 긁는 예고(바닥에 돌진 길) 시간(초)")]
        [Min(0.05f)] public float chargeWindup = 1.0f;
        [Min(0.1f)] public float chargeSpeed = 8.5f;
        [Tooltip("최대 거리(m). 그 전에 벽(경기장 끝)에 닿으면 박고 기절")]
        [Min(0f)] public float chargeDistance = 30f;
        [Tooltip("예고 길 폭(m)")]
        [Min(0.1f)] public float chargeLaneWidth = 3.2f;
        [Min(0)] public int chargeDamage = 25;
        [Tooltip("맞으면 옆으로 밀려나는 빠르기(m/s)")]
        [Min(0f)] public float chargePush = 10f;
        [Tooltip("벽에 박은 뒤 기절(초) — 마음껏 때릴 틈")]
        [Min(0f)] public float stunTime = 2.5f;
        [Tooltip("벽에 안 박고 멈췄을 때 빈틈(초)")]
        [Min(0f)] public float chargeRecover = 0.6f;
        [Tooltip("벽에 박을 때 화면 흔들기 세기")]
        [Min(0f)] public float wallShake = 0.9f;
        public string chargeName = "돌진";

        [Header("패턴 4: 하울링 (격노 전환도 이것 한 번)")]
        [Tooltip("예고(곰 둘레 바닥 원) 시간(초) — 포효 동작의 포효 시작(howlHitTime)이 이 끝에 온다")]
        [Min(0.05f)] public float howlWindup = 1.2f;
        [Tooltip("포효 동작에서 입을 크게 벌리는 순간(초, 원래 빠르기) = 피해 순간")]
        [Min(0.01f)] public float howlHitTime = 0.7f;
        [Tooltip("피해 원 반지름(m, 곰 몸 가운데부터)")]
        [Min(0.1f)] public float howlRadius = 6.5f;
        [Tooltip("격노 뒤 원 반지름 배율")]
        [Min(1f)] public float enragedHowlRadiusMultiplier = 1.2f;
        [Min(0)] public int howlDamage = 20;
        [Tooltip("맞으면 곰에서 바깥으로 밀려나는 빠르기(m/s, 약 이 값 ÷ 5 m)")]
        [Min(0f)] public float howlPush = 14f;
        [Tooltip("포효 뒤 빈틈(초) — 포효가 이어지는 동안")]
        [Min(0f)] public float howlRecover = 1.0f;
        [Tooltip("포효 때 화면 흔들기 세기(포효 구간 동안)")]
        [Min(0f)] public float howlShake = 0.6f;
        public string howlName = "하울링";

        [Header("포효 (등장 — 피해 없음)")]
        [Tooltip("포효 동작 길이(초, 원래 빠르기)")]
        [Min(0f)] public float roarTime = 2f;
        [Tooltip("포효 동안 화면이 흔들리는 구간(초)")]
        public Vector2 roarShakeWindow = new Vector2(0.7f, 1.5f);
        [Min(0f)] public float roarShake = 0.35f;
        [Tooltip("싸움을 시작할 때 포효한다(짧은 등장 — 이름 표시와 함께)")]
        public bool roarOnEntrance = true;
    }
}
