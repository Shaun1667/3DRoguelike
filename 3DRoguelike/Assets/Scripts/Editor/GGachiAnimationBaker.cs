using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 까치 보스 애니메이션을 코드로 만들어 클립으로 굽는 에디터 메뉴.
/// 까치는 몸통(GGachi_Body.fbx, Humanoid)과 날개(GGachi_Wings.fbx, Generic)로 나뉘어 있어서
/// 동작 하나당 클립이 두 개 생긴다.
///
/// Tools > Boss Fight > 4. Build GGachi Hover Idle   : 공중에 떠서 날갯짓 (Idle 상태)
/// Tools > Boss Fight > 5. Build GGachi Slam Dive    : 내려찍기 때 발부터 내리꽂는 자세 (SlamDive 상태)
///
/// 만드는 방법:
///  1) 몸통/날개 모델을 임시로 만들고
///  2) 1/30초마다 뼈를 "보스 기준 방향(오른쪽·위·앞)"으로 돌려 자세를 만든 뒤
///  3) 몸통: HumanPoseHandler로 그 자세를 휴머노이드 근육 값으로 읽어 Humanoid 클립에 기록
///           (꼬리는 휴머노이드 뼈가 아니라서 Transform 회전으로 같이 기록)
///     날개: 각 뼈의 localRotation을 Generic 클립에 기록
///
/// 회전 방향 메모 (모델은 +Z를 바라보고 Left 뼈가 -X 쪽):
///  - right 축 + 회전 : 아래를 향한 뼈는 뒤로, 앞을 향한 뼈는 아래로
///  - forward 축      : 왼날개/왼팔은 - 가 위(바깥)로, 오른쪽은 + 가 위(바깥)로
/// </summary>
public static class GGachiAnimationBaker
{
    private const string BodyModelPath = "Assets/Resources/VARCO/GGachi.fbx"; // Mixamo로 다시 리깅한 몸통 (꼬리 뼈 없음)
    private const string WingsModelPath = "Assets/Resources/VARCO/GGachi_Wings.fbx";
    private const string AnimDir = "Assets/Resources/VARCO/Animations";
    public const string BodyControllerPath = AnimDir + "/GGachi.controller";
    public const string WingsControllerPath = AnimDir + "/GGachi_Wings.controller";
    private const string BodyHoverPath = AnimDir + "/GGachi_Hover.anim";
    private const string BodyDivePath = AnimDir + "/GGachi_SlamDive.anim";
    private const string WingsHoverPath = AnimDir + "/GGachi_Wings_Hover.anim";
    private const string WingsDivePath = AnimDir + "/GGachi_Wings_SlamDive.anim";
    private const int Fps = 30;

    // ── 호버링 Idle ──
    private const float HoverClipLength = 4f;     // 날갯짓 2번 (반복 시 이음매가 없도록 주기의 정수배)
    private const float FlapPeriod = 2f;          // 날갯짓 1번에 걸리는 시간
    private const float HoverHeightRatio = 0.13f; // 모델 키 대비 떠 있는 높이
    private const float BobHeightRatio = 0.03f;   // 날갯짓에 따라 오르내리는 폭
    private const float WingUpperAmp = 35f;
    private const float WingForeAmp = 20f;
    private const float WingHandAmp = 15f;
    private const float WingUpBias = 12f;         // 위로는 크게 펴고 아래로는 덜 내려가 날개가 몸을 파고들지 않게

    // ── 하강 찍기 ──
    private const float DiveClipLength = 1f;
    private const float DiveTremblePeriod = 0.25f; // 낙하 바람에 날개 끝·꼬리가 떨리는 주기

    /// <summary>자세를 만들 때 쓰는 뼈 묶음 (몸통 또는 날개 모델 하나)</summary>
    private class Rig
    {
        public Transform root;
        public Vector3 right, up, forward;
        public float height;
        public Vector3 hipsRestWorld;
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();

        public Rig(Transform root)
        {
            this.root = root;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                bones[t.name] = t;
                // Mixamo 리그는 "mixamorig:Hips"처럼 접두어가 붙어 있어서 접두어 없는 이름으로도 찾을 수 있게 등록
                int colon = t.name.IndexOf(':');
                if (colon >= 0) bones[t.name.Substring(colon + 1)] = t;
            }
            right = root.right; up = root.up; forward = root.forward;
        }

        public bool Has(string name) => bones.ContainsKey(name);

        public Transform this[string name] =>
            bones.TryGetValue(name, out Transform t) ? t : throw new Exception("[GGachiAnimationBaker] 뼈를 찾지 못했습니다: " + name);

        /// <summary>뼈를 보스 기준 축으로 돌린다. 리그에 없는 뼈(예: 꼬리가 없는 리그의 tail_1)는 건너뛴다</summary>
        public void Rotate(string bone, Vector3 axis, float degrees)
        {
            if (!bones.TryGetValue(bone, out Transform b)) return;
            b.rotation = Quaternion.AngleAxis(degrees, axis) * b.rotation;
        }
    }

    // ───────────────────────── 메뉴 ─────────────────────────

    [MenuItem("Tools/Boss Fight/4. Build GGachi Hover Idle")]
    public static void BuildHover()
    {
        BakeBody(BodyHoverPath, "GGachi_Hover", HoverClipLength, HoverBody);
        BakeWings(WingsHoverPath, "GGachi_Wings_Hover", HoverClipLength, HoverWings);
        SetupControllers();
        Debug.Log("[GGachiAnimationBaker] 호버링 Idle 생성 완료");
    }

    [MenuItem("Tools/Boss Fight/5. Build GGachi Slam Dive")]
    public static void BuildSlamDive()
    {
        BakeBody(BodyDivePath, "GGachi_SlamDive", DiveClipLength, DiveBody);
        BakeWings(WingsDivePath, "GGachi_Wings_SlamDive", DiveClipLength, DiveWings);
        SetupControllers();
        Debug.Log("[GGachiAnimationBaker] 하강 찍기 생성 완료");
    }

    // ───────────────────────── 자세: 호버링 ─────────────────────────

    /// <summary>
    /// 몸통: 날갯짓에 맞춰 오르내리고, 두 다리는 곧게 펴서 늘어뜨림, 팔은 내림
    /// </summary>
    private static void HoverBody(Rig r, float time)
    {
        float phase = time / FlapPeriod * Mathf.PI * 2f;
        float slow = phase * 0.5f; // 4초 주기 느린 흔들림

        // 몸: 떠오름 + 날갯짓 반동 (날개를 내려치는 순간 몸이 위로)
        float bob = HoverHeightRatio + BobHeightRatio * -Mathf.Sin(phase);
        r["Hips"].position = r.hipsRestWorld + r.up * (bob * r.height);
        r.Rotate("Spine1", r.right, 3f * Mathf.Sin(phase));

        // 두 다리: 무릎을 펴고 곧게 늘어뜨림, 발끝은 아래로. 엉덩이에서만 엇갈린 박자로 살짝 흔들림
        r.Rotate("LeftUpLeg", r.right, 3f * Mathf.Sin(slow + 0.5f));
        r.Rotate("LeftFoot", r.right, 40f + 4f * Mathf.Sin(slow - 0.9f));
        r.Rotate("RightUpLeg", r.right, 3f * Mathf.Sin(slow + 2.2f));
        r.Rotate("RightFoot", r.right, 40f + 4f * Mathf.Sin(slow + 0.6f));

        // 팔: 몸 옆으로 자연스럽게 내리고 숨 쉬듯 살짝 흔들림
        float sway = 3f * Mathf.Sin(slow);
        r.Rotate("LeftArm", r.forward, 14f);
        r.Rotate("LeftArm", r.right, sway);
        r.Rotate("LeftForeArm", r.right, -12f);
        r.Rotate("RightArm", r.forward, -14f);
        r.Rotate("RightArm", r.right, -sway);
        r.Rotate("RightForeArm", r.right, -12f);
    }

    /// <summary>날개: 크게 퍼덕임. 위팔 → 아래팔 → 손 순서로 조금씩 늦게 따라와 휘는 느낌</summary>
    private static void HoverWings(Rig r, float time)
    {
        float phase = time / FlapPeriod * Mathf.PI * 2f;
        float upper = WingUpBias + WingUpperAmp * Mathf.Sin(phase);
        float fore = WingUpBias * 0.5f + WingForeAmp * Mathf.Sin(phase - 0.6f);
        float hand = WingHandAmp * Mathf.Sin(phase - 1.1f);
        r.Rotate("wing_upper.L", r.forward, -upper);
        r.Rotate("wing_fore.L", r.forward, -fore);
        r.Rotate("wing_hand.L", r.forward, -hand);
        r.Rotate("wing_upper.R", r.forward, upper);
        r.Rotate("wing_fore.R", r.forward, fore);
        r.Rotate("wing_hand.R", r.forward, hand);
    }

    // ───────────────────────── 자세: 하강 찍기 ─────────────────────────

    /// <summary>
    /// 몸통: 몸을 곧게 세우고 발부터 내리꽂는 자세
    ///  - 왼다리는 곧게 뻗어 발톱을 아래로, 오른다리는 무릎을 들어 'ㄱ'자 (허벅지 수평, 정강이 수직)
    ///  - 팔은 어깨에서 옆으로 벌려 내리고 뒤로 살짝 젖힘
    ///  - 머리는 내려찍을 곳을 내려다봄
    /// </summary>
    private static void DiveBody(Rig r, float time)
    {
        float tremble = Mathf.Sin(time / DiveTremblePeriod * Mathf.PI * 2f);

        r.Rotate("Head", r.right, 25f);

        r.Rotate("LeftUpLeg", r.forward, 5f);
        r.Rotate("LeftFoot", r.right, 55f + 2f * tremble);

        r.Rotate("RightUpLeg", r.right, -90f);
        r.Rotate("RightLeg", r.right, 90f);
        r.Rotate("RightFoot", r.right, 40f - 3f * tremble);

        r.Rotate("LeftArm", r.forward, -35f);
        r.Rotate("LeftArm", r.right, 12f);
        r.Rotate("LeftForeArm", r.forward, -10f);
        r.Rotate("RightArm", r.forward, 35f);
        r.Rotate("RightArm", r.right, 12f);
        r.Rotate("RightForeArm", r.forward, 10f);
    }

    /// <summary>날개: 위로 접어 올리고(forward 축) 뒤로 젖힘(up 축), 끝은 낙하 바람에 떨림</summary>
    private static void DiveWings(Rig r, float time)
    {
        float tremble = Mathf.Sin(time / DiveTremblePeriod * Mathf.PI * 2f);
        r.Rotate("wing_upper.L", r.forward, -55f);
        r.Rotate("wing_upper.L", r.up, -30f);
        r.Rotate("wing_fore.L", r.forward, -25f + 4f * tremble);
        r.Rotate("wing_hand.L", r.forward, -15f + 6f * tremble);
        r.Rotate("wing_upper.R", r.forward, 55f);
        r.Rotate("wing_upper.R", r.up, 30f);
        r.Rotate("wing_fore.R", r.forward, 25f - 4f * tremble);
        r.Rotate("wing_hand.R", r.forward, 15f - 6f * tremble);
    }

    // ───────────────────────── 굽기: 몸통 (Humanoid) ─────────────────────────

    private static readonly string[] BodyExtraBones = { "tail_1", "tail_2", "tail_3" };

    private static void BakeBody(string path, string clipName, float length, Action<Rig, float> pose)
    {
        GameObject model = Spawn(BodyModelPath);
        try
        {
            Avatar avatar = FindAvatar(BodyModelPath);
            var r = new Rig(model.transform) { height = RendererHeight(model.transform) };
            r.hipsRestWorld = r["Hips"].position;
            var handler = new HumanPoseHandler(avatar, model.transform);
            var humanPose = new HumanPose();

            var rest = SnapshotRotations(model.transform);
            Vector3 hipsRestLocal = r["Hips"].localPosition;

            // 손가락 근육은 클립 속성 이름이 달라서 빼고, 몸통·팔·다리·머리 근육만 기록
            var muscleIndices = new List<int>();
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string n = HumanTrait.MuscleName[i];
                if (!(n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little"))) muscleIndices.Add(i);
            }
            var muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
            foreach (int i in muscleIndices) muscleCurves[i] = new AnimationCurve();
            AnimationCurve[] rootT = NewCurves(3), rootQ = NewCurves(4);
            // 꼬리는 돌리지 않고 쉬는 자세 회전을 그대로 기록해 몸에 고정한다 (꼬리 뼈가 없는 리그는 건너뜀)
            var extras = new List<string>();
            foreach (string b in BodyExtraBones) if (r.Has(b)) extras.Add(b);
            var extraCurves = new Dictionary<string, AnimationCurve[]>();
            foreach (string b in extras) extraCurves[b] = NewCurves(4);
            var lastRot = new Dictionary<string, Quaternion>();

            int frames = Mathf.RoundToInt(length * Fps);
            for (int f = 0; f <= frames; f++)
            {
                float time = f / (float)Fps;
                RestoreRotations(rest);
                r["Hips"].localPosition = hipsRestLocal;
                pose(r, time);

                handler.GetHumanPose(ref humanPose);
                foreach (int i in muscleIndices) muscleCurves[i].AddKey(time, humanPose.muscles[i]);
                rootT[0].AddKey(time, humanPose.bodyPosition.x);
                rootT[1].AddKey(time, humanPose.bodyPosition.y);
                rootT[2].AddKey(time, humanPose.bodyPosition.z);
                Quaternion bq = humanPose.bodyRotation;
                rootQ[0].AddKey(time, bq.x); rootQ[1].AddKey(time, bq.y); rootQ[2].AddKey(time, bq.z); rootQ[3].AddKey(time, bq.w);

                foreach (string b in extras) AddRotationKey(extraCurves[b], r[b].localRotation, time, b, lastRot);
            }

            var clip = new AnimationClip { frameRate = Fps };
            foreach (int i in muscleIndices) clip.SetCurve("", typeof(Animator), HumanTrait.MuscleName[i], muscleCurves[i]);
            string[] t = { "RootT.x", "RootT.y", "RootT.z" }, q = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < 3; i++) clip.SetCurve("", typeof(Animator), t[i], rootT[i]);
            for (int i = 0; i < 4; i++) clip.SetCurve("", typeof(Animator), q[i], rootQ[i]);
            foreach (string b in extras) SetRotationCurves(clip, AnimationUtility.CalculateTransformPath(r[b], model.transform), extraCurves[b]);

            // 루트 이동은 PlayerMovement처럼 스크립트가 맡으므로 모든 루트 움직임을 자세에 굽는다 (떠 있는 높이 포함)
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true;
            s.loopBlendOrientation = true; s.keepOriginalOrientation = true;
            s.loopBlendPositionY = true; s.keepOriginalPositionY = true;
            s.loopBlendPositionXZ = true; s.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            SaveClip(clip, path, clipName);
            handler.Dispose();
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }

    // ───────────────────────── 굽기: 날개 (Generic) ─────────────────────────

    private static readonly string[] WingBones =
    {
        "wing_upper.L", "wing_fore.L", "wing_hand.L",
        "wing_upper.R", "wing_fore.R", "wing_hand.R"
    };

    private static void BakeWings(string path, string clipName, float length, Action<Rig, float> pose)
    {
        GameObject model = Spawn(WingsModelPath);
        try
        {
            var r = new Rig(model.transform) { height = RendererHeight(model.transform) };
            var rest = SnapshotRotations(model.transform);
            var curves = new Dictionary<string, AnimationCurve[]>();
            foreach (string b in WingBones) curves[b] = NewCurves(4);
            var lastRot = new Dictionary<string, Quaternion>();

            int frames = Mathf.RoundToInt(length * Fps);
            for (int f = 0; f <= frames; f++)
            {
                float time = f / (float)Fps;
                RestoreRotations(rest);
                pose(r, time);
                foreach (string b in WingBones) AddRotationKey(curves[b], r[b].localRotation, time, b, lastRot);
            }

            var clip = new AnimationClip { frameRate = Fps };
            foreach (string b in WingBones) SetRotationCurves(clip, AnimationUtility.CalculateTransformPath(r[b], model.transform), curves[b]);
            clip.EnsureQuaternionContinuity();
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            SaveClip(clip, path, clipName);
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }

    // ───────────────────────── 컨트롤러 ─────────────────────────

    /// <summary>
    /// 몸통·날개 컨트롤러를 같은 구조로 만든다 (여러 번 실행해도 같은 결과):
    ///  - Idle(기본) = 호버링, 어느 상태에서든 Dive(trigger) → SlamDive, Land(trigger) → Idle
    /// </summary>
    private static void SetupControllers()
    {
        SetupController(BodyControllerPath, BodyHoverPath, BodyDivePath);
        SetupController(WingsControllerPath, WingsHoverPath, WingsDivePath);
    }

    private static void SetupController(string controllerPath, string hoverPath, string divePath)
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        AnimationClip hover = AssetDatabase.LoadAssetAtPath<AnimationClip>(hoverPath);
        AnimationClip dive = AssetDatabase.LoadAssetAtPath<AnimationClip>(divePath);

        AnimatorState idle = FindState(sm, "Idle") ?? sm.AddState("Idle");
        if (hover != null) idle.motion = hover;
        sm.defaultState = idle;
        if (dive == null)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return;
        }

        EnsureParameter(ctrl, "Dive", AnimatorControllerParameterType.Trigger);
        EnsureParameter(ctrl, "Land", AnimatorControllerParameterType.Trigger);

        AnimatorState old = FindState(sm, "SlamDive");
        if (old != null) sm.RemoveState(old);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
        {
            if (t.destinationState == null) sm.RemoveAnyStateTransition(t);
        }

        AnimatorState diveState = sm.AddState("SlamDive");
        diveState.motion = dive;

        AnimatorStateTransition toDive = sm.AddAnyStateTransition(diveState);
        toDive.hasExitTime = false;
        toDive.duration = 0.12f;          // 낙하 지점 고정 순간 빠르게 자세 전환
        toDive.canTransitionToSelf = false;
        toDive.AddCondition(AnimatorConditionMode.If, 0f, "Dive");

        AnimatorStateTransition toIdle = diveState.AddTransition(idle);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.3f;           // 착지 후 날개를 펴며 호버링으로 돌아감
        toIdle.AddCondition(AnimatorConditionMode.If, 0f, "Land");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState s in sm.states)
        {
            if (s.state.name == name) return s.state;
        }
        return null;
    }

    private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in ctrl.parameters)
        {
            if (p.name == name) return;
        }
        ctrl.AddParameter(name, type);
    }

    // ───────────────────────── 도우미 ─────────────────────────

    private static GameObject Spawn(string modelPath)
    {
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        model.hideFlags = HideFlags.HideAndDontSave;
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return model;
    }

    private static Dictionary<Transform, Quaternion> SnapshotRotations(Transform root)
    {
        var d = new Dictionary<Transform, Quaternion>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>()) d[t] = t.localRotation;
        return d;
    }

    private static void RestoreRotations(Dictionary<Transform, Quaternion> rest)
    {
        foreach (var kv in rest) kv.Key.localRotation = kv.Value;
    }

    private static void AddRotationKey(AnimationCurve[] c, Quaternion q, float time, string key, Dictionary<string, Quaternion> last)
    {
        if (last.TryGetValue(key, out Quaternion prev) && Quaternion.Dot(prev, q) < 0f)
        {
            q = new Quaternion(-q.x, -q.y, -q.z, -q.w); // 보간이 반대로 도는 것 방지
        }
        last[key] = q;
        c[0].AddKey(time, q.x); c[1].AddKey(time, q.y); c[2].AddKey(time, q.z); c[3].AddKey(time, q.w);
    }

    private static void SetRotationCurves(AnimationClip clip, string path, AnimationCurve[] c)
    {
        string[] props = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        for (int i = 0; i < 4; i++) clip.SetCurve(path, typeof(Transform), props[i], c[i]);
    }

    private static void SaveClip(AnimationClip clip, string path, string clipName)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            clip.name = clipName;
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            EditorUtility.CopySerialized(clip, existing); // 같은 에셋(GUID)을 유지한 채 내용만 교체
            existing.name = clipName;
        }
        AssetDatabase.SaveAssets();
    }

    private static AnimationCurve[] NewCurves(int n)
    {
        var c = new AnimationCurve[n];
        for (int i = 0; i < n; i++) c[i] = new AnimationCurve();
        return c;
    }

    private static Avatar FindAvatar(string modelPath)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            if (o is Avatar a) return a;
        }
        throw new Exception("[GGachiAnimationBaker] 아바타가 없습니다: " + modelPath);
    }

    private static float RendererHeight(Transform root)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 1f;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
    }
}
