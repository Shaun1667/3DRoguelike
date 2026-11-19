using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 마을 집 VARCO 모델(Art/Village/Houses)의 가져오기 설정 · 재질. <see cref="VillageSetup"/>이 집을 세울 때 부른다. 여러 번 불러도 결과가 같다.
    /// 텍스처 · 재질 처리는 주민(<see cref="VillagerSetup"/>)과 같은 방식: 알베도 sRGB, 노멀 맵, ORM(G 거칠기 · B 금속)을 URP 금속 맵
    /// (R 금속 × houseMetallicScale · A 매끄러움 = (1 - 거칠기) × houseSmoothnessScale)으로 바꿔 Houses/Materials/House_{id}.mat.
    /// 모델은 뼈 · 애니메이션 · 재질 없이 정적 메시로 가져오고(재질은 VillageSetup이 넣는다), 루트의 X 회전(Blender Z up → Y up)은 그대로 둔다.
    /// </summary>
    public static class VillageHouseArt
    {
        public const string Folder = "Assets/Game/Art/Village/Houses";
        private const string MaterialFolder = Folder + "/Materials";

        public static string ModelPath(VillageTuning.HouseModel model) => $"{Folder}/{model.modelFile}.fbx";

        /// <summary>모델 가져오기 설정을 맞추고 모델 에셋을 돌려준다. 없으면 null.</summary>
        public static GameObject LoadModel(VillageTuning.HouseModel model)
        {
            string path = ModelPath(model);
            if (!ConfigureStaticModel(path)) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>정적 메시로 가져오기: 뼈 · 애니메이션 · 카메라 · 조명 · 재질 · 충돌체 없음. 모델이 없으면 false. 숲 적 모델(<see cref="EnemySetup"/>)도 쓴다.</summary>
        internal static bool ConfigureStaticModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;
            bool changed = importer.animationType != ModelImporterAnimationType.None || importer.importAnimation || importer.importCameras
                || importer.importLights || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.isReadable
                || importer.addCollider || importer.importBlendShapes;
            if (changed)
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.isReadable = false;
                importer.addCollider = false;
                importer.importBlendShapes = false;
                importer.SaveAndReimport();
            }
            return true;
        }

        /// <summary>집 재질(URP Lit, 알베도 · 노멀 · 금속/매끄러움). 텍스처가 없으면 null.</summary>
        public static Material LoadMaterial(VillageTuning village, VillageTuning.HouseModel model)
        {
            string textureFolder = $"{Folder}/{model.modelFile}_tex";
            string albedoPath = $"{textureFolder}/{model.modelFile}_0_Image_0.png";
            string ormPath = $"{textureFolder}/{model.modelFile}_1_Image_1.png";
            string normalPath = $"{textureFolder}/{model.modelFile}_2_Image_2.png";
            if (AssetImporter.GetAtPath(albedoPath) == null || AssetImporter.GetAtPath(ormPath) == null || AssetImporter.GetAtPath(normalPath) == null) return null;
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(Folder, "Materials");

            Texture2D albedo = ConfigureTexture(albedoPath, srgb: true, village.houseAlbedoSize, normal: false);
            Texture2D normalMap = ConfigureTexture(normalPath, srgb: false, village.houseDetailSize, normal: true);
            // ORM 원본은 금속 맵을 만드는 데만 쓴다(재질에는 안 들어간다) — 작게 가져온다
            ConfigureTexture(ormPath, srgb: false, 256, normal: false);
            Texture2D metallic = MetallicSmoothness(ormPath, $"{textureFolder}/{model.modelFile}_MetallicSmoothness.png",
                village.houseMetallicScale, village.houseSmoothnessScale, village.houseDetailSize);

            string path = $"{MaterialFolder}/House_{model.id}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat("_Smoothness", 1f); // 맵의 알파 × 이 값
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.enableInstancing = true; // 같은 집 여러 채
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static Texture2D ConfigureTexture(string path, bool srgb, int maxSize, bool normal, bool keepAlpha = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            TextureImporterAlphaSource alpha = keepAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != maxSize
                || !importer.mipmapEnabled || importer.isReadable || importer.alphaSource != alpha
                || importer.wrapMode != TextureWrapMode.Clamp;
            if (changed)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = maxSize;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.alphaSource = alpha;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Texture2D MetallicSmoothness(string ormPath, string path, float metallicScale, float smoothnessScale, int maxSize)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(ormPath));
            Color32[] pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                byte metal = (byte)Mathf.RoundToInt(p.b * metallicScale);
                pixels[i] = new Color32(metal, metal, metal, (byte)Mathf.RoundToInt((255 - p.g) * smoothnessScale));
            }
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, linear: true);
            result.SetPixels32(pixels);
            result.Apply();
            byte[] bytes = result.EncodeToPNG();
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
            if (!File.Exists(path) || !SameBytes(File.ReadAllBytes(path), bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path);
            }
            return ConfigureTexture(path, srgb: false, maxSize, normal: false, keepAlpha: true);
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
