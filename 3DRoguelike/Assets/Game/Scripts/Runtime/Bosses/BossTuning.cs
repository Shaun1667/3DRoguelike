using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 보스 하나의 수치(BALANCE 15번). 던전마다 `WorldTuning.dungeons[].boss`로 고른다(비면 그 던전 보스 방은 일반 적).
    /// 예전 까치 보스전(main) 값을 옮겼다 — 거리 · 속도는 예전 캐릭터 배율 2.75로 나누고, 피해는 예전 플레이어 체력 5 → 지금 100에 맞춰 늘림.
    /// 새 보스 = 이 에셋 하나 + 프리팹(`BossSetup`).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Boss Tuning", fileName = "BossTuning")]
    public sealed class BossTuning : ScriptableObject
    {
        [Tooltip("화면 위 체력 막대 · 등장 이름 · 사망 원인 '{이름}의 {공격}'")]
        public string displayName = "까치";
        [Tooltip("체력이 강화 비율 아래로 떨어지면 이름 옆에 붙는 글")]
        public string enragedLabel = "격노";
        [Tooltip("보스 방에 놓을 프리팹(Boss 컴포넌트)")]
        public Boss prefab;
        [Tooltip("등장 연출(Timeline + Cinemachine) 수치. 비면 연출 없이 아래 introTime 동안 이름만 뜨고 싸움 시작")]
        public BossIntroTuning intro;


        [Header("기본")]
        [Min(1)] public int maxHealth = 300;
        [Min(0f)] public float hitFlashTime = 0.08f;
        [Tooltip("등장 연출이 없을 때: 방에 들어선 뒤 이름이 떠 있는 시간(초)")]
        [Min(0f)] public float introTime = 2f;
        [Tooltip("싸움이 시작된 뒤(등장 연출이 있으면 끝난 뒤) 첫 패턴 전 대기(초) — 이 동안은 공격하지 않는다(맞기는 함)")]
        [Min(0f)] public float fightDelay = 1.5f;
        [Tooltip("초당 회전 각도")]
        [Min(0f)] public float turnSpeed = 140f;
        [Tooltip("대기 시간 범위(초), 강화 전")]
        public Vector2 idleTime = new Vector2(1.2f, 2f);
        [Tooltip("대기 시간 범위(초), 강화 뒤")]
        public Vector2 enragedIdleTime = new Vector2(0.6f, 1.1f);
        [Tooltip("체력이 최대의 이 비율 이하가 되면 강화(대기 짧아짐 · 돌진 빨라짐 · 깃털 많아짐 · 착지 빈틈 짧아짐)")]
        [Range(0f, 1f)] public float enrageHealthRatio = 0.5f;
        [Tooltip("몸통 반지름(m, 높이 뺌). 이 안에 있으면 몸통 박치기")]
        [Min(0.1f)] public float bodyRadius = 1.1f;
        [Min(0)] public int contactDamage = 10;
        public string contactName = "몸통 박치기";
        [Tooltip("모델 크기 배율(예전 8 ÷ 캐릭터 배율 2.75)")]
        [Min(0.01f)] public float modelScale = 2.9f;
        [Tooltip("죽은 뒤 몸이 사라질 때까지(초)")]
        [Min(0f)] public float corpseTime = 3f;

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

        public BossPatternSettings ToPatternSettings()
        {
            return new BossPatternSettings(idleTime.x, idleTime.y, enragedIdleTime.x, enragedIdleTime.y, enrageHealthRatio);
        }
    }
}
