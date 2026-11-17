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
    /// 모양은 임시 도형(Mutant = 네 발로 낮고 긴 몸, E 개체 = 세운 캡슐) — VARCO 모델이 오면 프리팹의 <c>Body</c> 아래만 바꾼다.
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

        private struct Built
        {
            public GameObject root;
            public GameObject body;
            public GameObject spawnWarning;
            public float height;
            public float length;
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
            Built built = BuildShape(EnemySpecies.Badger, grade);
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
            Built built = BuildShape(EnemySpecies.Boar, grade);
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
            Built built = BuildShape(EnemySpecies.Hare, grade);
            GameObject landing = TestRoomSetup.FlatMarker(built.root.transform, "LandingWarning", PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(1f, 0.005f, 1f), warning);

            SerializedObject fields = Wire<Hare>(built, grade, tuning, warning);
            fields.FindProperty("landingWarning").objectReferenceValue = landing;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return Save(built, EnemySpecies.Hare, grade);
        }

        private static GameObject BuildToad(ToadTuning tuning, EnemyGrade grade, Material warning, ToadSpit spit, PoisonPuddle puddle)
        {
            Built built = BuildShape(EnemySpecies.Toad, grade);
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
        private static Built BuildShape(EnemySpecies species, EnemyGrade grade)
        {
            var (_, color, mutant, evolved) = LookOf(species);
            bool upright = grade == EnemyGrade.Evolved;
            string label = upright ? "E" : "Mutant";
            Color tint = upright ? Color.Lerp(color, EvolvedTint, 0.3f) : color;
            Material material = TestRoomSetup.LoadOrCreateLit($"{ArtFolder}/{species}_{label}.mat", tint);
            TestRoomSetup.EnableFlash(material);

            var root = new GameObject($"{species}_{label}");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);

            float height, length, radius;
            var capsule = root.AddComponent<CapsuleCollider>();
            if (upright)
            {
                float d = evolved.x, h = evolved.y;
                height = h;
                length = d;
                radius = d * 0.5f;
                Primitive(PrimitiveType.Capsule, "Torso", body.transform, new Vector3(0f, h * 0.55f, 0f), new Vector3(d, h * 0.35f, d), material);
                Primitive(PrimitiveType.Sphere, "Head", body.transform, new Vector3(0f, h - d * 0.3f, 0f), Vector3.one * d * 0.6f, material);
                Primitive(PrimitiveType.Cube, "Snout", body.transform, new Vector3(0f, h - d * 0.32f, d * 0.32f), new Vector3(d * 0.3f, d * 0.18f, d * 0.3f), material);
                for (int side = -1; side <= 1; side += 2)
                {
                    Primitive(PrimitiveType.Cube, side < 0 ? "LegL" : "LegR", body.transform,
                        new Vector3(side * d * 0.22f, h * 0.16f, 0f), new Vector3(d * 0.22f, h * 0.32f, d * 0.22f), material);
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

            return new Built { root = root, body = body, spawnWarning = spawnWarning, height = height, length = length };
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
