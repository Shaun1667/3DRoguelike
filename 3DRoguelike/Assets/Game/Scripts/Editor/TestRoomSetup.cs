using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const string ClipFolder = "Assets/Game/Art/Gorani/Animation";
        private const string ControllerPath = "Assets/Game/Art/Gorani/Gorani.controller";
        private const string GoraniPrefabPath = "Assets/Game/Prefabs/Gorani.prefab";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player.prefab";
        private const string ScenePath = "Assets/Game/Scenes/TestRoom.unity";
        private const string TracerMaterialPath = "Assets/Game/Art/Tracer.mat";

        private const float RoomSize = 24f;
        private const float WallHeight = 4f;
        private const float WallThickness = 1f;

        [MenuItem("Tools/Game/Setup Test Room")]
        public static void Run()
        {
            ModelPrefabSetup.Run();
            PlayerTuning tuning = LoadOrCreateTuning();
            AnimatorController controller = BuildController(tuning);
            GameObject playerPrefab = BuildPlayerPrefab(tuning, controller);
            BuildScene(playerPrefab);
            Debug.Log("[TestRoomSetup] 시험 방 생성: " + ScenePath);
        }

        private static PlayerTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath);
            if (tuning != null)
            {
                EditorUtility.SetDirty(tuning); // 새로 생긴 필드를 파일에 남긴다
                return tuning;
            }
            tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
            return tuning;
        }

        private static AnimationClip LoadClip(string fileName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath($"{ClipFolder}/{fileName}.fbx"))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) return clip;
            }
            throw new System.InvalidOperationException($"동작을 찾지 못했습니다: {fileName}");
        }

        private static AnimatorController BuildController(PlayerTuning tuning)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveY", AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            BlendTree tree = FindState(machine, "Move")?.motion as BlendTree;
            if (tree == null) controller.CreateBlendTreeInController("Move", out tree, 0);

            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            float walk = tuning.walkPlaybackSpeed;
            tree.children = new[]
            {
                Child("Idle", 0f, 0f, 1f),
                Child("Walk", 0f, 1f, walk),
                Child("Walking Backward", 0f, -1f, walk),
                Child("Left Strafe Walking", -1f, 0f, walk),
                Child("Right Strafe Walking", 1f, 0f, walk),
            };
            EditorUtility.SetDirty(tree);
            AddAttackStates(controller, machine, tuning);
            AddRollState(controller, machine, move: FindState(machine, "Move"), tuning);
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true; // 조준 중 오른팔 IK
            controller.layers = layers;
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

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == name) return child.state;
            }
            return null;
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

        private static GameObject BuildPlayerPrefab(PlayerTuning tuning, AnimatorController controller)
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
            ikFields.ApplyModifiedPropertiesWithoutUndo();

            var player = root.AddComponent<PlayerController>();
            var fields = new SerializedObject(player);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("cameraTarget").objectReferenceValue = cameraTarget;
            fields.FindProperty("animator").objectReferenceValue = animator;
            fields.FindProperty("melee").objectReferenceValue = melee;
            fields.FindProperty("gun").objectReferenceValue = gun;
            fields.FindProperty("roll").objectReferenceValue = roll;
            fields.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
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
            body.transform.localPosition = new Vector3(0f, -0.02f, 0.14f);
            body.transform.localScale = new Vector3(0.04f, 0.07f, 0.2f);

            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(pistol, false);
            muzzle.localPosition = new Vector3(0f, -0.01f, 0.25f);

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
            var material = AssetDatabase.LoadAssetAtPath<Material>(TracerMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f));
            AssetDatabase.CreateAsset(material, TracerMaterialPath);
            return material;
        }

        private static void BuildScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            BuildRoom();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 0.1f, -6f);
            BuildCamera(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildRoom()
        {
            var room = new GameObject("Room").transform;
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

        private static void BuildCamera(GameObject player)
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
            fields.FindProperty("crosshair").objectReferenceValue = BuildCrosshair();
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>화면 중앙 조준점. 조준할 때만 켠다.</summary>
        private static GameObject BuildCrosshair()
        {
            var canvas = new GameObject("HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler)).GetComponent<Canvas>();
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