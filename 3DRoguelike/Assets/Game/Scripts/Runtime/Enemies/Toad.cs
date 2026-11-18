using UnityEngine;

namespace Game
{
    /// <summary>
    /// 두꺼비(원거리): 플레이어와 거리를 유지하며(너무 가까우면 물러나고 멀면 다가감) 예고 뒤 포물선 독액을 쏜다.
    /// 떨어질 곳은 예고를 시작할 때 정해져 바닥에 보이고, 떨어지면 둘레 피해 + 독 웅덩이.
    /// E 개체는 두 번에 한 번 독액 3발 부채꼴.
    /// </summary>
    public sealed class Toad : Enemy
    {
        private enum State
        {
            Positioning,
            WindingUp,
            Recovering,
        }

        [SerializeField] private ToadTuning tuning;
        [SerializeField] private ToadSpit spitPrefab;
        [SerializeField] private PoisonPuddle puddlePrefab;
        [Tooltip("독액이 나가는 곳")]
        [SerializeField] private Transform muzzle;
        [Tooltip("떨어질 곳 예고(부채꼴 발 수만큼). 월드 자리에 놓는다")]
        [SerializeField] private GameObject[] targetWarnings;

        private State state;
        private float nextAttackTime;
        private bool fan;
        private Vector3[] targets = new Vector3[0];

        public override EnemyTuning Tuning => tuning;
        protected override float StoppingDistance => 0.5f;

        protected override void HideWarnings()
        {
            foreach (GameObject warning in targetWarnings) warning.SetActive(false);
        }

        protected override void OnAppear()
        {
            state = State.Positioning;
            nextAttackTime = Time.time + tuning.windupTime; // 나타나자마자 쏘지 않게
        }

        protected override void Tick()
        {
            switch (state)
            {
                case State.Positioning:
                    Position();
                    break;
                case State.WindingUp:
                    // 플레이어가 안전 지대(마을)로 들어가면 쏘지 않고 그만둔다
                    if (PlayerIsSafe)
                    {
                        HideWarnings();
                        Pose(0f);
                        state = State.Positioning;
                        break;
                    }
                    // 예고: 몸을 젖히며 부푼다
                    Pose(-tuning.windupLeanAngle * TimerProgress(tuning.windupTime));
                    if (TimerDone) Fire();
                    break;
                case State.Recovering:
                    Pose(tuning.strikeLeanAngle * (1f - TimerProgress(tuning.recoverTime)));
                    if (TimerDone)
                    {
                        Pose(0f);
                        state = State.Positioning;
                    }
                    break;
            }
        }

        /// <summary>거리 유지: 가까우면 반대쪽으로 물러나고, 멀면 다가가고, 사이면 멈춰 바라보다 쏠 수 있으면 예고.</summary>
        private void Position()
        {
            if (!CanAct)
            {
                WaitForPlayer();
                return;
            }
            Vector3 offset = ToPlayer;
            float distance = offset.magnitude;
            if (distance < tuning.keepMin)
            {
                Vector3 away = distance > 0.001f ? -offset / distance : -transform.forward;
                Vector3 wanted = transform.position + away * tuning.retreatStep;
                if (UnityEngine.AI.NavMesh.SamplePosition(wanted, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) MoveTo(hit.position);
                return;
            }
            // 멀거나, 사이에 기둥 · 벽이 있어 못 쏘면 다가간다(길이 트일 때까지)
            if (distance > tuning.keepMax || !ClearLineToPlayer())
            {
                ChasePlayer();
                return;
            }
            StopMoving();
            Face(offset);
            if (Time.time >= nextAttackTime) BeginWindup(offset);
        }

        private void BeginWindup(Vector3 offset)
        {
            fan = NextAttackIsExtra();
            int shots = fan ? Mathf.Max(1, tuning.fanShots) : 1;
            targets = new Vector3[shots];
            Vector3 origin = transform.position;
            float playerY = Player.transform.position.y;
            for (int i = 0; i < shots; i++)
            {
                float angle = shots == 1 ? 0f : Mathf.Lerp(-tuning.fanAngle, tuning.fanAngle, i / (float)(shots - 1));
                Vector3 point = origin + Quaternion.Euler(0f, angle, 0f) * offset;
                point.y = playerY;
                targets[i] = point;
            }
            for (int i = 0; i < targetWarnings.Length; i++)
            {
                GameObject warning = targetWarnings[i];
                bool used = i < shots;
                warning.SetActive(used);
                if (!used) continue;
                warning.transform.position = targets[i] + Vector3.up * 0.02f;
                warning.transform.localScale = new Vector3(tuning.splashRadius * 2f, warning.transform.localScale.y, tuning.splashRadius * 2f);
            }
            state = State.WindingUp;
            StartTimer(tuning.windupTime);
        }

        private void Fire()
        {
            HideWarnings();
            string attack = fan ? tuning.fanName : tuning.attackName;
            foreach (Vector3 target in targets)
            {
                ToadSpit spit = Instantiate(spitPrefab, muzzle.position, Quaternion.identity);
                spit.Launch(new ToadSpit.Shot
                {
                    from = muzzle.position,
                    to = target,
                    flightTime = tuning.flightTime,
                    arcHeight = tuning.arcHeight,
                    target = Player,
                    damage = tuning.damage,
                    splashRadius = tuning.splashRadius,
                    source = $"{DisplayName}의 {attack}",
                    puddlePrefab = puddlePrefab,
                    puddleRadius = tuning.puddleRadius,
                    puddleTime = tuning.puddleTime,
                    puddleTickDamage = Mathf.Max(1, Mathf.RoundToInt(tuning.puddleDamagePerSecond * tuning.puddleTickInterval)),
                    puddleTickInterval = tuning.puddleTickInterval,
                    puddleSource = $"{DisplayName}의 {tuning.puddleName}",
                });
            }
            Pose(tuning.strikeLeanAngle);
            state = State.Recovering;
            StartTimer(tuning.recoverTime);
            nextAttackTime = Time.time + Mathf.Max(tuning.attackInterval, tuning.recoverTime);
        }
    }
}
