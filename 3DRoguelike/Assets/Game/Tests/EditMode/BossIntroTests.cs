using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;

namespace Game.Tests
{
    /// <summary>
    /// 보스 등장 연출(3-5): 예전 까치 보스전(main)의 컷 순서 · 시간 그대로인지, 연출 흐름(배우 자리 · 한 번씩 일어나는 일 · 건너뛰기),
    /// 연출 수치 에셋(시간 = 예전 값, 거리 = 예전 값 ÷ 2.75), 타임라인 에셋이 시간표와 같은지, 까치 프리팹이 연출 동작을 갖고 있는지.
    /// </summary>
    public class BossIntroTests
    {
        const string BossTuningPath = "Assets/Game/Data/Bosses/MagpieBossTuning.asset";
        const string IntroTuningPath = "Assets/Game/Data/Bosses/MagpieIntroTuning.asset";
        const string TimelinePath = "Assets/Game/Art/Bosses/Magpie/Timeline/BossIntro.playable";
        const float OldScale = 2.75f;

        static BossIntroScript Old() => new BossIntroScript(new BossIntroTimes());

        /// <summary>예전 BossIntroSetup의 컷 표(카메라, 시작, 끝).</summary>
        static readonly (BossIntroShot shot, float start, float end)[] OldCuts =
        {
            (BossIntroShot.WideA, 0f, 3f),
            (BossIntroShot.WideB, 0.3f, 4.5f),
            (BossIntroShot.OverShoulder, 4.5f, 7f),
            (BossIntroShot.FirstPerson, 6f, 8.9f),
            (BossIntroShot.Ground, 8.6f, 12.4f),
            (BossIntroShot.Gameplay, 11.2f, 12.6f),
        };

        /// <summary>예전 BossIntroCinematic.FireEvents의 순서 · 시간.</summary>
        static readonly (BossIntroCue cue, float time)[] OldCues =
        {
            (BossIntroCue.Crouch, 7.3f),
            (BossIntroCue.Charge, 7.9f),
            (BossIntroCue.Pounce, 8.6f),
            (BossIntroCue.StompImpact, 8.6f + 0.68f),
            (BossIntroCue.ClawRight, 10.5f),
            (BossIntroCue.ClawLeft, 10.75f),
            (BossIntroCue.Recoil, 10.9f),
        };

        [Test]
        public void Cuts_MatchOldCinematic_OrderAndTimes()
        {
            BossIntroScript script = Old();
            Assert.That(script.Duration, Is.EqualTo(12.6f));
            Assert.That(script.Cuts.Count, Is.EqualTo(OldCuts.Length));
            for (int i = 0; i < OldCuts.Length; i++)
            {
                Assert.That(script.Cuts[i].Shot, Is.EqualTo(OldCuts[i].shot), $"컷 {i}");
                Assert.That(script.Cuts[i].Start, Is.EqualTo(OldCuts[i].start).Within(1e-5f), $"컷 {i} 시작");
                Assert.That(script.Cuts[i].End, Is.EqualTo(OldCuts[i].end).Within(1e-5f), $"컷 {i} 끝");
            }
            // 겹치는 구간 = 블렌드: 와이드 흐름 2.7초, 3인칭은 컷(겹침 없음), 머리 속으로 1초, 쓰러짐 0.3초, 게임 카메라로 1.2초
            float[] blends = { 2.7f, 0f, 1f, 0.3f, 1.2f };
            for (int i = 1; i < script.Cuts.Count; i++)
                Assert.That(script.Cuts[i - 1].End - script.Cuts[i].Start, Is.EqualTo(blends[i - 1]).Within(1e-4f), $"컷 {i} 블렌드");
        }

        [Test]
        public void ShotAt_FollowsCutOrder()
        {
            BossIntroScript script = Old();
            Assert.That(script.ShotAt(0f), Is.EqualTo(BossIntroShot.WideA));
            Assert.That(script.ShotAt(2f), Is.EqualTo(BossIntroShot.WideB));
            Assert.That(script.ShotAt(5f), Is.EqualTo(BossIntroShot.OverShoulder));
            Assert.That(script.ShotAt(7.5f), Is.EqualTo(BossIntroShot.FirstPerson));
            Assert.That(script.ShotAt(9.5f), Is.EqualTo(BossIntroShot.Ground));
            Assert.That(script.ShotAt(12.5f), Is.EqualTo(BossIntroShot.Gameplay));
        }

        [Test]
        public void Flow_SteppedPlayback_FiresEachCueOnce_InOldOrder()
        {
            BossIntroScript script = Old();
            var fired = new List<(BossIntroCue cue, float time)>();
            var step = new List<BossIntroCue>();
            float last = -1f;
            for (int frame = 0; frame <= 12.6f * 60f + 1; frame++)
            {
                float time = Math.Min(frame / 60f, script.Duration);
                step.Clear();
                script.Cues(last, time, step);
                foreach (BossIntroCue cue in step) fired.Add((cue, time));
                last = time;
            }
            Assert.That(fired.Select(f => f.cue), Is.EqualTo(OldCues.Select(c => c.cue)), "순서 · 한 번씩");
            for (int i = 0; i < OldCues.Length; i++)
                Assert.That(fired[i].time, Is.InRange(OldCues[i].time - 1e-4f, OldCues[i].time + 1f / 60f + 1e-4f), OldCues[i].cue.ToString());
        }

        [Test]
        public void Flow_PlayerWalksIn_ThenStands_BodyHiddenOnlyInFirstPerson()
        {
            BossIntroScript script = Old();
            Assert.That(script.WalkProgress(0f), Is.EqualTo(0f));
            Assert.That(script.WalkProgress(3.5f), Is.EqualTo(0.5f).Within(1e-5f), "일정한 빠르기로 걸어 들어옴");
            Assert.That(script.WalkProgress(7f), Is.EqualTo(1f));
            Assert.That(script.WalkProgress(12f), Is.EqualTo(1f), "멈춘 자리 = 싸움을 시작할 자리");
            Assert.That(script.WalkAmount(6.5f), Is.EqualTo(1f));
            Assert.That(script.WalkAmount(6.8f), Is.EqualTo(0.5f).Within(1e-4f), "마지막 0.4초 동안 천천히 멈춤");
            Assert.That(script.WalkAmount(7f), Is.EqualTo(0f));

            Assert.That(script.BodyVisible(6.5f), Is.True);
            Assert.That(script.BodyVisible(6.6f), Is.False, "1인칭이 되기 직전에 숨김");
            Assert.That(script.BodyVisible(11.1f), Is.False);
            Assert.That(script.BodyVisible(11.2f), Is.True, "게임 카메라로 이어질 때 다시 보임");
            Assert.That(script.BodyVisible(script.Duration), Is.True);
        }

        [Test]
        public void Flow_BossChargesStompsAndReturns()
        {
            BossIntroScript script = Old();
            Assert.That(script.BossAdvance(7.8f), Is.EqualTo(0f), "웅크리는 동안 제자리");
            Assert.That(script.BossAdvance(8.25f), Is.EqualTo(0.25f).Within(1e-4f), "점점 빨라지며 덮쳐 옴(절반 시간에 1/4)");
            Assert.That(script.BossAdvance(8.6f), Is.EqualTo(1f), "도착 = 밟는 자리");
            Assert.That(script.BossAdvance(10.8f), Is.EqualTo(1f), "할퀴기 동안 그 자리");
            Assert.That(script.BossAdvance(11.4f), Is.EqualTo(0.25f).Within(1e-4f), "휙 물러났다가 느려짐(절반 시간에 3/4)");
            Assert.That(script.BossAdvance(11.9f), Is.EqualTo(0f), "제자리로");
            Assert.That(script.BossAdvance(script.Duration), Is.EqualTo(0f));

            Assert.That(script.BossLean(10.8f), Is.EqualTo(0f));
            Assert.That(script.BossLean(11.4f), Is.EqualTo(1f).Within(1e-4f), "물러나는 가운데에서 가장 젖힘");
            Assert.That(script.BossLean(11.9f), Is.EqualTo(0f));
        }

        [Test]
        public void Flow_LetterboxAndTitle_OldTimes()
        {
            BossIntroScript script = Old();
            Assert.That(script.LetterboxShown(0f), Is.True, "처음부터 검은 띠");
            Assert.That(script.LetterboxShown(12.1f), Is.True);
            Assert.That(script.LetterboxShown(12.2f), Is.False);
            Assert.That(script.TitleShown(10.9f), Is.False);
            Assert.That(script.TitleShown(11f), Is.True, "물러날 때 이름 타이틀");
            Assert.That(script.TitleShown(12.2f), Is.True);
            Assert.That(script.TitleShown(12.3f), Is.False);
        }

        [Test]
        public void Flow_Skip_LeavesNothingPending_AndEndStateIsFightStart()
        {
            // 건너뛰기 = 끝으로: 끝 시간의 배우 상태가 싸움 시작 상태(플레이어 도착 · 몸 보임 · 보스 제자리 · 검은 띠 · 타이틀 꺼짐)
            BossIntroScript script = Old();
            float end = script.Duration;
            Assert.That(script.WalkProgress(end), Is.EqualTo(1f));
            Assert.That(script.WalkAmount(end), Is.EqualTo(0f));
            Assert.That(script.BodyVisible(end), Is.True);
            Assert.That(script.BossAdvance(end), Is.EqualTo(0f));
            Assert.That(script.BossLean(end), Is.EqualTo(0f));
            Assert.That(script.LetterboxShown(end), Is.False);
            Assert.That(script.TitleShown(end), Is.False);
            // 한 번에 끝까지 가도 일은 순서대로 모두 나온다(중간에 건너뛴 뒤에는 실행 쪽이 더 묻지 않는다)
            var all = new List<BossIntroCue>();
            script.Cues(-1f, end, all);
            Assert.That(all, Is.EqualTo(OldCues.Select(c => c.cue)));
            var none = new List<BossIntroCue>();
            script.Cues(end, end, none);
            Assert.That(none, Is.Empty);
        }

        [Test]
        public void Times_RejectOutOfOrder()
        {
            Assert.Throws<ArgumentNullException>(() => new BossIntroScript(null));
            Assert.Throws<ArgumentException>(() => new BossIntroScript(new BossIntroTimes { ChargeEnd = 7.5f }), "돌진이 시작보다 먼저 끝남");
            Assert.Throws<ArgumentException>(() => new BossIntroScript(new BossIntroTimes { End = 12f }), "게임 카메라 블렌드보다 먼저 끝남");
            Assert.Throws<ArgumentException>(() => new BossIntroScript(new BossIntroTimes { ThirdPersonCut = 2f }), "와이드가 흘러가는 중에 컷");
            Assert.Throws<ArgumentException>(() => new BossIntroScript(new BossIntroTimes { TitleEnd = 13f }));
        }

        [Test]
        public void MagpieIntroTuning_TimesAreOld_DistancesAreOldDividedByScale()
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossTuning>(BossTuningPath);
            var tuning = AssetDatabase.LoadAssetAtPath<BossIntroTuning>(IntroTuningPath);
            Assert.That(tuning, Is.Not.Null, "Setup Floor 실행");
            Assert.That(boss.intro, Is.SameAs(tuning), "까치 = 이 연출");
            Assert.That(AssetDatabase.GetAssetPath(tuning.timeline), Is.EqualTo(TimelinePath));
            Assert.That(tuning.clawEffect, Is.Not.Null, "할퀴기 이펙트");
            Assert.That(tuning.title, Is.EqualTo("까치 대장"));
            Assert.That(tuning.subtitle, Is.EqualTo("보물 둥지의 수호자"));

            // 시간 · 컷 순서는 예전 그대로
            BossIntroScript script = tuning.ToScript();
            for (int i = 0; i < OldCuts.Length; i++)
            {
                Assert.That(script.Cuts[i].Shot, Is.EqualTo(OldCuts[i].shot));
                Assert.That(script.Cuts[i].Start, Is.EqualTo(OldCuts[i].start).Within(1e-5f));
                Assert.That(script.Cuts[i].End, Is.EqualTo(OldCuts[i].end).Within(1e-5f));
            }
            var cues = new List<BossIntroCue>();
            foreach ((BossIntroCue cue, float time) in OldCues)
            {
                cues.Clear();
                script.Cues(time - 0.001f, time + 0.001f, cues);
                Assert.That(cues, Is.EqualTo(new[] { cue }), $"{cue} @ {time}");
            }
            BossIntroTimes t = script.Times;
            Assert.That(t.WalkEnd, Is.EqualTo(7f));
            Assert.That(t.HideBodyTime, Is.EqualTo(6.6f));
            Assert.That(t.RecoilEnd, Is.EqualTo(11.9f));
            Assert.That(t.TitleStart, Is.EqualTo(11f)); Assert.That(t.TitleEnd, Is.EqualTo(12.3f));
            Assert.That(t.LetterboxEnd, Is.EqualTo(12.2f));

            // 화각 · 기울기 · 흔들림 세기 · 젖힘은 예전 그대로
            Assert.That(tuning.wideFov, Is.EqualTo(50f)); Assert.That(tuning.shoulderFov, Is.EqualTo(55f));
            Assert.That(tuning.firstPersonFov, Is.EqualTo(65f)); Assert.That(tuning.groundFov, Is.EqualTo(70f));
            Assert.That(tuning.groundDutch, Is.EqualTo(12f));
            Assert.That(tuning.recoilLean, Is.EqualTo(14f));
            Assert.That(tuning.pounceShake, Is.EqualTo(0.7f)); Assert.That(tuning.stompShake, Is.EqualTo(1.6f)); Assert.That(tuning.clawShake, Is.EqualTo(0.5f));
            Assert.That(tuning.shakeDuration, Is.EqualTo(0.35f));

            // 거리 · 위치는 예전 값 ÷ 2.75(2% 안)
            Near(tuning.walkDistance, 18f);
            Near(tuning.stompDistance, 2.6f);
            Near(tuning.wideA, new Vector3(-20f, 16f, 12f));
            Near(tuning.wideB, new Vector3(-17f, 13f, 8f));
            Near(tuning.wideLookHeight, 3f);
            Near(tuning.shoulder, new Vector3(2.2f, 6.5f, -9f));
            Near(tuning.groundEye, new Vector3(0f, 0.75f, -1.2f));
            Near(tuning.groundLookHeight, 5.5f);
            Near(tuning.shakeVelocity, 0.6f);
            Near(tuning.clawScale, 1f);
            Assert.That(tuning.minBossDistance, Is.GreaterThan(boss.bodyRadius + tuning.stompDistance), "멈춘 자리가 보스 몸 밖");
            Assert.That(tuning.allowSkip, Is.True);
        }

        [Test]
        public void Timeline_MatchesScript_TracksAndClips()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BossIntroTuning>(IntroTuningPath);
            PlayableAsset timeline = tuning.timeline;
            Assert.That(timeline, Is.Not.Null);
            BossIntroScript script = tuning.ToScript();
            Assert.That(timeline.duration, Is.EqualTo(script.Duration).Within(1e-4), "길이 12.6초");

            // 트랙 셋(이름으로 씬의 Brain · 검은 띠 · 타이틀에 이어진다)
            List<PlayableBinding> tracks = timeline.outputs.ToList();
            Assert.That(tracks.Select(track => track.streamName), Is.EqualTo(new[] { BossIntroCinematic.CameraTrack, BossIntroCinematic.LetterboxTrack, BossIntroCinematic.TitleTrack }));

            List<(string name, double start, double end)> cameras = Clips(tracks[0].sourceObject);
            Assert.That(cameras.Count, Is.EqualTo(script.Cuts.Count));
            for (int i = 0; i < cameras.Count; i++)
            {
                Assert.That(cameras[i].name, Is.EqualTo(BossIntroCinematic.ShotName(script.Cuts[i].Shot)));
                Assert.That(cameras[i].start, Is.EqualTo(script.Cuts[i].Start).Within(1e-4));
                Assert.That(cameras[i].end, Is.EqualTo(script.Cuts[i].End).Within(1e-4));
            }
            Assert.That(cameras.Select(c => c.name), Is.EqualTo(new[] { "CM_01_Wide_A", "CM_02_Wide_B", "CM_03_OverShoulder", "CM_04_POV", "CM_05_Ground", "CM_06_Gameplay" }), "예전 카메라 이름 · 순서");

            List<(string name, double start, double end)> letterbox = Clips(tracks[1].sourceObject);
            Assert.That(letterbox.Count, Is.EqualTo(1));
            Assert.That(letterbox[0].start, Is.EqualTo(0).Within(1e-4));
            Assert.That(letterbox[0].end, Is.EqualTo(12.2).Within(1e-4));
            List<(string name, double start, double end)> title = Clips(tracks[2].sourceObject);
            Assert.That(title.Count, Is.EqualTo(1));
            Assert.That(title[0].start, Is.EqualTo(11.0).Within(1e-4));
            Assert.That(title[0].end, Is.EqualTo(12.3).Within(1e-4));
        }

        [Test]
        public void MagpiePrefab_HasIntroMotions()
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossTuning>(BossTuningPath);
            Animator[] animators = boss.prefab.GetComponentsInChildren<Animator>(true);
            Assert.That(animators.Length, Is.EqualTo(2), "몸통 + 날개");
            foreach (Animator animator in animators)
            {
                var controller = animator.runtimeAnimatorController as AnimatorController;
                Assert.That(controller, Is.Not.Null, animator.name);
                string[] parameters = controller.parameters.Select(p => p.name).ToArray();
                foreach (string trigger in new[] { "ChargeReady", "Charge", "Stomp", "Land" }) Assert.That(parameters, Does.Contain(trigger), animator.name);
                ChildAnimatorState stomp = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Stomp");
                Assert.That(stomp.state, Is.Not.Null, animator.name + " 밟기 상태");
                Assert.That(stomp.state.motion.name, Does.EndWith("Stomp"), "밟기 클립(GGachi_Stomp · GGachi_Wings_Stomp)");
                Assert.That(controller.layers[0].stateMachine.states.Any(s => s.state.name == "Idle"), Is.True);
            }
            Assert.That(boss.prefab.Head, Is.Not.SameAs(boss.prefab.transform), "카메라가 볼 머리 뼈");
        }

        /// <summary>트랙의 클립(이름, 시작, 끝)을 시작 순서로. Timeline 형식을 직접 쓰지 않고 직렬화된 값으로 읽는다.</summary>
        static List<(string name, double start, double end)> Clips(UnityEngine.Object track)
        {
            var list = new List<(string name, double start, double end)>();
            SerializedProperty clips = new SerializedObject(track).FindProperty("m_Clips");
            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(i);
                double start = clip.FindPropertyRelative("m_Start").doubleValue;
                list.Add((clip.FindPropertyRelative("m_DisplayName").stringValue, start, start + clip.FindPropertyRelative("m_Duration").doubleValue));
            }
            list.Sort((a, b) => a.start.CompareTo(b.start));
            return list;
        }

        static void Near(float value, float old)
        {
            Assert.That(value, Is.EqualTo(old / OldScale).Within(Math.Abs(old / OldScale) * 0.02f + 1e-4f), $"예전 {old} ÷ {OldScale}");
        }

        static void Near(Vector3 value, Vector3 old)
        {
            Near(value.x, old.x);
            Near(value.y, old.y);
            Near(value.z, old.z);
        }
    }
}
