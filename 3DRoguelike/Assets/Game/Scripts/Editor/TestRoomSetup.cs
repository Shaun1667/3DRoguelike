using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 플레이어 프리팹과 회색 시험 방을 만든다. 여러 번 실행해도 결과가 같다.
    /// 수치 에셋은 없을 때만 만들고 값을 덮어쓰지 않는다.
    /// </summary>
    public static class TestRoomSetup
    {
        private const string TuningPath = "Assets/Game/Data/PlayerTuning.asset";
        private const string MeleeEffectPath = "Assets/Game/Art/Effects/ClawSlash/Player_ClawSlash.prefab";

        private const string ClipFolder = "Assets/Game/Art/Gorani/Animation";
        private const string ControllerPath = "Assets/Game/Art/Gorani/Gorani.controller";
        private const string GoraniPrefabPath = "Assets/Game/Prefabs/Gorani.prefab";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player.prefab";
        private const string ScenePath = "Assets/Game/Scenes/TestRoom.unity";
        private const string TracerMaterialPath = "Assets/Game/Art/Tracer.mat";
        private const string GoraniMaterialPath = "Assets/Game/Art/Gorani/Gorani_Mat.mat";

        private const float RoomSize = 24f;
        private const float WallHeight = 4f;
        private const float WallThickness = 1f;

        [MenuItem("Tools/Game/Setup Test Room")]
        public static void Run()
        {
            ModelPrefabSetup.Run();
            EnableFlash(AssetDatabase.LoadAssetAtPath<Material>(GoraniMaterialPath));
            PlayerTuning tuning = LoadOrCreateTuning();
            HandPoseTable hands = HandPoseSetup.LoadOrCreateTable();
            AnimatorController controller = BuildController(tuning, ControllerPath, NormalMotions, hands);
            AnimatorController mutantController = null;
            if (HasClips(MutantMotions)) mutantController = BuildController(tuning, MutantControllerPath, MutantMotions, hands);
            else Debug.LogWarning($"[TestRoomSetup] Mutant 동작 파일({MutantMotions.Walk} · {MutantMotions.Run} · {MutantMotions.Jump}.fbx)이 없어 Mutant 모습은 빼고 만듭니다.");
            GoraniFurSetup.BuildAssets(); // 머리 · 손 털(셸) 메시 · 재질
            GameObject playerPrefab = BuildPlayerPrefab(tuning, controller, mutantController, hands);
            EnemySetup.Run(); // 숲 적 4종 수치 · 프리팹 · 구성 표
            BuildScene(playerPrefab);
            BuildSceneList.Ensure(ScenePath, first: false);
            AssetDatabase.SaveAssets();
            Debug.Log("[TestRoomSetup] 시험 방 생성: " + ScenePath);
        }

        private static PlayerTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<PlayerTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }
            // 근접 이펙트는 비어 있을 때만 채운다(다른 것으로 바꿔 둔 값은 그대로 둔다)
            if (tuning.meleeEffect == null) tuning.meleeEffect = AssetDatabase.LoadAssetAtPath<GameObject>(MeleeEffectPath);
            EditorUtility.SetDirty(tuning); // 새로 생긴 필드를 파일에 남긴다
            return tuning;
        }

        /// <summary>HitFlash가 프로퍼티 블록으로 켤 수 있도록 발광을 검은색으로 켜 둔다.</summary>
        internal static void EnableFlash(Material material)
        {
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", Color.black);
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }

        internal static Material LoadOrCreateLit(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Material LoadOrCreateUnlit(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static AnimationClip LoadClip(string fileName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath($"{ClipFolder}/{fileName}.fbx"))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            }
            throw new System.InvalidOperationException($"동작을 찾지 못했습니다: {fileName}");
        }

        /// <summary>
        /// 앞으로 가는 동작 묶음(파일 이름, `Art/Gorani/Animation/` 안). 보통 모습과 Beast가 높을 때의 Mutant 모습이 컨트롤러를 하나씩 갖고,
        /// 나머지 동작(대기 · 옆 · 뒤 · 공격 · 구르기 · 죽음)은 같이 쓴다. 새 모습 = 묶음 한 줄 + 컨트롤러 경로.
        /// </summary>
        private readonly struct MotionSet
        {
            public readonly string Walk, Run, Jump;
            public MotionSet(string walk, string run, string jump) { Walk = walk; Run = run; Jump = jump; }
        }

        // 보통 모습 점프 = Mixamo "Standing Jump"의 공중 구간만(프레임 17–37, FBX .meta의 clipAnimations). 발이 뜨는 것은 20, 첫 발이 닿는 것은 36이지만,
        // 위아래 이동이 루트 모션으로 빠질 때 몸 높이 기준이 첫 프레임이라 서 있을 때와 몸 높이가 같은 17에서 시작한다. 도움닫기 · 착지 동작은 뺐다(2026-11-20)
        private static readonly MotionSet NormalMotions = new MotionSet("Walk", "Jog Forward", "Standing Jump");
        private static readonly MotionSet MutantMotions = new MotionSet("Mutant Walk", "Mutant Run", "Mutant Jump");
        private const string MutantControllerPath = "Assets/Game/Art/Gorani/Gorani_Mutant.controller";

        /// <summary>walkPlaybackSpeed 2.4가 4 m/s에 발이 맞았던 걷기 클립(Mixamo Standard Walk)의 앞으로 가는 속도. 다른 걷기 클립은 이 비율로 맞춘다.</summary>
        private const float ReferenceWalkClipSpeed = 1.4976f;

        private static bool HasClips(MotionSet motions)
        {
            foreach (string clip in new[] { motions.Walk, motions.Run, motions.Jump })
            {
                if (AssetImporter.GetAtPath($"{ClipFolder}/{clip}.fbx") == null) return false;
            }
            return true;
        }

private static AnimatorController BuildController(PlayerTuning tuning, string path, MotionSet motions, HandPoseTable hands)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveY", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveAmount", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, DiagonalMixParameter, AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            BlendTree tree = FindState(machine, "Move")?.motion as BlendTree;
            if (tree == null) controller.CreateBlendTreeInController("Move", out tree, 0);
            DestroyNestedTrees(tree); // 안쪽 트리는 매번 새로 만든다(컨트롤러 파일에 남은 것이 쌓이지 않게)

            // 이동 상태 = 1D(MoveAmount): 0 대기, 1 방향 트리(MoveX · MoveY, 방향만 넣는다).
            // 대기(8초)를 방향 트리에 그대로 섞으면 길이 맞춤 때문에 발이 몇 배 느려지므로(대각선 · 조준 이동) 따로 둔다.
            float walk = tuning.walkPlaybackSpeed;
            float sprint = tuning.sprintMultiplier;
            EnsureLoop(motions.Walk);
            EnsureLoop(motions.Run);
            foreach (var jog in JogRing) EnsureLoop(jog.clip);
            ChildMotion forward = Child(motions.Walk, 0f, 1f, MoveTimeScale(motions.Walk, walk, 1f));
            ChildMotion back = Child("Walking Backward", 0f, -1f, MoveTimeScale("Walking Backward", walk, 1f));
            ChildMotion left = Child("Left Strafe Walking", -1f, 0f, MoveTimeScale("Left Strafe Walking", walk, 1f));
            ChildMotion right = Child("Right Strafe Walking", 1f, 0f, MoveTimeScale("Right Strafe Walking", walk, 1f));
            // 방향 트리 가운데(방향을 뒤집을 때만 지나간다): 대기 동작을 걷기 한 걸음 길이에 맞춰 넣어 발 박자를 늦추지 않게 한다
            AnimationClip idle = LoadClip("Idle");
            float stride = forward.motion.averageDuration / forward.timeScale;

            var ring = new System.Collections.Generic.List<ChildMotion>
            {
                Child("Idle", 0f, 0f, idle.averageDuration / stride),
                forward,
                back,
                left,
                right,
                Diagonal(controller, "ForwardLeft", -1f, 1f, 1f, forward, left),
                Diagonal(controller, "ForwardRight", 1f, 1f, 1f, forward, right),
                // 뒤 대각선은 반대쪽 옆걸음을 거꾸로 재생해 섞는다. 같은 쪽 옆걸음을 그대로 섞으면 다리가 서로 반대로 가서 제자리걸음이 된다
                Diagonal(controller, "BackLeft", -1f, -1f, 1f, back, Reversed(right)),
                Diagonal(controller, "BackRight", 1f, -1f, 1f, back, Reversed(left)),
                // 달리기 링(반지름 sprintMultiplier): 걷기 링과 같은 8방향. 앞은 모습별 달리기 클립, 나머지 7방향은 Jog 세트의 방향별 전용 클립을 두 모습이 같이 쓴다
                RunChild(motions.Run, 0f, 1f, walk, sprint),
            };
            foreach (var jog in JogRing) ring.Add(RunChild(jog.clip, jog.x, jog.y, walk, sprint));

            BlendTree directions = NestedTree(controller, "Directions");
            directions.blendType = BlendTreeType.FreeformDirectional2D;
            directions.blendParameter = "MoveX";
            directions.blendParameterY = "MoveY";
            directions.children = ring.ToArray();
            EditorUtility.SetDirty(directions);


            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "MoveAmount";
            tree.useAutomaticThresholds = false;
            tree.children = new[]
            {
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = directions, threshold = 1f, timeScale = 1f },
            };
            EditorUtility.SetDirty(tree);
            // Shift 달리기: PlayerController가 방향(MoveX · MoveY)의 길이를 sprintMultiplier까지 올려 어느 방향이든 달리기 링으로 넘어간다.
            // MoveSpeed(평소 1, 달릴 때 sprintMultiplier, 조준 · 스틱을 덜 밀 때는 그만큼 작게)는 상태 전체 배속이다
            EnsureParameter(controller, "MoveSpeed", AnimatorControllerParameterType.Float);
            AnimatorControllerParameter[] parameters = controller.parameters;
            foreach (AnimatorControllerParameter parameter in parameters)
            {
                if (parameter.name == "MoveSpeed") parameter.defaultFloat = 1f;
                if (parameter.name == DiagonalMixParameter) parameter.defaultFloat = 0.5f;
            }
            controller.parameters = parameters;
            AnimatorState moveState = FindState(machine, "Move");
            moveState.speedParameter = "MoveSpeed";
            moveState.speedParameterActive = true;
            AddAttackStates(controller, machine, tuning);
            AddRollState(controller, machine, move: FindState(machine, "Move"), tuning);
            AddJumpState(controller, machine, move: FindState(machine, "Move"), tuning, motions.Jump);
            AddDeathState(controller, machine);
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true; // 조준 중 오른팔 IK
            controller.layers = layers;
            HandPoseSetup.AddLayer(controller, hands); // 손가락은 모든 동작 위에 손 모양 레이어로 덮는다
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>오른손 공격과 왼손 공격(같은 동작 좌우 반전). 끝나면 이동으로 돌아간다.</summary>
        private static void AddAttackStates(AnimatorController controller, AnimatorStateMachine machine, PlayerTuning tuning)
        {
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "AttackLeft", AnimatorControllerParameterType.Bool);

            AnimatorState move = FindState(machine, "Move");
            machine.anyStateTransitions = new AnimatorStateTransition[0];
            AddAttackState(machine, move, "AttackRight", false, AnimatorConditionMode.IfNot, tuning);
            AddAttackState(machine, move, "AttackLeft", true, AnimatorConditionMode.If, tuning);
        }

        private static void AddAttackState(AnimatorStateMachine machine, AnimatorState move, string name, bool mirror, AnimatorConditionMode leftCondition, PlayerTuning tuning)
        {
            AnimatorState state = FindState(machine, name) ?? machine.AddState(name);
            state.motion = LoadClip("Attack");
            state.mirror = mirror;
            state.speed = tuning.meleePlaybackSpeed;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = true;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            enter.AddCondition(leftCondition, 0f, "AttackLeft");

            state.transitions = new AnimatorStateTransition[0];
            AnimatorStateTransition exit = state.AddTransition(move);
            exit.hasExitTime = true;
            exit.exitTime = tuning.meleeLockTime * tuning.meleePlaybackSpeed / state.motion.averageDuration;
            exit.duration = 0.1f;
        }

        /// <summary>구르기. 클립 앞뒤의 서기 동작을 빼고 구르는 구간만 구르는 시간에 맞춰 재생한다.</summary>
        private static void AddRollState(AnimatorController controller, AnimatorStateMachine machine, AnimatorState move, PlayerTuning tuning)
        {
            const float clipStart = 0.15f;
            const float clipEnd = 0.85f;

            EnsureParameter(controller, "Roll", AnimatorControllerParameterType.Trigger);
            AnimatorState state = FindState(machine, "Roll") ?? machine.AddState("Roll");
            state.motion = LoadClip("Stand To Roll");
            state.speed = state.motion.averageDuration * (clipEnd - clipStart) / tuning.rollDuration;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.offset = clipStart;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Roll");

            state.transitions = new AnimatorStateTransition[0];
            AnimatorStateTransition exit = state.AddTransition(move);
            exit.hasExitTime = true;
            exit.exitTime = clipEnd;
            exit.duration = 0.1f;
        }

        /// <summary>
        /// 점프(스페이스바, 2026-11-10). 클립 전체를 공중에 떠 있는 시간(2 × √(2 × 높이 ÷ |중력|))에 맞춰 재생하고 이동으로 돌아간다.
        /// 클립의 몸 위아래 움직임은 루트 모션으로 빠지고(applyRootMotion 꺼짐), 실제 높이는 PlayerController가 정한다.
        /// </summary>
        private static void AddJumpState(AnimatorController controller, AnimatorStateMachine machine, AnimatorState move, PlayerTuning tuning, string clip)
        {
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            AnimatorState state = FindState(machine, "Jump") ?? machine.AddState("Jump");
            state.motion = LoadClip(clip);
            float airTime = 2f * Mathf.Sqrt(2f * tuning.jumpHeight / Mathf.Max(-tuning.gravity, 0.01f));
            state.speed = state.motion.averageDuration / Mathf.Max(airTime, 0.1f);

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

            state.transitions = new AnimatorStateTransition[0];
            AnimatorStateTransition exit = state.AddTransition(move);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.1f;
        }

        /// <summary>사망. 한 번 쓰러지고 그대로 멈춘다.</summary>
        private static void AddDeathState(AnimatorController controller, AnimatorStateMachine machine)
        {
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
            AnimatorState state = FindState(machine, "Death") ?? machine.AddState("Death");
            state.motion = LoadClip("Death From Right");

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.1f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == name) return child.state;
            }
            return null;
        }

        /// <summary>
        /// 이동 클립(걷기 · 달리기 · 옆걸음 · 뒷걸음)의 배속. 기준 걷기 클립이 walkPlaybackSpeed에서 moveSpeed에 발이 맞았으므로,
        /// 클립이 가는 속도(수평 크기)의 비율로 맞추면 어느 클립이든 발이 맞는다. 달릴 때는 상태 배속 MoveSpeed가 sprintMultiplier를 곱하므로 여기서는 곱하지 않는다.
        /// </summary>
        /// <param name="stateSpeed">이 클립을 쓸 때의 상태 배속(걷기 1, 달리기 sprintMultiplier). 제자리 클립일 때 원래 속도로 되돌리는 데만 쓴다.</param>
private static float MoveTimeScale(string clip, float walkPlayback, float stateSpeed)
        {
            Vector3 average = LoadClip(clip).averageSpeed;
            float speed = new Vector2(average.x, average.z).magnitude;
            if (speed <= 0.01f)
            {
                // 제자리 동작이면 속도가 0이다. 그때는 상태 배속을 빼서 클립 원래 속도로 재생한다
                Debug.LogWarning($"[TestRoomSetup] '{clip}' 클립이 움직이지 않아(In Place) 원래 속도로 재생합니다. Mixamo에서 In Place를 끄고 받으면 발이 맞습니다.");
                return 1f / Mathf.Max(stateSpeed, 0.01f);
            }
            float scale = walkPlayback * ReferenceWalkClipSpeed / speed;
            Debug.Log($"[TestRoomSetup] '{clip}' 배속 {scale:0.00} (클립 속도 {speed:0.00})");
            return scale;
        }

        // 달리기 링의 앞을 뺀 7방향(Mixamo Jog 세트, 팔을 내리고 뛴다). 방향마다 전용 클립이 있어 섞거나 거꾸로 재생하지 않는다.
        // 보통 모습과 Mutant 모습이 같이 쓴다. 보통 모습의 앞 달리기(Jog Forward)도 같은 세트다. x · y = 가는 방향(오른쪽 · 앞)
        private static readonly (string clip, float x, float y)[] JogRing =
        {
            ("Jog Backward", 0f, -1f),
            ("Jog Left", -1f, 0f),
            ("Jog Right", 1f, 0f),
            ("Jog Forward Left", -1f, 1f),
            ("Jog Forward Right", 1f, 1f),
            ("Jog Backward Left", -1f, -1f),
            ("Jog Backward Right", 1f, -1f),
        };

        /// <summary>
        /// 달리기 클립의 배속. 클립 속도 비율(MoveTimeScale)만으로는 달리기 클립마다 디딘 발이 미끄러지는 정도가 달라서,
        /// 디딘 발 속도 ÷ 이동 속도가 앞걷기(약 0.82)와 같아지도록 클립별로 잰 보정값을 곱한다(Jog 세트는 2026-11-18, Mutant Run은 2026-11-17에 고라니 뼈대에서 잰 값).
        /// 재는 법: 달리기 링의 그 방향 자리에서 발(발목 · 발끝 뼈의 가운데)이 가장 낮은 높이 + 1 cm 안에 있는 동안의 수평 속도를 이동 방향으로 평균한다.
        /// 클립을 바꾸면 다시 재야 한다. 표에 없는 클립은 1.
        /// </summary>
        private static float RunTimeScale(string clip, float walkPlayback, float stateSpeed)
        {
            float fix;
            if (!RunStrideFix.TryGetValue(clip, out fix)) fix = 1f;
            return MoveTimeScale(clip, walkPlayback, stateSpeed) * fix;
        }

        private static readonly System.Collections.Generic.Dictionary<string, float> RunStrideFix = new System.Collections.Generic.Dictionary<string, float>
        {
            { "Mutant Run", 0.82f },
            { "Jog Forward", 1.02f },
            { "Jog Backward", 1.06f },
            { "Jog Left", 1.065f },
            { "Jog Right", 1.085f },
            { "Jog Forward Left", 1.08f },
            { "Jog Forward Right", 1.08f },
            { "Jog Backward Left", 1.04f },
            { "Jog Backward Right", 1.08f },
        };

        /// <summary>
        /// 달리기 클립의 시작 위치(한 바퀴 = 1). Jog 세트는 클립마다 먼저 디디는 발이 달라서(앞 왼쪽 · 앞 오른쪽 · 오른쪽 · 뒤 오른쪽이 반 바퀴 어긋난다)
        /// 그대로 두면 이웃한 방향 사이에서 두 클립이 서로 지워져 제자리걸음이 된다. 왼발을 디디는 때가 모두 바퀴의 0.5(걷기 클립들과 같다)가 되게 맞춘다.
        /// 값 = 그 클립에서 왼발을 디디는 때 − 0.5(2026-11-18에 잰 값). 클립을 바꾸면 다시 재야 한다. 표에 없는 클립은 0.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, float> RunCycleOffset = new System.Collections.Generic.Dictionary<string, float>
        {
            { "Mutant Run", 0.01f },
            { "Jog Forward", 0.96f },
            { "Jog Backward", 0.04f },
            { "Jog Left", 0.03f },
            { "Jog Right", 0.55f },
            { "Jog Forward Left", 0.425f },
            { "Jog Forward Right", 0.48f },
            { "Jog Backward Left", 0f },
            { "Jog Backward Right", 0.535f },
        };

        /// <summary>달리기 링의 한 자리. x · y = 가는 방향(길이는 링 반지름으로 맞춘다).</summary>
        private static ChildMotion RunChild(string clip, float x, float y, float walkPlayback, float radius)
        {
            Vector2 at = new Vector2(x, y).normalized * radius;
            ChildMotion child = Child(clip, at.x, at.y, RunTimeScale(clip, walkPlayback, radius));
            float offset;
            if (RunCycleOffset.TryGetValue(clip, out offset)) child.cycleOffset = offset;
            return child;
        }

        /// <summary>대각선 자리의 안쪽 트리가 두 동작을 반씩 섞는 데 쓰는 파라미터. 값은 0.5로 두고 아무도 바꾸지 않는다.</summary>
        private const string DiagonalMixParameter = "DiagonalMix";

        /// <summary>
        /// 대각선 동작 = 두 동작을 반씩 섞은 것. 옆걸음 클립은 골반을 가는 쪽으로 66도쯤 돌린 앞걸음이라, 앞걸음과 섞으면 보폭이 거의 그대로인 대각선 걸음이 된다.
        /// 대각선 전용 클립이 생기면 이 자리에 그 클립을 넣는다.
        /// </summary>
private static ChildMotion Diagonal(AnimatorController controller, string name, float x, float y, float radius, ChildMotion a, ChildMotion b)
        {
            BlendTree mix = NestedTree(controller, name);
            mix.blendType = BlendTreeType.Simple1D;
            mix.blendParameter = DiagonalMixParameter;
            mix.useAutomaticThresholds = false;
            a.threshold = 0f;
            b.threshold = 1f;
            mix.children = new[] { a, b };
            EditorUtility.SetDirty(mix);
            // 안쪽 트리에는 timeScale이 먹지 않는다(배속은 안에 든 클립의 것을 따른다)
            return new ChildMotion { motion = mix, position = new Vector2(x, y).normalized * radius, timeScale = 1f };
        }

        /// <summary>거꾸로 재생한다(왼쪽 옆걸음을 거꾸로 = 골반을 왼쪽으로 돌린 채 오른쪽으로 가는 뒷걸음).</summary>
        private static ChildMotion Reversed(ChildMotion child)
        {
            child.timeScale = -child.timeScale;
            return child;
        }

        private static BlendTree NestedTree(AnimatorController controller, string name)
        {
            var tree = new BlendTree { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        private static void DestroyNestedTrees(BlendTree tree)
        {
            foreach (ChildMotion child in tree.children)
            {
                if (!(child.motion is BlendTree nested)) continue;
                DestroyNestedTrees(nested);
                Object.DestroyImmediate(nested, true);
            }
            tree.children = new ChildMotion[0];
        }

        /// <summary>새로 넣은 Mixamo 동작을 걷기와 같은 설정(반복, 회전 · 높이 고정)으로 가져온다. 이미 맞으면 다시 가져오지 않는다.</summary>
        private static void EnsureLoop(string fileName)
        {
            string path = $"{ClipFolder}/{fileName}.fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) throw new System.InvalidOperationException($"동작을 찾지 못했습니다: {fileName}");
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            bool alreadySet = clips.Length > 0;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                alreadySet &= clip.loopTime && clip.loopPose && clip.lockRootRotation && clip.lockRootHeightY && !clip.lockRootPositionXZ;
            }
            if (alreadySet) return;
            if (clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = false;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static ChildMotion Child(string clip, float x, float y, float speed)
        {
            return new ChildMotion { motion = LoadClip(clip), position = new Vector2(x, y), timeScale = speed };
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        private static GameObject BuildPlayerPrefab(PlayerTuning tuning, AnimatorController controller, AnimatorController mutantController, HandPoseTable hands)
        {
            var root = new GameObject("Player") { tag = "Player" };
            var body = root.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = new Vector3(0f, 0.9f, 0f);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GoraniPrefabPath), root.transform);
            visual.name = "Visual";
            var animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            GoraniFurSetup.AddTo(visual); // 머리 · 손 털. 보통 · Mutant 모습이 같이 쓴다

            var cameraTarget = new GameObject("CameraTarget").transform;
            cameraTarget.SetParent(root.transform, false);
            cameraTarget.localPosition = new Vector3(0f, 1.5f, 0f);

            var melee = root.AddComponent<PlayerMelee>();
            var gun = root.AddComponent<PlayerGun>();
            var roll = root.AddComponent<PlayerRoll>();
            BuildPistol(root.transform, out Transform pistol, out Transform muzzle, out LineRenderer tracer);

            var meleeFields = new SerializedObject(melee);
            meleeFields.FindProperty("tuning").objectReferenceValue = tuning;
            meleeFields.FindProperty("animator").objectReferenceValue = animator;
            meleeFields.FindProperty("gun").objectReferenceValue = gun;
            meleeFields.FindProperty("roll").objectReferenceValue = roll;
            meleeFields.ApplyModifiedPropertiesWithoutUndo();

            var gunFields = new SerializedObject(gun);
            gunFields.FindProperty("tuning").objectReferenceValue = tuning;
            gunFields.FindProperty("melee").objectReferenceValue = melee;
            gunFields.FindProperty("roll").objectReferenceValue = roll;
            gunFields.FindProperty("animator").objectReferenceValue = animator;
            gunFields.FindProperty("pistol").objectReferenceValue = pistol;
            gunFields.FindProperty("muzzle").objectReferenceValue = muzzle;
            gunFields.FindProperty("tracer").objectReferenceValue = tracer;
            gunFields.ApplyModifiedPropertiesWithoutUndo();

            var rollFields = new SerializedObject(roll);
            rollFields.FindProperty("tuning").objectReferenceValue = tuning;
            rollFields.FindProperty("melee").objectReferenceValue = melee;
            rollFields.FindProperty("animator").objectReferenceValue = animator;
            rollFields.ApplyModifiedPropertiesWithoutUndo();

            var aimIK = visual.AddComponent<PlayerAimIK>();
            var ikFields = new SerializedObject(aimIK);
            ikFields.FindProperty("tuning").objectReferenceValue = tuning;
            ikFields.FindProperty("gun").objectReferenceValue = gun;
            ikFields.FindProperty("hands").objectReferenceValue = hands;
            ikFields.ApplyModifiedPropertiesWithoutUndo();

            var handFields = new SerializedObject(visual.AddComponent<PlayerHandPose>()); // 조준 · 근접 때 손 모양
            handFields.FindProperty("poses").objectReferenceValue = hands;
            handFields.FindProperty("gun").objectReferenceValue = gun;
            handFields.FindProperty("melee").objectReferenceValue = melee;
            handFields.ApplyModifiedPropertiesWithoutUndo();

            var player = root.AddComponent<PlayerController>();
            var fields = new SerializedObject(player);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("cameraTarget").objectReferenceValue = cameraTarget;
            fields.FindProperty("animator").objectReferenceValue = animator;
            fields.FindProperty("melee").objectReferenceValue = melee;
            fields.FindProperty("gun").objectReferenceValue = gun;
            fields.FindProperty("roll").objectReferenceValue = roll;
            fields.ApplyModifiedPropertiesWithoutUndo();

            var health = root.AddComponent<Health>();
            var flash = root.AddComponent<HitFlash>();
            var vitals = new SerializedObject(root.AddComponent<PlayerVitals>());
            vitals.FindProperty("tuning").objectReferenceValue = tuning;
            vitals.FindProperty("health").objectReferenceValue = health;
            vitals.FindProperty("flash").objectReferenceValue = flash;
            vitals.FindProperty("controller").objectReferenceValue = player;
            vitals.FindProperty("melee").objectReferenceValue = melee;
            vitals.FindProperty("gun").objectReferenceValue = gun;
            vitals.FindProperty("roll").objectReferenceValue = roll;
            vitals.FindProperty("animator").objectReferenceValue = animator;
            vitals.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<PlayerUpgrades>(); // 판 안 강화(3-2). 층에서는 FloorRun이 판 기록의 합계를 넘긴다
            var form = new SerializedObject(root.AddComponent<PlayerForm>()); // Beast가 높으면 Mutant 동작으로
            form.FindProperty("tuning").objectReferenceValue = tuning;
            form.FindProperty("animator").objectReferenceValue = animator;
            form.FindProperty("mutantController").objectReferenceValue = mutantController;
            form.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>바닥에 붙은 얇은 예고 표시. 충돌 · 그림자 없음.</summary>
        internal static GameObject FlatMarker(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var marker = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position + Vector3.up * 0.02f;
            marker.transform.localScale = scale;
            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return marker;
        }

        /// <summary>방(자식 도형)의 NavMesh를 플레이 시작 때 굽게 한다.</summary>
        internal static NavMeshSurface AddNavMeshSurface(GameObject owner)
        {
            var surface = owner.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return surface;
        }

        /// <summary>회색 도형 권총과 탄도 선.</summary>
        private static void BuildPistol(Transform root, out Transform pistol, out Transform muzzle, out LineRenderer tracer)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "PistolBody";
            pistol = new GameObject("Pistol").transform;
            pistol.SetParent(root, false);
            body.transform.SetParent(pistol, false);
            // 자리는 오른손 손목 기준(z = 조준 방향, y = 위). 조준 중 손바닥이 왼쪽을 보므로(HandPoseTable.gripRoll) 총은 손목보다 왼쪽 4 cm
            body.transform.localPosition = new Vector3(-0.04f, 0.088f, 0.19f);
            body.transform.localScale = new Vector3(0.042f, 0.045f, 0.26f);

            // 손잡이: 권총 쥐는 손 모양(HandPoseTable.grip)의 손바닥 안에 들어가는 자리
            var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(grip.GetComponent<Collider>());
            grip.name = "PistolGrip";
            grip.transform.SetParent(pistol, false);
            grip.transform.localPosition = new Vector3(-0.04f, 0.012f, 0.125f);
            grip.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            grip.transform.localScale = new Vector3(0.036f, 0.11f, 0.055f);

            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(pistol, false);
            muzzle.localPosition = new Vector3(-0.04f, 0.088f, 0.33f);

            tracer = new GameObject("Tracer").AddComponent<LineRenderer>();
            tracer.transform.SetParent(root, false);
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.widthMultiplier = 0.02f;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.sharedMaterial = LoadOrCreateTracerMaterial();
        }

        private static Material LoadOrCreateTracerMaterial()
        {
            return LoadOrCreateUnlit(TracerMaterialPath, new Color(1f, 0.85f, 0.4f));
        }

        private static void BuildScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildRoom();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 0.1f, -6f);
            Canvas hud = BuildCamera(player);
            HudSetup.BuildHud(hud, player.GetComponent<PlayerVitals>(), floor: null);

            // 연습용 오소리 Mutant 1마리. 다른 종은 디버그 창(F1)의 '앞에 소환'으로
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(EnemySetup.LoadPrefab(Rules.EnemySpecies.Badger, Rules.EnemyGrade.Mutant));
            enemy.transform.position = new Vector3(0f, 0f, 2f);
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            // 구성 표는 새 씬을 연 뒤에 불러온다(FloorSetup과 같은 이유)
            var source = new SerializedObject(new GameObject("Enemies").AddComponent<EnemyRosterSource>());
            source.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyRoster>(EnemySetup.RosterPath);
            source.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        internal static void BuildLighting()
        {
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }

        private static void BuildRoom()
        {
            var room = new GameObject("Room").transform;
            var bake = new SerializedObject(room.gameObject.AddComponent<BuildNavMeshOnAwake>());
            bake.FindProperty("surface").objectReferenceValue = AddNavMeshSurface(room.gameObject);
            bake.ApplyModifiedPropertiesWithoutUndo();
            float half = RoomSize * 0.5f;
            Block(room, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(RoomSize, 1f, RoomSize));
            float wallY = WallHeight * 0.5f;
            Block(room, "Wall_N", new Vector3(0f, wallY, half), new Vector3(RoomSize, WallHeight, WallThickness));
            Block(room, "Wall_S", new Vector3(0f, wallY, -half), new Vector3(RoomSize, WallHeight, WallThickness));
            Block(room, "Wall_E", new Vector3(half, wallY, 0f), new Vector3(WallThickness, WallHeight, RoomSize));
            Block(room, "Wall_W", new Vector3(-half, wallY, 0f), new Vector3(WallThickness, WallHeight, RoomSize));

            // 이동과 카메라 충돌을 볼 기둥과 낮은 상자
            Block(room, "Pillar_1", new Vector3(-5f, 1.5f, 3f), new Vector3(1.5f, 3f, 1.5f));
            Block(room, "Pillar_2", new Vector3(5f, 1.5f, 3f), new Vector3(1.5f, 3f, 1.5f));
            Block(room, "Pillar_3", new Vector3(0f, 1.5f, 8f), new Vector3(1.5f, 3f, 1.5f));
            Block(room, "Box_1", new Vector3(-7f, 0.5f, -5f), new Vector3(2f, 1f, 2f));
            Block(room, "Box_2", new Vector3(7f, 0.5f, -5f), new Vector3(2f, 1f, 2f));
        }

        private static void Block(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
        }

        /// <returns>조준점이 든 HUD 캔버스. HudSetup이 그 위에 체력 · 탄 · 미니맵을 더한다.</returns>
        internal static Canvas BuildCamera(GameObject player)
        {
            Transform target = player.transform.Find("CameraTarget");
            var main = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = main.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            main.AddComponent<AudioListener>();
            main.AddComponent<CinemachineBrain>();

            var follow = new GameObject("PlayerCamera").AddComponent<CinemachineCamera>();
            follow.Follow = target;
            var thirdPerson = follow.gameObject.AddComponent<CinemachineThirdPersonFollow>();
            thirdPerson.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
            thirdPerson.VerticalArmLength = 0f;
            thirdPerson.CameraSide = 1f;
            PlayerTuning tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath);
            thirdPerson.CameraDistance = tuning.cameraDistance;
            thirdPerson.Damping = new Vector3(0.1f, 0.2f, 0.2f);
            thirdPerson.AvoidObstacles.Enabled = true;
            thirdPerson.AvoidObstacles.CollisionFilter = ~0;
            thirdPerson.AvoidObstacles.IgnoreTag = "Player";
            thirdPerson.AvoidObstacles.CameraRadius = 0.2f;

            var zoom = follow.gameObject.AddComponent<AimCameraZoom>();
            var fields = new SerializedObject(zoom);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("gun").objectReferenceValue = player.GetComponent<PlayerGun>();
            fields.FindProperty("follow").objectReferenceValue = thirdPerson;
            GameObject crosshair = BuildCrosshair();
            fields.FindProperty("crosshair").objectReferenceValue = crosshair;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return crosshair.transform.parent.GetComponent<Canvas>();
        }

        /// <summary>화면 중앙 조준점. 조준할 때만 켠다.</summary>
        private static GameObject BuildCrosshair()
        {
            var canvas = new GameObject("HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var dot = new GameObject("Crosshair", typeof(UnityEngine.UI.Image));
            dot.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)dot.transform;
            rect.sizeDelta = new Vector2(6f, 6f);
            dot.SetActive(false);
            return dot;
        }
    }
}