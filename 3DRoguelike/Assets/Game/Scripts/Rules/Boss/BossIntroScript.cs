using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>등장 연출의 카메라 컷. 순서 = 화면에 나오는 순서.</summary>
    public enum BossIntroShot
    {
        /// <summary>와이드: 방 안쪽 높은 곳에서 입구로 걸어 들어오는 플레이어를 멀리 내려다봄.</summary>
        WideA,
        /// <summary>와이드가 천천히 흘러가 닿는 자리(걷는 길 가운데를 봄).</summary>
        WideB,
        /// <summary>어깨 너머 3인칭: 플레이어를 따라가며 저 멀리 보스를 봄.</summary>
        OverShoulder,
        /// <summary>1인칭: 플레이어 머리에 붙어 다가오는 보스를 봄.</summary>
        FirstPerson,
        /// <summary>바닥 1인칭: 넘어져 누운 눈높이에서 보스를 올려다봄.</summary>
        Ground,
        /// <summary>게임 카메라 자리.</summary>
        Gameplay,
    }

    /// <summary>등장 연출에서 시간이 되면 한 번 일어나는 일.</summary>
    public enum BossIntroCue
    {
        /// <summary>보스가 웅크림.</summary>
        Crouch,
        /// <summary>보스가 돌진을 시작.</summary>
        Charge,
        /// <summary>보스가 덮쳐 플레이어가 넘어짐 + 밟기 동작 시작.</summary>
        Pounce,
        /// <summary>발이 바닥에 닿음.</summary>
        StompImpact,
        /// <summary>플레이어의 할퀴기(오른손).</summary>
        ClawRight,
        /// <summary>플레이어의 할퀴기(왼손).</summary>
        ClawLeft,
        /// <summary>보스가 주춤하며 물러나기 시작.</summary>
        Recoil,
    }

    /// <summary>카메라 컷 하나: 이 컷이 타임라인에 놓인 구간(초). 앞 컷과 겹치는 구간은 블렌드(카메라 이동).</summary>
    public readonly struct BossIntroCut
    {
        public BossIntroCut(BossIntroShot shot, float start, float end)
        {
            Shot = shot;
            Start = start;
            End = end;
        }

        public BossIntroShot Shot { get; }
        public float Start { get; }
        public float End { get; }
    }

    /// <summary>
    /// 등장 연출의 시간표(초). 기본값 = 예전 까치 보스전(main)의 BossIntroCinematic · BossIntroSetup 상수 그대로.
    /// 게임에서는 수치 에셋(BossIntroTuning)이 채운다.
    /// </summary>
    public sealed class BossIntroTimes
    {
        /// <summary>와이드가 흘러가기 시작.</summary>
        public float WideDriftStart = 0.3f;
        /// <summary>와이드가 다 흘러감.</summary>
        public float WideDriftEnd = 3.0f;
        /// <summary>와이드 → 어깨 너머 3인칭(컷).</summary>
        public float ThirdPersonCut = 4.5f;
        /// <summary>3인칭 → 1인칭으로 카메라가 머리 속으로.</summary>
        public float FirstPersonBlendStart = 6.0f;
        public float FirstPersonBlendEnd = 7.0f;
        /// <summary>걸어 들어와 멈춤.</summary>
        public float WalkEnd = 7.0f;
        /// <summary>걷다가 마지막 이 시간 동안 천천히 멈춤.</summary>
        public float WalkStopTime = 0.4f;
        /// <summary>1인칭이 되기 직전에 몸을 숨김(카메라가 머리 속을 비추지 않게).</summary>
        public float HideBodyTime = 6.6f;
        /// <summary>보스가 웅크림.</summary>
        public float CrouchTime = 7.3f;
        public float ChargeStart = 7.9f;
        /// <summary>돌진 도착 → 넘어짐 + 밟기 시작.</summary>
        public float ChargeEnd = 8.6f;
        /// <summary>1인칭 카메라가 바닥에 떨어짐.</summary>
        public float FallBlendEnd = 8.9f;
        /// <summary>밟기 동작을 시작한 뒤 발이 바닥에 닿기까지(밟기 클립의 내리찍는 순간).</summary>
        public float StompDelay = 0.68f;
        /// <summary>할퀴기(오른손 → 왼손).</summary>
        public float Claw1 = 10.5f;
        public float Claw2 = 10.75f;
        /// <summary>주춤하며 물러남.</summary>
        public float RecoilStart = 10.9f;
        public float RecoilEnd = 11.9f;
        /// <summary>게임 카메라로 이어지기 시작(이때 몸을 다시 보임).</summary>
        public float GameplayBlendStart = 11.2f;
        public float GameplayBlendEnd = 12.4f;
        public float End = 12.6f;
        /// <summary>보스 이름 타이틀.</summary>
        public float TitleStart = 11.0f;
        public float TitleEnd = 12.3f;
        /// <summary>위아래 검은 띠가 사라지는 때(처음부터 켜져 있다).</summary>
        public float LetterboxEnd = 12.2f;

        /// <summary>발이 바닥에 닿는 순간.</summary>
        public float StompImpact => ChargeEnd + StompDelay;
    }

    /// <summary>
    /// 보스 등장 연출의 흐름(엔진 없음) — 예전 까치 보스전(main) 그대로:
    /// ① 와이드(걸어 들어옴) → ② 어깨 너머 3인칭 → ③ 1인칭 → ④ 보스가 웅크렸다 돌진해 넘어뜨리고 발로 찍음(바닥 1인칭)
    /// → ⑤ 할퀴기 두 번에 보스가 주춤하며 제자리로 → ⑥ 게임 카메라로 이어지고 싸움 시작.
    /// 카메라 컷 구간, 시간마다 배우가 있을 자리(비율), 한 번씩 일어나는 일을 시간으로 답한다.
    /// 타임라인 에셋(BossIntroSetup)과 실행(BossIntroCinematic)이 같은 이 표를 쓴다.
    /// </summary>
    public sealed class BossIntroScript
    {
        readonly BossIntroTimes t;
        readonly BossIntroCut[] cuts;

        public BossIntroScript(BossIntroTimes times)
        {
            t = times ?? throw new ArgumentNullException(nameof(times));
            Validate(t);
            cuts = new[]
            {
                new BossIntroCut(BossIntroShot.WideA, 0f, t.WideDriftEnd),
                new BossIntroCut(BossIntroShot.WideB, t.WideDriftStart, t.ThirdPersonCut),          // 흘러간 뒤 3인칭으로 컷
                new BossIntroCut(BossIntroShot.OverShoulder, t.ThirdPersonCut, t.FirstPersonBlendEnd),
                new BossIntroCut(BossIntroShot.FirstPerson, t.FirstPersonBlendStart, t.FallBlendEnd),  // 머리 속으로 들어감
                new BossIntroCut(BossIntroShot.Ground, t.ChargeEnd, t.GameplayBlendEnd),              // 바닥으로 쓰러짐
                new BossIntroCut(BossIntroShot.Gameplay, t.GameplayBlendStart, t.End),               // 게임 카메라로
            };
        }

        public BossIntroTimes Times => t;
        public IReadOnlyList<BossIntroCut> Cuts => cuts;
        public float Duration => t.End;

        /// <summary>그 시간에 들어와 있는(가장 늦게 시작한) 컷.</summary>
        public BossIntroShot ShotAt(float time)
        {
            BossIntroShot shot = cuts[0].Shot;
            foreach (BossIntroCut cut in cuts)
            {
                if (time >= cut.Start) shot = cut.Shot;
            }
            return shot;
        }

        /// <summary>플레이어가 걸어온 비율(0 = 들어선 자리, 1 = 싸움을 시작할 자리).</summary>
        public float WalkProgress(float time) => Clamp01(time / t.WalkEnd);

        /// <summary>걷기 동작 세기(1 걷기 ~ 0 서기). 마지막 <see cref="BossIntroTimes.WalkStopTime"/> 동안 천천히 멈춘다.</summary>
        public float WalkAmount(float time) => time < t.WalkEnd ? Clamp01((t.WalkEnd - time) / t.WalkStopTime) : 0f;

        /// <summary>플레이어 몸이 보이는지. 1인칭 동안은 숨긴다.</summary>
        public bool BodyVisible(float time) => time < t.HideBodyTime || time >= t.GameplayBlendStart;

        /// <summary>보스가 나아간 비율(0 = 제자리, 1 = 플레이어 앞 밟는 자리). 점점 빨라지며 덮쳐 오고, 휙 물러났다가 느려진다.</summary>
        public float BossAdvance(float time)
        {
            if (time >= t.ChargeStart && time < t.ChargeEnd)
            {
                float k = (time - t.ChargeStart) / (t.ChargeEnd - t.ChargeStart);
                return k * k;
            }
            if (time >= t.ChargeEnd && time < t.RecoilStart) return 1f;
            if (time >= t.RecoilStart && time < t.RecoilEnd)
            {
                float k = (time - t.RecoilStart) / (t.RecoilEnd - t.RecoilStart);
                return (1f - k) * (1f - k);
            }
            return 0f;
        }

        /// <summary>보스가 주춤하며 상체를 뒤로 젖힌 정도(0 ~ 1). 물러나는 동안 젖혔다가 돌아온다.</summary>
        public float BossLean(float time)
        {
            if (time < t.RecoilStart || time >= t.RecoilEnd) return 0f;
            float k = (time - t.RecoilStart) / (t.RecoilEnd - t.RecoilStart);
            return (float)Math.Sin(k * Math.PI);
        }

        public bool LetterboxShown(float time) => time >= 0f && time < t.LetterboxEnd;
        public bool TitleShown(float time) => time >= t.TitleStart && time < t.TitleEnd;

        /// <summary>from(뺌) ~ to(넣음) 사이에 지난 일을 시간 순서로 into에 더한다. 처음에는 from을 음수로.</summary>
        public void Cues(float from, float to, List<BossIntroCue> into)
        {
            Add(from, to, t.CrouchTime, BossIntroCue.Crouch, into);
            Add(from, to, t.ChargeStart, BossIntroCue.Charge, into);
            Add(from, to, t.ChargeEnd, BossIntroCue.Pounce, into);
            Add(from, to, t.StompImpact, BossIntroCue.StompImpact, into);
            Add(from, to, t.Claw1, BossIntroCue.ClawRight, into);
            Add(from, to, t.Claw2, BossIntroCue.ClawLeft, into);
            Add(from, to, t.RecoilStart, BossIntroCue.Recoil, into);
        }

        static void Add(float from, float to, float at, BossIntroCue cue, List<BossIntroCue> into)
        {
            if (from < at && to >= at) into.Add(cue);
        }

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        static void Validate(BossIntroTimes t)
        {
            Ordered("wide", 0f, t.WideDriftStart, t.WideDriftEnd, t.ThirdPersonCut);
            Ordered("first person", t.ThirdPersonCut, t.FirstPersonBlendStart, t.FirstPersonBlendEnd);
            Ordered("walk", 0f, t.WalkEnd - t.WalkStopTime, t.WalkEnd);
            Ordered("charge", t.CrouchTime, t.ChargeStart, t.ChargeEnd, t.FallBlendEnd);
            Ordered("stomp", t.ChargeEnd, t.StompImpact, t.Claw1, t.Claw2, t.RecoilStart, t.RecoilEnd, t.End);
            Ordered("gameplay", t.FallBlendEnd, t.GameplayBlendStart, t.GameplayBlendEnd, t.End);
            Ordered("title", 0f, t.TitleStart, t.TitleEnd, t.End);
            Ordered("letterbox", 0f, t.LetterboxEnd, t.End);
            Ordered("body", 0f, t.HideBodyTime, t.GameplayBlendStart);
            if (t.FirstPersonBlendEnd > t.ChargeEnd) throw new ArgumentException("first person must finish before the pounce");
            if (t.WalkStopTime <= 0f || t.ChargeEnd <= t.ChargeStart || t.RecoilEnd <= t.RecoilStart) throw new ArgumentException("zero length");
        }

        static void Ordered(string name, params float[] times)
        {
            for (int i = 1; i < times.Length; i++)
            {
                if (times[i] < times[i - 1]) throw new ArgumentException($"intro times out of order: {name}");
            }
        }
    }
}
