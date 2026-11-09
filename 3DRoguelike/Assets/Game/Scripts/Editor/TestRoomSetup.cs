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
            if (tuning != null) return tuning;
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

            EnsureFloat(controller, "MoveX");
            EnsureFloat(controller, "MoveY");

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            BlendTree tree = null;
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == "Move") tree = child.state.motion as BlendTree;
            }
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
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static ChildMotion Child(string clip, float x, float y, float speed)
        {
            return new ChildMotion { motion = LoadClip(clip), position = new Vector2(x, y), timeScale = speed };
        }

        private static void EnsureFloat(AnimatorController controller, string name)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name) return;
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
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

            var player = root.AddComponent<PlayerController>();
            var fields = new SerializedObject(player);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("cameraTarget").objectReferenceValue = cameraTarget;
            fields.FindProperty("animator").objectReferenceValue = animator;
            fields.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
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
            BuildCamera(player.transform.Find("CameraTarget"));

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

        private static void BuildCamera(Transform target)
        {
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
            thirdPerson.CameraDistance = 3f;
            thirdPerson.Damping = new Vector3(0.1f, 0.2f, 0.2f);
            thirdPerson.AvoidObstacles.Enabled = true;
            thirdPerson.AvoidObstacles.CollisionFilter = ~0;
            thirdPerson.AvoidObstacles.IgnoreTag = "Player";
            thirdPerson.AvoidObstacles.CameraRadius = 0.2f;
        }
    }
}