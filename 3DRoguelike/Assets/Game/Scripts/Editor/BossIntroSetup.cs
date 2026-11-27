using System.Collections.Generic;
using System.Linq;
using Game.Rules;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Game.EditorTools
{
    /// <summary>
    /// 보스 등장 연출(Timeline + Cinemachine)을 만든다 — 예전 까치 보스전(main)의 "8. Setup Boss Intro Cinematic"을 옮긴 것.
    /// <see cref="Run"/>(BossSetup이 부른다): 연출 수치 에셋(없을 때만 만들고 값을 덮어쓰지 않는다)과 타임라인 에셋(카메라 컷 · 검은 띠 · 이름 타이틀 트랙).
    /// <see cref="BuildScene"/>(FloorSetup이 부른다): 층 씬의 BossIntro 오브젝트(PlayableDirector + BossIntroCinematic + 흔들림 + 연출용 카메라 6대)와 HUD의 검은 띠 · 타이틀.
    /// 여러 번 실행해도 결과가 같다 — 타임라인은 수치와 이미 같으면 손대지 않고, 다르면 같은 파일 안에서 트랙만 새로 만든다(GUID 유지).
    /// 방 · 들어선 자리는 판마다 달라서 카메라 자리는 여기서 정하지 않고 재생할 때 BossIntroCinematic이 계산한다.
    /// </summary>
    public static class BossIntroSetup
    {
        internal const string TuningPath = "Assets/Game/Data/Bosses/MagpieIntroTuning.asset";
        internal const string TimelinePath = "Assets/Game/Art/Bosses/Magpie/Timeline/BossIntro.playable";
        private const string ClawEffectPath = "Assets/Game/Art/Effects/ClawSlash/Player_ClawSlash.prefab";
        private const string RootName = "BossIntro";
        private static readonly string[] HudToHide = { "HealthBar", "AmmoText", "Minimap", "Objective", "BossBar" };

        /// <summary>연출 수치 에셋과 타임라인 에셋. 수치 값은 그대로 두고 타임라인 · 이펙트 연결만 고친다.</summary>
        public static BossIntroTuning Run()
        {
            EnsureFolder("Assets/Game/Data/Bosses");
            EnsureFolder(System.IO.Path.GetDirectoryName(TimelinePath).Replace('\\', '/'));

            var tuning = AssetDatabase.LoadAssetAtPath<BossIntroTuning>(TuningPath);
            if (tuning == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BossIntroTuning>(), TuningPath);
                AssetDatabase.SaveAssets();
                tuning = AssetDatabase.LoadAssetAtPath<BossIntroTuning>(TuningPath);
            }
            // 할퀴기 이펙트는 비어 있을 때만 채운다(다른 것으로 바꿔 둔 값은 그대로 둔다)
            if (tuning.clawEffect == null) tuning.clawEffect = AssetDatabase.LoadAssetAtPath<GameObject>(ClawEffectPath);
            if (TimelineBuilder != null) tuning.timeline = TimelineBuilder(tuning.ToScript(), TimelinePath);
            else Debug.LogError("[BossIntroSetup] 타임라인을 만드는 코드(Scripts/Editor/Timeline)가 등록되지 않아 등장 연출 타임라인을 만들지 못했습니다.");
            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssets();
            return tuning;
        }

        /// <summary>
        /// 시간표대로 타임라인 에셋을 만드는 함수(시간표, 경로 → 에셋). Timeline 패키지를 쓰는 코드는 따로 둔 어셈블리
        /// (`Scripts/Editor/Timeline`, Game.Editor.Timeline)에 있고, 에디터가 스크립트를 읽을 때마다 여기에 등록한다.
        /// </summary>
        public static System.Func<BossIntroScript, string, PlayableAsset> TimelineBuilder;

        // ───────────────────────── 씬 ─────────────────────────

        /// <summary>층 씬에 연출 오브젝트를 만든다(HUD · 카메라가 만들어진 뒤에 부른다).</summary>
        public static BossIntroCinematic BuildScene(Canvas hud)
        {
            CinemachineBrain brain = Object.FindAnyObjectByType<CinemachineBrain>();
            Camera mainCamera = brain.GetComponent<Camera>();
            CinemachineCamera playerCamera = GameObject.Find("PlayerCamera").GetComponent<CinemachineCamera>();
            var defaults = ScriptableObject.CreateInstance<BossIntroTuning>(); // 화각 · 흔들림 기본값(재생할 때 수치 에셋 값으로 다시 맞춘다)

            var root = new GameObject(RootName);
            var director = root.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
            var intro = root.AddComponent<BossIntroCinematic>();
            var impulse = root.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseDuration = defaults.shakeDuration;
            impulse.DefaultVelocity = Vector3.down * defaults.shakeVelocity;

            var cameraRoot = new GameObject("IntroCameras");
            cameraRoot.transform.SetParent(root.transform, false);

            // 1. 와이드(시작 · 끝) — 제자리 카메라 둘 사이의 블렌드가 천천히 흘러가는 움직임
            CinemachineCamera wideA = NewCamera(cameraRoot, BossIntroShot.WideA, mainCamera, defaults.wideFov);
            CinemachineCamera wideB = NewCamera(cameraRoot, BossIntroShot.WideB, mainCamera, defaults.wideFov);

            // 2. 어깨 너머 3인칭: 플레이어를 따라가며 보스 머리를 봄(대상은 재생할 때)
            CinemachineCamera shoulder = NewCamera(cameraRoot, BossIntroShot.OverShoulder, mainCamera, defaults.shoulderFov);
            var follow = shoulder.gameObject.AddComponent<CinemachineFollow>();
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = Vector3.zero;
            follow.FollowOffset = defaults.shoulder;
            shoulder.gameObject.AddComponent<CinemachineRotationComposer>();

            // 3. 1인칭: 머리 뼈에 붙어 다가오는 보스 머리를 봄
            CinemachineCamera firstPerson = NewCamera(cameraRoot, BossIntroShot.FirstPerson, mainCamera, defaults.firstPersonFov);
            firstPerson.gameObject.AddComponent<CinemachineHardLockToTarget>();
            firstPerson.gameObject.AddComponent<CinemachineRotationComposer>();
            firstPerson.gameObject.AddComponent<CinemachineImpulseListener>();

            // 4. 바닥 1인칭: 넘어져 누운 눈높이에서 올려다봄, 화면이 기울어짐
            CinemachineCamera ground = NewCamera(cameraRoot, BossIntroShot.Ground, mainCamera, defaults.groundFov);
            ground.Lens.Dutch = defaults.groundDutch;
            ground.gameObject.AddComponent<CinemachineImpulseListener>();

            // 5. 게임 카메라 자리(재생하는 동안 게임 카메라를 따라간다)
            CinemachineCamera gameplay = NewCamera(cameraRoot, BossIntroShot.Gameplay, mainCamera, mainCamera.fieldOfView);
            gameplay.gameObject.AddComponent<CinemachineImpulseListener>();
            cameraRoot.SetActive(false); // 연출 동안만 켠다
            Object.DestroyImmediate(defaults);

            GameObject letterbox = HudSetup.BuildLetterbox(hud);
            GameObject title = HudSetup.BuildBossTitle(hud, out TMP_Text titleText, out TMP_Text subtitleText);
            // 결과 · 보상 화면보다는 아래에 그린다
            Transform result = hud.transform.Find("ResultPanel");
            if (result != null)
            {
                letterbox.transform.SetSiblingIndex(result.GetSiblingIndex());
                title.transform.SetSiblingIndex(result.GetSiblingIndex());
            }

            var fields = new SerializedObject(intro);
            fields.FindProperty("director").objectReferenceValue = director;
            fields.FindProperty("brain").objectReferenceValue = brain;
            fields.FindProperty("playerCamera").objectReferenceValue = playerCamera;
            fields.FindProperty("cameraRoot").objectReferenceValue = cameraRoot;
            SetArray(fields.FindProperty("shots"), new Object[] { wideA, wideB, shoulder, firstPerson, ground, gameplay });
            fields.FindProperty("impulse").objectReferenceValue = impulse;
            fields.FindProperty("letterbox").objectReferenceValue = letterbox;
            fields.FindProperty("title").objectReferenceValue = title;
            fields.FindProperty("titleText").objectReferenceValue = titleText;
            fields.FindProperty("subtitleText").objectReferenceValue = subtitleText;
            SetArray(fields.FindProperty("hideDuringIntro"), HudToHide.Select(name => hud.transform.Find(name)).Where(found => found != null).Select(found => (Object)found.gameObject).ToArray());
            fields.ApplyModifiedPropertiesWithoutUndo();
            return intro;
        }

        private static CinemachineCamera NewCamera(GameObject parent, BossIntroShot shot, Camera mainCamera, float fov)
        {
            var go = new GameObject(BossIntroCinematic.ShotName(shot));
            go.transform.SetParent(parent.transform, false);
            var camera = go.AddComponent<CinemachineCamera>();
            camera.Lens = LensSettings.FromCamera(mainCamera);
            camera.Lens.FieldOfView = fov;
            // 게임 카메라(0)보다 낮게: 타임라인이 고를 때만 쓰인다
            camera.Priority.Enabled = true;
            camera.Priority.Value = BossIntroCinematic.ShotPriority;
            return camera;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
