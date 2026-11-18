using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 고라니 머리(목 위)에 털을 입힌다(FurSettings.includeHands를 켜면 양손에도)(셸 방식, 2026-11-18). 여러 번 실행해도 결과가 같다.
    /// 1. 털 메시: 고라니 메시에서 Head · HeadTop_End(1), Neck(neckWeight), (includeHands일 때) 양손 · 손가락 뼈(1)의 가중치 합을 털 덮임 마스크로 삼아,
    ///    마스크가 있는 삼각형만 뽑아 겹 수만큼 복사한다(TEXCOORD1 = 겹 높이, 길이 비율(손은 handLength), 덮임). 뼈 가중치 · bindpose는 원본 그대로.
    /// 2. 털 재질: Game/Fur Shell 셰이더 + 고라니 알베도. 값은 FurSettings(Assets/Game/Data/GoraniFur.asset).
    /// 3. Player.prefab의 Visual 아래 "Fur" 스킨드 렌더러: 고라니 몸과 같은 뼈를 써서 스키닝을 그대로 따라간다.
    /// 보통 · Mutant 모습은 같은 메시 · 재질에 컨트롤러만 다르므로(PlayerForm) 둘 다 이 털을 쓴다.
    /// 수치 에셋은 없을 때만 만들고 값을 덮어쓰지 않는다. 겹 수 · 목 비중을 바꾸면 이 메뉴를 다시 실행한다.
    /// </summary>
    public static class GoraniFurSetup
    {
        public const string SettingsPath = "Assets/Game/Data/GoraniFur.asset";
        private const string FurFolder = "Assets/Game/Art/Gorani/Fur";
        private const string MeshPath = FurFolder + "/GoraniFur_Mesh.asset";
        private const string MaterialPath = FurFolder + "/GoraniFur_Mat.mat";
        private const string ShaderName = "Game/Fur Shell";
        private const string GoraniModelPath = "Assets/Game/Art/Gorani/Gorani.fbx";
        private const string GoraniMaterialPath = "Assets/Game/Art/Gorani/Gorani_Mat.mat";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player.prefab";
        private const string FurObjectName = "Fur";

        /// <summary>마스크가 이보다 작은 정점만으로 된 삼각형은 털 메시에 넣지 않는다.</summary>
        private const float MinMask = 0.02f;

        [MenuItem("Tools/Game/Setup Gorani Fur")]
        public static void Run()
        {
            BuildAssets();
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    Debug.LogError("[GoraniFurSetup] Player.prefab에 Visual이 없습니다. Tools/Game/Setup Test Room을 먼저 실행하세요.");
                    return;
                }
                AddTo(visual.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[GoraniFurSetup] Player.prefab에 털 적용");
        }

        /// <summary>설정 · 재질 · 털 메시를 만들거나 다시 굽는다.</summary>
        public static void BuildAssets()
        {
            if (!AssetDatabase.IsValidFolder(FurFolder)) AssetDatabase.CreateFolder("Assets/Game/Art/Gorani", "Fur");
            FurSettings settings = LoadOrCreateSettings();
            Material material = LoadOrCreateMaterial(settings);
            BuildMesh(settings);
            settings.material = material;
            settings.ApplyTo(material);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        /// <summary>고라니 모습(Visual) 아래에 털 렌더러를 둔다. 있던 것은 지우고 다시 만든다. BuildAssets를 먼저 불러야 한다.</summary>
        public static void AddTo(GameObject visual)
        {
            SkinnedMeshRenderer body = FindBody(visual);
            if (body == null)
            {
                Debug.LogError("[GoraniFurSetup] 고라니 몸 스킨드 렌더러를 찾지 못했습니다: " + visual.name);
                return;
            }
            Transform old = visual.transform.Find(FurObjectName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mesh == null || material == null)
            {
                Debug.LogError("[GoraniFurSetup] 털 메시 · 재질이 없습니다. BuildAssets를 먼저 실행하세요.");
                return;
            }

            var fur = new GameObject(FurObjectName);
            fur.layer = body.gameObject.layer;
            fur.transform.SetParent(visual.transform, false);
            fur.transform.localPosition = body.transform.localPosition;
            fur.transform.localRotation = body.transform.localRotation;
            fur.transform.localScale = body.transform.localScale;

            var renderer = fur.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = body.bones;
            renderer.rootBone = body.rootBone;
            renderer.sharedMaterial = material;
            renderer.localBounds = body.localBounds;
            renderer.quality = body.quality;
            renderer.updateWhenOffscreen = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // 털 그림자는 몸이 이미 드리운다
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = body.lightProbeUsage;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.skinnedMotionVectors = false;
        }

        private static SkinnedMeshRenderer FindBody(GameObject visual)
        {
            foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.gameObject.name != FurObjectName) return renderer;
            }
            return null;
        }

        private static FurSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FurSettings>(SettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<FurSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static Material LoadOrCreateMaterial(FurSettings settings)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new System.InvalidOperationException("털 셰이더를 찾지 못했습니다: " + ShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            var gorani = AssetDatabase.LoadAssetAtPath<Material>(GoraniMaterialPath);
            if (gorani != null)
            {
                material.SetTexture("_BaseMap", gorani.GetTexture("_BaseMap"));
                material.SetTextureScale("_BaseMap", gorani.GetTextureScale("_BaseMap"));
                material.SetTextureOffset("_BaseMap", gorani.GetTextureOffset("_BaseMap"));
            }
            material.SetColor("_EmissionColor", Color.black); // HitFlash가 프로퍼티 블록으로 흰색을 넣는다
            material.enableInstancing = false;
            return material;
        }

        /// <summary>뼈 이름 → (털 길이 비중, 털 덮임 비중). 없는 뼈는 0.</summary>
        private static Vector2 BoneFur(string boneName, FurSettings settings)
        {
            string name = boneName.Replace("mixamorig:", "");
            if (name == "Head" || name == "HeadTop_End") return Vector2.one;
            if (name == "Neck") return new Vector2(settings.neckWeight, settings.neckWeight);
            if (settings.includeHands && (name.StartsWith("LeftHand") || name.StartsWith("RightHand"))) return new Vector2(settings.handLength, 1f); // 손목 · 손가락
            return Vector2.zero;
        }

        private static void BuildMesh(FurSettings settings)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GoraniModelPath);
            SkinnedMeshRenderer body = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (body == null) throw new System.InvalidOperationException("고라니 모델을 찾지 못했습니다: " + GoraniModelPath);
            Mesh source = body.sharedMesh;

            var boneFur = new Vector2[body.bones.Length];
            for (int i = 0; i < boneFur.Length; i++) boneFur[i] = BoneFur(body.bones[i].name, settings);

            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            BoneWeight[] weights = source.boneWeights;
            int[] triangles = source.triangles;

            var mask = new float[positions.Length];   // 덮임(가닥 수): 이 값으로 털 범위를 정한다
            var lengths = new float[positions.Length]; // 길이 비율
            for (int i = 0; i < positions.Length; i++)
            {
                BoneWeight w = weights[i];
                Vector2 m = boneFur[w.boneIndex0] * w.weight0 + boneFur[w.boneIndex1] * w.weight1
                          + boneFur[w.boneIndex2] * w.weight2 + boneFur[w.boneIndex3] * w.weight3;
                lengths[i] = Mathf.Clamp01(m.x);
                mask[i] = Mathf.Clamp01(m.y);
            }

            // UV · 노멀 이음매에서 셸이 갈라지지 않도록, 같은 자리 정점의 노멀을 평균해 민다
            Vector3[] smooth = SmoothNormals(positions, normals);

            var furTriangles = new List<int>();
            var remap = new Dictionary<int, int>();
            var used = new List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (Mathf.Max(mask[a], Mathf.Max(mask[b], mask[c])) < MinMask) continue;
                furTriangles.Add(Remap(a, remap, used));
                furTriangles.Add(Remap(b, remap, used));
                furTriangles.Add(Remap(c, remap, used));
            }

            int shells = Mathf.Max(1, settings.shellCount);
            int count = used.Count;
            var outPositions = new Vector3[count * shells];
            var outNormals = new Vector3[count * shells];
            var outUv = new Vector2[count * shells];
            var outShell = new Vector3[count * shells];
            var outWeights = new BoneWeight[count * shells];
            var outTriangles = new int[furTriangles.Count * shells];
            for (int s = 0; s < shells; s++)
            {
                float h = (s + 1f) / shells; // 0은 몸 표면이라 빼고 1/N부터
                int offset = s * count;
                for (int i = 0; i < count; i++)
                {
                    int src = used[i];
                    outPositions[offset + i] = positions[src];
                    outNormals[offset + i] = smooth[src];
                    outUv[offset + i] = uvs[src];
                    outShell[offset + i] = new Vector3(h, lengths[src], mask[src]);
                    outWeights[offset + i] = weights[src];
                }
                int triOffset = s * furTriangles.Count;
                for (int i = 0; i < furTriangles.Count; i++) outTriangles[triOffset + i] = furTriangles[i] + offset;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "GoraniFur_Mesh";
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = outPositions;
            mesh.normals = outNormals;
            mesh.uv = outUv;
            mesh.SetUVs(1, outShell); // TEXCOORD1 = (겹 높이, 길이 비율, 덮임)
            mesh.boneWeights = outWeights;
            mesh.bindposes = source.bindposes;
            mesh.triangles = outTriangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            if (created) AssetDatabase.CreateAsset(mesh, MeshPath);
            else EditorUtility.SetDirty(mesh);
            Debug.Log($"[GoraniFurSetup] 털 메시: 털 범위 정점 {count} · 삼각형 {furTriangles.Count / 3} × {shells}겹 = 정점 {outPositions.Length} · 삼각형 {outTriangles.Length / 3}");
        }

        private static int Remap(int index, Dictionary<int, int> remap, List<int> used)
        {
            if (remap.TryGetValue(index, out int mapped)) return mapped;
            mapped = used.Count;
            remap[index] = mapped;
            used.Add(index);
            return mapped;
        }

        private static Vector3[] SmoothNormals(Vector3[] positions, Vector3[] normals)
        {
            var sums = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i] * 1e6f; // 메시 단위가 작아(고라니는 175배로 키워 쓴다) 넉넉히 키워 묶는다
                var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                keys[i] = key;
                sums.TryGetValue(key, out Vector3 sum);
                sums[key] = sum + normals[i];
            }
            var result = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 n = sums[keys[i]];
                result[i] = n.sqrMagnitude > 1e-12f ? n.normalized : normals[i];
            }
            return result;
        }
    }
}
