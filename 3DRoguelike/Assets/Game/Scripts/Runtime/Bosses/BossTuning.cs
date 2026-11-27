using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 보스 하나의 수치 — 모든 보스가 같이 쓰는 칸(BALANCE 15번). 던전마다 `WorldTuning.dungeons[].boss`로 고른다(비면 그 던전 보스 방은 일반 적).
    /// 보스 종마다 이것을 이어받은 수치 클래스 하나(`MagpieBossTuning` · `BearBossTuning`)가 패턴 칸과 고를 패턴 목록(<see cref="Patterns"/>)을 더한다.
    /// 새 보스 = 수치 클래스 + 에셋 하나(`Data/Bosses/{이름}BossTuning.asset`) + 패턴을 하는 <see cref="Boss"/> 클래스 + 프리팹(`BossSetup`).
    /// </summary>
    public abstract class BossTuning : ScriptableObject
    {
        [Tooltip("화면 위 체력 막대 · 등장 이름 · 사망 원인 '{이름}의 {공격}'")]
        public string displayName = "까치";
        [Tooltip("체력이 강화 비율 아래로 떨어지면 이름 옆에 붙는 글")]
        public string enragedLabel = "격노";
        [Tooltip("보스 방에 놓을 프리팹(Boss 컴포넌트)")]
        public Boss prefab;
        [Tooltip("등장 연출(Timeline + Cinemachine) 수치. 비면 연출 없이 아래 introTime 동안 이름만 뜨고 싸움 시작(보스 클래스의 짧은 등장 동작이 있으면 그것)")]
        public BossIntroTuning intro;


        [Header("기본")]
        [Min(1)] public int maxHealth = 300;
        [Min(0f)] public float hitFlashTime = 0.08f;
        [Tooltip("등장 연출이 없을 때: 방에 들어선 뒤 이름이 떠 있는 시간(초)")]
        [Min(0f)] public float introTime = 2f;
        [Tooltip("싸움이 시작된 뒤(등장 연출 · 등장 동작이 있으면 끝난 뒤) 첫 패턴 전 대기(초) — 이 동안은 공격하지 않는다(맞기는 함)")]
        [Min(0f)] public float fightDelay = 1.5f;
        [Tooltip("초당 회전 각도")]
        [Min(0f)] public float turnSpeed = 140f;
        [Tooltip("대기 시간 범위(초), 강화 전")]
        public Vector2 idleTime = new Vector2(1.2f, 2f);
        [Tooltip("대기 시간 범위(초), 강화 뒤")]
        public Vector2 enragedIdleTime = new Vector2(0.6f, 1.1f);
        [Tooltip("체력이 최대의 이 비율 이하가 되면 강화")]
        [Range(0f, 1f)] public float enrageHealthRatio = 0.5f;
        [Tooltip("몸통 반지름(m, 높이 뺌). 이 안에 있으면 몸통 박치기")]
        [Min(0.1f)] public float bodyRadius = 1.1f;
        [Min(0)] public int contactDamage = 10;
        public string contactName = "몸통 박치기";
        [Tooltip("모델 크기 배율")]
        [Min(0.01f)] public float modelScale = 2.9f;
        [Tooltip("죽은 뒤 몸이 사라질 때까지(초)")]
        [Min(0f)] public float corpseTime = 3f;

        /// <summary>이 보스가 고르는 패턴(<see cref="BossPatternSets"/>, 순서가 시드 결과를 정한다).</summary>
        public abstract BossPattern[] Patterns { get; }

        public BossPatternSettings ToPatternSettings()
        {
            return new BossPatternSettings(idleTime.x, idleTime.y, enragedIdleTime.x, enragedIdleTime.y, enrageHealthRatio);
        }
    }
}
