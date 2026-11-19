using Game.Rules;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 숲 적 4종의 수치 에셋(`Data/Enemies/*Tuning.asset`) · 프리팹(`Prefabs/Enemies/{종}_Mutant|E.prefab`) · 구성 표(`Data/EnemyRoster.asset`)를 만든다.
    /// `TestRoomSetup`이 부른다. 여러 번 실행해도 결과가 같고, 수치 에셋 · 구성 표의 값은 없을 때만 만들고 덮어쓰지 않는다(구성 표의 프리팹 칸만 채운다).
    /// 모양은 VARCO 모델(<see cref="Models"/> 표, Art/Enemies/Models)을 프리팹의 <c>Body/Model</c>로 넣는다.
    /// Mutant(네 발) = 네 발 리깅 모델(Models/Rigged/{파일}_Rigged, Blender Generic 리그) + Animator, E 개체(두 발) = 리깅 모델(Models/Rigged, Mixamo) + Animator —
    /// 동작 · 컨트롤러는 <see cref="EnemyAnimationSetup"/>. 리깅 파일이 없으면 정적 모델로 만든다(움직임은 Enemy의 몸 기울이기).
    /// 모델 파일 · 텍스처가 없으면 예전 임시 도형(Mutant = 네 발로 낮고 긴 몸, E 개체 = 세운 캡슐)으로 만든다.
    /// 충돌체 · NavMeshAgent 크기는 모델과 상관없이 <see cref="Looks"/> 표에서 정해진다.
    /// </summary>
    public static class EnemySetup
    {
        internal const string RosterPath = "Assets/Game/Data/EnemyRoster.asset";
        private const string TuningFolder = "Assets/Game/Data/Enemies";
        private const string PrefabFolder = "Assets/Game/Prefabs/Enemies";
        private const string ArtFolder = "Assets/Game/Art/Enemies";
        private const string WarningMaterialPath = "Assets/Game/Art/Warning.mat";

        /// <summary>종마다 임시 모양: 색, Mutant(폭 · 몸 높이 · 길이), E 개체(지름 · 키). 새 종 = 한 줄.</summary>
        private static readonly (EnemySpecies species, Color color, Vector3 mutant, Vector2 evolved)[] Looks =
        {
            (EnemySpecies.Badger, new Color(0.33f, 0.33f, 0.36f), new Vector3(0.6f, 0.45f, 1.2f), new Vector2(0.7f, 1.7f)),
            (EnemySpecies.Boar, new Color(0.42f, 0.28f, 0.18f), new Vector3(1.0f, 0.8f, 1.9f), new Vector2(1.1f, 2.1f)),
            (EnemySpecies.Hare, new Color(0.72f, 0.62f, 0.48f), new Vector3(0.35f, 0.35f, 0.7f), new Vector2(0.45f, 1.1f)),
            (EnemySpecies.Toad, new Color(0.32f, 0.48f, 0.22f), new Vector3(1.0f, 0.45f, 0.9f), new Vector2(0.9f, 1.4f)),
        };

        /// <summary>E 개체 색을 이 색 쪽으로 조금 당겨 Mutant와 구별한다.</summary>
        private static readonly Color EvolvedTint = new Color(0.65f, 0.2f, 0.2f);

        private const string ModelFolder = ArtFolder + "/Models";
        private const string ModelMaterialFolder = ModelFolder + "/Materials";
        private const int ModelAlbedoSize = 1024;
        private const int ModelDetailSize = 1024; // 노멀 · 금속/매끄러움
        private const float ModelMetallicScale = 0.2f; // 털 · 가죽이라 금속감은 거의 뺀다
        private const float ModelSmoothnessScale = 0.5f;

        /// <summary>
        /// VARCO 정적 모델 표: 종 · 모습 → 파일(Art/Enemies/Models/{file}.fbx, 텍스처는 {file}_tex), 목표 크기, 정면 보정(Y 회전, 도), 위치 보정.
        /// 목표 크기: Mutant = 앞뒤(z) 길이(충돌 캡슐 길이와 같게), E 개체 = 키(충돌 캡슐 높이와 같게, 토끼는 귀만큼 더).
        /// 가져온 모델은 모두 +z가 앞이라 회전 보정은 0. 위치는 바닥 가운데를 발밑 피벗에 자동으로 맞춘 뒤 offset을 더한다.
        /// </summary>
        private static readonly (EnemySpecies species, EnemyGrade grade, string file, float size, float yaw, Vector3 offset)[] Models =
        {
            (EnemySpecies.Badger, EnemyGrade.Mutant, "Badger_Mutant", 1.38f, 0f, Vector3.zero),
            (EnemySpecies.Boar, EnemyGrade.Mutant, "Boar_Mutant", 2.22f, 0f, Vector3.zero),
            (EnemySpecies.Hare, EnemyGrade.Mutant, "Hare_Mutant", 0.84f, 0f, Vector3.zero),
            (EnemySpecies.Toad, EnemyGrade.Mutant, "Toad_Mutant", 1.08f, 0f, Vector3.zero),
            (EnemySpecies.Badger, EnemyGrade.Evolved, "Badger_E", 1.7f, 0f, Vector3.zero),
            (EnemySpecies.Boar, EnemyGrade.Evolved, "Boar_E", 2.1f, 0f, Vector3.zero),
            (EnemySpecies.Hare, EnemyGrade.Evolved, "Hare_E", 1.25f, 0f, Vector3.zero),
            (EnemySpecies.Toad, EnemyGrade.Evolved, "Toad_E", 1.4f, 0f, Vector3.zero),
        };

        private struct Built
        {
            public GameObject root;
            public GameObject body;
            public GameObject spawnWarning;
            public float height;
            public float length;
            /// <summary>E 개체 리깅 모델일 때만(Animator · 머리 뼈 · 입 앞). 정적 모델 · 도형이면 비어 있다.</summary>
            public EnemyAnimationSetup.Rig rig;
        }

        public static EnemyRoster Run()
        {
            EnsureFolder("Assets/Game/Data", "Enemies");
            EnsureFolder("Assets/Game/Prefabs", "Enemies");
            EnsureFolder("Assets/Game/Art", "Enemies");

            Material warning = TestRoomSetup.LoadOrCreateUnlit(WarningMaterialPath, new Color(0.8f, 0.15f, 0.1f));
            Material stun = TestRoomSetup.LoadOrCreateUnlit($"{ArtFolder}/Stun.mat", new Color(1f, 0.85f, 0.2f));
            Material poison = TestRoomSetup.LoadOrCreateUnlit($"{ArtFolder}/Poison.mat", new Color(0.35f, 0.75f, 0.15f));
            Material spitMaterial = TestRoomSetup.LoadOrCreateUnlit($"{ArtFolder}/Spit.mat", new Color(0.55f, 0.95f, 0.2f));

            var badger = LoadOrCreate<BadgerTuning>("BadgerTuning");
            var boar = LoadOrCreate<BoarTuning>("BoarTuning");
            var hare = LoadOrCreate<HareTuning>("HareTuning");
            var toad = LoadOrCreate<ToadTuning>("ToadTuning");
            // E 개체 동작: 표의 구간대로 클립 자르기(수치 에셋의 시간에 맞춘 컨트롤러는 모델을 넣을 때 만든다)
            EnemyAnimationSetup.ImportClips((EnemySpecies.Badger, badger), (EnemySpecies.Boar, boar), (EnemySpecies.Hare, hare), (EnemySpecies.Toad, toad));
            PoisonPuddle puddle = BuildPuddle(poison);
            ToadSpit spit = BuildSpit(spitMaterial);

            EnemyRoster roster = LoadOrCreateRoster();
            foreach (EnemyGrade grade in new[] { EnemyGrade.Mutant, EnemyGrade.Evolved })
            {
                SetPrefab(roster, EnemySpecies.Badger, grade, BuildBadger(badger, grade, warning));
                SetPrefab(roster, EnemySpecies.Boar, grade, BuildBoar(boar, grade, warning, stun));
                SetPrefab(roster, EnemySpecies.Hare, grade, BuildHare(hare, grade, warning));
                SetPrefab(roster, EnemySpecies.Toad, grade, BuildToad(toad, grade, warning, spit, puddle));
            }
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemySetup] 숲 적 4종 프리팹 8개 · 구성 표: " + RosterPath);
            return roster;
        }

        internal static GameObject LoadPrefab(EnemySpecies species, EnemyGrade grade)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(species, grade));
        }

        private static string PathFor(EnemySpecies species, EnemyGrade grade)
        {
            return $"{PrefabFolder}/{species}_{(grade == EnemyGrade.Evolved ? "E" : "Mutant")}.prefab";
        }

        // ---------- 종마다 ----------

        private static GameObject BuildBadger(BadgerTuning tuning, EnemyGrade grade, Material warning)
        {
            Built built = BuildShape(EnemySpecies.Badger, grade, tuning);
            float halfAngle = Mathf.Min(tuning.hitAngle, 180f) * 0.5f * Mathf.Deg2Rad;
            float width = 2f * tuning.hitRange * Mathf.Sin(halfAngle);
            GameObject attack = TestRoomSetup.FlatMarker(built.root.transform, "AttackWarning", PrimitiveType.Cube,
                new Vector3(0f, 0f, tuning.hitRange * 0.5f), new Vector3(width, 0.005f, tuning.hitRange), warning);

            SerializedObject fields = Wire<Badger>(built, grade, tuning, warning);
            fields.FindProperty("attackWarning").objectReferenceValue = attack;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return Save(built, EnemySpecies.Badger, grade);
        }

        private static GameObject BuildBoar(BoarTuning tuning, EnemyGrade grade, Material warning, Material stun)
        {
            Built built = BuildShape(EnemySpecies.Boar, grade, tuning);
            Transform root = built.root.transform;
            GameObject lane = TestRoomSetup.FlatMarker(root, "LaneWarning", PrimitiveType.Cube,
                new Vector3(0f, 0f, tuning.chargeDistance * 0.5f), new Vector3(tuning.hitRadius * 2f, 0.005f, tuning.chargeDistance), warning);
            GameObject slam = TestRoomSetup.FlatMarker(root, "SlamWarning", PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(tuning.slamRadius * 2f, 0.005f, tuning.slamRadius * 2f), warning);

            var mark = new GameObject("StunMark");
            mark.transform.SetParent(root, false);
            mark.transform.localPosition = new Vector3(0f, built.height + 0.35f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                GameObject star = Primitive(PrimitiveType.Sphere, $"Star{i + 1}", mark.transform,
                    new Vector3(Mathf.Cos(angle) * 0.4f, 0f, Mathf.Sin(angle) * 0.4f), Vector3.one * 0.18f, stun);
                star.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            SerializedObject fields = Wire<Boar>(built, grade, tuning, warning);
            fields.FindProperty("laneWarning").objectReferenceValue = lane;
            fields.FindProperty("slamWarning").objectReferenceValue = slam;
            fields.FindProperty("stunMark").objectReferenceValue = mark;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return Save(built, EnemySpecies.Boar, grade);
        }

        private static GameObject BuildHare(HareTuning tuning, EnemyGrade grade, Material warning)
        {
            Built built = BuildShape(EnemySpecies.Hare, grade, tuning);
            GameObject landing = TestRoomSetup.FlatMarker(built.root.transform, "LandingWarning", PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(1f, 0.005f, 1f), warning);

            SerializedObject fields = Wire<Hare>(built, grade, tuning, warning);
            fields.FindProperty("landingWarning").objectReferenceValue = landing;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return Save(built, EnemySpecies.Hare, grade);
        }

        private static GameObject BuildToad(ToadTuning tuning, EnemyGrade grade, Material warning, ToadSpit spit, PoisonPuddle puddle)
        {
            Built built = BuildShape(EnemySpecies.Toad, grade, tuning);
            Transform root = built.root.transform;
            int count = Mathf.Max(1, tuning.fanShots);
            var targets = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                targets[i] = TestRoomSetup.FlatMarker(root, $"TargetWarning{i + 1}", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1f, 0.005f, 1f), warning);
            }
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(built.body.transform, false);
            muzzle.localPosition = new Vector3(0f, built.height * 0.75f, built.length * 0.5f);
            if (built.rig.head != null)
            {
                // 리깅 모델: 입 앞에 두고 머리 뼈에 붙인다(내지르는 동작을 따라간다)
                muzzle.position = built.body.transform.TransformPoint(built.rig.mouth);
                muzzle.SetParent(built.rig.head, true);
            }

            SerializedObject fields = Wire<Toad>(built, grade, tuning, warning);
            fields.FindProperty("spitPrefab").objectReferenceValue = spit;
            fields.FindProperty("puddlePrefab").objectReferenceValue = puddle;
            fields.FindProperty("muzzle").objectReferenceValue = muzzle;
            SerializedProperty array = fields.FindProperty("targetWarnings");
            array.arraySize = count;
            for (int i = 0; i < count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            return Save(built, EnemySpecies.Toad, grade);
        }

        private static PoisonPuddle BuildPuddle(Material material)
        {
            GameObject root = Primitive(PrimitiveType.Cylinder, "PoisonPuddle", null, Vector3.zero, new Vector3(1f, 0.01f, 1f), material);
            root.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            root.AddComponent<PoisonPuddle>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/PoisonPuddle.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<PoisonPuddle>();
        }

        private static ToadSpit BuildSpit(Material material)
        {
            GameObject root = Primitive(PrimitiveType.Sphere, "ToadSpit", null, Vector3.zero, Vector3.one * 0.35f, material);
            root.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            root.AddComponent<ToadSpit>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ToadSpit.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ToadSpit>();
        }

        // ---------- 공통 ----------

        /// <summary>뿌리(충돌 · NavMeshAgent) + Body(임시 도형) + 나타나기 예고. 피벗은 발밑, 앞은 +z.</summary>
        /// <summary>뿌리(충돌 · NavMeshAgent) + Body(VARCO 모델, 없으면 임시 도형) + 나타나기 예고. 피벗은 발밑, 앞은 +z.</summary>
        private static Built BuildShape(EnemySpecies species, EnemyGrade grade, EnemyTuning tuning)
        {
            var (_, color, mutant, evolved) = LookOf(species);
            bool upright = grade == EnemyGrade.Evolved;
            string label = upright ? "E" : "Mutant";

            var root = new GameObject($"{species}_{label}");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            bool hasModel = TryBuildModel(species, grade, tuning, body.transform, out EnemyAnimationSetup.Rig rig);
            Material material = null;
            if (!hasModel)
            {
                Debug.LogWarning($"[EnemySetup] {species}_{label}: VARCO 모델 또는 텍스처가 없어 임시 도형으로 만듭니다({ModelFolder} 확인).");
                Color tint = upright ? Color.Lerp(color, EvolvedTint, 0.3f) : color;
                material = TestRoomSetup.LoadOrCreateLit($"{ArtFolder}/{species}_{label}.mat", tint);
                TestRoomSetup.EnableFlash(material);
            }

            float height, length, radius;
            var capsule = root.AddComponent<CapsuleCollider>();
            if (upright)
            {
                float d = evolved.x, h = evolved.y;
                height = h;
                length = d;
                radius = d * 0.5f;
                if (!hasModel)
                {
                    Primitive(PrimitiveType.Capsule, "Torso", body.transform, new Vector3(0f, h * 0.55f, 0f), new Vector3(d, h * 0.35f, d), material);
                    Primitive(PrimitiveType.Sphere, "Head", body.transform, new Vector3(0f, h - d * 0.3f, 0f), Vector3.one * d * 0.6f, material);
                    Primitive(PrimitiveType.Cube, "Snout", body.transform, new Vector3(0f, h - d * 0.32f, d * 0.32f), new Vector3(d * 0.3f, d * 0.18f, d * 0.3f), material);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Primitive(PrimitiveType.Cube, side < 0 ? "LegL" : "LegR", body.transform,
                            new Vector3(side * d * 0.22f, h * 0.16f, 0f), new Vector3(d * 0.22f, h * 0.32f, d * 0.22f), material);
                    }
                }
                capsule.direction = 1;
                capsule.height = h;
                capsule.radius = radius;
                capsule.center = new Vector3(0f, h * 0.5f, 0f);
            }
            else
            {
                float w = mutant.x, bodyHeight = mutant.y, l = mutant.z;
                float leg = bodyHeight * 0.45f;
                height = leg + bodyHeight;
                length = l;
                radius = Mathf.Max(w, height) * 0.5f;
                if (!hasModel)
                {
                    float torsoY = leg + bodyHeight * 0.5f;
                    // 캡슐을 눕혀 앞뒤(z)로 길게: 회전한 캡슐의 길이 = 2 × 크기 y
                    GameObject torso = Primitive(PrimitiveType.Capsule, "Torso", body.transform, new Vector3(0f, torsoY, 0f), new Vector3(w, l * 0.5f, bodyHeight), material);
                    torso.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Primitive(PrimitiveType.Sphere, "Head", body.transform, new Vector3(0f, leg + bodyHeight * 0.75f, l * 0.45f), Vector3.one * bodyHeight * 0.7f, material);
                    Primitive(PrimitiveType.Cube, "Snout", body.transform, new Vector3(0f, leg + bodyHeight * 0.65f, l * 0.45f + bodyHeight * 0.38f),
                        new Vector3(w * 0.3f, bodyHeight * 0.25f, bodyHeight * 0.35f), material);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0 ? -1f : 1f) * w * 0.3f;
                        float z = (i < 2 ? 1f : -1f) * l * 0.3f;
                        Primitive(PrimitiveType.Cube, $"Leg{i + 1}", body.transform, new Vector3(x, leg * 0.5f, z), new Vector3(w * 0.18f, leg, w * 0.18f), material);
                    }
                }
                capsule.direction = 2;
                capsule.radius = Mathf.Min(radius, l * 0.5f);
                capsule.height = l + bodyHeight * 0.4f;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);
            }

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = Mathf.Clamp(Mathf.Max(radius, length * 0.35f), 0.25f, 0.6f);
            agent.height = height;
            agent.baseOffset = 0f;
            agent.enabled = false; // 나타날 때 켠다. NavMesh를 굽기 전에 켜지면 경고가 난다

            float ring = Mathf.Max(1.2f, Mathf.Max(length, radius * 2f) * 1.3f);
            GameObject spawnWarning = TestRoomSetup.FlatMarker(root.transform, "SpawnWarning", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(ring, 0.005f, ring), AssetDatabase.LoadAssetAtPath<Material>(WarningMaterialPath));

            return new Built { root = root, body = body, spawnWarning = spawnWarning, height = height, length = length, rig = rig };
        }

        /// <summary>
        /// Body 아래에 VARCO 모델을 <c>Model</c>(크기 · 회전 · 위치 보정을 담는 그릇) → 모델 인스턴스로 넣는다.
        /// 모델 · 텍스처가 하나라도 없으면 아무것도 넣지 않고 false. 모델에는 충돌체가 없다(가져오기에서 끈다).
        /// </summary>
        private static bool TryBuildModel(EnemySpecies species, EnemyGrade grade, EnemyTuning tuning, Transform body, out EnemyAnimationSetup.Rig rig)
        {
            rig = default;
            int index = System.Array.FindIndex(Models, m => m.species == species && m.grade == grade);
            if (index < 0) return false;
            var entry = Models[index];
            if (grade == EnemyGrade.Evolved)
            {
                // 두 발: 리깅 모델 + 동작(같은 텍스처 · 재질). 리깅 파일이 없으면 아래 정적 모델로
                Material skin = LoadModelMaterial(entry.file);
                if (skin != null && EnemyAnimationSetup.TryBuild(species, entry.file, entry.size, entry.yaw, entry.offset, skin, tuning, body, out rig)) return true;
                rig = default;
            }
            else
            {
                // 네 발: 리깅 모델(Generic) + 동작. 리깅 파일이 없으면 아래 정적 모델로
                Material skin = LoadModelMaterial(entry.file);
                if (skin != null && EnemyAnimationSetup.TryBuildMutant(species, entry.file, entry.size, entry.yaw, entry.offset, skin, tuning, body, out rig)) return true;
                rig = default;
            }
            string path = $"{ModelFolder}/{entry.file}.fbx";
            if (!VillageHouseArt.ConfigureStaticModel(path)) return false;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Material material = LoadModelMaterial(entry.file);
            if (asset == null || material == null) return false;

            var holder = new GameObject("Model").transform;
            holder.SetParent(body, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(holder.gameObject);
                return false;
            }
            foreach (Renderer r in renderers)
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            // 크기: 돌린 뒤 재서 Mutant는 앞뒤 길이, E 개체는 키를 맞춘다. 피벗: 바닥 가운데를 발밑으로
            holder.localRotation = Quaternion.Euler(0f, entry.yaw, 0f);
            Bounds local = VillageSetup.LocalBounds(body, renderers);
            float measured = grade == EnemyGrade.Evolved ? local.size.y : local.size.z;
            holder.localScale = Vector3.one * (entry.size / Mathf.Max(0.001f, measured));
            local = VillageSetup.LocalBounds(body, renderers);
            holder.localPosition = new Vector3(-local.center.x, -local.min.y, -local.center.z) + entry.offset;
            return true;
        }

        /// <summary>모델 재질(URP Lit: 알베도 · 노멀 · ORM에서 만든 금속/매끄러움, HitFlash용 발광 켬). 텍스처가 없으면 null.</summary>
        private static Material LoadModelMaterial(string file)
        {
            string textureFolder = $"{ModelFolder}/{file}_tex";
            string albedoPath = $"{textureFolder}/{file}_0_Image_0.png";
            string ormPath = $"{textureFolder}/{file}_1_Image_1.png";
            string normalPath = $"{textureFolder}/{file}_2_Image_2.png";
            if (AssetImporter.GetAtPath(albedoPath) == null || AssetImporter.GetAtPath(ormPath) == null || AssetImporter.GetAtPath(normalPath) == null) return null;
            EnsureFolder(ModelFolder, "Materials");

            Texture2D albedo = VillageHouseArt.ConfigureTexture(albedoPath, srgb: true, ModelAlbedoSize, normal: false);
            Texture2D normalMap = VillageHouseArt.ConfigureTexture(normalPath, srgb: false, ModelDetailSize, normal: true);
            // ORM 원본(선형)은 금속 맵을 만드는 데만 쓴다(재질에는 안 들어간다) — 작게 가져온다
            VillageHouseArt.ConfigureTexture(ormPath, srgb: false, 256, normal: false);
            Texture2D metallic = VillageHouseArt.MetallicSmoothness(ormPath, $"{textureFolder}/{file}_MetallicSmoothness.png",
                ModelMetallicScale, ModelSmoothnessScale, ModelDetailSize);

            string path = $"{ModelMaterialFolder}/{file}.mat";
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
            material.enableInstancing = true; // 같은 적 여러 마리
            TestRoomSetup.EnableFlash(material); // HitFlash가 프로퍼티 블록으로 발광을 켠다
            return material;
        }


        private static SerializedObject Wire<T>(Built built, EnemyGrade grade, EnemyTuning tuning, Material warning) where T : Enemy
        {
            var health = built.root.AddComponent<Health>();
            var flash = built.root.AddComponent<HitFlash>();
            var fields = new SerializedObject(built.root.AddComponent<T>());
            fields.FindProperty("grade").enumValueIndex = (int)grade;
            fields.FindProperty("health").objectReferenceValue = health;
            fields.FindProperty("flash").objectReferenceValue = flash;
            fields.FindProperty("body").objectReferenceValue = built.body;
            fields.FindProperty("spawnWarning").objectReferenceValue = built.spawnWarning;
            fields.FindProperty("animator").objectReferenceValue = built.rig.animator;
            fields.FindProperty("tuning").objectReferenceValue = tuning;
            return fields;
        }

        private static GameObject Save(Built built, EnemySpecies species, EnemyGrade grade)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built.root, PathFor(species, grade));
            Object.DestroyImmediate(built.root);
            return prefab;
        }

        private static GameObject Primitive(PrimitiveType shape, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.name = name;
            if (parent != null) part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static (EnemySpecies species, Color color, Vector3 mutant, Vector2 evolved) LookOf(EnemySpecies species)
        {
            foreach (var look in Looks)
            {
                if (look.species == species) return look;
            }
            throw new System.ArgumentException($"모양 표에 {species}가 없습니다.");
        }

        private static void SetPrefab(EnemyRoster roster, EnemySpecies species, EnemyGrade grade, GameObject prefab)
        {
            for (int i = 0; i < roster.species.Length; i++)
            {
                if (roster.species[i].species != species) continue;
                if (grade == EnemyGrade.Evolved) roster.species[i].evolvedPrefab = prefab;
                else roster.species[i].mutantPrefab = prefab;
                return;
            }
            Debug.LogWarning($"[EnemySetup] 구성 표에 {species} 줄이 없어 프리팹을 넣지 못했습니다.");
        }

        private static T LoadOrCreate<T>(string name) where T : ScriptableObject
        {
            string path = $"{TuningFolder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                EditorUtility.SetDirty(asset); // 새로 생긴 필드를 파일에 남긴다
                return asset;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static EnemyRoster LoadOrCreateRoster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(RosterPath);
            if (roster != null) return roster;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<EnemyRoster>(), RosterPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<EnemyRoster>(RosterPath);
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
