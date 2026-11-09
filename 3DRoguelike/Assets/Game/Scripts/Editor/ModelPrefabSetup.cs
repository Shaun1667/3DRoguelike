using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 모델 표시용 프리팹을 만든다. 새 모델 = Models 표 한 줄 + 메뉴 다시 실행.
    /// 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class ModelPrefabSetup
    {
        private const string PrefabFolder = "Assets/Game/Prefabs";

        private static readonly (string name, string model, string material)[] Models =
        {
            ("Gorani", "Assets/Game/Art/Gorani/Gorani.fbx", "Assets/Game/Art/Gorani/Gorani_Mat.mat"),
        };

        [MenuItem("Tools/Game/Setup Model Prefabs")]
        public static void Run()
        {
            foreach (var entry in Models)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.model);
                var material = AssetDatabase.LoadAssetAtPath<Material>(entry.material);
                if (model == null || material == null)
                {
                    Debug.LogError($"[ModelPrefabSetup] {entry.name}: 모델 또는 재질을 찾지 못했습니다.");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                }

                PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabFolder}/{entry.name}.prefab");
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ModelPrefabSetup] 프리팹 {Models.Length}개 생성");
        }
    }
}
