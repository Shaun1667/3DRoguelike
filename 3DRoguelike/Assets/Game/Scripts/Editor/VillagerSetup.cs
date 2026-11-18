using System.Collections.Generic;
using System.IO;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// E-07 마을 주민 프리팹을 만든다(메뉴 Tools/Game/Setup Villagers, Setup Village · Setup World도 부른다). 여러 번 실행해도 결과가 같다.
    /// 종마다(VillagerTuning.species): 텍스처 가져오기 설정 → URP Lit 재질(알베도 · 노멀 · 금속/매끄러움) → 리깅 FBX(Art/Villagers/Rigged, Mixamo)를
    /// Humanoid로 가져오기 → 꼬리 찾기 · 꼬리 뼈 → 털(셸) 메시 · 재질 → 프리팹 Prefabs/Villagers/Villager_{id}
    /// (Villager · 캡슐 충돌 · Model(Animator, 대기 동작) · Fur · LODGroup · 꼬리가 있으면 TailChain). 피벗은 발밑, 앞은 +Z.
    ///
    /// 모델: Mixamo가 내보낸 T-포즈는 메시 원래 자리(리깅 전 모양)와 조금 다르다(팔 · 다리를 T-포즈로 돌려 놓음). 키 · 발밑은 스키닝한 T-포즈로 재고,
    /// 털 범위는 리깅 전 모양(= 예전 정지 모델과 같은 모양)으로 정해서 정지 모델 때와 같은 범위가 나온다.
    /// 대기 동작: 컨트롤러 Art/Villagers/Villager.controller(고라니 Idle 클립 + 손가락 레이어 HandRelaxed). 고라니 쪽 에셋은 읽기만 한다.
    /// 털: 고라니 털과 같은 셰이더(Game/Fur Shell) · 같은 셸 방식(털 범위 삼각형을 겹 수만큼 복사, TEXCOORD1 = (겹 높이, 길이 비율, 덮임)).
    /// 몸과 같은 뼈 · 가중치 · bindpose를 복사한 SkinnedMeshRenderer라 스키닝을 그대로 따라간다(GoraniFurSetup과 같은 방식). 고라니 털 에셋은 건드리지 않는다.
    /// 꼬리: Mixamo는 꼬리 뼈를 만들지 않는다. 엉덩이 뼈에 붙여 본 메시에서 엉덩이 뒤(등보다 뒤)로 튀어나온 부분을 찾아(VillagerTuning.tailRig)
    /// 꼬리 정점으로 삼고, 그 가운데 선을 따라 Hips 아래 뼈 사슬(Tail1…, Tail_End)을 넣고, 꼬리 정점의 가중치를 그 사슬로 다시 나눈 몸 메시
    /// (Rigged/Meshes/Villager_{id}_Body.asset)를 만든다. 꼬리 뼈의 bindpose는 엉덩이 뼈와 같게 잡아, 꼬리가 리깅 전 모양 그대로 엉덩이를 따라간다.
    /// </summary>
    public static class VillagerSetup
    {
        public const string TuningPath = "Assets/Game/Data/VillagerTuning.asset";
        public const string FurBasePath = "Assets/Game/Data/VillagerFur.asset";
        public const string PrefabFolder = "Assets/Game/Prefabs/Villagers";
        private const string ArtFolder = "Assets/Game/Art/Villagers";
        private const string MaterialFolder = ArtFolder + "/Materials";
        private const string FurFolder = ArtFolder + "/Fur";
        private const string FurShaderName = "Game/Fur Shell";
        private const string RiggedFolder = ArtFolder + "/Rigged";
        private const string BodyMeshFolder = RiggedFolder + "/Meshes";
        private const string ControllerPath = ArtFolder + "/Villager.controller";
        // 고라니 쪽 에셋(읽기만): 대기 클립, 손가락 마스크 · 평소 손 포즈(HandPoseSetup이 만든 것)
        private const string IdleClipPath = "Assets/Game/Art/Gorani/Animation/Idle.fbx";
        private const string HandMaskPath = "Assets/Game/Art/Gorani/Animation/Hands/Fingers.mask";
        private const string HandClipPath = "Assets/Game/Art/Gorani/Animation/Hands/HandRelaxed.anim";
        private const string HandLayerName = "Hands";
        private const string HandStateName = "Pose";
        private const float MinMask = 0.02f;

        public static string PrefabPath(string id) => $"{PrefabFolder}/Villager_{id}.prefab";

        [MenuItem("Tools/Game/Setup Villagers")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Game/Prefabs", "Villagers");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(ArtFolder, "Materials");
            if (!AssetDatabase.IsValidFolder(FurFolder)) AssetDatabase.CreateFolder(ArtFolder, "Fur");
            if (!AssetDatabase.IsValidFolder(BodyMeshFolder)) AssetDatabase.CreateFolder(RiggedFolder, "Meshes");

            VillagerTuning tuning = LoadOrCreateTuning();
            FurSettings furBase = LoadOrCreateFurBase();
            RuntimeAnimatorController controller = BuildController();
            long furVertices = 0;
            int furCount = 0;
            int tailCount = 0;
            var report = new System.Text.StringBuilder();
            foreach (VillagerTuning.Species species in tuning.species)
            {
                Built built = BuildSpecies(species, tuning, furBase, controller);
                if (built.furVertices > 0)
                {
                    furVertices += built.furVertices;
                    furCount++;
                    report.Append($" · {species.id} {built.furVertices}");
                }
                if (built.tailBones > 0) tailCount++;
                Debug.Log($"[VillagerSetup] 꼬리 판정(키 비율) {built.tailNote}"); // 콘솔은 줄바꿈 뒤를 접어서 종마다 한 줄씩
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[VillagerSetup] 주민 프리팹 {tuning.species.Length}종: {PrefabFolder} · 털 {furCount}종, 털 메시 정점 합계 {furVertices} ({furBase.shellCount}겹, 스킨드){report}");
            Debug.Log($"[VillagerSetup] 꼬리 {tailCount}종 — 엉덩이 뒤로 가장 나온 곳이 등보다 {tuning.tailRig.minProtrusion:0.000}(키 비율) 넘게 뒤면 꼬리(위 줄들)");
        }

        public static VillagerTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<VillagerTuning>(TuningPath);
            if (tuning != null)
            {
                EditorUtility.SetDirty(tuning);
                return tuning;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<VillagerTuning>(), TuningPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<VillagerTuning>(TuningPath);
        }

        /// <summary>주민 털 공통 값. 처음 만들 때만 주민용으로 맞춘다(겹 8 — 주민 15명이 한 화면에 모여서 고라니 12겹보다 낮춘다).</summary>
        private static FurSettings LoadOrCreateFurBase()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FurSettings>(FurBasePath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<FurSettings>();
            settings.shellCount = 8;
            settings.thickness = 1.05f;
            settings.gravity = 0.2f;
            settings.windStrength = 0.1f;
            settings.occlusion = 0.4f;
            AssetDatabase.CreateAsset(settings, FurBasePath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private struct Built
        {
            public int furVertices;
            public int tailBones;
            public string tailNote;
        }

        private static Built BuildSpecies(VillagerTuning.Species species, VillagerTuning tuning, FurSettings furBase, RuntimeAnimatorController controller)
        {
            var built = new Built { tailNote = $"{species.id}: 만들지 못함" };
            string modelPath = $"{RiggedFolder}/{species.modelFile}.fbx";
            string textureFolder = $"{ArtFolder}/{species.modelFile}_tex";
            string albedoPath = $"{textureFolder}/{species.modelFile}_0_Image_0.png";
            string ormPath = $"{textureFolder}/{species.modelFile}_1_Image_1.png";
            string normalPath = $"{textureFolder}/{species.modelFile}_2_Image_2.png";
            if (AssetImporter.GetAtPath(modelPath) == null || AssetImporter.GetAtPath(albedoPath) == null)
            {
                Debug.LogError($"[VillagerSetup] {species.id}: 모델 · 텍스처가 없습니다: {modelPath}");
                return built;
            }

            Texture2D albedo = ConfigureTexture(albedoPath, srgb: true, tuning.albedoSize, normal: false);
            Texture2D normalMap = ConfigureTexture(normalPath, srgb: false, tuning.detailSize, normal: true);
            Texture2D metallic = MetallicSmoothness(ormPath, $"{textureFolder}/{species.modelFile}_MetallicSmoothness.png", tuning.metallicScale, tuning.smoothnessScale, tuning.detailSize);
            Material bodyMaterial = BodyMaterial($"{MaterialFolder}/Villager_{species.id}.mat", albedo, normalMap, metallic);

            ConfigureModel(modelPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            SkinnedMeshRenderer source = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (source == null || source.sharedMesh == null)
            {
                Debug.LogError($"[VillagerSetup] {species.id}: 스킨드 메시를 찾지 못했습니다: {modelPath}");
                return built;
            }
            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is Avatar found) avatar = found;
            }
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                Debug.LogError($"[VillagerSetup] {species.id}: Humanoid 아바타를 만들지 못했습니다(대기 동작이 나오지 않습니다): {modelPath}");

            Mesh mesh = source.sharedMesh;
            Vector3[] raw = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;
            Transform[] sourceBones = source.bones;
            // 모델 좌표 = FBX 뿌리 기준. 스키닝 행렬(뼈 × bindpose)로 지금 자세(T-포즈)의 정점 자리를 구한다
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            var skin = new Matrix4x4[bindposes.Length];
            for (int j = 0; j < skin.Length; j++) skin[j] = toModel * sourceBones[j].localToWorldMatrix * bindposes[j];
            Measure(Skinned(raw, weights, skin), out float bottom, out float top, out Vector3 feet);
            float scale = species.height / Mathf.Max(0.01f, top - bottom);
            Quaternion yaw = Quaternion.Euler(0f, tuning.modelYaw, 0f);

            // 꼬리: 엉덩이 뼈에 붙여 본 메시(리깅 전 모양이 엉덩이를 따라간 자리)에서 찾는다
            int hipsIndex = BoneIndex(sourceBones, "Hips");
            TailPlan tail = null;
            if (hipsIndex < 0) built.tailNote = $"{species.id}: Hips 뼈가 스킨에 없어 꼬리를 판정하지 못함";
            else tail = PlanTail(species, tuning, raw, mesh.triangles, skin[hipsIndex], bottom, top, feet, scale, out built.tailNote);
            Mesh bodyMesh = mesh;
            string bodyMeshPath = $"{BodyMeshFolder}/Villager_{species.id}_Body.asset";
            if (tail != null)
            {
                bodyMesh = BuildTailBody(bodyMeshPath, species, mesh, tail, tuning.tailRig, skin[hipsIndex], sourceBones[hipsIndex].lossyScale, ref weights, ref bindposes);
                built.tailBones = tail.joints.Length - 1;
            }
            else if (AssetDatabase.LoadAssetAtPath<Mesh>(bodyMeshPath) != null)
            {
                AssetDatabase.DeleteAsset(bodyMeshPath); // 꼬리 판정이 바뀌어 남은 메시
            }

            Mesh furMesh = null;
            Material furMaterial = null;
            if (species.furZones.enabled)
            {
                // 털 범위는 리깅 전 모양(메시 원래 자리 = 예전 정지 모델)으로 정한다 — VillagerTuning.FurZones 값이 그 모양 기준이다
                Matrix4x4 rawToModel = toModel * source.transform.localToWorldMatrix;
                var rawPoints = new Vector3[raw.Length];
                for (int i = 0; i < raw.Length; i++) rawPoints[i] = rawToModel.MultiplyPoint3x4(raw[i]);
                furMesh = BuildFurMesh(species, mesh, rawPoints, weights, bindposes, tuning.modelYaw, albedoPath, furBase.shellCount, scale, out float uvToWorld);
                built.furVertices = furMesh != null ? furMesh.vertexCount : 0;
                furMaterial = FurMaterial(species, albedo, furBase, uvToWorld);
            }

            var root = new GameObject($"Villager_{species.id}");
            try
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = species.colliderRadius;
                capsule.height = species.height;
                capsule.center = new Vector3(0f, species.height * 0.5f, 0f);

                // Humanoid 동작이 몸을 Model 원점(발밑) 위에 세운다 — T-포즈 발 가운데로 옮기지 않고 원점에 둔다
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";
                instance.transform.localRotation = yaw * model.transform.localRotation;
                instance.transform.localScale = model.transform.localScale * scale;
                instance.transform.localPosition = new Vector3(0f, -bottom * scale, 0f);

                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var villager = root.AddComponent<Villager>();
                var fields = new SerializedObject(villager);
                fields.FindProperty("speciesId").stringValue = species.id;
                fields.FindProperty("displayName").stringValue = species.displayName;
                fields.FindProperty("animator").objectReferenceValue = animator;
                fields.FindProperty("idleSpeedRange").vector2Value = tuning.idleSpeedRange;
                fields.ApplyModifiedPropertiesWithoutUndo();

                var body = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                body.sharedMaterial = bodyMaterial;
                body.shadowCastingMode = ShadowCastingMode.On;
                body.receiveShadows = true;
                body.updateWhenOffscreen = false;
                Transform[] bones = body.bones;

                if (tail != null)
                {
                    // 꼬리 뼈 사슬: Hips 아래 Tail1 → … → Tail_End(끝점, 스키닝에는 안 쓴다)
                    int count = tail.joints.Length - 1;
                    var chain = new Transform[count + 1];
                    var chainRadii = new float[count + 1];
                    Transform parent = bones[hipsIndex];
                    float hipsScale = Mathf.Max(1e-6f, sourceBones[hipsIndex].lossyScale.x);
                    for (int k = 0; k <= count; k++)
                    {
                        Transform joint = new GameObject(k < count ? $"Tail{k + 1}" : "Tail_End").transform;
                        joint.SetParent(parent, false);
                        joint.SetPositionAndRotation(instance.transform.TransformPoint(tail.joints[k]), instance.transform.rotation * tail.rotations[Mathf.Min(k, count - 1)]);
                        chain[k] = joint;
                        chainRadii[k] = tail.radii[k] / hipsScale;
                        parent = joint;
                    }
                    var extended = new Transform[bones.Length + count];
                    bones.CopyTo(extended, 0);
                    for (int k = 0; k < count; k++) extended[bones.Length + k] = chain[k];
                    bones = extended;
                    body.sharedMesh = bodyMesh;
                    body.bones = bones;
                    Bounds bodyBounds = body.localBounds;
                    bodyBounds.Expand(tail.length * 2f / hipsScale); // 꼬리가 흔들려도 잘리지 않게
                    body.localBounds = bodyBounds;

                    var tailChain = root.AddComponent<TailChain>();
                    tailChain.Configure(chain, chainRadii, LegSpheres(instance.transform, tuning.tailRig.thighRadius * species.height, tuning.tailRig.calfRadius * species.height, tuning.tailRig.armRadius * species.height), tuning.TailSettingsOf(species.id));
                }

                var lod = root.AddComponent<LODGroup>();
                if (furMesh != null)
                {
                    // 몸과 같은 뼈를 쓰는 스킨드 렌더러(고라니 털과 같은 방식)
                    var fur = new GameObject("Fur");
                    fur.layer = body.gameObject.layer;
                    fur.transform.SetParent(instance.transform, false);
                    fur.transform.localPosition = body.transform.localPosition;
                    fur.transform.localRotation = body.transform.localRotation;
                    fur.transform.localScale = body.transform.localScale;
                    var furRenderer = fur.AddComponent<SkinnedMeshRenderer>();
                    furRenderer.sharedMesh = furMesh;
                    furRenderer.bones = bones;
                    furRenderer.rootBone = body.rootBone;
                    furRenderer.sharedMaterial = furMaterial;
                    Bounds furBounds = body.localBounds;
                    furBounds.Expand(species.fur.length * 8f / Mathf.Max(0.01f, scale)); // 셰이더가 털을 노멀 방향으로 밀어내는 만큼
                    furRenderer.localBounds = furBounds;
                    furRenderer.quality = body.quality;
                    furRenderer.updateWhenOffscreen = false;
                    furRenderer.shadowCastingMode = ShadowCastingMode.Off; // 털 그림자는 몸이 이미 드리운다
                    furRenderer.receiveShadows = true;
                    furRenderer.lightProbeUsage = body.lightProbeUsage;
                    furRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    furRenderer.skinnedMotionVectors = false;
                    lod.SetLODs(new[]
                    {
                        new LOD(tuning.furScreenHeight, new Renderer[] { body, furRenderer }),
                        new LOD(tuning.cullScreenHeight, new Renderer[] { body }),
                    });
                }
                else
                {
                    lod.SetLODs(new[] { new LOD(tuning.cullScreenHeight, new Renderer[] { body }) });
                }
                lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(species.id));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return built;
        }

        // ---------- 대기 동작 ----------

        /// <summary>
        /// 주민 공통 컨트롤러: 대기(고라니 Idle 클립, 빠르기 = IdleSpeed 파라미터) + 손가락 레이어(고라니 HandRelaxed 포즈 · Fingers 마스크).
        /// 같은 에셋을 고쳐 쓴다(프리팹 연결 유지). 시작 시점 · 빠르기는 Villager가 주민마다 다르게 넣는다.
        /// </summary>
        private static RuntimeAnimatorController BuildController()
        {
            AnimationClip idle = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(IdleClipPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) idle = clip;
            }
            if (idle == null) Debug.LogError($"[VillagerSetup] 대기 클립을 찾지 못했습니다: {IdleClipPath}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            bool hasSpeed = false;
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == Villager.IdleSpeedParameter) hasSpeed = true;
            }
            if (!hasSpeed) controller.AddParameter(new AnimatorControllerParameter { name = Villager.IdleSpeedParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = FindOrAddState(machine, Villager.IdleState);
            state.motion = idle;
            state.speed = 1f;
            state.speedParameterActive = true;
            state.speedParameter = Villager.IdleSpeedParameter;
            state.iKOnFeet = false;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);

            // 손가락: Mixamo 대기 동작의 손가락 커브 대신 고라니와 같은 평소 손 포즈를 덮는다
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(HandMaskPath);
            var relaxed = AssetDatabase.LoadAssetAtPath<AnimationClip>(HandClipPath);
            if (mask == null || relaxed == null)
            {
                Debug.LogWarning($"[VillagerSetup] 손가락 마스크 · 포즈가 없어 손가락 레이어를 건너뜁니다(Tools/Game/Setup Test Room이 만든다): {HandMaskPath}");
            }
            else
            {
                int index = FindLayer(controller, HandLayerName);
                if (index < 0)
                {
                    var hands = new AnimatorStateMachine { name = HandLayerName, hideFlags = HideFlags.HideInHierarchy };
                    AssetDatabase.AddObjectToAsset(hands, controller);
                    controller.AddLayer(new AnimatorControllerLayer { name = HandLayerName, stateMachine = hands, defaultWeight = 1f });
                    index = FindLayer(controller, HandLayerName);
                }
                AnimatorControllerLayer[] layers = controller.layers;
                layers[index].avatarMask = mask;
                layers[index].defaultWeight = 1f;
                layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
                controller.layers = layers;
                AnimatorState pose = FindOrAddState(controller.layers[index].stateMachine, HandStateName);
                pose.motion = relaxed;
                controller.layers[index].stateMachine.defaultState = pose;
                EditorUtility.SetDirty(pose);
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState FindOrAddState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == name) return child.state;
            }
            return machine.AddState(name);
        }

        private static int FindLayer(AnimatorController controller, string name)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == name) return i;
            }
            return -1;
        }

        // ---------- 텍스처 · 재질 · 모델 ----------

        /// <param name="keepAlpha">금속/매끄러움 맵처럼 알파에 값이 있으면 true. false면 알파를 버린다(버리면 알파 = 1 → 매끄러움 1로 젖은 듯 번들거린다)</param>
        private static Texture2D ConfigureTexture(string path, bool srgb, int maxSize, bool normal, bool keepAlpha = false)
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

        /// <summary>VARCO ORM(G 거칠기 · B 금속)을 URP 금속 맵(R 금속 × metallicScale · A 매끄러움 = (1 - 거칠기) × smoothnessScale)으로. 내용이 같으면 다시 쓰지 않는다.</summary>
        private static Texture2D MetallicSmoothness(string ormPath, string path, float metallicScale, float smoothnessScale, int maxSize)
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

        private static Material BodyMaterial(string path, Texture2D albedo, Texture2D normal, Texture2D metallic)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat("_Smoothness", 1f); // 맵의 알파 × 이 값
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>리깅 FBX를 Humanoid(이 모델로 아바타 만들기)로 가져온다. 재질은 프리팹에서 넣으므로 가져오지 않는다(10_Wolf에 든 텍스처도 풀지 않는다).</summary>
        private static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.importAnimation || importer.importCameras || importer.importLights || importer.optimizeGameObjects || importer.bakeAxisConversion
                || importer.materialImportMode != ModelImporterMaterialImportMode.None;
            if (!changed) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false; // T-포즈 한 장뿐 — 동작은 컨트롤러의 Humanoid 클립
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false; // 꼬리 뼈를 Hips 아래에 넣어야 한다
            importer.bakeAxisConversion = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static Vector3[] Skinned(Vector3[] raw, BoneWeight[] weights, Matrix4x4[] skin)
        {
            var result = new Vector3[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                BoneWeight w = weights[i];
                Vector3 v = raw[i];
                result[i] = skin[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + skin[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                          + skin[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + skin[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
            }
            return result;
        }

        /// <summary>뼈 이름(mixamorig: 같은 머리말 빼고)으로 스킨 뼈 번호를 찾는다.</summary>
        private static int BoneIndex(Transform[] bones, string name)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && ShortName(bones[i].name) == name) return i;
            }
            return -1;
        }

        private static string ShortName(string boneName)
        {
            int colon = boneName.LastIndexOf(':');
            return colon >= 0 ? boneName.Substring(colon + 1) : boneName;
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (ShortName(child.name) == name) return child;
            }
            return null;
        }

        /// <summary>모델 좌표 점들의 발바닥 · 머리 꼭대기 높이와 발 가운데(바닥 3% 정점 평균).</summary>
        private static void Measure(Vector3[] points, out float bottom, out float top, out Vector3 feet)
        {
            bottom = float.MaxValue;
            top = float.MinValue;
            foreach (Vector3 p in points)
            {
                bottom = Mathf.Min(bottom, p.y);
                top = Mathf.Max(top, p.y);
            }
            float limit = bottom + (top - bottom) * 0.03f;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 p in points)
            {
                if (p.y > limit) continue;
                sum += p;
                count++;
            }
            feet = count > 0 ? sum / count : Vector3.zero;
        }

        private static Material FurMaterial(VillagerTuning.Species species, Texture2D albedo, FurSettings furBase, float uvToWorld)
        {
            string path = $"{FurFolder}/Villager_{species.id}_Fur.mat";
            Shader shader = Shader.Find(FurShaderName);
            if (shader == null) throw new System.InvalidOperationException("털 셰이더를 찾지 못했습니다: " + FurShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", albedo);
            furBase.ApplyTo(material); // 공통 모양 · 움직임 · 빛
            material.SetFloat("_FurLength", species.fur.length);
            material.SetFloat("_FurDensity", uvToWorld / Mathf.Max(0.0001f, species.fur.strandSpacing));
            material.SetFloat("_DarkCutoff", species.fur.darkCutoff);
            material.SetColor("_BaseColor", species.fur.tint);
            material.SetColor("_EmissionColor", Color.black);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- 털 범위 · 털 메시 ----------

        /// <summary>
        /// 정점마다 털 덮임(0~1)과 길이 비율을 정한다. 모델 좌표(축 굽기 뒤 Y 위)를 키 비율로 바꿔
        /// 머리(headFrom + neckBand 위, 좌우 headHalfWidth 안)는 무조건 털, 목 띠 · 팔 · 꼬리 · 발 · 몸통은 머리 색과 맞을 때만 털.
        /// </summary>
        private static void FurMask(VillagerTuning.FurZones zones, Vector3[] points, Vector2[] uvs, Texture2D albedo,
            float bottom, float top, Vector3 feet, float modelYaw, float darkCutoff, out float[] mask, out float[] lengths)
        {
            float height = top - bottom;
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0f, modelYaw, 0f));
            Vector3 forward = inverse * Vector3.forward; // 모델 좌표에서 프리팹 앞
            Vector3 right = inverse * Vector3.right;
            int n = points.Length;
            var yN = new float[n];
            var side = new float[n];
            var front = new float[n];
            var colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = points[i] - feet;
                yN[i] = (points[i].y - bottom) / height;
                side[i] = Mathf.Abs(Vector3.Dot(p, right)) / height;
                front[i] = Vector3.Dot(p, forward) / height;
                colors[i] = albedo.GetPixelBilinear(uvs[i].x, uvs[i].y);
            }

            // 머리 색 묶음: 목 띠 위 머리(손 제외)의 알베도
            var headColors = new List<Color>();
            for (int i = 0; i < n; i++)
            {
                if (yN[i] < zones.headFrom + zones.neckBand * 2f || side[i] > zones.headHalfWidth) continue;
                headColors.Add(colors[i]);
            }
            List<Color> palette = KMeans(headColors, Mathf.Max(1, zones.paletteSize));
            if (zones.extraFurColors != null) palette.AddRange(zones.extraFurColors);

            mask = new float[n];
            lengths = new float[n];
            float tolerance = zones.colorTolerance;
            for (int i = 0; i < n; i++)
            {
                float match = ColorMatch(colors[i], palette, tolerance);
                // 셰이더가 어차피 잘라 버릴 어두운 곳(검은 레깅스 · 신발 등)은 몸 쪽 털 메시에 넣지 않는다(정점 아끼기). 셰이더와 같이 선형 밝기로 본다
                Color c = colors[i];
                float luminance = Mathf.GammaToLinearSpace(c.r) * 0.299f + Mathf.GammaToLinearSpace(c.g) * 0.587f + Mathf.GammaToLinearSpace(c.b) * 0.114f;
                if (luminance < darkCutoff * 1.3f) match = 0f;
                float y = yN[i];
                bool headColumn = side[i] <= zones.headHalfWidth;
                if (headColumn && y >= zones.headFrom - zones.neckBand)
                {
                    // 목 띠: 아래쪽은 색이 맞아야, headFrom + neckBand부터는 무조건
                    float sure = Smooth01((y - zones.headFrom) / Mathf.Max(0.001f, zones.neckBand));
                    mask[i] = Mathf.Max(sure, match * Smooth01((y - (zones.headFrom - zones.neckBand)) / Mathf.Max(0.001f, zones.neckBand)));
                    lengths[i] = 1f;
                }
                else if (zones.arms && side[i] >= zones.armFrom && y > 0.55f)
                {
                    mask[i] = match;
                    lengths[i] = zones.armLength;
                }
                else if (zones.tail && front[i] <= -zones.tailBehind && y > 0.15f && y < 0.65f && side[i] < 0.2f)
                {
                    mask[i] = match;
                    lengths[i] = zones.tailLength;
                }
                else if (zones.feet && y <= zones.feetTo)
                {
                    mask[i] = match;
                    lengths[i] = zones.feetLength;
                }
                else if (zones.body)
                {
                    mask[i] = match;
                    lengths[i] = zones.bodyLength;
                }
            }
        }

        private static float ColorMatch(Color color, List<Color> palette, float tolerance)
        {
            float best = float.MaxValue;
            foreach (Color c in palette)
            {
                float dr = color.r - c.r, dg = color.g - c.g, db = color.b - c.b;
                best = Mathf.Min(best, Mathf.Sqrt(dr * dr + dg * dg + db * db));
            }
            return 1f - Smooth01((best - tolerance * 0.7f) / (tolerance * 0.3f));
        }

        /// <summary>결정적 k-평균(첫 중심은 평균에서 가장 먼 색부터 차례로 — 멀리 떨어진 점 고르기).</summary>
        private static List<Color> KMeans(List<Color> colors, int k)
        {
            var centers = new List<Color>();
            if (colors.Count == 0) return centers;
            Color mean = Color.clear;
            foreach (Color c in colors) mean += c;
            mean /= colors.Count;
            centers.Add(mean);
            while (centers.Count < k)
            {
                float far = -1f;
                Color pick = mean;
                for (int i = 0; i < colors.Count; i += 7)
                {
                    float d = float.MaxValue;
                    foreach (Color c in centers) d = Mathf.Min(d, Distance(colors[i], c));
                    if (d > far) { far = d; pick = colors[i]; }
                }
                centers.Add(pick);
            }
            for (int iteration = 0; iteration < 12; iteration++)
            {
                var sums = new Color[centers.Count];
                var counts = new int[centers.Count];
                foreach (Color c in colors)
                {
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int j = 0; j < centers.Count; j++)
                    {
                        float d = Distance(c, centers[j]);
                        if (d < bestD) { bestD = d; best = j; }
                    }
                    sums[best] += c;
                    counts[best]++;
                }
                for (int j = 0; j < centers.Count; j++) if (counts[j] > 0) centers[j] = sums[j] / counts[j];
            }
            return centers;
        }

        private static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }

        /// <param name="points">리깅 전 모양의 정점 자리(모델 좌표) — 털 범위 판정용</param>
        /// <param name="weights">몸 메시와 같은 뼈 가중치(꼬리 뼈로 다시 나눈 것 포함)</param>
        private static Mesh BuildFurMesh(VillagerTuning.Species species, Mesh source, Vector3[] points, BoneWeight[] weights, Matrix4x4[] bindposes,
            float modelYaw, string albedoPath, int shellCount, float scale, out float uvToWorld)
        {
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            int[] triangles = source.triangles;
            Measure(points, out float bottom, out float top, out Vector3 feet);

            var albedo = new Texture2D(2, 2);
            albedo.LoadImage(File.ReadAllBytes(albedoPath));
            FurMask(species.furZones, points, uvs, albedo, bottom, top, feet, modelYaw, species.fur.darkCutoff, out float[] mask, out float[] lengths);
            Object.DestroyImmediate(albedo);

            Vector3[] smooth = SmoothNormals(points, normals);
            var furTriangles = new List<int>();
            var remap = new Dictionary<int, int>();
            var used = new List<int>();
            double worldArea = 0, uvArea = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (Mathf.Max(mask[a], Mathf.Max(mask[b], mask[c])) < MinMask) continue;
                furTriangles.Add(Remap(a, remap, used));
                furTriangles.Add(Remap(b, remap, used));
                furTriangles.Add(Remap(c, remap, used));
                worldArea += Vector3.Cross(points[b] - points[a], points[c] - points[a]).magnitude * 0.5f * scale * scale;
                Vector2 u = uvs[b] - uvs[a], v = uvs[c] - uvs[a];
                uvArea += Mathf.Abs(u.x * v.y - u.y * v.x) * 0.5f;
            }
            // UV 1칸이 월드 몇 m인가(털 범위 평균) — 가닥 간격(m)을 셰이더 밀도(UV 1칸 가닥 수)로 바꾼다
            uvToWorld = uvArea > 1e-9 ? (float)System.Math.Sqrt(worldArea / uvArea) : 1f;

            int shells = Mathf.Max(1, shellCount);
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

            string path = $"{FurFolder}/Villager_{species.id}_Fur.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = $"Villager_{species.id}_Fur";
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = outPositions;
            mesh.normals = outNormals;
            mesh.uv = outUv;
            mesh.SetUVs(1, outShell); // TEXCOORD1 = (겹 높이, 길이 비율, 덮임)
            mesh.boneWeights = outWeights; // 몸과 같은 뼈 · bindpose — 스킨드 렌더러가 몸과 똑같이 움직인다(경계는 렌더러 localBounds)
            mesh.bindposes = bindposes;
            mesh.triangles = outTriangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static int Remap(int index, Dictionary<int, int> remap, List<int> used)
        {
            if (remap.TryGetValue(index, out int mapped)) return mapped;
            mapped = used.Count;
            remap[index] = mapped;
            used.Add(index);
            return mapped;
        }

        /// <summary>UV · 노멀 이음매에서 셸이 갈라지지 않도록 같은 자리 정점의 노멀을 평균한다(고라니 털과 같은 방식). 자리는 모델 좌표(약 1 m)로 묶는다.</summary>
        private static Vector3[] SmoothNormals(Vector3[] positions, Vector3[] normals)
        {
            var sums = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i] * 1e5f; // 0.01 mm 단위로 묶는다
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

        // ---------- 꼬리 ----------

        private sealed class TailPlan
        {
            /// <summary>꼬리 마디 자리(모델 좌표): 뼈 Tail1…TailN의 자리 + 끝점. 길이 N + 1.</summary>
            public Vector3[] joints;
            /// <summary>뼈 회전(모델 좌표, z = 다음 마디 쪽). 길이 N.</summary>
            public Quaternion[] rotations;
            /// <summary>마디마다 꼬리 굵기(반지름, 모델 단위). 길이 N + 1.</summary>
            public float[] radii;
            /// <summary>정점마다 꼬리 뿌리 0 → 끝 1. 꼬리가 아니면 음수.</summary>
            public float[] along;
            /// <summary>꼬리 길이(모델 단위).</summary>
            public float length;
        }

        /// <summary>
        /// 꼬리 판정 + 꼬리 뼈 자리. 엉덩이 뼈에 붙여 본 메시에서, 꼬리 높이 범위 안 가장 뒤로 나온 곳이 등(위쪽 몸통 뒤)보다 minProtrusion 넘게 뒤면 꼬리.
        /// 꼬리 정점 = 등 + cutMargin보다 뒤에 있고 꼬리 끝과 이어진 정점. 가운데 선은 뿌리(자른 면 쪽 정점 가운데) → 끝을 잇는 꺾은선을
        /// 마디 구간 정점들의 가운데로 몇 번 다듬어 구한다(굽은 꼬리도 따라간다).
        /// </summary>
        private static TailPlan PlanTail(VillagerTuning.Species species, VillagerTuning tuning, Vector3[] raw, int[] triangles, Matrix4x4 hipsSkin,
            float bottom, float top, Vector3 feet, float scale, out string note)
        {
            VillagerTuning.TailRig rig = tuning.tailRig;
            float height = top - bottom;
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0f, tuning.modelYaw, 0f));
            Vector3 forward = inverse * Vector3.forward;
            Vector3 right = inverse * Vector3.right;
            int n = raw.Length;
            var points = new Vector3[n];
            var behind = new float[n];
            var inBand = new bool[n];
            float back = 0f, protrusion = 0f;
            int tipIndex = -1;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = hipsSkin.MultiplyPoint3x4(raw[i]);
                points[i] = p;
                Vector3 d = p - feet;
                float y = (p.y - bottom) / height;
                float side = Mathf.Abs(Vector3.Dot(d, right)) / height;
                float b = -Vector3.Dot(d, forward) / height;
                behind[i] = b;
                if (y >= rig.backFrom && y <= rig.backTo && side <= rig.backHalfWidth) back = Mathf.Max(back, b);
                inBand[i] = y >= rig.bandFrom && y <= rig.bandTo && side <= rig.halfWidth;
                if (inBand[i] && b > protrusion)
                {
                    protrusion = b;
                    tipIndex = i;
                }
            }
            float extra = protrusion - back;
            bool has = extra >= rig.minProtrusion;
            string how = "메시 판정";
            if (tuning.TryGetTailOverride(species.id, out bool forced))
            {
                has = forced;
                how = "tailOverrides로 정함";
            }
            note = $"{species.id}({species.displayName}): 엉덩이 뒤 {protrusion:0.000} · 등 {back:0.000} · 차이 {extra:+0.000;-0.000} → 꼬리 {(has ? "있음" : "없음")}({how})";
            if (!has || tipIndex < 0) return null;

            float cut = back + rig.cutMargin;
            var selected = new bool[n];
            for (int i = 0; i < n; i++) selected[i] = inBand[i] && behind[i] > cut;
            if (!selected[tipIndex])
            {
                note += " — 자르는 면 뒤에 정점이 없어 뼈를 넣지 못함";
                return null;
            }
            KeepConnected(points, triangles, selected, tipIndex);

            // 뿌리: 자른 면에 가까운 꼬리 정점들의 가운데. 끝: 뿌리에서 가장 먼 꼬리 정점
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++) if (selected[i]) nearest = Mathf.Min(nearest, behind[i]);
            Vector3 sum = Vector3.zero;
            int sumCount = 0, tailVertices = 0;
            for (int i = 0; i < n; i++)
            {
                if (!selected[i]) continue;
                tailVertices++;
                if (behind[i] > nearest + 0.015f) continue;
                sum += points[i];
                sumCount++;
            }
            Vector3 basePoint = sum / Mathf.Max(1, sumCount);
            Vector3 tipPoint = basePoint;
            float far = -1f;
            for (int i = 0; i < n; i++)
            {
                if (!selected[i]) continue;
                float distance = (points[i] - basePoint).sqrMagnitude;
                if (distance > far)
                {
                    far = distance;
                    tipPoint = points[i];
                }
            }

            var line = new List<Vector3> { basePoint, tipPoint };
            var along = new float[n];
            var off = new float[n];
            int bones = rig.minBones;
            float total = 0f;
            for (int pass = 0; pass < 4; pass++)
            {
                total = Project(line, points, selected, along, off);
                bones = Mathf.Clamp(Mathf.RoundToInt(total * scale / rig.segmentLength), rig.minBones, Mathf.Max(rig.minBones, rig.maxBones));
                var next = new List<Vector3> { basePoint };
                for (int k = 1; k < bones; k++)
                {
                    Vector3 center = Vector3.zero;
                    int centerCount = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (!selected[i] || Mathf.Abs(along[i] * bones - k) > 0.5f) continue;
                        center += points[i];
                        centerCount++;
                    }
                    next.Add(centerCount > 0 ? center / centerCount : PointAt(line, (float)k / bones));
                }
                next.Add(tipPoint);
                line = next;
            }
            total = Project(line, points, selected, along, off);

            var plan = new TailPlan
            {
                joints = line.ToArray(),
                rotations = new Quaternion[bones],
                radii = new float[bones + 1],
                along = along,
                length = total,
            };
            for (int k = 0; k < bones; k++)
            {
                Vector3 direction = line[k + 1] - line[k];
                Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.95f ? forward : Vector3.up;
                plan.rotations[k] = direction.sqrMagnitude > 1e-10f ? Quaternion.LookRotation(direction, up) : Quaternion.identity;
            }
            for (int k = 0; k <= bones; k++)
            {
                float radius = 0f;
                int radiusCount = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!selected[i] || Mathf.Abs(along[i] * bones - k) > 0.5f) continue;
                    radius += off[i];
                    radiusCount++;
                }
                plan.radii[k] = radiusCount > 0 ? radius / radiusCount : 0f;
            }
            note += $" · 꼬리 정점 {tailVertices} · 길이 {total * scale:0.00} m · 뼈 {bones}개";
            return plan;
        }

        /// <summary>selected 가운데 seed와 삼각형으로 이어진 정점만 남긴다(UV 이음매로 갈린 정점은 자리로 묶는다).</summary>
        private static void KeepConnected(Vector3[] points, int[] triangles, bool[] selected, int seed)
        {
            int n = points.Length;
            var welded = new Dictionary<Vector3Int, int>();
            var group = new int[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = points[i] * 1e5f;
                var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (!welded.TryGetValue(key, out int id))
                {
                    id = welded.Count;
                    welded[key] = id;
                }
                group[i] = id;
            }
            var parent = new int[welded.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = triangles[t + e], b = triangles[t + (e + 1) % 3];
                    if (!selected[a] || !selected[b]) continue;
                    int ra = Find(parent, group[a]), rb = Find(parent, group[b]);
                    if (ra != rb) parent[ra] = rb;
                }
            }
            int keep = Find(parent, group[seed]);
            for (int i = 0; i < n; i++)
            {
                if (selected[i] && Find(parent, group[i]) != keep) selected[i] = false;
            }
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        /// <summary>꼬리 정점마다 꺾은선에서 가장 가까운 자리를 찾아 along(0~1) · off(선까지 거리)에 넣는다. 꺾은선 길이를 돌려준다.</summary>
        private static float Project(List<Vector3> line, Vector3[] points, bool[] selected, float[] along, float[] off)
        {
            int segments = line.Count - 1;
            var start = new float[segments + 1];
            for (int s = 0; s < segments; s++) start[s + 1] = start[s] + Vector3.Distance(line[s], line[s + 1]);
            float total = Mathf.Max(1e-6f, start[segments]);
            for (int i = 0; i < points.Length; i++)
            {
                if (!selected[i])
                {
                    along[i] = -1f;
                    continue;
                }
                float best = float.MaxValue, at = 0f;
                for (int s = 0; s < segments; s++)
                {
                    Vector3 a = line[s], d = line[s + 1] - a;
                    float sqr = d.sqrMagnitude;
                    float k = sqr > 1e-12f ? Mathf.Clamp01(Vector3.Dot(points[i] - a, d) / sqr) : 0f;
                    float distance = (points[i] - (a + d * k)).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    at = start[s] + Mathf.Sqrt(sqr) * k;
                }
                along[i] = at / total;
                off[i] = Mathf.Sqrt(best);
            }
            return total;
        }

        private static Vector3 PointAt(List<Vector3> line, float fraction)
        {
            float total = 0f;
            for (int s = 0; s < line.Count - 1; s++) total += Vector3.Distance(line[s], line[s + 1]);
            float target = total * Mathf.Clamp01(fraction);
            for (int s = 0; s < line.Count - 1; s++)
            {
                float length = Vector3.Distance(line[s], line[s + 1]);
                if (target <= length || s == line.Count - 2) return Vector3.Lerp(line[s], line[s + 1], length > 1e-6f ? target / length : 0f);
                target -= length;
            }
            return line[line.Count - 1];
        }

        /// <summary>
        /// 꼬리 뼈를 더한 몸 메시. 정점 자리 · 노멀 · UV · 삼각형은 원본 그대로, 꼬리 정점의 가중치만 꼬리 뼈로 다시 나눈다
        /// (뿌리 쪽은 원래 가중치와 섞고, 마디 사이는 두 뼈를 섞는다). 꼬리 뼈의 bindpose = (꼬리 뼈 자리)⁻¹ × 엉덩이 스키닝 행렬 —
        /// 기준 자세에서 꼬리 뼈가 엉덩이 뼈와 똑같이 작용하므로 꼬리는 리깅 전 모양 그대로 나온다.
        /// weights · bindposes를 새 것으로 바꿔 돌려준다(털 메시가 같이 쓴다).
        /// </summary>
        private static Mesh BuildTailBody(string path, VillagerTuning.Species species, Mesh source, TailPlan tail, VillagerTuning.TailRig rig,
            Matrix4x4 hipsSkin, Vector3 hipsScale, ref BoneWeight[] weights, ref Matrix4x4[] bindposes)
        {
            int count = tail.joints.Length - 1;
            int first = bindposes.Length;
            var newBindposes = new Matrix4x4[first + count];
            bindposes.CopyTo(newBindposes, 0);
            for (int k = 0; k < count; k++)
                newBindposes[first + k] = Matrix4x4.TRS(tail.joints[k], tail.rotations[k], hipsScale).inverse * hipsSkin;

            var newWeights = (BoneWeight[])weights.Clone();
            float jointBlend = Mathf.Clamp(rig.jointBlend, 0.01f, 0.5f);
            for (int i = 0; i < newWeights.Length; i++)
            {
                if (tail.along[i] < 0f) continue;
                float u = tail.along[i] * count;
                int a = Mathf.Clamp(Mathf.FloorToInt(u), 0, count - 1);
                float fraction = u - a;
                int b = a;
                float weightA = 1f;
                if (fraction < jointBlend && a > 0)
                {
                    b = a - 1;
                    weightA = 0.5f + 0.5f * fraction / jointBlend;
                }
                else if (fraction > 1f - jointBlend && a < count - 1)
                {
                    b = a + 1;
                    weightA = 0.5f + 0.5f * (1f - fraction) / jointBlend;
                }
                float tailShare = Smooth01(u / Mathf.Max(0.01f, rig.baseBlend));
                newWeights[i] = Blend(weights[i], 1f - tailShare, first + a, tailShare * weightA, first + b, tailShare * (1f - weightA));
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = $"Villager_{species.id}_Body";
            mesh.indexFormat = source.indexFormat;
            mesh.vertices = source.vertices;
            mesh.normals = source.normals;
            mesh.tangents = source.tangents;
            mesh.uv = source.uv;
            mesh.boneWeights = newWeights;
            mesh.bindposes = newBindposes;
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(source.GetTriangles(s), s);
            mesh.bounds = source.bounds;
            mesh.UploadMeshData(false);
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            weights = newWeights;
            bindposes = newBindposes;
            return mesh;
        }

        /// <summary>원래 가중치 × keep에 뼈 둘을 더하고, 큰 것 넷만 남겨 합이 1이 되게 한다.</summary>
        private static BoneWeight Blend(BoneWeight w, float keep, int boneA, float weightA, int boneB, float weightB)
        {
            var index = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3, boneA, boneB };
            var value = new[] { w.weight0 * keep, w.weight1 * keep, w.weight2 * keep, w.weight3 * keep, weightA, boneB == boneA ? 0f : weightB };
            if (boneB == boneA) value[4] += weightB;
            for (int i = 0; i < 4; i++)
            {
                int best = i;
                for (int j = i + 1; j < 6; j++) if (value[j] > value[best]) best = j;
                (index[i], index[best]) = (index[best], index[i]);
                (value[i], value[best]) = (value[best], value[i]);
            }
            float sum = value[0] + value[1] + value[2] + value[3];
            if (sum < 1e-6f) return w;
            return new BoneWeight
            {
                boneIndex0 = index[0], weight0 = value[0] / sum,
                boneIndex1 = index[1], weight1 = value[1] / sum,
                boneIndex2 = index[2], weight2 = value[2] / sum,
                boneIndex3 = index[3], weight3 = value[3] / sum,
            };
        }

        /// <summary>꼬리가 몸을 뚫지 않게 양쪽 넓적다리 · 종아리(뼈마다 3개)와 아래팔 · 손에 두는 충돌 구. 반지름은 월드 m로 받아 뼈 로컬 단위로 바꿔 넣는다.</summary>
private static TailChain.Sphere[] LegSpheres(Transform model, float thighRadius, float calfRadius, float armRadius)
        {
            var spheres = new List<TailChain.Sphere>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform thigh = FindBone(model, side + "UpLeg");
                Transform calf = FindBone(model, side + "Leg");
                Transform foot = FindBone(model, side + "Foot");
                if (thigh != null && calf != null && foot != null)
                {
                    foreach (float t in new[] { 0.2f, 0.5f, 0.8f }) spheres.Add(SphereOn(thigh, Vector3.Lerp(thigh.position, calf.position, t), thighRadius));
                    foreach (float t in new[] { 0.15f, 0.5f, 0.85f }) spheres.Add(SphereOn(calf, Vector3.Lerp(calf.position, foot.position, t), calfRadius));
                }
                // 대기 자세에서 팔이 엉덩이 옆으로 내려와 옆으로 휘는 꼬리와 겹친다 — 아래팔 끝과 손에도 둔다(armRadius 0이면 안 둔다)
                Transform forearm = FindBone(model, side + "ForeArm");
                Transform hand = FindBone(model, side + "Hand");
                if (armRadius > 0f && forearm != null && hand != null)
                {
                    spheres.Add(SphereOn(forearm, Vector3.Lerp(forearm.position, hand.position, 0.6f), armRadius));
                    // 손: 손목에서 아래팔 방향으로 손 길이만큼 더 나간 곳까지
                    Vector3 reach = (hand.position - forearm.position) * 0.35f;
                    spheres.Add(SphereOn(hand, hand.position + reach * 0.3f, armRadius));
                    spheres.Add(SphereOn(hand, hand.position + reach, armRadius));
                }
            }
            return spheres.ToArray();
        }

        private static TailChain.Sphere SphereOn(Transform bone, Vector3 worldCenter, float worldRadius)
        {
            return new TailChain.Sphere { bone = bone, center = bone.InverseTransformPoint(worldCenter), radius = worldRadius / Mathf.Max(1e-6f, bone.lossyScale.x) };
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
