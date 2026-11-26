using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 폐허 도시 소품 프리팹을 만든다(메뉴 Tools/Game/Setup City Props). 모델은 VARCO → Blender에서 다듬은 FBX(Art/City/{id}, 1 = 1 m, Y 위, 피벗 바닥 가운데, 정면 -Z).
    /// 텍스처 가져오기 설정 · 금속/매끄러움 맵 · 차폐 맵 · 머티리얼(URP Lit) · 모델 가져오기 설정 · 프리팹(BoxCollider · 정적 · 큰 건물은 LODGroup 컬링)
    /// · 바닥 지형 레이어 4개를 매번 같은 결과로 세운다. 월드 배치는 하지 않는다.
    /// 새 소품 = Models 표 한 줄 + 메뉴 다시 실행. 새 바닥 = Grounds 표 한 줄.
    /// </summary>
    public static class CitySetup
    {
        public const string PrefabFolder = "Assets/Game/Prefabs/World/City";
        private const string ArtFolder = "Assets/Game/Art/City";
        private const string GroundFolder = ArtFolder + "/Ground";
        private const int DetailSize = 2048;
        private const int OcclusionSize = 1024; // 차폐는 대부분 1이고 틈만 어두워 작게
        private const float SmoothnessScale = 1f; // 맵 알파(1 - 거칠기) × 이 값. 알파가 실제로 들어가므로 1 (나무는 알파가 없어 0.4로 눌렀음)
        private const float CullScreenHeight = 0.03f; // 화면 높이 3% 아래면 그리지 않음 — 상가(7 m) 약 320 m(안개 끝) · 아파트(16 m) 약 730 m

        public enum ColliderKind
        {
            /// <summary>정점 경계 그대로의 상자.</summary>
            Box,
            /// <summary>잔해 더미: 아래 25%의 흩어진 조각을 뺀 몸통 둘레, 높이 60%의 낮은 상자.</summary>
            LowBox,
        }

        /// <summary>id(프리팹 City_{id}, 폴더 Art/City/{id}), 충돌체, 멀면 그리지 않기(큰 건물).</summary>
        private static readonly (string id, ColliderKind collider, bool cull)[] Models =
        {
            ("Shop", ColliderKind.Box, true),
            ("Apartment", ColliderKind.Box, true),
            ("Shelter", ColliderKind.Box, true),
            ("Car", ColliderKind.Box, false),
            ("Barrier", ColliderKind.Box, false),
            ("Barricade", ColliderKind.Box, false),
            ("Debris", ColliderKind.LowBox, false),
        };

        /// <summary>지형 레이어 이름(텍스처 City_{name}.png), 타일 크기 m, 매끄러움(상수).</summary>
        private static readonly (string name, float tile, float smoothness)[] Grounds =
        {
            ("Asphalt", 10f, 0.1f), // 6 m이면 노란 선이 격자처럼 되풀이되어 보여 10 m(3-8 ④) + 월드가 낡은 얼룩으로 덮음(roadWear)
            ("Sidewalk", 4f, 0.05f), // 보도블록 8 × 8장 → 한 장 50 cm
            ("RubbleConcrete", 5f, 0.05f),
            ("WeedyPavement", 5f, 0.05f),
        };

        [MenuItem("Tools/Game/Setup City Props")]
        public static void Run()
        {
            EnsureFolder("Assets/Game/Prefabs", "World");
            EnsureFolder("Assets/Game/Prefabs/World", "City");
            int built = 0;
            foreach (var model in Models) if (BuildProp(model.id, model.collider, model.cull)) built++;
            foreach (var ground in Grounds) GroundLayer(ground.name, ground.tile, ground.smoothness);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CitySetup] 도시 소품 프리팹 {built}/{Models.Length}개: {PrefabFolder}, 바닥 레이어 {Grounds.Length}개: {GroundFolder}");
        }

        public static string PrefabPath(string id) => $"{PrefabFolder}/City_{id}.prefab";

        public static string GroundLayerPath(string name) => $"{GroundFolder}/City_{name}.terrainlayer";

        private static bool BuildProp(string id, ColliderKind colliderKind, bool cull)
        {
            string folder = $"{ArtFolder}/{id}";
            string prefix = $"{folder}/City_{id}";
            string fbxPath = prefix + ".fbx";
            if (!ConfigureModel(fbxPath))
            {
                Debug.LogError($"[CitySetup] 모델이 없습니다: {fbxPath}");
                return false;
            }

            Texture2D baseColor = ConfigureTexture(prefix + "_BaseColor.png", srgb: true, DetailSize, normal: false, keepAlpha: false);
            Texture2D normal = ConfigureTexture(prefix + "_Normal.png", srgb: false, DetailSize, normal: true, keepAlpha: false);
            // ORM 원본은 아래 두 맵을 만드는 데만 쓴다(재질에는 안 들어간다) — 작게 가져온다
            if (ConfigureTexture(prefix + "_ORM.png", srgb: false, 256, normal: false, keepAlpha: false) == null) return false;
            Texture2D metallicSmooth = DerivedTexture(prefix + "_ORM.png", prefix + "_MetallicSmoothness.png", DetailSize, keepAlpha: true,
                p => new Color32(p.b, p.b, p.b, (byte)(255 - p.g)));
            // URP Lit 차폐는 G 채널을 읽는다 — ORM의 R(차폐)을 회색으로 옮긴 맵을 따로 만든다
            Texture2D occlusion = DerivedTexture(prefix + "_ORM.png", prefix + "_Occlusion.png", OcclusionSize, keepAlpha: false,
                p => new Color32(p.r, p.r, p.r, 255));
            if (baseColor == null || normal == null) return false;

            Material material = PropMaterial($"{prefix}.mat", baseColor, normal, metallicSmooth, occlusion);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = new GameObject($"City_{id}");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Model";

            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = new Bounds();
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                Bounds local = LocalVertexBounds(renderer.GetComponent<MeshFilter>().sharedMesh, renderer.transform, root.transform, 0f);
                if (hasBounds) bounds.Encapsulate(local); else bounds = local;
                hasBounds = true;
            }

            var collider = root.AddComponent<BoxCollider>();
            if (colliderKind == ColliderKind.LowBox && renderers.Length > 0)
            {
                float height = bounds.size.y;
                Bounds body = LocalVertexBounds(renderers[0].GetComponent<MeshFilter>().sharedMesh, renderers[0].transform, root.transform, bounds.min.y + height * 0.25f);
                float top = bounds.min.y + height * 0.6f;
                collider.center = new Vector3(body.center.x, (bounds.min.y + top) * 0.5f, body.center.z);
                collider.size = new Vector3(body.size.x, top - bounds.min.y, body.size.z);
            }
            else
            {
                collider.center = bounds.center;
                collider.size = bounds.size;
            }

            if (cull)
            {
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(CullScreenHeight, renderers) });
                lod.RecalculateBounds();
            }

            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

            Vector3 colliderSize = collider.size;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(id));
            Object.DestroyImmediate(root);
            Debug.Log($"[CitySetup] City_{id}: 크기 {bounds.size.x:0.00} × {bounds.size.y:0.00} × {bounds.size.z:0.00} m, 충돌 {colliderKind} {colliderSize.x:0.00} × {colliderSize.y:0.00} × {colliderSize.z:0.00}{(cull ? ", 컬링" : "")}");
            return true;
        }

        /// <summary>root 기준 정점 경계. minY보다 낮은 정점은 뺀다.</summary>
        private static Bounds LocalVertexBounds(Mesh mesh, Transform meshTransform, Transform root, float minY)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 p = root.InverseTransformPoint(meshTransform.TransformPoint(v));
                if (p.y < minY) continue;
                if (any) bounds.Encapsulate(p); else bounds = new Bounds(p, Vector3.zero);
                any = true;
            }
            return bounds;
        }

        // ---------- 모델 ----------

        /// <summary>정적 메시로: 크기 1(파일 단위), 뼈 · 애니메이션 · 카메라 · 조명 · 재질(프리팹에서 넣음) · 충돌체 없음, Read/Write 끔.</summary>
        private static bool ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;
            bool changed = importer.globalScale != 1f || !importer.useFileScale || importer.bakeAxisConversion
                || importer.animationType != ModelImporterAnimationType.None || importer.importAnimation || importer.importCameras
                || importer.importLights || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.isReadable
                || importer.addCollider || importer.importBlendShapes;
            if (changed)
            {
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.bakeAxisConversion = false; // Blender에서 Y 위 · 회전 0으로 내보냄
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

        // ---------- 텍스처 ----------

        private static Texture2D ConfigureTexture(string path, bool srgb, int maxSize, bool normal, bool keepAlpha, bool repeat = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[CitySetup] 텍스처가 없습니다: {path}");
                return null;
            }
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            TextureImporterAlphaSource alpha = keepAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            TextureWrapMode wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != maxSize
                || !importer.mipmapEnabled || importer.isReadable || importer.alphaSource != alpha || importer.alphaIsTransparency
                || importer.wrapMode != wrap;
            if (repeat) changed |= importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 4;
            if (changed)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = maxSize;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.alphaSource = alpha;
                importer.alphaIsTransparency = false;
                importer.wrapMode = wrap;
                if (repeat)
                {
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                }
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>ORM 원본 PNG에서 픽셀마다 바꾼 맵을 만든다. 내용이 같으면 파일을 다시 쓰지 않는다.</summary>
        private static Texture2D DerivedTexture(string sourcePath, string path, int maxSize, bool keepAlpha, System.Func<Color32, Color32> convert)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(sourcePath));
            Color32[] pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = convert(pixels[i]);
            var result = new Texture2D(source.width, source.height, keepAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, linear: true);
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
            return ConfigureTexture(path, srgb: false, maxSize, normal: false, keepAlpha);
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // ---------- 머티리얼 ----------

        private static Material PropMaterial(string path, Texture2D baseColor, Texture2D normal, Texture2D metallicSmooth, Texture2D occlusion)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_MainTex", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", metallicSmooth);
            material.SetFloat("_Metallic", 1f); // 맵이 있으면 쓰이지 않음
            material.SetFloat("_Smoothness", SmoothnessScale);
            material.SetFloat("_SmoothnessTextureChannel", 0f); // 금속 맵의 알파
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.enableInstancing = true; // 같은 소품 여러 개
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- 바닥 ----------

        /// <summary>지형 레이어를 만들거나 매번 모든 값을 덮어쓴다. 매끄러움은 상수(텍스처 알파에서 읽으면 1이 되어 하늘이 비친다 — WorldSetup과 같은 이유).</summary>
        private static TerrainLayer GroundLayer(string name, float tile, float smoothness)
        {
            Texture2D texture = ConfigureTexture($"{GroundFolder}/City_{name}.png", srgb: true, 2048, normal: false, keepAlpha: false, repeat: true);
            string layerPath = GroundLayerPath(name);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = texture;
            layer.normalMapTexture = null;
            layer.maskMapTexture = null;
            layer.tileSize = new Vector2(tile, tile);
            layer.tileOffset = Vector2.zero;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = Vector4.one;
            layer.smoothnessSource = TerrainLayerSmoothnessSource.Constant;
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            layer.specular = Color.black;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
