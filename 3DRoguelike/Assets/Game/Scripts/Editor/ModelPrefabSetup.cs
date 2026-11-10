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
        private const string GoraniModel = "Assets/Game/Art/Gorani/Gorani.fbx";
        private const string GoraniClipFolder = "Assets/Game/Art/Gorani/Animation";

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
            UseGoraniAvatarForClips();
        }

        /// <summary>
        /// 고라니 동작 FBX가 고라니 아바타로 가져와지게 한다(2026-11-10).
        /// Mixamo에서 뼈대만 받은 동작 FBX는 각자 첫 프레임 자세로 아바타를 만들어, 그 자세가 기준이 되면서 왼발 끝이 약 25도 들려 보였다.
        /// 이미 맞으면 다시 가져오지 않는다.
        /// </summary>
        private static void UseGoraniAvatarForClips()
        {
            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(GoraniModel))
            {
                if (asset is Avatar found) avatar = found;
            }
            if (avatar == null)
            {
                Debug.LogError("[ModelPrefabSetup] 고라니 아바타를 찾지 못했습니다: " + GoraniModel);
                return;
            }

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { GoraniClipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                if (importer.animationType == ModelImporterAnimationType.Human
                    && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther && importer.sourceAvatar == avatar) continue;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.SaveAndReimport();
                changed++;
            }
            Debug.Log($"[ModelPrefabSetup] 동작 FBX 아바타를 고라니로: {changed}개 바꿈");
        }
    }
}
