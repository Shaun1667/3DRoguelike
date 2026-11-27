using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 까치 보스 수치(BALANCE 15번, `Data/Bosses/MagpieBossTuning.asset`). 공통 칸은 <see cref="BossTuning"/>, 패턴은 <see cref="MagpieBoss"/>.
    /// 예전 까치 보스전(main) 값을 옮겼다 — 거리 · 속도는 예전 캐릭터 배율 2.75로 나누고, 피해는 예전 플레이어 체력 5 → 지금 100에 맞춰 늘림.
    /// (2026-11-27 보스 종마다 나눔: 이 파일은 예전 BossTuning.cs 그대로 — GUID가 같아 에셋 값이 이어진다.)
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Boss Tuning (Magpie)", fileName = "MagpieBossTuning")]
    public sealed class MagpieBossTuning : BossTuning
    {
        public override BossPattern[] Patterns => BossPatternSets.Magpie;

        public MagpieBossTuning()
        {
            maxHealth = 500; // 2026-11-27 사용자 결정(예전 300) — 새 에셋을 만들 때만 쓰인다, 에셋 값이 우선
        }

        [Header("패턴 1: 돌진")]
        [Tooltip("예고(바닥에 돌진 길) 시간(초)")]
        [Min(0f)] public float chargeWindup = 0.9f;
        [Min(0.1f)] public float chargeSpeed = 10.9f;
        [Tooltip("최대 거리(m). 방 벽에서 멈춘다")]
        [Min(0f)] public float chargeDistance = 14.5f;
        [Tooltip("예고 길 폭(m)")]
        [Min(0.1f)] public float chargeLaneWidth = 1.8f;
        [Min(0)] public int chargeDamage = 20;
        [Tooltip("강화 뒤 돌진 속도 배율")]
        [Min(1f)] public float enragedChargeSpeedMultiplier = 1.25f;
        [Tooltip("돌진 뒤 빈틈(초)")]
        [Min(0f)] public float chargeRecover = 0.4f;
        public string chargeName = "돌진";

        [Header("패턴 2: 깃털 날리기")]
        [Tooltip("준비 동작(몸을 부풀림) 시간(초)")]
        [Min(0f)] public float featherWindup = 0.6f;
        [Min(1)] public int featherCount = 7;
        [Min(1)] public int enragedFeatherCount = 11;
        [Tooltip("부채꼴 전체 각도(도)")]
        [Range(0f, 180f)] public float featherSpread = 70f;
        [Min(1)] public int volleys = 1;
        [Min(1)] public int enragedVolleys = 2;
        [Tooltip("연사 간격(초)")]
        [Min(0f)] public float volleyInterval = 0.55f;
        [Min(0.1f)] public float featherSpeed = 6.5f;
        [Tooltip("깃털이 나오는 높이(m)")]
        [Min(0f)] public float featherSpawnHeight = 1.45f;
        [Min(0)] public int featherDamage = 10;
        [Tooltip("플레이어 몸 가운데와 이 거리 안이면 맞음(m)")]
        [Min(0.05f)] public float featherHitRadius = 0.5f;
        [Tooltip("플레이어 발밑에서 몸 가운데까지(m) — 조준 · 판정")]
        [Min(0f)] public float playerCenterHeight = 0.9f;
        [Min(0.1f)] public float featherLifetime = 4f;
        [Tooltip("마지막 연사 뒤 빈틈(초)")]
        [Min(0f)] public float featherRecover = 0.3f;
        public FeatherProjectile featherPrefab;
        public string featherName = "깃털 날리기";

        [Header("패턴 3: 급강하 내려찍기")]
        [Min(0.01f)] public float slamRiseTime = 0.6f;
        [Tooltip("떠오르는 높이(m)")]
        [Min(0f)] public float slamHeight = 4.4f;
        [Tooltip("공중에서 플레이어 위를 따라다니는 시간(초) — 바닥 예고 원이 따라옴")]
        [Min(0f)] public float slamTrackTime = 1f;
        [Min(0f)] public float slamTrackSpeed = 5.1f;
        [Tooltip("낙하 지점을 고정하고 기다리는 시간(초) — 예고 원이 멈춤 = 피할 시간")]
        [Min(0f)] public float slamLockTime = 0.45f;
        [Min(0.01f)] public float slamFallTime = 0.22f;
        [Min(0.1f)] public float slamRadius = 2.9f;
        [Min(0)] public int slamDamage = 30;
        [Tooltip("플레이어 발이 바닥에서 이만큼 넘게 떠 있으면(점프) 맞지 않음(m)")]
        [Min(0f)] public float slamJumpClearance = 0.4f;
        [Tooltip("착지 뒤 빈틈(초), 강화 전 · 뒤")]
        [Min(0f)] public float slamRecover = 0.8f;
        [Min(0f)] public float enragedSlamRecover = 0.5f;
        public string slamName = "급강하";
    }
}
