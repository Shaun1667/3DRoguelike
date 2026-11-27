using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Game.EditorTools
{
    /// <summary>
    /// 보스 등장 연출의 타임라인 에셋을 시간표(<see cref="BossIntroScript"/>)대로 만든다 — 예전 까치 보스전(main) BossIntroSetup의 타임라인 부분.
    /// 트랙 셋: 카메라 컷(Cinemachine, 겹치는 구간이 블렌드 = 카메라 이동) · 검은 띠 · 보스 이름 타이틀(Activation, 끝나면 꺼짐).
    /// 수치와 이미 같으면 파일을 건드리지 않고, 다르면 같은 파일 안에서 트랙만 새로 만든다(GUID 유지) — 여러 번 실행해도 결과가 같다.
    /// Timeline 패키지를 쓰는 코드라 어셈블리를 따로 두었고(Game.Editor.Timeline), 스크립트를 읽을 때마다 <see cref="BossIntroSetup.TimelineBuilder"/>에 등록한다.
    /// </summary>
    [InitializeOnLoad]
    public static class BossIntroTimelineBuilder
    {
        static BossIntroTimelineBuilder()
        {
            BossIntroSetup.TimelineBuilder = Build;
        }

        public static PlayableAsset Build(BossIntroScript script, string path)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline != null && Matches(timeline, script)) return timeline;
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, path);
            }
            else
            {
                foreach (TrackAsset track in timeline.GetRootTracks().ToList()) timeline.DeleteTrack(track);
            }

            BossIntroTimes times = script.Times;
            CinemachineTrack cameraTrack = timeline.CreateTrack<CinemachineTrack>(BossIntroCinematic.CameraTrack);
            TimelineClip previous = null;
            foreach (BossIntroCut cut in script.Cuts)
            {
                string name = BossIntroCinematic.ShotName(cut.Shot);
                TimelineClip clip = cameraTrack.CreateClip<CinemachineShot>();
                clip.start = cut.Start;
                clip.duration = cut.End - cut.Start;
                clip.displayName = name;
                // 카메라는 씬에 있으므로 이름으로 잇는다(BossIntroCinematic이 재생할 때 채운다)
                ((CinemachineShot)clip.asset).VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = name };
                if (previous != null && previous.end > clip.start)
                {
                    double overlap = previous.end - clip.start;
                    previous.blendOutDuration = overlap;
                    clip.blendInDuration = overlap;
                }
                previous = clip;
            }

            ActivationTrack letterboxTrack = timeline.CreateTrack<ActivationTrack>(BossIntroCinematic.LetterboxTrack);
            letterboxTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
            TimelineClip letterbox = letterboxTrack.CreateDefaultClip();
            letterbox.start = 0;
            letterbox.duration = times.LetterboxEnd;
            letterbox.displayName = BossIntroCinematic.LetterboxTrack;

            ActivationTrack titleTrack = timeline.CreateTrack<ActivationTrack>(BossIntroCinematic.TitleTrack);
            titleTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
            TimelineClip title = titleTrack.CreateDefaultClip();
            title.start = times.TitleStart;
            title.duration = times.TitleEnd - times.TitleStart;
            title.displayName = BossIntroCinematic.TitleTrack;

            timeline.fixedDuration = times.End;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BossIntroSetup] 등장 연출 타임라인 {path} (컷 {script.Cuts.Count}개, {times.End:0.0}초)");
            return timeline;
        }

        /// <summary>타임라인이 이미 이 시간표와 같은지(같으면 파일을 건드리지 않는다).</summary>
        public static bool Matches(TimelineAsset timeline, BossIntroScript script)
        {
            BossIntroTimes times = script.Times;
            if (timeline.durationMode != TimelineAsset.DurationMode.FixedLength || !Same(timeline.fixedDuration, times.End)) return false;
            List<TrackAsset> tracks = timeline.GetRootTracks().ToList();
            if (tracks.Count != 3) return false;
            if (!(tracks[0] is CinemachineTrack) || tracks[0].name != BossIntroCinematic.CameraTrack) return false;
            if (!(tracks[1] is ActivationTrack) || tracks[1].name != BossIntroCinematic.LetterboxTrack) return false;
            if (!(tracks[2] is ActivationTrack) || tracks[2].name != BossIntroCinematic.TitleTrack) return false;

            List<TimelineClip> clips = tracks[0].GetClips().OrderBy(clip => clip.start).ToList();
            if (clips.Count != script.Cuts.Count) return false;
            for (int i = 0; i < clips.Count; i++)
            {
                BossIntroCut cut = script.Cuts[i];
                string name = BossIntroCinematic.ShotName(cut.Shot);
                if (clips[i].displayName != name || !Same(clips[i].start, cut.Start) || !Same(clips[i].end, cut.End)) return false;
                if (!(clips[i].asset is CinemachineShot shot) || shot.VirtualCamera.exposedName != name) return false;
            }
            return SingleClip(tracks[1], 0f, times.LetterboxEnd) && SingleClip(tracks[2], times.TitleStart, times.TitleEnd);
        }

        private static bool SingleClip(TrackAsset track, float start, float end)
        {
            List<TimelineClip> clips = track.GetClips().ToList();
            return clips.Count == 1 && Same(clips[0].start, start) && Same(clips[0].end, end)
                   && ((ActivationTrack)track).postPlaybackState == ActivationTrack.PostPlaybackState.Inactive;
        }

        private static bool Same(double a, double b)
        {
            return System.Math.Abs(a - b) < 1e-4;
        }
    }
}
