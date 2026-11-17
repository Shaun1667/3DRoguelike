using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 층 씬을 만든다. 방은 플레이할 때 시드로 세워지므로 씬에는 조명 · 플레이어 · 카메라 · FloorRun(+ NavMesh)만 둔다.
    /// 여러 번 실행해도 결과가 같다. 수치 에셋은 없을 때만 만들고 값을 덮어쓰지 않는다.
    /// </summary>
    public static class FloorSetup
    {
        private const string TuningPath = "Assets/Game/Data/FloorTuning.asset";
        private const string ScenePath = CurrentRun.FloorScene;
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player.prefab";
        private const string MaterialFolder = "Assets/Game/Art/Floor";

        [MenuItem("Tools/Game/Setup Floor")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null || AssetDatabase.LoadAssetAtPath<EnemyRoster>(EnemySetup.RosterPath) == null)
            {
                TestRoomSetup.Run(); // 플레이어 프리팹 · 숲 적 4종 · 구성 표를 만든다
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Game/Art", "Floor");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 새 씬을 연 뒤에 불러온다. 앞에서 새로 만든 에셋은 씬을 바꿀 때 참조가 끊겨 씬에 빈칸으로 저장됐다.
            FloorTuning tuning = LoadOrCreateTuning();
            TestRoomSetup.BuildLighting();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            player.transform.position = new Vector3(0f, 0.1f, 0f); // 시작 방 가운데가 원점
            Canvas hud = TestRoomSetup.BuildCamera(player);
            var vitals = player.GetComponent<PlayerVitals>();
            var vitalsFields = new SerializedObject(vitals);
            vitalsFields.FindProperty("restartOnR").boolValue = false; // 다시 시작은 결과 화면이 맡는다
            vitalsFields.ApplyModifiedPropertiesWithoutUndo();

            var run = new GameObject("Floor").AddComponent<FloorRun>();
            HudSetup.BuildHud(hud, vitals, run);
            RunResultScreen resultScreen = HudSetup.BuildResultScreen(hud);
            RewardChoiceScreen rewardScreen = HudSetup.BuildRewardScreen(hud);
            RewardTable rewardTable = LoadOrCreateRewardTable();
            TMP_Text objective = HudSetup.BuildObjective(hud);

            var fields = new SerializedObject(run);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("rewardTable").objectReferenceValue = rewardTable;
            fields.FindProperty("rewardScreen").objectReferenceValue = rewardScreen;
            fields.FindProperty("objective").objectReferenceValue = objective;
            fields.FindProperty("world").objectReferenceValue = WorldSetup.LoadOrCreateTuning();
            fields.FindProperty("treasureFloorMaterial").objectReferenceValue = Lit("TreasureFloor", new Color(0.62f, 0.55f, 0.3f));
            fields.FindProperty("rewardMaterial").objectReferenceValue = Lit("Reward", new Color(0.55f, 0.75f, 0.95f));
            fields.FindProperty("rareRewardMaterial").objectReferenceValue = Lit("RewardRare", new Color(1f, 0.82f, 0.25f));
            fields.FindProperty("player").objectReferenceValue = vitals;
            fields.FindProperty("resultScreen").objectReferenceValue = resultScreen;
            fields.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyRoster>(EnemySetup.RosterPath);
            fields.FindProperty("navMesh").objectReferenceValue = TestRoomSetup.AddNavMeshSurface(run.gameObject);
            fields.FindProperty("floorMaterial").objectReferenceValue = Lit("Floor", new Color(0.55f, 0.55f, 0.55f));
            fields.FindProperty("startFloorMaterial").objectReferenceValue = Lit("StartFloor", new Color(0.45f, 0.6f, 0.45f));
            fields.FindProperty("bossFloorMaterial").objectReferenceValue = Lit("BossFloor", new Color(0.6f, 0.4f, 0.4f));
            fields.FindProperty("wallMaterial").objectReferenceValue = Lit("Wall", new Color(0.7f, 0.7f, 0.72f));
            fields.FindProperty("lockedDoorMaterial").objectReferenceValue = Lit("LockedDoor", new Color(0.75f, 0.2f, 0.15f));
            fields.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSceneList.Ensure(ScenePath, first: false); // 게임은 월드 씬에서 시작한다(WorldSetup)
            AssetDatabase.SaveAssets();
            Debug.Log("[FloorSetup] 층 씬 생성: " + ScenePath);
        }

        private static FloorTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<FloorTuning>(TuningPath);
            if (tuning != null)
            {
                EditorUtility.SetDirty(tuning); // 새로 생긴 필드를 파일에 남긴다
                return tuning;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FloorTuning>(), TuningPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<FloorTuning>(TuningPath);
        }

        /// <summary>보상 표는 없을 때만 기본 줄로 만들고, 있으면 고친 내용을 그대로 둔다.</summary>
        private static RewardTable LoadOrCreateRewardTable()
        {
            const string path = "Assets/Game/Data/RewardTable.asset";
            var table = AssetDatabase.LoadAssetAtPath<RewardTable>(path);
            if (table != null) return table;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<RewardTable>(), path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<RewardTable>(path);
        }

        private static Material Lit(string name, Color color)
        {
            return TestRoomSetup.LoadOrCreateLit($"{MaterialFolder}/{name}.mat", color);
        }
    }
}
