using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 숲 나무 프리팹을 만든다(메뉴 Tools/Game/Setup Trees, Setup World도 부른다). 모델은 VARCO 줄기 + 잎 카드 FBX(Art/Trees).
    /// 텍스처 가져오기 설정 · 머티리얼(URP Lit) · 모델 가져오기 설정 · 프리팹(줄기 캡슐 충돌 + LODGroup)을 매번 같은 결과로 세운다.
    /// 새 나무 = Models 표 한 줄 + WorldTuning.treeKinds 한 줄 + 메뉴 다시 실행.
    /// </summary>
    public static class TreeSetup
    {
        public const string PrefabFolder = "Assets/Game/Prefabs/World";
        private const string ArtFolder = "Assets/Game/Art/Trees";
        private const string MaterialFolder = ArtFolder + "/Materials";
        private const float LeafCutoff = 0.45f;
        private const float CullScreenHeight = 0.05f; // 화면 높이의 5% 아래로 작아지면 그리지 않음 — 시야각 40°에서 약 230~350 m(안개 끝 320 m)
        private const float ColliderHeight = 4f;

        /// <summary>id(프리팹 Tree_{id}), FBX 이름, FBX 안 줄기 머티리얼 이름, 잎 머티리얼 이름 두 개.</summary>
        private static readonly (string id, string trunkMaterial, string leafMaterial1, string leafMaterial2)[] Models =
        {
            ("Pine", "material_0", "M_leafpine_1_alpha", "M_leafpine_2_alpha"),
            ("Oak", "material_0.001", "M_leafoak_1_alpha", "M_leafoak_2_alpha"),
            ("Mutant", "material_0.002", "M_leafmut_1_alpha", "M_leafmut_2_alpha"),
        };

        [MenuItem("Tools/Game/Setup Trees")]
        public static void Run()
        {
            EnsureFolder("Assets/Game/Prefabs", "World");
            EnsureFolder(ArtFolder, "Materials");
            foreach (var model in Models) BuildTree(model.id, model.trunkMaterial, model.leafMaterial1, model.leafMaterial2);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TreeSetup] 나무 프리팹 {Models.Length}개: {PrefabFolder}");
        }

        public static string PrefabPath(string id) => $"{PrefabFolder}/Tree_{id}.prefab";

        private static void BuildTree(string id, string trunkSource, string leafSource1, string leafSource2)
        {
            string fbxPath = $"{ArtFolder}/Tree_{id}.fbx";
            Texture2D baseColor = Texture($"{id}_BaseColor", srgb: true, 2048, normal: false, leaf: false);
            Texture2D normal = Texture($"{id}_Normal", srgb: false, 2048, normal: true, leaf: false);
            Texture($"{id}_ORM", srgb: false, 2048, normal: false, leaf: false);
            Texture2D metallicSmooth = MetallicSmoothness(id);
            Texture2D leaf1 = Texture($"{id}_Leaf1", srgb: true, 1024, normal: false, leaf: true);
            Texture2D leaf2 = Texture($"{id}_Leaf2", srgb: true, 1024, normal: false, leaf: true);

            Material trunk = TrunkMaterial($"{id}_Trunk", baseColor, normal, metallicSmooth);
            Material leaves1 = LeafMaterial($"{id}_Leaf1", leaf1);
            Material leaves2 = LeafMaterial($"{id}_Leaf2", leaf2);

            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.bakeAxisConversion = true; // Blender -Z forward · Y up → 뿌리 회전 없이
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), trunkSource), trunk);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), leafSource1), leaves1);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), leafSource2), leaves2);
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = new GameObject($"Tree_{id}");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Model";

            var renderers = new List<Renderer>();
            Mesh trunkMesh = null;
            Transform trunkTransform = null;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderers.Add(renderer);
                if (!renderer.name.EndsWith("_Leaves"))
                {
                    trunkMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    trunkTransform = renderer.transform;
                }
            }

            // 줄기 충돌: 밑동 둘레 정점으로 가운데 · 반지름을 잰다(잎에는 충돌 없음). NavMesh는 물리 충돌로 구워 나무 자리가 구멍이 된다
            var collider = root.AddComponent<CapsuleCollider>();
            MeasureTrunk(trunkMesh, trunkTransform, root.transform, out Vector3 center, out float radius);
            collider.radius = radius;
            collider.height = ColliderHeight;
            collider.center = new Vector3(center.x, ColliderHeight * 0.5f, center.z);

            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(CullScreenHeight, renderers.ToArray()) });
            lod.RecalculateBounds();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(id));
            Object.DestroyImmediate(root);
            Debug.Log($"[TreeSetup] Tree_{id}: 줄기 반지름 {radius:0.00} m, 가운데 ({center.x:0.00}, {center.z:0.00})");
        }

        private static void MeasureTrunk(Mesh mesh, Transform meshTransform, Transform root, out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0.35f;
            if (mesh == null) return;
            Vector3[] vertices = mesh.vertices;
            var low = new List<Vector3>();
            foreach (Vector3 v in vertices)
            {
                Vector3 p = root.InverseTransformPoint(meshTransform.TransformPoint(v));
                if (p.y > 0.6f && p.y < 1.8f) low.Add(p); // 뿌리 퍼짐 위 · 사람 키 높이
            }
            if (low.Count < 8) return;
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in low) sum += p;
            center = sum / low.Count;
            var distances = new List<float>();
            foreach (Vector3 p in low) distances.Add(new Vector2(p.x - center.x, p.z - center.z).magnitude);
            distances.Sort();
            radius = Mathf.Clamp(distances[distances.Count / 2], 0.2f, 0.9f); // 가운데값 — 줄기 표면 정점이라 반지름에 가깝다
        }

        // ---------- 텍스처 ----------

        private static Texture2D Texture(string name, bool srgb, int maxSize, bool normal, bool leaf)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"[TreeSetup] 텍스처가 없습니다: {path}");
                return null;
            }
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = leaf;
            importer.alphaSource = leaf ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.mipMapsPreserveCoverage = leaf; // 멀리서 잎이 얇아지지 않게
            importer.alphaTestReferenceValue = LeafCutoff;
            importer.wrapMode = leaf ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>VARCO ORM(R 차폐 · G 거칠기 · B 금속)을 URP 금속 맵(R 금속 · A 매끄러움 = 1 - 거칠기)으로. 없을 때만 만든다.</summary>
        private static Texture2D MetallicSmoothness(string id)
        {
            string path = $"{ArtFolder}/{id}_MetallicSmoothness.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                var source = new Texture2D(2, 2);
                source.LoadImage(System.IO.File.ReadAllBytes($"{ArtFolder}/{id}_ORM.png"));
                Color32[] pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    pixels[i] = new Color32(p.b, p.b, p.b, (byte)(255 - p.g));
                }
                var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, linear: true);
                result.SetPixels32(pixels);
                result.Apply();
                System.IO.File.WriteAllBytes(path, result.EncodeToPNG());
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(result);
                AssetDatabase.ImportAsset(path);
            }
            return Texture($"{id}_MetallicSmoothness", srgb: false, 2048, normal: false, leaf: false);
        }

        // ---------- 머티리얼 ----------

        private static Material LoadOrCreate(string name)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material TrunkMaterial(string name, Texture2D baseColor, Texture2D normal, Texture2D metallicSmooth)
        {
            Material material = LoadOrCreate(name);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_MainTex", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", metallicSmooth);
            material.SetFloat("_Smoothness", 1f); // 맵의 알파 × 이 값
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LeafMaterial(string name, Texture2D texture)
        {
            Material material = LoadOrCreate(name);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", LeafCutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off); // 앞뒤 모두
            material.SetFloat("_Surface", 0f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
