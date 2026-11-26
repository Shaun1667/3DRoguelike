using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 월드 가운데 E-07 마을(우호적 · 전투 없는 안전 지대, 결정 2026-11-18 사용자): 자리 · 바닥 · 울타리 · 집 · 소품 · 주민 배치.
    /// 메뉴 Tools/Game/Setup World가 월드를 세울 때 이 값으로 마을도 세우고, Tools/Game/Setup Village는 열린 World 씬의 마을만 다시 세운다.
    /// 자리는 진행 길 거리(routeDistance)와 길 옆 거리(sideOffset, +면 나아가는 방향 오른쪽)로 정한다(WorldTuning과 같은 방식).
    /// 마을 안 좌표(Vector2)는 마을 가운데 기준 (x = 길 방향 오른쪽, y = 길이 나아가는 방향) m. 집은 VARCO 모델(houseModels, 2026-11-18), 울타리 · 정문 · 소품은 회색 도형, 주민은 정지.
    /// 대화 · 상점 · 이야기 조각은 나중에 주민(Villager)과 이 표에 붙인다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Village Tuning")]
    public sealed class VillageTuning : ScriptableObject
    {
        [Serializable]
        public struct House
        {
            public string name;
            [Tooltip("마을 안 자리(m)")]
            public Vector2 position;
            [Tooltip("houseModels의 id(VARCO 모델). 비우면 size · wallHeight로 회색 도형 집")]
            public string model;
            [Tooltip("회색 도형 집의 폭 · 깊이(m). 모델 집은 쓰지 않는다")]
            public Vector2 size;
            [Min(1.5f)] public float wallHeight;
            [Tooltip("앞(문)이 광장 가운데를 보는 방향에서 더 돌리는 각도(도, + = 위에서 보아 시계 방향)")]
            public float turn;
        }

        /// <summary>VARCO 집 모델 한 종. 크기 맞춘 뒤 집 기준(피벗 = 바닥 가운데, 앞(문) = +z) m.</summary>
        [Serializable]
        public struct HouseModel
        {
            public string id;
            [Tooltip("Art/Village/Houses/{modelFile}.fbx, 텍스처는 {modelFile}_tex/{modelFile}_0_Image_0(알베도) · _1_Image_1(ORM) · _2_Image_2(노멀)")]
            public string modelFile;
            [Tooltip("모델 전체 높이(m, 굴뚝 · 지붕 장식 포함). 이 높이로 고르게 키운다")]
            [Min(0.5f)] public float height;
            [Tooltip("모델을 Y축으로 돌리는 각도(도). 문이 집 앞(+z)을 보게")]
            public float frontYaw;
            [Tooltip("크기 · 방향을 맞춘 뒤 더 옮기는 거리(m). 피벗을 바닥 가운데로(가져온 모델은 이미 바닥 가운데)")]
            public Vector3 pivotOffset;
            [Tooltip("문 앞 자리(집 기준 x, z m). 주민 · 자리 확인용")]
            public Vector2 door;
            [Tooltip("충돌 상자(집 기준 가운데 · 크기 m). 본채 · 데크 · 계단을 따로 — 낮은 데크 · 계단은 올라설 수 있게")]
            public HouseBox[] colliders;
        }

        [Serializable]
        public struct HouseBox
        {
            public Vector3 center;
            public Vector3 size;
            [Tooltip("집 기준 Y축 회전(도)")]
            public float yaw;
        }

        public enum PropShape { Box, Cylinder, Sphere }

        [Serializable]
        public struct Prop
        {
            public string name;
            public PropShape shape;
            [Tooltip("마을 안 자리(m)")]
            public Vector2 position;
            [Tooltip("가운데 높이(땅 위 m)")]
            public float elevation;
            [Tooltip("크기(m). 원기둥은 (지름, 높이, 지름)")]
            public Vector3 size;
            [Tooltip("마을 기준 회전(도)")]
            public Vector3 rotation;
            [Tooltip("재질 이름(Art/Village/Village_{material}.mat). 색은 VillageSetup 색 표")]
            public string material;
            public bool collide;
        }

        [Serializable]
        public struct Resident
        {
            [Tooltip("VillagerTuning.species의 id")]
            public string species;
            [Tooltip("마을 안 자리(m)")]
            public Vector2 position;
            [Tooltip("보는 방향(도, 마을 기준 0 = 길이 나아가는 쪽, 90 = 길 쪽)")]
            public float yaw;
        }

        [Header("자리")]
        [Tooltip("월드에 마을을 둔다. 끄면 Setup World가 마을(지형 평지 · 나무 치우기 · 집 · 주민 · 안전 지대)을 빼고 세운다 — 켜고 routeDistance · sideOffset을 맞춘 뒤 Setup World. 결정 2026-11-25(사용자): 나중에 다시 배치")]
        public bool placeInWorld = true;
        public string displayName = "숲속 마을";
        [Tooltip("길 시작에서 마을 가운데까지 길 거리(m). 2지역(관문 1 뒤) 가운데 — 적 무리 465 m · 520 m 사이")]
        [Min(0f)] public float routeDistance = 488f;
        [Tooltip("길 가운데에서 마을 가운데까지 옆 거리(m). -면 왼쪽. 길이 마을 동쪽 입구 앞을 지나간다")]
        public float sideOffset = -22f;
        [Tooltip("마을 범위 반지름(m). 이 안은 평평한 흙바닥 · 나무 없음 · 적 무리 · 던전 입구 · 오염 지대가 없어야 한다")]
        [Min(5f)] public float radius = 28f;
        [Tooltip("마을 둘레 밖으로 땅이 원래 굴곡으로 돌아가는 거리(m)")]
        [Min(1f)] public float flatFade = 8f;
        [Tooltip("마을 범위 밖 이만큼까지 나무를 치운다(m)")]
        [Min(0f)] public float treeClearance = 2f;

        [Header("바닥 (지형 흙길 레이어)")]
        [Tooltip("광장 가운데(마을 안 m)")]
        public Vector2 plazaCenter = new Vector2(-3f, 0f);
        [Tooltip("광장 반지름(m). 안은 흙만")]
        [Min(1f)] public float plazaRadius = 9f;
        [Tooltip("마을 안 나머지 바닥의 흙 비율(0~1, 노이즈로 들쭉날쭉)")]
        [Range(0f, 1f)] public float villageDirt = 0.65f;

        [Header("울타리 (둥근 판자 울타리 + 길 쪽 곧은 울타리, 그 가운데 정문)")]
        [Min(5f)] public float fenceRadius = 27f;
        [Tooltip("길 쪽 곧은 울타리 · 정문의 x(마을 안 m). 길(x = -sideOffset)보다 안쪽")]
        public float gateLineX = 17f;
        [Tooltip("정문 반폭(m)")]
        [Min(1f)] public float gateHalfWidth = 3.2f;
        [Min(0.5f)] public float fenceHeight = 1.9f;
        [Tooltip("울타리 한 칸 길이(m)")]
        [Min(0.5f)] public float fenceSegment = 2.5f;

        [Header("안전 지대 (적이 못 들어온다, 결정 2026-11-18 사용자) — 모양은 울타리(fenceRadius · gateLineX)")]
        [Tooltip("플레이어는 울타리 밖으로 이만큼(m)까지도 보호된다(정문 문턱). 안에서는 적이 쫓지 않고 · 적 공격 피해가 없고 · 길 위 적 무리가 나타나지 않는다")]
        [Min(0f)] public float safeZoneEnterMargin = 1f;
        [Tooltip("적은 울타리 밖으로 이만큼(m) 더 떨어진 곳까지만 온다(NavMesh 구멍 + 밀어내기)")]
        [Min(0f)] public float safeZoneEnemyMargin = 4f;
        [Tooltip("NavMesh 구멍을 만드는 띠(NavMeshModifierVolume) 폭(m). 작을수록 둥근 모양에 가깝고 띠가 많아진다")]
        [Min(0.5f)] public float safeZoneNavMeshStrip = 1.5f;

        [Header("모닥불 빛")]
        public Color fireColor = new Color(1f, 0.55f, 0.25f);
        [Min(0f)] public float fireIntensity = 3f;
        [Min(0f)] public float fireRange = 9f;

        [Header("집 모델 (VARCO, 2026-11-18)")]
        [Tooltip("VARCO ORM의 금속(B)에 곱하는 값. 함석 · 컨테이너가 금속")]
        [Range(0f, 1f)] public float houseMetallicScale = 0.6f;
        [Tooltip("VARCO ORM의 매끄러움(1 - 거칠기 G)에 곱하는 값")]
        [Range(0f, 1f)] public float houseSmoothnessScale = 0.6f;
        [Tooltip("알베도 텍스처 최대 크기")]
        public int houseAlbedoSize = 2048;
        [Tooltip("노멀 · 금속 텍스처 최대 크기")]
        public int houseDetailSize = 1024;
        [Tooltip("집 모델 표. 크기 · 앞 방향 · 문 자리 · 충돌 상자는 모델을 크기 맞춰 돌린 뒤 재 값(2026-11-18)")]
        public HouseModel[] houseModels =
        {
            // 판잣집 4.5 × 5.5 m(현관 데크 · 계단 포함), 높이 4.3 m(굴뚝 포함). 데크 윗면 0.66 m, 계단 2단
            new HouseModel
            {
                id = "Shack", modelFile = "House_Shack", height = 4.3f, frontYaw = 0f, door = ShackDoor,
                colliders = new[]
                {
                    Box(-0.02f, 1.5f, -0.67f, 4.05f, 3f, 3.55f),
                    Box(0f, 0.33f, 1.62f, 4f, 0.66f, 1.05f),
                    Box(0f, 0.225f, 2.3f, 1.2f, 0.45f, 0.3f),
                    Box(0f, 0.115f, 2.6f, 1.2f, 0.23f, 0.3f),
                },
            },
            // 컨테이너 집 5.6 × 5.1 m, 높이 4.1 m. 문은 앞 오른쪽(+x 1.45, 앞에서 보면 왼쪽), 앞 데크 윗면 0.58 m · 난간, +x 끝에 옆 계단.
            // -x 쪽은 나무 덧방(물통)
            new HouseModel
            {
                id = "Container", modelFile = "House_Container", height = 4.1f, frontYaw = 0f, door = ContainerDoor,
                colliders = new[]
                {
                    Box(0f, 1.5f, -0.85f, 5.5f, 3f, 2.5f),
                    Box(0.6f, 0.29f, 1.2f, 3.6f, 0.58f, 1.6f),
                    Box(-0.1f, 0.7f, 2f, 2.2f, 1.4f, 0.2f),
                    Box(2.62f, 0.15f, 1.2f, 0.45f, 0.3f, 1.6f),
                },
            },
            // 2층 목조집 4.5 × 5.5 m, 높이 7 m. 모델은 문이 +x라 -90° 돌린다. 2층이 1층보다 넓다(발코니). 계단 3단 · 왼쪽 앞 사다리
            new HouseModel
            {
                id = "TwoStory", modelFile = "House_TwoStory", height = 7f, frontYaw = -90f, door = TwoStoryDoor,
                colliders = new[]
                {
                    Box(0.05f, 1.7f, -0.45f, 3.5f, 3.4f, 3.3f),
                    Box(-0.05f, 4.2f, -0.05f, 4.3f, 2.4f, 4.7f),
                    Box(0.4f, 0.35f, 1.45f, 1.6f, 0.7f, 0.5f),
                    Box(0.4f, 0.225f, 1.82f, 1.6f, 0.45f, 0.25f),
                    Box(0.4f, 0.11f, 2.12f, 1.6f, 0.22f, 0.35f),
                    Box(-1.95f, 1.2f, 2f, 0.6f, 2.4f, 0.5f),
                },
            },
            // 마을회관 7.8 × 12 m, 높이 6 m. 박공 쪽(+z)에 쌍여닫이 문 · 현판, 양옆에 장작 더미
            new HouseModel
            {
                id = "Hall", modelFile = "House_Hall", height = 6f, frontYaw = 0f, door = HallDoor,
                colliders = new[]
                {
                    Box(0.07f, 1.8f, -0.17f, 7.05f, 3.6f, 10.85f),
                    Box(3f, 0.75f, 5.6f, 1.3f, 1.5f, 0.9f), // 문 +x 쪽 물통 · 받침
                    Box(-2.6f, 0.25f, 5.6f, 1f, 0.5f, 0.7f), // 문 -x 쪽 상자
                },
            },
        };

        [Header("집 (광장 둘레, 문이 광장을 본다)")]
        [Tooltip("회관은 정문 맞은편(서쪽)에서 광장을 본다. 나머지 7채는 판잣집 3 · 컨테이너 2 · 2층집 2. 각도는 광장 가운데에서 x축(정문 쪽) 반시계")]
        public House[] houses =
        {
            HouseAt("House1", "Container", 40f),
            HouseAt("House2", "Shack", 80f),
            HouseAt("House3", "TwoStory", 117f),
            HouseAt("Hall", "Hall", 180f),
            HouseAt("House5", "Shack", 222f),
            HouseAt("House6", "Container", 256f, 14f), // 뒤뜰에 밭(Garden)
            HouseAt("House7", "TwoStory", 295f),
            HouseAt("House8", "Shack", 330f),
        };

        [Header("소품 (모닥불 · 의자 · 우물 · 망루 · 상자 · 장작 · 밭)")]
        public Prop[] props =
        {
            P("FireRing", PropShape.Cylinder, -3f, 0f, 0.1f, new Vector3(1.8f, 0.2f, 1.8f), Vector3.zero, "Stone", false),
            P("FireLogA", PropShape.Box, -3f, 0f, 0.25f, new Vector3(1.3f, 0.18f, 0.18f), new Vector3(0f, 30f, 8f), "Wood", false),
            P("FireLogB", PropShape.Box, -3f, 0f, 0.25f, new Vector3(1.3f, 0.18f, 0.18f), new Vector3(0f, -40f, -8f), "Wood", false),
            P("FireEmbers", PropShape.Sphere, -3f, 0f, 0.22f, new Vector3(0.7f, 0.3f, 0.7f), Vector3.zero, "Ember", false),
            P("BenchN", PropShape.Cylinder, -3f, 2.6f, 0.25f, new Vector3(0.45f, 2.2f, 0.45f), new Vector3(0f, 0f, 90f), "Wood", true),
            P("BenchSW", PropShape.Cylinder, -5.25f, -1.3f, 0.25f, new Vector3(0.45f, 2.2f, 0.45f), new Vector3(0f, 60f, 90f), "Wood", true),
            P("BenchSE", PropShape.Cylinder, -0.75f, -1.3f, 0.25f, new Vector3(0.45f, 2.2f, 0.45f), new Vector3(0f, 120f, 90f), "Wood", true),

            P("WellBase", PropShape.Cylinder, 3.5f, 6.5f, 0.45f, new Vector3(1.6f, 0.9f, 1.6f), Vector3.zero, "Stone", true),
            P("WellWater", PropShape.Cylinder, 3.5f, 6.5f, 0.91f, new Vector3(1.2f, 0.02f, 1.2f), Vector3.zero, "Dark", false),
            P("WellPostA", PropShape.Box, 2.75f, 6.5f, 1.2f, new Vector3(0.15f, 1.6f, 0.15f), Vector3.zero, "Wood", true),
            P("WellPostB", PropShape.Box, 4.25f, 6.5f, 1.2f, new Vector3(0.15f, 1.6f, 0.15f), Vector3.zero, "Wood", true),
            P("WellRoof", PropShape.Box, 3.5f, 6.5f, 2.05f, new Vector3(1.9f, 0.12f, 1.1f), Vector3.zero, "Roof", false),

            P("TowerLegA", PropShape.Box, 11.9f, -15.9f, 2.5f, new Vector3(0.25f, 5f, 0.25f), Vector3.zero, "Wood", true),
            P("TowerLegB", PropShape.Box, 14.1f, -15.9f, 2.5f, new Vector3(0.25f, 5f, 0.25f), Vector3.zero, "Wood", true),
            P("TowerLegC", PropShape.Box, 11.9f, -18.1f, 2.5f, new Vector3(0.25f, 5f, 0.25f), Vector3.zero, "Wood", true),
            P("TowerLegD", PropShape.Box, 14.1f, -18.1f, 2.5f, new Vector3(0.25f, 5f, 0.25f), Vector3.zero, "Wood", true),
            P("TowerDeck", PropShape.Box, 13f, -17f, 4f, new Vector3(2.8f, 0.2f, 2.8f), Vector3.zero, "Wood", true),
            P("TowerRail", PropShape.Box, 13f, -17f, 4.55f, new Vector3(2.8f, 0.9f, 2.8f), Vector3.zero, "Plank", false),
            P("TowerRoof", PropShape.Box, 13f, -17f, 6f, new Vector3(3.2f, 0.15f, 3.2f), new Vector3(0f, 45f, 0f), "Roof", false),

            P("CrateA", PropShape.Box, 14.4f, 4.7f, 0.4f, new Vector3(0.8f, 0.8f, 0.8f), new Vector3(0f, 15f, 0f), "Plank", true),
            P("CrateB", PropShape.Box, 15.3f, 5.6f, 0.35f, new Vector3(0.7f, 0.7f, 0.7f), new Vector3(0f, -10f, 0f), "Plank", true),
            P("CrateC", PropShape.Box, 14.8f, 5.1f, 1.05f, new Vector3(0.6f, 0.5f, 0.6f), new Vector3(0f, 35f, 0f), "Plank", true),
            P("Barrel", PropShape.Cylinder, 14.6f, -4.6f, 0.5f, new Vector3(0.7f, 1f, 0.7f), Vector3.zero, "Metal", true),

            P("Woodpile", PropShape.Box, -14.2f, 9.6f, 0.45f, new Vector3(2.2f, 0.9f, 0.9f), new Vector3(0f, 65f, 0f), "Wood", true),
            P("Garden", PropShape.Box, -8f, -21f, 0.08f, new Vector3(4f, 0.16f, 3f), new Vector3(0f, 20f, 0f), "Soil", false),
            P("GardenRowA", PropShape.Box, -8.4f, -20.2f, 0.25f, new Vector3(3.4f, 0.25f, 0.35f), new Vector3(0f, 20f, 0f), "Crop", false),
            P("GardenRowB", PropShape.Box, -7.6f, -21.8f, 0.25f, new Vector3(3.4f, 0.25f, 0.35f), new Vector3(0f, 20f, 0f), "Crop", false),
        };

        [Header("주민 15명 (광장 7 · 집 앞 7 · 정문 1)")]
        public Resident[] residents =
        {
            // 광장: 모닥불(-3, 0) 둘레 4.5 m
            Look("Fox", Around(-3f, 0f, 4.5f, 20f), new Vector2(-3f, 0f), 25f),
            Look("Hare", Around(-3f, 0f, 4.5f, 70f), Around(-3f, 0f, 4.5f, 20f), 0f),
            Look("Magpie", Around(-3f, 0f, 4.5f, 120f), new Vector2(-3f, 0f), -30f),
            Look("Tiger", Around(-3f, 0f, 4.5f, 165f), new Vector2(-3f, 0f), 0f),
            Look("RaccoonDog", Around(-3f, 0f, 4.5f, 215f), Around(-3f, 0f, 4.5f, 265f), 0f),
            Look("Otter", Around(-3f, 0f, 4.5f, 265f), Around(-3f, 0f, 4.5f, 215f), 0f),
            Look("Toad", Around(-3f, 0f, 4.5f, 320f), new Vector2(17f, 0f), 0f),
            // 집 문 앞(houseModels.door 자리). House8(판잣집, 정문 남쪽)은 빈집
            AtDoor("Badger", 40f, 16f, ContainerDoor, new Vector2(-3f, 0f), 0f),
            AtDoor("Boar", 80f, 16f, ShackDoor, new Vector2(-3f, 0f), 50f),
            AtDoor("LeopardCat", 117f, 16f, TwoStoryDoor, new Vector2(-3f, 0f), -60f),
            AtDoor("Goshawk", 180f, 16f, HallDoor, new Vector2(-3f, 0f), 15f),
            AtDoor("Wolf", 222f, 16f, ShackDoor, new Vector2(17f, 0f), 0f),
            AtDoor("Marten", 256f, 14f, ContainerDoor, new Vector2(-3f, 0f), 90f),
            AtDoor("Goral", 295f, 16f, TwoStoryDoor, new Vector2(-3f, 0f), -35f),
            // 정문 지기: 길 쪽을 본다
            Look("Bear", new Vector2(13.5f, -4f), new Vector2(30f, -4f), -20f),
        };

        // 문 앞 자리(집 기준 x, z m) — houseModels · 주민이 같이 쓴다
        private static readonly Vector2 ShackDoor = new Vector2(0f, 3.3f);
        private static readonly Vector2 ContainerDoor = new Vector2(1.7f, 2.6f);
        private static readonly Vector2 TwoStoryDoor = new Vector2(0.4f, 2.9f);
        private static readonly Vector2 HallDoor = new Vector2(0f, 6.6f);
        private static readonly Vector2 PlazaCenter = new Vector2(-3f, 0f);

        /// <summary>광장 가운데에서 반지름 r m, angle(도, x축에서 반시계)에 놓인 모델 집. 앞(문)은 광장 가운데를 본다.</summary>
        private static House HouseAt(string name, string model, float angle, float r = 16f)
        {
            return new House { name = name, model = model, position = Around(PlazaCenter.x, PlazaCenter.y, r, angle), size = new Vector2(5f, 4f), wallHeight = 2.6f, turn = 0f };
        }

        /// <summary>광장 가운데에서 (angle, r)에 놓인 집(앞이 광장)의 문 앞 자리에 서서 target을 보는(+ turn) 주민.</summary>
        private static Resident AtDoor(string species, float angle, float r, Vector2 door, Vector2 target, float turn)
        {
            Vector2 center = Around(PlazaCenter.x, PlazaCenter.y, r, angle);
            Vector2 forward = (PlazaCenter - center).normalized;
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 position = center + right * door.x + forward * door.y;
            position = new Vector2(Mathf.Round(position.x * 100f) / 100f, Mathf.Round(position.y * 100f) / 100f);
            return Look(species, position, target, turn);
        }

        private static HouseBox Box(float x, float y, float z, float sx, float sy, float sz)
        {
            return new HouseBox { center = new Vector3(x, y, z), size = new Vector3(sx, sy, sz), yaw = 0f };
        }

        private static Vector2 Around(float cx, float cz, float r, float angle)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector2(cx + Mathf.Cos(a) * r, cz + Mathf.Sin(a) * r);
        }

        private static Resident Look(string species, Vector2 position, Vector2 target, float turn)
        {
            Vector2 d = target - position;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + turn;
            return new Resident { species = species, position = position, yaw = Mathf.Round(yaw) };
        }

        private static Prop P(string name, PropShape shape, float x, float z, float elevation, Vector3 size, Vector3 rotation, string material, bool collide)
        {
            return new Prop { name = name, shape = shape, position = new Vector2(x, z), elevation = elevation, size = size, rotation = rotation, material = material, collide = collide };
        }
    }
}
