using System.Collections.Generic;
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
        /// 고라니 동작 FBX가 고라니의 기본 자세(T자세)를 기준으로 아바타를 만들게 한다(2026-11-10).
        /// Mixamo에서 뼈대만 받은 동작 FBX는 각자 첫 프레임 자세로 아바타를 만들어, 그 자세가 기준이 되면서 왼발 끝이 약 25도 들려 보였다.
        /// 고라니 아바타를 그대로 복사(CopyFromOther)할 수는 없다 — 동작 FBX에는 Armature 노드가 없어 뼈대 구조가 달라 동작을 못 가져온다.
        /// 그래서 동작 FBX 자신의 뼈대 구조 · 위치는 두고, mixamorig 뼈의 기본 회전만 고라니 값으로 바꿔 아바타를 만든다.
        /// 이미 맞으면 다시 가져오지 않는다.
        /// </summary>
        private static void UseGoraniAvatarForClips() => UseGoraniAvatarForClips(GoraniClipFolder);

        /// <summary>그 폴더의 Mixamo 동작 FBX(뼈대만, mixamorig 이름)를 고라니 동작과 같은 방식으로 Humanoid로 가져온다(적 E 개체 동작: EnemyAnimationSetup). 바꾼 파일 수를 돌려준다.
        /// subfolders가 false면 그 폴더 바로 아래 파일만(아래 폴더에 다른 뼈대의 동작이 있을 때 — 네 발 Mutant 동작은 Generic).</summary>
        internal static int UseGoraniAvatarForClips(string clipFolder, bool subfolders = true)
        {
            var goraniImporter = AssetImporter.GetAtPath(GoraniModel) as ModelImporter;
            if (goraniImporter == null || goraniImporter.humanDescription.human == null || goraniImporter.humanDescription.human.Length == 0)
            {
                Debug.LogError("[ModelPrefabSetup] 고라니 휴머노이드 설정을 찾지 못했습니다: " + GoraniModel);
                return 0;
            }
            HumanDescription source = goraniImporter.humanDescription;
            var rest = new Dictionary<string, SkeletonBone>();
            foreach (SkeletonBone bone in source.skeleton) rest[bone.name] = bone;
            Quaternion hipsWorld = RestWorldRotation(source.skeleton, "mixamorig:Hips");

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { clipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!subfolders && System.IO.Path.GetDirectoryName(path).Replace('\\', '/') != clipFolder) continue;
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                var skeleton = new List<SkeletonBone>();
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    bool root = t == model.transform;
                    var bone = new SkeletonBone
                    {
                        name = root ? model.name + "(Clone)" : t.name,
                        position = t.localPosition,
                        rotation = t.localRotation,
                        scale = t.localScale,
                    };
                    if (t.name == "mixamorig:Hips")
                    {
                        Quaternion parentWorld = Quaternion.identity;
                        for (Transform p = t.parent; p != null && p != model.transform; p = p.parent) parentWorld = p.localRotation * parentWorld;
                        bone.rotation = Quaternion.Inverse(parentWorld) * hipsWorld;
                    }
                    else if (!root && rest.TryGetValue(t.name, out SkeletonBone goraniBone))
                    {
                        bone.rotation = goraniBone.rotation;
                    }
                    skeleton.Add(bone);
                }

                HumanDescription description = source;
                description.skeleton = skeleton.ToArray();
                if (importer.animationType == ModelImporterAnimationType.Human
                    && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
                    && SameSkeleton(importer.humanDescription.skeleton, description.skeleton)) continue;

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.humanDescription = description;
                importer.SaveAndReimport();
                changed++;
            }
            Debug.Log($"[ModelPrefabSetup] 동작 FBX 아바타 기준 자세를 고라니로({clipFolder}): {changed}개 바꿈");
            return changed;
        }

        /// <summary>뼈대 목록에서 이름이 같은 뼈의 기본 자세 월드 회전(부모 이름을 따라 곱한다).</summary>
        private static Quaternion RestWorldRotation(SkeletonBone[] skeleton, string name)
        {
            var byName = new Dictionary<string, SkeletonBone>();
            foreach (SkeletonBone bone in skeleton) byName[bone.name] = bone;
            var parentOf = ParentNames(skeleton);
            Quaternion world = Quaternion.identity;
            string current = name;
            while (byName.TryGetValue(current, out SkeletonBone bone))
            {
                world = bone.rotation * world;
                if (!parentOf.TryGetValue(current, out current)) break;
            }
            return world;
        }

        /// <summary>SkeletonBone에는 부모 이름이 공개돼 있지 않아 고라니 모델의 실제 계층에서 읽는다.</summary>
        private static Dictionary<string, string> ParentNames(SkeletonBone[] skeleton)
        {
            var parents = new Dictionary<string, string>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GoraniModel);
            if (model == null) return parents;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model.transform) continue;
                parents[t.name] = t.parent == model.transform ? model.name + "(Clone)" : t.parent.name;
            }
            return parents;
        }

        private static bool SameSkeleton(SkeletonBone[] a, SkeletonBone[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].name != b[i].name || Quaternion.Angle(a[i].rotation, b[i].rotation) > 0.01f
                    || (a[i].position - b[i].position).sqrMagnitude > 1e-12f) return false;
            }
            return true;
        }
    }
}
