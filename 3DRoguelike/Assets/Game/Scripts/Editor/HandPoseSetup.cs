using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 고라니 손 모양: 손가락만 덮는 애니메이터 레이어(Hands)와 그 포즈 클립을 만든다(TestRoomSetup이 부름). 여러 번 실행해도 결과가 같다.
    /// Mixamo 동작마다 손가락 모양이 제각각이고 예전 클립은 약지 · 새끼 커브가 없어서, 손가락은 동작 클립 대신 이 레이어의 포즈를 쓴다.
    /// 값은 Data/HandPoses.asset(없을 때만 만들고 덮어쓰지 않음), 클립 · 마스크는 매번 그 값으로 다시 쓴다.
    /// </summary>
    internal static class HandPoseSetup
    {
        internal const string TablePath = "Assets/Game/Data/HandPoses.asset";
        private const string Folder = "Assets/Game/Art/Gorani/Animation/Hands";
        private const string MaskPath = Folder + "/Fingers.mask";
        private const string LayerName = "Hands";
        private const string StateName = "Pose";

        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        internal static HandPoseTable LoadOrCreateTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<HandPoseTable>(TablePath);
            if (table != null) return table;
            table = ScriptableObject.CreateInstance<HandPoseTable>();
            AssetDatabase.CreateAsset(table, TablePath);
            return table;
        }

        /// <summary>컨트롤러에 손가락 레이어를 만든다: 평소 손 → (HandGrip) 권총 쥔 오른손 → (HandClaw) 할퀴는 양손.</summary>
        internal static void AddLayer(AnimatorController controller, HandPoseTable table)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(Folder).Replace('\\', '/'), System.IO.Path.GetFileName(Folder));
            AnimationClip relaxed = WriteClip("HandRelaxed", table.relaxed, table.relaxed);
            AnimationClip grip = WriteClip("HandGrip", table.relaxed, table.grip);
            AnimationClip claw = WriteClip("HandClaw", table.claw, table.claw);
            AvatarMask mask = WriteMask();

            EnsureFloat(controller, PlayerHandPose.GripParameter);
            EnsureFloat(controller, PlayerHandPose.ClawParameter);

            int index = FindLayer(controller);
            if (index < 0)
            {
                var machine = new AnimatorStateMachine { name = LayerName, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(machine, controller);
                controller.AddLayer(new AnimatorControllerLayer { name = LayerName, stateMachine = machine, defaultWeight = 1f });
                index = FindLayer(controller);
            }
            AnimatorControllerLayer[] layers = controller.layers;
            layers[index].avatarMask = mask;
            layers[index].defaultWeight = 1f;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].iKPass = false;
            controller.layers = layers;

            AnimatorStateMachine hands = controller.layers[index].stateMachine;
            BlendTree tree = null;
            foreach (ChildAnimatorState child in hands.states)
            {
                if (child.state.name == StateName) tree = child.state.motion as BlendTree;
            }
            if (tree == null) controller.CreateBlendTreeInController(StateName, out tree, index);

            // 바깥: 할퀴기(HandClaw 0 → 1), 안쪽: 평소 손 → 권총 쥔 손(HandGrip 0 → 1)
            BlendTree inner = tree.children.Length > 0 ? tree.children[0].motion as BlendTree : null;
            if (inner == null)
            {
                tree.children = new ChildMotion[0];
                inner = tree.CreateBlendTreeChild(0f);
                inner.name = "Grip";
            }
            inner.blendType = BlendTreeType.Simple1D;
            inner.blendParameter = PlayerHandPose.GripParameter;
            inner.useAutomaticThresholds = false;
            inner.children = new[] { Child(relaxed, 0f), Child(grip, 1f) };
            EditorUtility.SetDirty(inner);

            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = PlayerHandPose.ClawParameter;
            tree.useAutomaticThresholds = false;
            tree.children = new[] { Child(inner, 0f), Child(claw, 1f) };
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(controller);
        }

        private static ChildMotion Child(Motion motion, float threshold)
        {
            return new ChildMotion { motion = motion, threshold = threshold, timeScale = 1f };
        }

        private static int FindLayer(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == LayerName) return i;
            }
            return -1;
        }

        private static void EnsureFloat(AnimatorController controller, string name)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name) return;
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        /// <summary>손가락 머슬 값만 든 멈춘 포즈 클립. 같은 파일을 다시 써서 컨트롤러의 연결이 유지된다.</summary>
        private static AnimationClip WriteClip(string name, HandPoseTable.Pose left, HandPoseTable.Pose right)
        {
            string path = $"{Folder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            WriteHand(clip, "LeftHand", left);
            WriteHand(clip, "RightHand", right);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void WriteHand(AnimationClip clip, string hand, HandPoseTable.Pose pose)
        {
            for (int i = 0; i < FingerNames.Length; i++)
            {
                HandPoseTable.Finger finger = pose[i];
                string prefix = $"{hand}.{FingerNames[i]}.";
                SetMuscle(clip, prefix + "1 Stretched", finger.stretch1);
                SetMuscle(clip, prefix + "Spread", finger.spread);
                SetMuscle(clip, prefix + "2 Stretched", finger.stretch2);
                SetMuscle(clip, prefix + "3 Stretched", finger.stretch3);
            }
        }

        private static void SetMuscle(AnimationClip clip, string muscle, float value)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), AnimationCurve.Constant(0f, 1f, value));
        }

        /// <summary>양손 손가락만 켠 Humanoid 마스크.</summary>
        private static AvatarMask WriteMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "Fingers" };
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }
    }
}
