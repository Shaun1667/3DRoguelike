using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 반달가슴곰 보스 프리팹 · 수치 에셋 · 컨트롤러 · 재질(BossSetup이 부른다, Setup Floor). 여러 번 실행해도 결과가 같다(컨트롤러 안 fileID만 바뀜).
    /// 수치 에셋은 없을 때만 만들고 값을 덮어쓰지 않는다(프리팹 연결만 고친다).
    /// 모델: `Art/Bosses/Bear/Bear_Mutant.fbx`(Blender 리그, Generic, 숲 Mutant와 같은 뼈 + Jaw, 길이 1 기준) + 동작 `Animation/Bear_Mutant@{상태}.fbx`(파일 하나 = 클립 하나, 30 fps, 제자리).
    /// 원본 자세가 비대칭이라 크기 · 피벗은 대기 동작 첫 프레임 자세로 잰다. 모델이 없으면 임시 도형.
    /// 재생 빠르기는 수치 시간으로 굽는다: 예고 동작은 맞는 순간이 예고 끝에 오게, 걷기 · 달리기 · 돌진은 발 빠르기(길이 1 기준 × 모델 길이)를 이동 빠르기에 맞춰.
    /// </summary>
    public static class BearBossSetup
    {
        internal const string TuningPath = "Assets/Game/Data/Bosses/BearBossTuning.asset";
        internal const string PrefabPath = "Assets/Game/Prefabs/Bosses/Boss_Bear.prefab";
        private const string Folder = "Assets/Game/Art/Bosses/Bear";
        private const string ModelPath = Folder + "/Bear_Mutant.fbx";
        private const string ClipFolder = Folder + "/Animation";
        private const string TextureFolder = Folder + "/Bear_Mutant_tex";
        private const string MaterialPath = Folder + "/Bear_Mutant.mat";
        internal const string ControllerPath = Folder + "/Bear_Boss.controller";

        /// <summary>발이 안 미끄러지는 빠르기(m/s, 모델 길이 1 기준 — 감독이 Blender에서 잰 값).</summary>
        private const float WalkFoot = 0.23f, RunFoot = 1.16f, ChargeFoot = 1.90f;
        /// <summary>도는 동작. 나머지는 한 번.</summary>
        private static readonly string[] Loops = { "Idle", "Walk", "Run", BearBoss.ChargeState, BearBoss.StunState };
        private const int AlbedoSize = 2048, DetailSize = 1024;
        private const float ShakeVelocity = 0.5f;

        public static BearBossTuning Run(Material warning)
        {
            BearBossTuning tuning = LoadOrCreateTuning();
            ImportClips();
            tuning.prefab = BuildPrefab(tuning, warning);
            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssets();
            return tuning;
        }

        private static BearBossTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BearBossTuning>(TuningPath);
            if (tuning != null) return tuning;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BearBossTuning>(), TuningPath); // 값은 BearBossTuning 생성자(BALANCE 15-2)
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<BearBossTuning>(TuningPath);
        }

        private static Boss BuildPrefab(BearBossTuning tuning, Material warning)
        {
            var root = new GameObject("Boss_Bear");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);

            Animator animator = null;
            Transform head = null;
            Vector3 size;
            if (!TryAttachModel(body.transform, tuning, out animator, out head, out size))
            {
                size = BuildShape(body.transform, tuning.modelScale);
            }

            // 몸: 앞뒤로 누운 캡슐(근접 · 총이 맞는 판정). 플레이어를 밀지는 않는다(Boss.Begin)
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = Mathf.Max(0.3f, Mathf.Min(size.x, size.y) * 0.5f);
            capsule.height = Mathf.Max(capsule.radius * 2f, size.z);
            capsule.center = new Vector3(0f, capsule.radius, 0f);
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true; // 물리에 밀리지 않고 BearBoss가 직접 옮긴다
            rigidbody.useGravity = false;

            var health = root.AddComponent<Health>();
            var flash = root.AddComponent<HitFlash>();
            GameObject lane = TestRoomSetup.FlatMarker(root.transform, "LaneWarning", PrimitiveType.Cube, Vector3.zero, new Vector3(1f, 0.005f, 1f), warning);
            GameObject slam = TestRoomSetup.FlatMarker(root.transform, "SlamWarning", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1f, 0.005f, 1f), warning);
            GameObject howl = TestRoomSetup.FlatMarker(root.transform, "HowlWarning", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1f, 0.004f, 1f), warning);
            var swipe = new GameObject("SwipeWarning");
            swipe.transform.SetParent(root.transform, false);
            swipe.AddComponent<MeshFilter>(); // 부채꼴 메시는 실행 때 수치(각도)로 만든다
            var swipeRenderer = swipe.AddComponent<MeshRenderer>();
            swipeRenderer.sharedMaterial = warning;
            swipeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            swipeRenderer.receiveShadows = false;

            var impulse = root.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseChannel = Boss.ShakeChannel;
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseDuration = 0.3f;
            impulse.DefaultVelocity = Vector3.down * ShakeVelocity;

            var boss = root.AddComponent<BearBoss>();
            var fields = new SerializedObject(boss);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("health").objectReferenceValue = health;
            fields.FindProperty("flash").objectReferenceValue = flash;
            fields.FindProperty("body").objectReferenceValue = body;
            fields.FindProperty("animator").objectReferenceValue = animator;
            fields.FindProperty("impulse").objectReferenceValue = impulse;
            fields.FindProperty("laneWarning").objectReferenceValue = lane;
            fields.FindProperty("slamWarning").objectReferenceValue = slam;
            fields.FindProperty("swipeWarning").objectReferenceValue = swipe;
            fields.FindProperty("howlWarning").objectReferenceValue = howl;
            fields.FindProperty("head").objectReferenceValue = head;
            fields.ApplyModifiedPropertiesWithoutUndo();

            bool rigged = animator != null;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[BearBossSetup] {PrefabPath}: 크기 {size.x:0.00} × {size.y:0.00} × {size.z:0.00} m(폭 × 높이 × 길이) · 모델 {(rigged ? "Blender 리그" : "임시 도형")}");
            return prefab.GetComponent<Boss>();
        }

        /// <summary>
        /// Body 아래 Model(크기 · 위치 보정) → 모델 인스턴스(Animator). 앞뒤 길이를 modelScale(m)에 맞추고 바닥 가운데를 발밑 피벗에.
        /// 비대칭 원본 자세 대신 대기 동작 첫 프레임 자세로 잰다(그 자세로 프리팹에 남는다). size = 폭 · 높이 · 길이(m).
        /// </summary>
        private static bool TryAttachModel(Transform body, BearBossTuning tuning, out Animator animator, out Transform head, out Vector3 size)
        {
            animator = null;
            head = null;
            size = Vector3.zero;
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer)) return false;
            ConfigureModel(importer);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            AnimationClip idle = Clip("Idle");
            Material material = LoadMaterial();
            if (model == null || idle == null || material == null)
            {
                Debug.LogWarning($"[BearBossSetup] 곰 모델 · 대기 동작 · 텍스처 중 없는 것이 있어 임시 도형을 씁니다: {Folder}");
                return false;
            }

            var holder = new GameObject("Model").transform;
            holder.SetParent(body, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            idle.SampleAnimation(instance, 0f);
            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var points = new List<Vector3>();
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r.sharedMesh == null) continue;
                r.BakeMesh(baked, true);
                Matrix4x4 toBody = body.worldToLocalMatrix * r.transform.localToWorldMatrix;
                foreach (Vector3 v in baked.vertices) points.Add(toBody.MultiplyPoint3x4(v));
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.updateWhenOffscreen = false;
            }
            Object.DestroyImmediate(baked);
            if (points.Count == 0)
            {
                Object.DestroyImmediate(holder.gameObject);
                return false;
            }
            Vector3 min = points[0], max = points[0];
            foreach (Vector3 p in points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 center = (min + max) * 0.5f;
            float scale = tuning.modelScale / Mathf.Max(0.001f, max.z - min.z);
            holder.localScale = Vector3.one * scale;
            holder.localPosition = new Vector3(-center.x * scale, -min.y * scale, -center.z * scale);
            size = (max - min) * scale;

            animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Avatar avatar && avatar.isValid) animator.avatar = avatar;
            }
            animator.runtimeAnimatorController = BuildController(tuning);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head") head = t;
            }
            return true;
        }

        /// <summary>임시 도형 곰: 검은 몸통 · 머리 · 가슴의 흰 반달. 앞은 +z. 크기를 돌려준다.</summary>
        private static Vector3 BuildShape(Transform parent, float length)
        {
            Material black = TestRoomSetup.LoadOrCreateLit("Assets/Game/Art/Bosses/Bear_Black.mat", new Color(0.08f, 0.07f, 0.07f));
            Material white = TestRoomSetup.LoadOrCreateLit("Assets/Game/Art/Bosses/Bear_White.mat", new Color(0.9f, 0.88f, 0.82f));
            float height = length * 0.67f;
            Shape(PrimitiveType.Capsule, "Torso", parent, new Vector3(0f, height * 0.55f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(height * 0.8f, length * 0.42f, height * 0.8f), black);
            Shape(PrimitiveType.Sphere, "Head", parent, new Vector3(0f, height * 0.75f, length * 0.42f), Quaternion.identity, Vector3.one * height * 0.45f, black);
            Shape(PrimitiveType.Cube, "Moon", parent, new Vector3(0f, height * 0.55f, length * 0.33f), Quaternion.identity, new Vector3(height * 0.5f, height * 0.1f, height * 0.1f), white);
            return new Vector3(height * 0.8f, height, length);
        }

        private static void Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localRotation = rotation;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>리깅 FBX를 Generic(이 모델로 아바타 만들기, 루트 모션 뼈 없음)으로. 동작 · 재질은 가져오지 않는다(숲 Mutant와 같은 설정).</summary>
        private static void ConfigureModel(ModelImporter importer)
        {
            bool changed = importer.animationType != ModelImporterAnimationType.Generic || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || !string.IsNullOrEmpty(importer.motionNodeName)
                || importer.importAnimation || importer.importCameras || importer.importLights || importer.optimizeGameObjects
                || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.addCollider;
            if (!changed) return;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "";
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false; // 머리 뼈를 이름으로 찾는다
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.SaveAndReimport();
        }

        /// <summary>동작 FBX를 Generic으로: 클립 이름 = @ 뒤(= 상태 이름), 도는 동작은 루프, Hit는 더하기(기준 프레임 0). 아바타 없이 뼈 경로로 맞는다.</summary>
        private static void ImportClips()
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ClipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                int at = fileName.LastIndexOf('@');
                if (at < 0 || !(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                string clipName = fileName.Substring(at + 1);
                bool loop = System.Array.IndexOf(Loops, clipName) >= 0;
                bool additive = clipName == BearBoss.HitState;

                bool changed = importer.animationType != ModelImporterAnimationType.Generic || importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar
                    || !importer.importAnimation || importer.importCameras || importer.importLights || !string.IsNullOrEmpty(importer.motionNodeName)
                    || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.addCollider;
                if (changed)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                    importer.importAnimation = true;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.motionNodeName = "";
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.addCollider = false;
                }
                ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
                if (defaults.Length == 0)
                {
                    Debug.LogWarning($"[BearBossSetup] 동작이 들어 있지 않습니다: {path}");
                    if (changed) importer.SaveAndReimport();
                    continue;
                }
                ModelImporterClipAnimation clip = defaults[0];
                clip.name = clipName;
                clip.loopTime = loop;
                clip.loopPose = false;
                clip.hasAdditiveReferencePose = additive;
                clip.additiveReferencePoseFrame = additive ? clip.firstFrame : 0f;
                ModelImporterClipAnimation[] now = importer.clipAnimations;
                bool same = now != null && now.Length == 1 && now[0].name == clip.name && now[0].firstFrame == clip.firstFrame && now[0].lastFrame == clip.lastFrame
                    && now[0].loopTime == clip.loopTime && now[0].loopPose == clip.loopPose
                    && now[0].hasAdditiveReferencePose == clip.hasAdditiveReferencePose && now[0].additiveReferencePoseFrame == clip.additiveReferencePoseFrame;
                if (same && !changed) continue;
                importer.clipAnimations = new[] { clip };
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip Clip(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath($"{ClipFolder}/Bear_Mutant@{name}.fbx"))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.name == name) return clip;
            }
            return null;
        }

        /// <summary>
        /// 컨트롤러를 같은 에셋에 다시 만든다. Base: Locomotion(대기 ↔ 걷기 ↔ 달리기, Speed m/s) + 패턴 상태 + Roar · Stun · Death. Hit 레이어: 더하기.
        /// 공격 상태는 Tempo 파라미터(격노 배율)를 재생 빠르기에 곱한다.
        /// </summary>
        private static RuntimeAnimatorController BuildController(BearBossTuning tuning)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.layers = new AnimatorControllerLayer[0];
            controller.parameters = new AnimatorControllerParameter[0];
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (sub != null && sub != controller) Object.DestroyImmediate(sub, true);
            }
            controller.AddParameter(BearBoss.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = BearBoss.TempoParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddLayer("Base");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            float length = tuning.modelScale;

            // 걷기 · 달리기: 발 빠르기(길이 1 기준 × 모델 길이)를 이동 빠르기에 맞춘 재생 빠르기
            float walkRate = tuning.walkSpeed / (WalkFoot * length);
            float runRate = tuning.runSpeed / (RunFoot * length);
            AnimatorState locomotion = controller.CreateBlendTreeInController(BearBoss.LocomotionState, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = BearBoss.SpeedParameter;
            tree.useAutomaticThresholds = false;
            tree.children = new[]
            {
                new ChildMotion { motion = Clip("Idle"), threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = Clip("Walk"), threshold = tuning.walkSpeed, timeScale = walkRate },
                new ChildMotion { motion = Clip("Run"), threshold = tuning.runSpeed, timeScale = runRate },
            };
            locomotion.iKOnFeet = false;
            machine.defaultState = locomotion;

            var report = new System.Text.StringBuilder();
            report.Append($"걷기 ×{walkRate:0.00} · 달리기 ×{runRate:0.00}");
            // (상태, 재생 빠르기, 격노 배율 적용, 끝나면 Locomotion으로)
            AnimationClip slamWindup = Clip(BearBoss.SlamWindupState), chargeWindup = Clip(BearBoss.ChargeWindupState), roar = Clip(BearBoss.RoarState);
            var states = new (string name, float speed, bool tempo, bool exit)[]
            {
                (BearBoss.SwipeState, tuning.swipeHitTime / tuning.swipeWindup, true, true),
                (BearBoss.SlamWindupState, (slamWindup != null ? slamWindup.length : 1f) / tuning.slamWindup, true, false),
                (BearBoss.SlamState, 0.3f / tuning.slamDropTime, true, true),          // 일어선 자세에서 0.30초에 착지(동작 기준)
                (BearBoss.ChargeWindupState, (chargeWindup != null ? chargeWindup.length : 1f) / tuning.chargeWindup, true, false),
                (BearBoss.ChargeState, tuning.chargeSpeed / (ChargeFoot * length), true, false),
                (BearBoss.StunState, 1f, false, false),
                (BearBoss.RoarState, (roar != null ? roar.length : tuning.roarTime) / Mathf.Max(0.01f, tuning.roarTime), false, true),
                (BearBoss.HowlState, tuning.howlHitTime / tuning.howlWindup, true, true),          // 하울링: 포효 클립, 입을 벌리는 순간 = 예고 끝
                (BearBoss.DeathState, 1f, false, false),
            };
            foreach ((string name, float speed, bool tempo, bool exit) in states)
            {
                AnimationClip clip = Clip(name == BearBoss.HowlState ? BearBoss.RoarState : name);
                if (clip == null) Debug.LogWarning($"[BearBossSetup] {name} 동작 클립이 없습니다");
                AnimatorState state = machine.AddState(name);
                state.motion = clip;
                state.speed = speed;
                state.iKOnFeet = false;
                if (tempo)
                {
                    state.speedParameter = BearBoss.TempoParameter;
                    state.speedParameterActive = true;
                }
                if (exit)
                {
                    AnimatorStateTransition back = state.AddTransition(locomotion);
                    back.hasExitTime = true;
                    back.exitTime = 1f;
                    back.duration = 0.25f;
                    back.hasFixedDuration = true;
                }
                report.Append($" · {name} ×{speed:0.00}");
            }

            var hitMachine = new AnimatorStateMachine { name = BearBoss.HitState, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(hitMachine, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = BearBoss.HitState,
                stateMachine = hitMachine,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
            });
            AnimatorState none = hitMachine.AddState("None");
            hitMachine.defaultState = none;
            AnimatorState hit = hitMachine.AddState(BearBoss.HitState);
            hit.motion = Clip(BearBoss.HitState);
            hit.iKOnFeet = false;
            AnimatorStateTransition done = hit.AddTransition(none);
            done.hasExitTime = true;
            done.exitTime = 1f;
            done.duration = 0.12f;
            done.hasFixedDuration = true;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[BearBossSetup] 컨트롤러 {ControllerPath}: {report}");
            return controller;
        }

        /// <summary>URP Lit 재질(알베도 · 노멀 · 금속/매끄러움 맵, HitFlash용 발광). 텍스처가 없으면 null.</summary>
        private static Material LoadMaterial()
        {
            string albedoPath = $"{TextureFolder}/Bear_Mutant_0_Image_0.png";
            string ormPath = $"{TextureFolder}/Bear_Mutant_1_Image_1.png";
            string normalPath = $"{TextureFolder}/Bear_Mutant_2_Image_2.png";
            string metallicPath = $"{TextureFolder}/Bear_Mutant_MetallicSmoothness.png";
            if (AssetImporter.GetAtPath(albedoPath) == null || AssetImporter.GetAtPath(normalPath) == null) return null;
            Texture2D albedo = VillageHouseArt.ConfigureTexture(albedoPath, srgb: true, AlbedoSize, normal: false);
            Texture2D normalMap = VillageHouseArt.ConfigureTexture(normalPath, srgb: false, DetailSize, normal: true);
            // 금속/매끄러움 맵은 감독이 넣어 둔 것을 그대로 쓰고, 없을 때만 ORM에서 만든다(숲 적과 같은 배율)
            Texture2D metallic = null;
            if (AssetImporter.GetAtPath(metallicPath) != null) metallic = VillageHouseArt.ConfigureTexture(metallicPath, srgb: false, DetailSize, normal: false, keepAlpha: true);
            else if (AssetImporter.GetAtPath(ormPath) != null)
            {
                VillageHouseArt.ConfigureTexture(ormPath, srgb: false, 256, normal: false);
                metallic = VillageHouseArt.MetallicSmoothness(ormPath, metallicPath, 0.2f, 0.5f, DetailSize);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetFloat("_Smoothness", 1f); // 맵의 알파 × 이 값
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            material.SetFloat("_Cull", (float)CullMode.Back);
            TestRoomSetup.EnableFlash(material); // HitFlash가 프로퍼티 블록으로 발광을 켠다
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
