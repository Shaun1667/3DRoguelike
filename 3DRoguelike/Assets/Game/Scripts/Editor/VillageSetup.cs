using System.Collections.Generic;
using Game.Rules;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// 월드 가운데 E-07 마을(우호적 · 전투 없음)을 세운다. 값은 <see cref="VillageTuning"/>(Data/VillageTuning.asset), 주민은 <see cref="VillagerSetup"/>.
    /// Tools/Game/Setup World가 월드를 새로 만들 때 이 코드로 지형(평평한 흙바닥) · 나무 치우기 · 마을 · NavMesh를 함께 만든다.
    /// Tools/Game/Setup Village는 열린 World 씬에서 마을만 다시 세운다: 지형 높이 · 바닥을 다시 칠하고, 마을 안 나무를 지우고,
    /// World/Village를 새로 만들고, NavMesh를 같은 에셋에 다시 굽는다. 씬은 저장하지 않는다(Undo로 씬 변경은 되돌릴 수 있다 —
    /// 지형 · NavMesh 에셋 내용은 되돌리기 밖). 건물은 지금 회색 도형이다.
    /// </summary>
    public static class VillageSetup
    {
        public const string TuningPath = "Assets/Game/Data/VillageTuning.asset";
        public const string RootName = "Village";
        private const string ArtFolder = "Assets/Game/Art/Village";

        /// <summary>재질 이름 → 색. 회색 도형 단계라 채도를 낮췄다.</summary>
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            { "Wall", new Color(0.56f, 0.54f, 0.50f) },
            { "WallAlt", new Color(0.50f, 0.49f, 0.47f) },
            { "Roof", new Color(0.36f, 0.34f, 0.32f) },
            { "Dark", new Color(0.10f, 0.09f, 0.09f) },
            { "Window", new Color(0.16f, 0.17f, 0.19f) },
            { "Wood", new Color(0.40f, 0.33f, 0.26f) },
            { "Plank", new Color(0.47f, 0.42f, 0.36f) },
            { "Stone", new Color(0.45f, 0.45f, 0.44f) },
            { "Metal", new Color(0.35f, 0.37f, 0.38f) },
            { "Soil", new Color(0.28f, 0.22f, 0.17f) },
            { "Crop", new Color(0.30f, 0.40f, 0.22f) },
            { "Fence", new Color(0.44f, 0.40f, 0.35f) },
            { "FenceAlt", new Color(0.38f, 0.36f, 0.33f) },
        };

        /// <summary>지형 · 나무가 쓰는 마을 범위(월드 x, z). 마을 안 좌표(x = 길 오른쪽, y = 길 앞)를 월드로 바꾼다.</summary>
        public sealed class Area
        {
            public Vector2 Center;
            public Vector2 Forward;
            public Vector2 Right;
            public float Radius;
            public float FlatFade;
            public float ClearRadius;
            public Vector2 Plaza;
            public float PlazaRadius;
            public float Dirt;

            public float Yaw => Mathf.Atan2(Forward.x, Forward.y) * Mathf.Rad2Deg;
            public Vector2 ToWorld(Vector2 local) => Center + Right * local.x + Forward * local.y;
            public float DistanceTo(float x, float z) => Vector2.Distance(Center, new Vector2(x, z));

            /// <summary>지형 굴곡 무게(0 = 마을 안 평지, 1 = 원래 굴곡).</summary>
            public float FlatWeight(float x, float z) => Smooth01((DistanceTo(x, z) - Radius) / FlatFade);

            /// <summary>마을 바닥 흙 비율(0~1). 광장은 흙만, 나머지는 노이즈 섞은 Dirt. 마을 밖은 0.</summary>
            public float DirtAt(float x, float z)
            {
                float inside = 1f - Smooth01((DistanceTo(x, z) - (Radius - 3f)) / 3f);
                if (inside <= 0f) return 0f;
                float plaza = 1f - Smooth01((Vector2.Distance(Plaza, new Vector2(x, z)) - PlazaRadius) / 2.5f);
                float noise = Mathf.PerlinNoise(x * 0.15f + 700f, z * 0.15f + 700f);
                return Mathf.Max(plaza, inside * Mathf.Clamp01(Dirt + (noise - 0.5f) * 0.6f));
            }

            public bool ClearsTree(float x, float z) => DistanceTo(x, z) < ClearRadius;
        }

        public static VillageTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<VillageTuning>(TuningPath);
            if (tuning != null)
            {
                EditorUtility.SetDirty(tuning);
                return tuning;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<VillageTuning>(), TuningPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<VillageTuning>(TuningPath);
        }

        public static Area AreaOf(VillageTuning village, WorldRoute route)
        {
            (float x, float z) = route.BesideAt(village.routeDistance, village.sideOffset);
            (float dx, float dz) = route.DirectionAt(village.routeDistance);
            var area = new Area
            {
                Center = new Vector2(x, z),
                Forward = new Vector2(dx, dz),
                Right = new Vector2(dz, -dx),
                Radius = village.radius,
                FlatFade = village.flatFade,
                ClearRadius = village.radius + village.treeClearance,
                PlazaRadius = village.plazaRadius,
                Dirt = village.villageDirt,
            };
            area.Plaza = area.ToWorld(village.plazaCenter);
            return area;
        }

        [MenuItem("Tools/Game/Setup Village")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject worldRoot = GameObject.Find("World");
            Terrain terrain = worldRoot != null ? worldRoot.GetComponentInChildren<Terrain>() : null;
            if (scene.path != CurrentRun.WorldScene || worldRoot == null || terrain == null)
            {
                Debug.LogError($"[VillageSetup] {CurrentRun.WorldScene}을 열고 실행하세요(World · Terrain이 있어야 합니다). 월드를 새로 만들려면 Tools/Game/Setup World.");
                return;
            }

            WorldTuning world = WorldSetup.LoadOrCreateTuning();
            WorldRoute route = world.ToRoute();
            VillageTuning village = LoadOrCreateTuning();
            Area area = AreaOf(village, route);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Setup Village terrain");
            Undo.RegisterCompleteObjectUndo(terrain.terrainData.alphamapTextures, "Setup Village terrain");
            Vector3 size = terrain.terrainData.size;
            WorldSetup.FillTerrain(world, route, area, terrain.terrainData, terrain.transform.position.x, terrain.transform.position.z, size.x, size.z, size.y);
            EditorUtility.SetDirty(terrain.terrainData);

            int cleared = 0;
            Transform trees = worldRoot.transform.Find("Trees");
            if (trees != null)
            {
                for (int i = trees.childCount - 1; i >= 0; i--)
                {
                    Transform tree = trees.GetChild(i);
                    if (!area.ClearsTree(tree.position.x, tree.position.z)) continue;
                    Undo.DestroyObjectImmediate(tree.gameObject);
                    cleared++;
                }
            }

            Transform old = worldRoot.transform.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            Transform built = Build(village, area, world, route, terrain, worldRoot.transform, HudSetup.LoadOrCreateFontAsset());
            built.SetSiblingIndex(Mathf.Min(worldRoot.transform.childCount - 1, terrain.transform.GetSiblingIndex() + 1));
            Undo.RegisterCreatedObjectUndo(built.gameObject, "Setup Village");

            WorldSetup.UpdateNavMeshInPlace(worldRoot);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VillageSetup] 마을을 다시 세웠습니다(씬은 저장하지 않음) · 마을 안 나무 {cleared}그루 치움");
        }

        /// <summary>마을 전체(울타리 · 정문 · 집 · 소품 · 모닥불 빛 · 주민)를 parent 아래 Village로 만든다. NavMesh를 굽기 전에 부른다.</summary>
        public static Transform Build(VillageTuning village, Area area, WorldTuning world, WorldRoute route, Terrain terrain, Transform parent, TMP_FontAsset font)
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Game/Art", "Village");
            VillagerSetup.Run(); // 주민 프리팹(털 포함)을 먼저 만든다 — TreeSetup처럼 매번 같은 결과
            CheckPlace(village, area, world, route);
            CheckHouses(village);

            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(Ground(terrain, area.Center), Quaternion.Euler(0f, area.Yaw, 0f));
            var context = new Context { Village = village, Area = area, Terrain = terrain, Root = root };

            BuildFence(context);
            BuildGate(context, font);
            Transform houses = Group(root, "Houses");
            for (int i = 0; i < village.houses.Length; i++) BuildHouse(context, houses, village.houses[i], i);
            Transform props = Group(root, "Props");
            foreach (VillageTuning.Prop prop in village.props) BuildProp(context, props, prop);
            BuildFireLight(context, props);
            BuildResidents(context);
            BuildSafeZone(context);
            Debug.Log($"[VillageSetup] {village.displayName}: 가운데 ({area.Center.x:0.0}, {area.Center.y:0.0}) · 길 거리 {village.routeDistance:0} m · 반지름 {village.radius:0} m · 집 {village.houses.Length}채 · 주민 {village.residents.Length}명");
            return root;
        }

        private sealed class Context
        {
            public VillageTuning Village;
            public Area Area;
            public Terrain Terrain;
            public Transform Root;

            /// <summary>마을 안 자리(x, y)를 땅 위 elevation 높이에, 마을 기준 회전으로 놓는다.</summary>
            public void Place(Transform target, Transform parent, Vector2 local, float elevation, Quaternion localRotation)
            {
                target.SetParent(parent, true);
                Vector3 ground = Ground(Terrain, Area.ToWorld(local));
                target.SetPositionAndRotation(ground + Vector3.up * elevation, Root.rotation * localRotation);
            }
        }

        // ---------- 자리 확인 ----------

        /// <summary>마을 범위에 적 무리 · 던전 입구(오염 지대) · 관문 · 시작점이 겹치거나 울타리가 절벽에 걸리면 경고한다(값은 바꾸지 않는다).</summary>
        private static void CheckPlace(VillageTuning village, Area area, WorldTuning world, WorldRoute route)
        {
            for (int i = 0; i < world.encounters.Length; i++)
            {
                WorldTuning.EncounterSite site = world.encounters[i];
                (float x, float z) = route.BesideAt(site.routeDistance, site.sideOffset);
                float d = area.DistanceTo(x, z);
                if (d < village.radius + world.encounterSpread)
                    Debug.LogWarning($"[VillageSetup] 적 무리 {i + 1}(길 {site.routeDistance:0} m)이 마을 범위와 겹칩니다: 가운데에서 {d:0.0} m(반지름 {village.radius:0} + 퍼짐 {world.encounterSpread:0}). 사용자 결정이 필요합니다.");
                else if (d < village.radius + world.encounterTriggerRange)
                    Debug.Log($"[VillageSetup] 참고: 적 무리 {i + 1}(길 {site.routeDistance:0} m)은 마을 가운데에서 {d:0.0} m — 마을 가장자리가 나타남 거리({world.encounterTriggerRange:0} m) 안이지만, 마을은 안전 지대라 안에 있는 동안은 나타나지 않고 밖으로 나오면 나타납니다.");
            }
            float pollutedReach = world.pollutedRadius + world.pollutedEdgeNoise + world.pollutedFade;
            for (int i = 0; i < world.dungeons.Length; i++)
            {
                (float x, float z) = route.BesideAt(world.dungeons[i].routeDistance, world.dungeons[i].sideOffset);
                float d = area.DistanceTo(x, z);
                if (d < village.radius + pollutedReach)
                    Debug.LogWarning($"[VillageSetup] 던전 {i + 1} 입구 · 오염 지대가 마을 범위와 겹칩니다: {d:0.0} m");
                (float gx, float gz) = route.PointAt(world.GateDistance(i));
                if (area.DistanceTo(gx, gz) < village.radius + world.passLength + world.passBlend)
                    Debug.LogWarning($"[VillageSetup] 관문 {i + 1} 고개가 마을 범위 가까이 있습니다: {area.DistanceTo(gx, gz):0.0} m");
            }
            for (int k = 0; k < 72; k++)
            {
                float a = k * Mathf.PI * 2f / 72f;
                Vector2 p = area.ToWorld(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * village.fenceRadius);
                float d = route.DistanceFrom(p.x, p.y, out float along);
                if (d > world.ValleyHalfWidthAt(along) - 1f)
                {
                    Debug.LogWarning($"[VillageSetup] 울타리가 골짜기 밖(절벽)에 걸립니다: ({p.x:0}, {p.y:0})");
                    break;
                }
            }
        }

        // ---------- 울타리 · 정문 ----------

        /// <summary>둥근 판자 울타리(길 쪽 gateLineX 밖은 자르고) + 길 쪽 곧은 울타리(가운데 정문 구멍).</summary>
        private static void BuildFence(Context c)
        {
            VillageTuning v = c.Village;
            Transform fence = Group(c.Root, "Fence");
            float r = v.fenceRadius;
            float cut = Mathf.Clamp(v.gateLineX / r, -1f, 1f);
            float start = Mathf.Acos(cut); // x = gateLineX 인 각도
            var arc = new List<Vector2>();
            float arcLength = (Mathf.PI * 2f - start * 2f) * r;
            int steps = Mathf.Max(2, Mathf.CeilToInt(arcLength / v.fenceSegment));
            for (int i = 0; i <= steps; i++)
            {
                float a = start + (Mathf.PI * 2f - start * 2f) * i / steps;
                arc.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            float zMax = Mathf.Sqrt(Mathf.Max(0f, r * r - v.gateLineX * v.gateLineX));
            int index = 0;
            index = FenceLine(c, fence, arc, index);
            index = FenceLine(c, fence, Line(new Vector2(v.gateLineX, zMax), new Vector2(v.gateLineX, v.gateHalfWidth), v.fenceSegment), index);
            FenceLine(c, fence, Line(new Vector2(v.gateLineX, -v.gateHalfWidth), new Vector2(v.gateLineX, -zMax), v.fenceSegment), index);
        }

        private static List<Vector2> Line(Vector2 from, Vector2 to, float segment)
        {
            var points = new List<Vector2>();
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / segment));
            for (int i = 0; i <= steps; i++) points.Add(Vector2.Lerp(from, to, (float)i / steps));
            return points;
        }

        /// <summary>점마다 기둥, 점 사이마다 판자 벽(충돌). 높이는 칸마다 조금씩 다르게(고정 해시) — 주워 모은 판자 느낌.</summary>
        private static int FenceLine(Context c, Transform parent, List<Vector2> points, int index)
        {
            VillageTuning v = c.Village;
            Material post = Lit("Wood");
            for (int i = 0; i < points.Count; i++)
            {
                GameObject p = Primitive(PrimitiveType.Cylinder, $"Post{index + i}", post, collide: true);
                p.transform.localScale = new Vector3(0.24f, (v.fenceHeight + 0.35f) * 0.5f, 0.24f);
                c.Place(p.transform, parent, points[i], (v.fenceHeight + 0.35f) * 0.5f, Quaternion.identity);
            }
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 d = b - a;
                float h = v.fenceHeight * (0.88f + 0.12f * Hash(index + i));
                GameObject plank = Primitive(PrimitiveType.Cube, $"Planks{index + i}", Lit(Hash(index + i + 17) > 0.5f ? "Fence" : "FenceAlt"), collide: true);
                plank.transform.localScale = new Vector3(0.12f, h, d.magnitude);
                c.Place(plank.transform, parent, (a + b) * 0.5f, h * 0.5f, Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f));
            }
            return index + points.Count;
        }

        private static void BuildGate(Context c, TMP_FontAsset font)
        {
            VillageTuning v = c.Village;
            Transform gate = Group(c.Root, "Gate");
            Material wood = Lit("Wood");
            float half = v.gateHalfWidth + 0.15f;
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject post = Primitive(PrimitiveType.Cube, side < 0f ? "PostS" : "PostN", wood, collide: true);
                post.transform.localScale = new Vector3(0.4f, 4.2f, 0.4f);
                c.Place(post.transform, gate, new Vector2(v.gateLineX, side * half), 2.1f, Quaternion.identity);
            }
            GameObject beam = Primitive(PrimitiveType.Cube, "Beam", wood, collide: false);
            beam.transform.localScale = new Vector3(0.35f, 0.35f, half * 2f + 0.9f);
            c.Place(beam.transform, gate, new Vector2(v.gateLineX, 0f), 3.9f, Quaternion.identity);
            GameObject sign = Primitive(PrimitiveType.Cube, "Sign", Lit("Plank"), collide: false);
            sign.transform.localScale = new Vector3(0.08f, 0.8f, 3.6f);
            c.Place(sign.transform, gate, new Vector2(v.gateLineX + 0.2f, 0f), 4.5f, Quaternion.identity);
            // 글자는 길(+x) 쪽에서 읽힌다(TextMeshPro는 -z 쪽에서 읽힌다)
            var holder = new GameObject("SignText").transform;
            c.Place(holder, gate, new Vector2(v.gateLineX + 0.3f, 0f), 4.5f, Quaternion.Euler(0f, -90f, 0f));
            WorldSetup.Label(holder, font, v.displayName, Vector3.zero, Quaternion.identity);
            Transform label = holder.GetChild(0);
            label.localScale = Vector3.one * 0.6f;
        }

        // ---------- 집 · 소품 ----------

        /// <summary>
        /// 집 한 채. 앞(+z, 문)은 광장 가운데를 본다(+ turn). model이 houseModels에 있으면 VARCO 모델 + 충돌 상자,
        /// 없거나 모델 파일이 없으면 회색 도형 집(판자 벽 상자 + 맞배지붕 + 문 · 창).
        /// </summary>
        private static void BuildHouse(Context c, Transform parent, VillageTuning.House data, int index)
        {
            Vector2 toPlaza = c.Village.plazaCenter - data.position;
            float yaw = Mathf.Atan2(toPlaza.x, toPlaza.y) * Mathf.Rad2Deg + data.turn;
            var house = new GameObject(string.IsNullOrEmpty(data.name) ? $"House{index + 1}" : data.name).transform;
            c.Place(house, parent, data.position, 0f, Quaternion.Euler(0f, yaw, 0f));
            if (!string.IsNullOrEmpty(data.model))
            {
                if (TryHouseModel(c.Village, data.model, out VillageTuning.HouseModel model) && BuildHouseModel(c, house, model)) return;
                Debug.LogWarning($"[VillageSetup] {house.name}: 집 모델 '{data.model}'을 못 넣어 회색 도형으로 세웁니다(houseModels · {VillageHouseArt.Folder} 확인).");
            }

            float w = data.size.x, d = data.size.y, h = data.wallHeight;
            Child(house, PrimitiveType.Cube, "Walls", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), Quaternion.identity, Lit(index % 2 == 0 ? "Wall" : "WallAlt"), collide: true);
            // 지붕: 부모를 위아래로 눌러 45° 상자를 낮은 맞배로
            var roof = new GameObject("Roof").transform;
            roof.SetParent(house, false);
            roof.localPosition = new Vector3(0f, h, 0f);
            roof.localScale = new Vector3(1f, 0.55f, 1f);
            float s = (d + 0.7f) / Mathf.Sqrt(2f);
            Child(roof, PrimitiveType.Cube, "Gable", Vector3.zero, new Vector3(w + 0.6f, s, s), Quaternion.Euler(45f, 0f, 0f), Lit("Roof"), collide: false);
            Child(house, PrimitiveType.Cube, "Door", new Vector3(w * 0.18f, 0.95f, d * 0.5f + 0.03f), new Vector3(1f, 1.9f, 0.06f), Quaternion.identity, Lit("Dark"), collide: false);
            Child(house, PrimitiveType.Cube, "WindowFront", new Vector3(-w * 0.25f, 1.45f, d * 0.5f + 0.03f), new Vector3(0.9f, 0.6f, 0.06f), Quaternion.identity, Lit("Window"), collide: false);
            Child(house, PrimitiveType.Cube, "WindowSide", new Vector3(w * 0.5f + 0.03f, 1.45f, 0f), new Vector3(0.06f, 0.6f, 0.9f), Quaternion.identity, Lit("Window"), collide: false);
            // 덧댄 판자(생존자 집 손질 자국)
            Child(house, PrimitiveType.Cube, "Patch", new Vector3(-w * 0.5f - 0.03f, h * 0.45f, d * 0.1f), new Vector3(0.06f, h * 0.5f, d * 0.4f), Quaternion.Euler(4f, 0f, 0f), Lit("Plank"), collide: false);
        }

        private static bool TryHouseModel(VillageTuning village, string id, out VillageTuning.HouseModel model)
        {
            foreach (VillageTuning.HouseModel m in village.houseModels)
            {
                if (m.id != id) continue;
                model = m;
                return true;
            }
            model = default;
            return false;
        }

        /// <summary>
        /// house 아래 Model(모델 프리팹 인스턴스를 높이 height로 고르게 키우고 frontYaw만큼 돌리고, 바닥 가운데를 집 피벗에 맞춘 뒤 pivotOffset)
        /// + Collision(상자 충돌, NavMesh 굽기에도 들어간다). 모델 메시에는 충돌을 넣지 않는다.
        /// </summary>
        private static bool BuildHouseModel(Context c, Transform house, VillageTuning.HouseModel model)
        {
            GameObject asset = VillageHouseArt.LoadModel(model);
            Material material = VillageHouseArt.LoadMaterial(c.Village, model);
            if (asset == null || material == null) return false;

            var holder = new GameObject("Model").transform;
            holder.SetParent(house, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            foreach (Renderer r in renderers)
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            // 크기: 가져온 그대로(돌리기 전) 재서 높이를 맞춘다. 피벗: 돌린 뒤 바닥 가운데를 집 피벗으로
            holder.localRotation = Quaternion.Euler(0f, model.frontYaw, 0f);
            Bounds local = LocalBounds(house, renderers);
            float scale = model.height / Mathf.Max(0.001f, local.size.y);
            holder.localScale = Vector3.one * scale;
            local = LocalBounds(house, renderers);
            holder.localPosition = new Vector3(-local.center.x, -local.min.y, -local.center.z) + model.pivotOffset;

            var collision = new GameObject("Collision").transform;
            collision.SetParent(house, false);
            if (model.colliders == null) return true;
            for (int i = 0; i < model.colliders.Length; i++)
            {
                VillageTuning.HouseBox box = model.colliders[i];
                var block = new GameObject($"Box{i}").transform;
                block.SetParent(collision, false);
                block.SetLocalPositionAndRotation(box.center, Quaternion.Euler(0f, box.yaw, 0f));
                block.gameObject.AddComponent<BoxCollider>().size = box.size;
            }
            return true;
        }

        /// <summary>renderers 경계를 space(집) 기준으로(집은 Y축만 돌아 있어 모델 돌림은 경계 상자 모서리로 충분).</summary>
        internal static Bounds LocalBounds(Transform space, Renderer[] renderers)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer r in renderers)
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds m = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = m.center + Vector3.Scale(m.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    Vector3 p = toSpace.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        /// <summary>
        /// 집 충돌 상자가 울타리 · 정문 줄 밖으로 나가거나, 다른 집 · 충돌 있는 소품 · 주민과 겹치면 경고한다(값은 바꾸지 않는다).
        /// 집마다 문 앞(houseModels.door) 1.5 m 안에 주민이 있는지도 로그로 남긴다. 마을 안 좌표(x, y)로 잰다.
        /// </summary>
        private static void CheckHouses(VillageTuning v)
        {
            var boxes = new List<(string house, Vector2 center, Vector2 half, float yaw)>();
            var report = new System.Text.StringBuilder();
            foreach (VillageTuning.House h in v.houses)
            {
                if (string.IsNullOrEmpty(h.model) || !TryHouseModel(v, h.model, out VillageTuning.HouseModel m) || m.colliders == null) continue;
                Vector2 toPlaza = v.plazaCenter - h.position;
                float yaw = Mathf.Atan2(toPlaza.x, toPlaza.y) * Mathf.Rad2Deg + h.turn;
                foreach (VillageTuning.HouseBox box in m.colliders)
                {
                    if (box.center.y - box.size.y * 0.5f > 2f) continue; // 위층은 땅 위 것과 안 부딪힌다
                    Vector2 center = h.position + Rotate(new Vector2(box.center.x, box.center.z), yaw);
                    boxes.Add((h.name, center, new Vector2(box.size.x, box.size.z) * 0.5f, yaw + box.yaw));
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 corner = h.position + Rotate(new Vector2(box.center.x + box.size.x * 0.5f * ((k & 1) == 0 ? -1f : 1f), box.center.z + box.size.z * 0.5f * ((k & 2) == 0 ? -1f : 1f)), yaw);
                        if (corner.magnitude > v.fenceRadius - 0.3f || corner.x > v.gateLineX - 0.3f)
                        {
                            Debug.LogWarning($"[VillageSetup] {h.name}({h.model})이 울타리에 닿거나 밖으로 나갑니다: 모서리 ({corner.x:0.0}, {corner.y:0.0})");
                            break;
                        }
                    }
                }
                Vector2 door = h.position + Rotate(m.door, yaw);
                string at = null;
                foreach (VillageTuning.Resident r in v.residents)
                    if (Vector2.Distance(r.position, door) < 1.5f) at = r.species;
                report.Append($" · {h.name}({h.model}) 문 앞 ({door.x:0.0}, {door.y:0.0}) {at ?? "비어 있음"}");
            }
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                    if (boxes[i].house != boxes[j].house && Overlap(boxes[i].center, boxes[i].half, boxes[i].yaw, boxes[j].center, boxes[j].half, boxes[j].yaw, 0.3f))
                        Debug.LogWarning($"[VillageSetup] 집 {boxes[i].house} · {boxes[j].house}가 겹칩니다.");
                foreach (VillageTuning.Prop p in v.props)
                {
                    if (!p.collide && p.elevation > 1.5f) continue;
                    float radius = Mathf.Max(p.size.x, p.size.z) * 0.5f;
                    if (Inside(boxes[i].center, boxes[i].half + Vector2.one * (radius + 0.3f), boxes[i].yaw, p.position))
                        Debug.LogWarning($"[VillageSetup] 집 {boxes[i].house}이 소품 {p.name}과 겹칩니다.");
                }
                foreach (VillageTuning.Resident r in v.residents)
                    if (Inside(boxes[i].center, boxes[i].half + Vector2.one * 0.5f, boxes[i].yaw, r.position))
                        Debug.LogWarning($"[VillageSetup] 집 {boxes[i].house}이 주민 {r.species}과 겹칩니다.");
            }
            Debug.Log($"[VillageSetup] 집 문 앞 주민{report}");
        }

        /// <summary>마을 안 좌표(x, y) 벡터를 Y축 yaw(도)만큼 돌린다(Unity와 같은 방향: 0° = +y, 90° = +x).</summary>
        private static Vector2 Rotate(Vector2 local, float yaw)
        {
            float a = yaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            return new Vector2(local.x * cos + local.y * sin, -local.x * sin + local.y * cos);
        }

        private static bool Inside(Vector2 center, Vector2 half, float yaw, Vector2 point)
        {
            Vector2 local = Rotate(point - center, -yaw);
            return Mathf.Abs(local.x) < half.x && Mathf.Abs(local.y) < half.y;
        }

        /// <summary>두 돌린 사각형이 margin 안쪽으로 겹치는지(분리축).</summary>
        private static bool Overlap(Vector2 ca, Vector2 ha, float ya, Vector2 cb, Vector2 hb, float yb, float margin)
        {
            Vector2[] axes = { Rotate(Vector2.right, ya), Rotate(Vector2.up, ya), Rotate(Vector2.right, yb), Rotate(Vector2.up, yb) };
            Vector2 d = cb - ca;
            foreach (Vector2 axis in axes)
            {
                float ra = Mathf.Abs(Vector2.Dot(Rotate(Vector2.right, ya) * ha.x, axis)) + Mathf.Abs(Vector2.Dot(Rotate(Vector2.up, ya) * ha.y, axis));
                float rb = Mathf.Abs(Vector2.Dot(Rotate(Vector2.right, yb) * hb.x, axis)) + Mathf.Abs(Vector2.Dot(Rotate(Vector2.up, yb) * hb.y, axis));
                if (Mathf.Abs(Vector2.Dot(d, axis)) > ra + rb + margin) return false;
            }
            return true;
        }

        private static void BuildProp(Context c, Transform parent, VillageTuning.Prop data)
        {
            PrimitiveType type = data.shape == VillageTuning.PropShape.Cylinder ? PrimitiveType.Cylinder
                : data.shape == VillageTuning.PropShape.Sphere ? PrimitiveType.Sphere : PrimitiveType.Cube;
            GameObject prop = Primitive(type, data.name, data.material == "Ember" ? Ember() : Lit(data.material), data.collide);
            Vector3 size = data.size;
            prop.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
            c.Place(prop.transform, parent, data.position, data.elevation, Quaternion.Euler(data.rotation));
            if (data.material == "Ember") prop.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void BuildFireLight(Context c, Transform parent)
        {
            VillageTuning v = c.Village;
            if (v.fireIntensity <= 0f) return;
            var light = new GameObject("FireLight").AddComponent<Light>();
            light.type = LightType.Point;
            light.color = v.fireColor;
            light.intensity = v.fireIntensity;
            light.range = v.fireRange;
            light.shadows = LightShadows.None;
            c.Place(light.transform, parent, v.plazaCenter, 0.9f, Quaternion.identity);
        }

        // ---------- 안전 지대 ----------

        /// <summary>
        /// 울타리 모양(fenceRadius · gateLineX)의 <see cref="SafeZone"/>과, 적 막기 범위(+ safeZoneEnemyMargin)를 덮는
        /// NavMesh 구멍(Not Walkable NavMeshModifierVolume 띠). 띠는 둥근 범위를 남김없이 덮도록 가운데 쪽 변 길이로 잰다.
        /// 주민은 서 있기만 하고 플레이어는 NavMesh를 쓰지 않아, 구멍은 적에게만 막힌다.
        /// </summary>
        private static void BuildSafeZone(Context c)
        {
            VillageTuning v = c.Village;
            var holder = new GameObject("SafeZone").transform;
            holder.SetParent(c.Root, false);
            var zone = holder.gameObject.AddComponent<SafeZone>();
            zone.Configure(v.displayName, v.fenceRadius, true, v.gateLineX, v.safeZoneEnterMargin, v.safeZoneEnemyMargin);

            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            float r = v.fenceRadius + v.safeZoneEnemyMargin;
            float maxX = Mathf.Min(r, v.gateLineX + v.safeZoneEnemyMargin);
            float step = Mathf.Max(0.5f, v.safeZoneNavMeshStrip);
            int strips = Mathf.CeilToInt((maxX + r) / step);
            const float Height = 20f; // 마을 바닥은 평평 — 위아래로 넉넉하게
            for (int i = 0; i < strips; i++)
            {
                float x0 = -r + i * step;
                float x1 = Mathf.Min(maxX, x0 + step);
                float near = (x0 <= 0f && x1 >= 0f) ? 0f : Mathf.Min(Mathf.Abs(x0), Mathf.Abs(x1));
                float halfZ = Mathf.Sqrt(Mathf.Max(0f, r * r - near * near));
                if (x1 <= x0 || halfZ <= 0f) continue;
                var strip = new GameObject($"NavBlock{i}").transform;
                strip.SetParent(holder, false);
                var volume = strip.gameObject.AddComponent<NavMeshModifierVolume>();
                volume.area = notWalkable;
                volume.center = new Vector3((x0 + x1) * 0.5f, 0f, 0f);
                volume.size = new Vector3(x1 - x0, Height, halfZ * 2f);
            }
        }

        // ---------- 주민 ----------

        private static void BuildResidents(Context c)
        {
            Transform parent = Group(c.Root, "Residents");
            foreach (VillageTuning.Resident resident in c.Village.residents)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillagerSetup.PrefabPath(resident.species));
                if (prefab == null)
                {
                    Debug.LogError($"[VillageSetup] 주민 프리팹이 없습니다: {resident.species} — VillagerTuning.species에 있는지 확인하세요.");
                    continue;
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                c.Place(instance.transform, parent, resident.position, 0f, Quaternion.Euler(0f, resident.yaw, 0f));
            }
        }

        // ---------- 도움 ----------

        private static Vector3 Ground(Terrain terrain, Vector2 point)
        {
            var position = new Vector3(point.x, 0f, point.y);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Material material, bool collide)
        {
            GameObject block = GameObject.CreatePrimitive(type);
            block.name = name;
            if (!collide) Object.DestroyImmediate(block.GetComponent<Collider>());
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        private static void Child(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Quaternion localRotation, Material material, bool collide)
        {
            GameObject block = Primitive(type, name, material, collide);
            block.transform.SetParent(parent, false);
            block.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            block.transform.localScale = scale;
        }

        private static Material Lit(string name)
        {
            if (!Colors.TryGetValue(name, out Color color)) color = new Color(0.5f, 0.5f, 0.5f);
            return TestRoomSetup.LoadOrCreateLit($"{ArtFolder}/Village_{name}.mat", color);
        }

        /// <summary>모닥불 불씨(빛나는 주황).</summary>
        private static Material Ember()
        {
            string path = $"{ArtFolder}/Village_Ember.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.35f, 0.12f, 0.04f));
            material.SetColor("_EmissionColor", new Color(1f, 0.38f, 0.1f) * 2.5f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static float Hash(int i)
        {
            float x = Mathf.Sin(i * 12.9898f + 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
