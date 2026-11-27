using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 보스 프리팹 · 수치 에셋 · 깃털 프리팹을 만든다(Setup Floor가 부른다). 여러 번 실행해도 결과가 같다.
    /// 수치 에셋은 없을 때만 만들고 값을 덮어쓰지 않는다(프리팹 연결만 고친다). 던전에 보스를 붙이는 것은 `WorldTuning.dungeons[].boss`(데이터).
    /// 까치 모델(예전 까치 보스전, main에서 가져옴)이 `Art/Bosses/Magpie`에 있으면 그것을, 없으면 임시 도형을 Body 아래에 둔다.
    /// 새 보스 = 수치 에셋 하나 + 여기 프리팹 한 벌.
    /// </summary>
    public static class BossSetup
    {
        internal const string TuningPath = "Assets/Game/Data/Bosses/MagpieBossTuning.asset";
        private const string PrefabFolder = "Assets/Game/Prefabs/Bosses";
        private const string ArtFolder = "Assets/Game/Art/Bosses";
        private const string WarningMaterialPath = "Assets/Game/Art/Warning.mat";
        internal const string PrefabPath = PrefabFolder + "/Boss_Magpie.prefab";
        internal const string FeatherPrefabPath = PrefabFolder + "/Boss_Magpie_Feather.prefab";

        // 예전 까치 보스전 에셋(main)을 옮겨 둘 자리 — 없으면 임시 도형
        private const string ModelFolder = ArtFolder + "/Magpie";
        private const string BodyModelPath = ModelFolder + "/GGachi.fbx";             // Humanoid(Mixamo 리그)
        private const string WingsModelPath = ModelFolder + "/GGachi_Wings.fbx";      // Generic, 윗가슴 뼈에 붙음
        private const string BodyControllerPath = ModelFolder + "/Animation/GGachi.controller";
        private const string WingsControllerPath = ModelFolder + "/Animation/GGachi_Wings.controller";
        private const string FeatherModelPath = ModelFolder + "/Feather/Feather_Diag0/Feather_Diag0.fbx";

        /// <summary>임시 도형 크기(m): 몸통 가운데 높이 · 몸 길이 · 날개 폭. 예전 까치 크기(배율 8, 캐릭터 배율 2.75)에 맞춤.</summary>
        private const float ShapeHoverHeight = 1.6f;
        private const float ShapeLength = 2.4f;
        private const float ShapeWingspan = 4.4f;
        private const float FeatherLength = 0.8f; // 예전 2.2 m ÷ 2.75

        public static BossTuning Run()
        {
            EnsureFolder("Assets/Game/Data", "Bosses");
            EnsureFolder("Assets/Game/Prefabs", "Bosses");
            EnsureFolder("Assets/Game/Art", "Bosses");

            BossTuning tuning = LoadOrCreateTuning();
            Material warning = TestRoomSetup.LoadOrCreateUnlit(WarningMaterialPath, new Color(0.8f, 0.15f, 0.1f));
            Material black = TestRoomSetup.LoadOrCreateLit($"{ArtFolder}/Magpie_Black.mat", new Color(0.06f, 0.06f, 0.08f));
            Material white = TestRoomSetup.LoadOrCreateLit($"{ArtFolder}/Magpie_White.mat", new Color(0.92f, 0.92f, 0.9f));
            Material blue = TestRoomSetup.LoadOrCreateLit($"{ArtFolder}/Magpie_Blue.mat", new Color(0.12f, 0.2f, 0.45f));

            FeatherProjectile feather = BuildFeather(black, white);
            Boss boss = BuildBoss(tuning, warning, black, white, blue);

            tuning.prefab = boss;
            tuning.featherPrefab = feather;
            tuning.intro = BossIntroSetup.Run(); // 등장 연출 수치 · 타임라인(예전 까치 보스전 그대로) — 프리팹처럼 연결만 고친다(값은 그대로)

            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BossSetup] 보스 프리팹 {PrefabPath} · 수치 {TuningPath} · 모델 {(HasModel ? "까치 VARCO" : "임시 도형(모델 없음: " + ModelFolder + ")")}");
            return tuning;
        }

        private static bool HasModel =>
            AssetDatabase.LoadAssetAtPath<GameObject>(BodyModelPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(WingsModelPath) != null;

        private static BossTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BossTuning>(TuningPath);
            if (tuning != null) return tuning;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BossTuning>(), TuningPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<BossTuning>(TuningPath);
        }

        private static Boss BuildBoss(BossTuning tuning, Material warning, Material black, Material white, Material blue)
        {
            var root = new GameObject("Boss_Magpie");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);

            Animator bodyAnimator = null, wingAnimator = null;
            float height;
            if (HasModel)
            {
                (bodyAnimator, wingAnimator) = AttachModel(body.transform, tuning.modelScale);
                Bounds bounds = RendererBounds(body);
                height = Mathf.Max(1f, bounds.max.y);
            }
            else
            {
                BuildShape(body.transform, black, white, blue);
                height = ShapeHoverHeight + 1.2f;
            }

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = tuning.bodyRadius;
            capsule.height = Mathf.Max(height, tuning.bodyRadius * 2f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true; // 물리에 밀리지 않고 Boss가 직접 옮긴다
            rigidbody.useGravity = false;

            var health = root.AddComponent<Health>();
            var flash = root.AddComponent<HitFlash>();
            GameObject lane = TestRoomSetup.FlatMarker(root.transform, "LaneWarning", PrimitiveType.Cube, Vector3.zero, new Vector3(1f, 0.005f, 1f), warning);
            GameObject slam = TestRoomSetup.FlatMarker(root.transform, "SlamWarning", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1f, 0.005f, 1f), warning);

            var boss = root.AddComponent<Boss>();
            var fields = new SerializedObject(boss);
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            fields.FindProperty("health").objectReferenceValue = health;
            fields.FindProperty("flash").objectReferenceValue = flash;
            fields.FindProperty("body").objectReferenceValue = body;
            fields.FindProperty("laneWarning").objectReferenceValue = lane;
            fields.FindProperty("slamWarning").objectReferenceValue = slam;
            fields.FindProperty("animator").objectReferenceValue = bodyAnimator;
            fields.FindProperty("wingAnimator").objectReferenceValue = wingAnimator;
            fields.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Boss>();
        }

        /// <summary>
        /// 예전 까치 보스전 모델: 몸통 GGachi(Humanoid) + 날개 GGachi_Wings(Generic, 윗가슴 뼈 자식).
        /// 두 FBX는 Blender에서 같은 좌표로 내보냈으므로 같은 자리 · 크기로 놓은 뒤 월드 자리를 지킨 채 가슴 뼈로 옮긴다(예전 BossFightSetup 방식).
        /// </summary>
        private static (Animator body, Animator wings) AttachModel(Transform parent, float scale)
        {
            var bodyModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BodyModelPath));
            bodyModel.name = "GGachi_Body";
            bodyModel.transform.SetParent(parent, false);
            bodyModel.transform.localScale = Vector3.one * scale;
            Animator bodyAnimator = bodyModel.GetComponent<Animator>();
            if (bodyAnimator == null) bodyAnimator = bodyModel.AddComponent<Animator>();
            bodyAnimator.avatar = AssetDatabase.LoadAllAssetsAtPath(BodyModelPath).OfType<Avatar>().FirstOrDefault();
            bodyAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BodyControllerPath);
            bodyAnimator.applyRootMotion = false;

            var wingsModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WingsModelPath));
            wingsModel.name = "GGachi_Wings";
            wingsModel.transform.SetParent(parent, false);
            wingsModel.transform.localScale = Vector3.one * scale;
            Transform chest = bodyAnimator.avatar != null && bodyAnimator.avatar.isHuman
                ? (bodyAnimator.GetBoneTransform(HumanBodyBones.UpperChest) ?? bodyAnimator.GetBoneTransform(HumanBodyBones.Chest))
                : null;
            if (chest != null) wingsModel.transform.SetParent(chest, true);
            Animator wingAnimator = wingsModel.GetComponent<Animator>();
            if (wingAnimator == null) wingAnimator = wingsModel.AddComponent<Animator>();
            wingAnimator.avatar = null;
            wingAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WingsControllerPath);
            wingAnimator.applyRootMotion = false;
            foreach (Collider collider in parent.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            return (bodyAnimator, wingAnimator);
        }

        /// <summary>임시 도형 까치: 검은 몸 · 흰 배 · 검푸른 날개 · 긴 꼬리, 바닥에서 떠 있다. 앞은 +z.</summary>
        private static void BuildShape(Transform parent, Material black, Material white, Material blue)
        {
            float y = ShapeHoverHeight;
            Shape(PrimitiveType.Capsule, "Torso", parent, new Vector3(0f, y, 0f), Quaternion.Euler(80f, 0f, 0f), new Vector3(1.1f, ShapeLength * 0.5f, 1.1f), black);
            Shape(PrimitiveType.Sphere, "Belly", parent, new Vector3(0f, y - 0.25f, 0.15f), Quaternion.identity, new Vector3(0.9f, 0.8f, 1.3f), white);
            Shape(PrimitiveType.Sphere, "Head", parent, new Vector3(0f, y + 0.55f, ShapeLength * 0.45f), Quaternion.identity, Vector3.one * 0.75f, black);
            Shape(PrimitiveType.Cube, "Beak", parent, new Vector3(0f, y + 0.5f, ShapeLength * 0.45f + 0.5f), Quaternion.Euler(10f, 0f, 45f), new Vector3(0.14f, 0.14f, 0.45f), black);
            Shape(PrimitiveType.Cube, "Tail", parent, new Vector3(0f, y + 0.1f, -ShapeLength * 0.75f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.35f, 0.08f, 1.6f), blue);
            for (int side = -1; side <= 1; side += 2)
            {
                float half = ShapeWingspan * 0.5f;
                Shape(PrimitiveType.Cube, side < 0 ? "WingL" : "WingR", parent, new Vector3(side * (half * 0.5f + 0.3f), y + 0.25f, -0.1f),
                    Quaternion.Euler(0f, 0f, side * -15f), new Vector3(half, 0.08f, 1.1f), blue);
                Shape(PrimitiveType.Cube, side < 0 ? "WingTipL" : "WingTipR", parent, new Vector3(side * (half + 0.15f), y + 0.65f, -0.25f),
                    Quaternion.Euler(0f, 0f, side * -15f), new Vector3(0.6f, 0.07f, 0.8f), white);
                Shape(PrimitiveType.Cube, side < 0 ? "LegL" : "LegR", parent, new Vector3(side * 0.25f, y - 0.95f, 0.1f), Quaternion.identity, new Vector3(0.09f, 0.7f, 0.09f), black);
            }
        }

        private static FeatherProjectile BuildFeather(Material black, Material white)
        {
            var root = new GameObject("Boss_Magpie_Feather");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FeatherModelPath);
            if (model != null)
            {
                // 예전 깃털 모델: 대각선으로 놓인 판 → 길이를 진행 방향(+z), 판을 눌히고(법선 +y), 깃털 끝을 앞으로(예전 BossFightSetup 방식)
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(visual.transform, false);
                OrientFeather(visual.transform, instance.transform);
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var featherMaterial = AssetDatabase.LoadAssetAtPath<Material>(ModelFolder + "/Feather/Feather_Diag0/Feather_Mat.mat");
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off; // 작은 투사체 여러 개라 그림자 생략
                    if (featherMaterial != null) renderer.sharedMaterial = featherMaterial;
                }
            }
            else
            {
                Shape(PrimitiveType.Cube, "Vane", root.transform, Vector3.zero, Quaternion.identity, new Vector3(0.16f, 0.03f, FeatherLength), black)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Shape(PrimitiveType.Cube, "Tip", root.transform, new Vector3(0f, 0f, FeatherLength * 0.42f), Quaternion.identity, new Vector3(0.18f, 0.035f, FeatherLength * 0.18f), white)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            root.AddComponent<FeatherProjectile>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FeatherPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<FeatherProjectile>();
        }

        /// <summary>
        /// 깃털 모델을 눌힌다: 정점이 가장 얇게 퍼진 축 = 판의 법선(위), 판 안에서 가장 길게 퍼진 방향 = 길이(앞).
        /// 끝에서 12 ~ 25% 구간 폭을 비교해 넓은 쪽(깃털 끝)이 앞, 깃대가 뒤.
        /// </summary>
        private static void OrientFeather(Transform visual, Transform model)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Matrix4x4 toVisual = visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices) points.Add(toVisual.MultiplyPoint3x4(vertex));
            }
            if (points.Count == 0) return;
            var bounds = new Bounds(points[0], Vector3.zero);
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in points) { bounds.Encapsulate(p); center += p; }
            center /= points.Count;
            Vector3 size = bounds.size;
            Vector3 up = Vector3.zero;
            up[size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2] = 1f;

            Vector3 a = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 c = Vector3.Cross(up, a);
            float xx = 0f, xy = 0f, yy = 0f;
            foreach (Vector3 p in points)
            {
                float x = Vector3.Dot(p - center, a), y = Vector3.Dot(p - center, c);
                xx += x * x; xy += x * y; yy += y * y;
            }
            float angle = 0.5f * Mathf.Atan2(2f * xy, xx - yy);
            Vector3 along = (a * Mathf.Cos(angle) + c * Mathf.Sin(angle)).normalized;
            Vector3 side = Vector3.Cross(up, along);

            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Vector3 p in points) { float t = Vector3.Dot(p - center, along); lo = Mathf.Min(lo, t); hi = Mathf.Max(hi, t); }
            float length = hi - lo;
            float widthLo = 0f, widthHi = 0f;
            foreach (Vector3 p in points)
            {
                float t = (Vector3.Dot(p - center, along) - lo) / length;
                float w = Mathf.Abs(Vector3.Dot(p - center, side));
                if (t > 0.12f && t < 0.25f) widthLo = Mathf.Max(widthLo, w);
                else if (t > 0.75f && t < 0.88f) widthHi = Mathf.Max(widthHi, w);
            }
            Vector3 tip = widthHi >= widthLo ? along : -along;
            Vector3 middle = center + along * ((lo + hi) * 0.5f);

            Quaternion toForward = Quaternion.Inverse(Quaternion.LookRotation(tip, up));
            float scale = FeatherLength / length;
            model.localPosition = toForward * (-middle * scale);
            model.localRotation = toForward * model.localRotation;
            model.localScale *= scale;
        }

        private static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localRotation = rotation;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }

        private static Bounds RendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
