using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// E-07 마을 주민 15종의 모델 · 키 · 충돌 · 털 · 대기 동작 · 꼬리 값. 메뉴 Tools/Game/Setup Villagers(Setup Village · Setup World도 부른다)가
    /// 이 값으로 재질 · 털 메시 · 꼬리 뼈 · 프리팹(Prefabs/Villagers/Villager_{id})을 만든다. 값을 바꾸면 메뉴를 다시 실행한다.
    /// 모델은 VARCO glb → Mixamo 자동 리깅 FBX(Art/Villagers/Rigged/{modelFile}.fbx, Humanoid). 텍스처는 Art/Villagers/{modelFile}_tex/ 의
    /// Image_0 알베도 · Image_1 ORM · Image_2 노멀(리깅 전 정지 모델과 UV가 같다).
    /// 털은 고라니와 같은 셸 셰이더(Game/Fur Shell). 털 범위는 리깅 전 모양(메시 원래 자리)의 높이 · 좌우 거리 · 알베도 색으로 정한다.
    /// 꼬리는 Mixamo가 뼈를 만들지 않아, 엉덩이 뒤로 튀어나온 부분을 메시에서 찾아 뼈 사슬을 넣는다(tailRig) — 흔들림은 TailChain.
    /// 높이 · 거리 값은 모두 모델 키에 대한 비율(0 = 발바닥, 1 = 머리 꼭대기 · 귀 · 뿔 끝).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Villager Tuning")]
    public sealed class VillagerTuning : ScriptableObject
    {
        [Serializable]
        public struct FurZones
        {
            [Tooltip("털을 낼지. 깃털(까치 · 참매) · 맨살(두꺼비)은 끈다")]
            public bool enabled;
            [Tooltip("목(머리 시작) 높이 비율. 이 위 neckBand부터는 무조건 털, 아래 neckBand까지는 머리 색과 맞는 곳만(옷깃 · 후드 제외)")]
            [Range(0.5f, 1f)] public float headFrom;
            [Tooltip("목 둘레에서 색으로 가르는 띠 두께(키 비율)")]
            [Range(0f, 0.1f)] public float neckBand;
            [Tooltip("머리 털 범위의 좌우 반폭(키 비율). T-포즈 손이 머리 높이에 들어오지 않게")]
            [Range(0.05f, 0.3f)] public float headHalfWidth;
            [Tooltip("팔 · 손(가운데에서 armFrom보다 먼 곳, 어깨 높이)에 머리 색과 맞는 곳만 털")]
            public bool arms;
            [Range(0.1f, 0.5f)] public float armFrom;
            [Tooltip("꼬리(몸 뒤로 tailBehind보다 먼 곳, 엉덩이 높이)에 머리 색과 맞는 곳만 털")]
            public bool tail;
            [Range(0.02f, 0.3f)] public float tailBehind;
            [Tooltip("발(feetTo 아래)에 머리 색과 맞는 곳만 털 — 맨발 종")]
            public bool feet;
            [Range(0.01f, 0.2f)] public float feetTo;
            [Tooltip("몸통 · 다리(나머지 전부)에 머리 색과 맞는 곳만 털 — 배 · 어깨가 드러난 종")]
            public bool body;

            [Header("색으로 털 / 옷 가르기")]
            [Tooltip("머리 색을 몇 가지로 묶을지(k-평균). 머리 꼭대기 쪽 알베도에서 뽑는다")]
            [Range(1, 8)] public int paletteSize;
            [Tooltip("이 색 거리(RGB 0~1, 유클리드) 안이면 털 색으로 본다")]
            [Range(0.02f, 0.4f)] public float colorTolerance;
            [Tooltip("머리에서 뽑은 색 말고 털로 볼 색을 더한다(예: 꼬리 끝 흰색)")]
            public Color[] extraFurColors;

            [Header("길이 비율(머리 = 1)")]
            [Range(0f, 3f)] public float armLength;
            [Range(0f, 4f)] public float tailLength;
            [Range(0f, 2f)] public float feetLength;
            [Range(0f, 2f)] public float bodyLength;
        }

        [Serializable]
        public struct FurLook
        {
            [Tooltip("털 길이(m, 월드). 머리 기준")]
            [Range(0.001f, 0.05f)] public float length;
            [Tooltip("가닥 사이 간격(m). UV 1칸이 몇 m인지 재서 밀도로 바꾼다(작을수록 촘촘)")]
            [Range(0.0003f, 0.01f)] public float strandSpacing;
            [Tooltip("이 밝기보다 어두운 알베도에는 털을 내지 않는다(눈 · 코 · 입). 검은 털(곰 · 오소리)은 낮게")]
            [Range(0f, 0.3f)] public float darkCutoff;
            [Tooltip("털 색에 곱하는 색")]
            public Color tint;
        }

        [Serializable]
        public struct Species
        {
            [Tooltip("프리팹 이름 Villager_{id}")]
            public string id;
            public string displayName;
            [Tooltip("Art/Villagers/Rigged 아래 FBX 이름(확장자 없이). 텍스처 폴더는 Art/Villagers/{modelFile}_tex")]
            public string modelFile;
            [Tooltip("모델 전체 높이(m, 귀 · 뿔 포함)")]
            [Min(0.3f)] public float height;
            [Tooltip("몸 충돌 캡슐 반지름(m). 높이는 키와 같다")]
            [Min(0.1f)] public float colliderRadius;
            public FurZones furZones;
            public FurLook fur;
        }

        /// <summary>꼬리 찾기 · 꼬리 뼈 넣기 값(모든 종 공통). 거리 · 높이는 키 비율, 엉덩이 뼈에 붙여 본 메시 원래 모양 기준.</summary>
        [Serializable]
        public struct TailRig
        {
            [Tooltip("꼬리가 있을 수 있는 높이 범위(키 비율)")]
            [Range(0f, 1f)] public float bandFrom;
            [Range(0f, 1f)] public float bandTo;
            [Tooltip("꼬리가 있을 수 있는 좌우 반폭(키 비율). T-포즈 팔이 들어오지 않게")]
            [Range(0.05f, 0.4f)] public float halfWidth;
            [Tooltip("등 기준을 재는 높이 범위(키 비율). 이 범위에서 가장 뒤로 나온 곳이 등")]
            [Range(0f, 1f)] public float backFrom;
            [Range(0f, 1f)] public float backTo;
            [Tooltip("등 기준을 재는 좌우 반폭(키 비율)")]
            [Range(0.02f, 0.3f)] public float backHalfWidth;
            [Tooltip("엉덩이 뒤가 등보다 이만큼(키 비율) 넘게 튀어나오면 꼬리가 있다고 본다")]
            [Range(0f, 0.3f)] public float minProtrusion;
            [Tooltip("등보다 이만큼(키 비율) 더 뒤부터 꼬리 정점으로 본다(꼬리 뿌리 자르는 면)")]
            [Range(0f, 0.1f)] public float cutMargin;
            [Tooltip("꼬리 뼈 수 범위. 꼬리 길이 ÷ segmentLength를 이 안으로 맞춘다")]
            [Range(2, 8)] public int minBones;
            [Range(2, 8)] public int maxBones;
            [Tooltip("꼬리 뼈 하나의 길이 기준(m)")]
            [Range(0.02f, 0.3f)] public float segmentLength;
            [Tooltip("꼬리 뿌리에서 원래 가중치(엉덩이 · 다리)와 섞는 길이(마디 수)")]
            [Range(0.1f, 2f)] public float baseBlend;
            [Tooltip("꼬리 마디 사이에서 두 뼈를 섞는 반폭(마디 비율)")]
            [Range(0.05f, 0.5f)] public float jointBlend;
            [Tooltip("꼬리가 뚫지 않게 다리에 두는 충돌 구 반지름(키 비율): 넓적다리 · 종아리")]
            [Range(0f, 0.15f)] public float thighRadius;
            [Range(0f, 0.15f)] public float calfRadius;
            [Tooltip("아래팔 · 손에 두는 충돌 구 반지름(키 비율). 대기 자세에서 내린 손을 꼬리가 뚫지 않게. 0이면 두지 않는다")]
            [Range(0f, 0.15f)] public float armRadius;

            public static TailRig Default => new TailRig
            {
                bandFrom = 0.05f,
                bandTo = 0.72f,
                halfWidth = 0.2f,
                backFrom = 0.62f,
                backTo = 0.8f,
                backHalfWidth = 0.1f,
                minProtrusion = 0.04f,
                cutMargin = 0.01f,
                minBones = 4,
                maxBones = 6,
                segmentLength = 0.1f,
                baseBlend = 0.6f,
                jointBlend = 0.35f,
                thighRadius = 0.05f,
                calfRadius = 0.04f,
                armRadius = 0.035f,
            };
        }

        /// <summary>메시 판정 대신 손으로 정하는 꼬리 유무.</summary>
        [Serializable]
        public struct TailOverride
        {
            public string species;
            public bool hasTail;
        }

        /// <summary>종마다 꼬리 흔들림 값.</summary>
        [Serializable]
        public struct TailEntry
        {
            public string species;
            public TailChain.Settings settings;
        }

        [Header("모델 공통")]
        [Tooltip("모델을 Y축으로 돌리는 각도(도). 얼굴이 프리팹 앞(+Z)을 보게")]
        public float modelYaw = 0f;
        [Tooltip("VARCO ORM의 금속(B)에 곱하는 값. 옷이 금속처럼 번들거리면 낮춘다")]
        [Range(0f, 1f)] public float metallicScale = 0.1f;
        [Tooltip("VARCO ORM의 매끄러움(1 - 거칠기 G)에 곱하는 값. 높으면 하늘이 비쳐 옷이 젖은 것처럼 보인다")]
        [Range(0f, 1f)] public float smoothnessScale = 0.55f;
        [Tooltip("알베도 텍스처 최대 크기")]
        public int albedoSize = 2048;
        [Tooltip("노멀 · 금속 텍스처 최대 크기")]
        public int detailSize = 1024;

        [Header("보이기 거리 (LODGroup, 화면 높이 비율)")]
        [Tooltip("이보다 작게 보이면 털을 그리지 않는다")]
        [Range(0.01f, 0.5f)] public float furScreenHeight = 0.12f;
        [Tooltip("이보다 작게 보이면 주민을 그리지 않는다")]
        [Range(0.001f, 0.1f)] public float cullScreenHeight = 0.012f;

        [Header("대기 동작")]
        [Tooltip("대기 동작 빠르기 범위(배). 주민마다 서 있는 자리로 이 안에서 정해지고, 시작 시점도 제각각이다")]
        public Vector2 idleSpeedRange = new Vector2(0.85f, 1.1f);

        [Header("꼬리 (메시에서 찾아 뼈를 넣는다)")]
        public TailRig tailRig = TailRig.Default;
        [Tooltip("꼬리 유무를 손으로 정할 종(메시 판정이 틀릴 때만). 없는 종은 메시로 판정한다")]
        public TailOverride[] tailOverrides =
        {
            // 참매: 허리에 묶은 웃옷이 엉덩이 뒤로 나와 꼬리로 잡힌다(꼬리 깃 없음)
            new TailOverride { species = "Goshawk", hasTail = false },
        };
        [Tooltip("꼬리 흔들림 값. tails에 없는 종은 이 값을 쓴다")]
        public TailChain.Settings tailDefault = TailChain.Settings.Default;
        [Tooltip("종마다 꼬리 흔들림 값")]
        public TailEntry[] tails =
        {
            // 길고 풍성한 꼬리: 느리고 크게 / 짧은 꼬리: 빠르고 작게
            Tail("Fox", wagAngle: 10f, wagFrequency: 0.4f, elasticity: 0.045f, stiffness: 0.4f),
            Tail("Marten", wagAngle: 12f, wagFrequency: 0.55f, elasticity: 0.05f, stiffness: 0.45f),
            Tail("Goral", wagAngle: 16f, wagFrequency: 1.1f, elasticity: 0.08f, stiffness: 0.55f),
        };

        [Header("털 공통 모양 · 움직임은 Data/VillagerFur.asset(FurSettings — 겹 수 · 굵기 · 바람 등). 길이 · 밀도 · 색은 종마다 아래 값")]
        public Species[] species =
        {
            // armFrom: 소매 끝(T-포즈에서 가운데에서 키 비율). 웃옷 색이 털 색과 비슷한 종(늑대 회색 스웨터 등)은 소매 끝 밖(손)부터만
            Make("Badger", "오소리", "01_Badger", 1.65f, 0.32f, Zones(0.865f, arms: true, armFrom: 0.40f), Look(0.009f, 0.0016f, 0.03f)),
            Make("Hare", "멧토끼", "02_Hare", 1.72f, 0.28f, Zones(0.76f, arms: true, armFrom: 0.30f), Look(0.007f, 0.0012f, 0.08f)),
            Make("Magpie", "까치", "03_Magpie", 1.7f, 0.3f, NoFur(), Look(0.007f, 0.0015f, 0.1f)),
            Make("Boar", "멧돼지", "04_Boar", 1.85f, 0.4f, Zones(0.87f, arms: true, armFrom: 0.28f), Look(0.012f, 0.0022f, 0.06f)),
            Make("Fox", "붉은여우", "05_Fox", 1.75f, 0.3f, Zones(0.83f, arms: true, tail: true, feet: true, tailLength: 2.5f, armFrom: 0.30f), Look(0.010f, 0.0014f, 0.07f)),
            Make("Bear", "반달가슴곰", "06_Bear", 2.2f, 0.45f, Wide(Zones(0.855f, arms: true, armFrom: 0.40f), 0.14f), Look(0.014f, 0.0018f, 0.01f)),
            Make("LeopardCat", "삵", "07_LeopardCat", 1.72f, 0.3f, Zones(0.85f, arms: true, body: true, armFrom: 0.18f), Look(0.006f, 0.0012f, 0.05f)),
            Make("Goshawk", "참매", "08_Goshawk", 1.75f, 0.3f, NoFur(), Look(0.007f, 0.0015f, 0.1f)),
            Make("Tiger", "호랑이", "09_Tiger", 1.9f, 0.38f, Zones(0.865f, arms: true, armFrom: 0.27f), Look(0.007f, 0.0014f, 0.03f)),
            Make("Wolf", "늑대", "10_Wolf", 1.9f, 0.34f, Zones(0.845f, arms: true, armFrom: 0.40f), Look(0.012f, 0.0015f, 0.05f)),
            Make("Marten", "담비", "11_Marten", 1.6f, 0.28f, Zones(0.845f, arms: true, tail: true, tailLength: 2f, armFrom: 0.20f), Look(0.008f, 0.0013f, 0.05f)),
            Make("Otter", "수달", "12_Otter", 1.55f, 0.28f, Zones(0.85f, arms: true, feet: true, armFrom: 0.20f), Look(0.004f, 0.0010f, 0.05f)),
            Make("RaccoonDog", "너구리", "13_RaccoonDog", 1.65f, 0.32f, Zones(0.835f), Look(0.012f, 0.0015f, 0.03f)),
            Make("Toad", "두꺼비", "14_Toad", 1.6f, 0.4f, NoFur(), Look(0.007f, 0.0015f, 0.1f)),
            Make("Goral", "산양", "15_Goral", 1.85f, 0.32f, Zones(0.815f, arms: true, tail: true, tailLength: 1.8f, armFrom: 0.20f), Look(0.010f, 0.0016f, 0.05f)),
        };

        public bool TryGet(string id, out Species result)
        {
            foreach (Species entry in species)
            {
                if (entry.id != id) continue;
                result = entry;
                return true;
            }
            result = default;
            return false;
        }

        public TailChain.Settings TailSettingsOf(string id)
        {
            foreach (TailEntry entry in tails)
            {
                if (entry.species == id) return entry.settings;
            }
            return tailDefault;
        }

        public bool TryGetTailOverride(string id, out bool hasTail)
        {
            foreach (TailOverride entry in tailOverrides)
            {
                if (entry.species != id) continue;
                hasTail = entry.hasTail;
                return true;
            }
            hasTail = false;
            return false;
        }

        private static TailEntry Tail(string id, float wagAngle, float wagFrequency, float elasticity, float stiffness)
        {
            TailChain.Settings settings = TailChain.Settings.Default;
            settings.wagAngle = wagAngle;
            settings.wagFrequency = wagFrequency;
            settings.elasticity = elasticity;
            settings.stiffness = stiffness;
            return new TailEntry { species = id, settings = settings };
        }

        private static Species Make(string id, string displayName, string modelFile, float height, float radius, FurZones zones, FurLook look)
        {
            return new Species { id = id, displayName = displayName, modelFile = modelFile, height = height, colliderRadius = radius, furZones = zones, fur = look };
        }

        private static FurZones Zones(float headFrom, bool arms = false, bool tail = false, bool feet = false, bool body = false, float tailLength = 1.5f, float armFrom = 0.22f)
        {
            return new FurZones
            {
                enabled = true,
                headFrom = headFrom,
                neckBand = 0.03f,
                headHalfWidth = 0.12f, // 넓으면 어깨 윗면 옷에도 목 띠 털이 난다
                arms = arms,
                armFrom = armFrom,
                tail = tail,
                tailBehind = 0.07f,
                feet = feet,
                feetTo = 0.06f,
                body = body,
                paletteSize = 4,
                colorTolerance = 0.12f,
                extraFurColors = new Color[0],
                armLength = 0.6f,
                tailLength = tailLength,
                feetLength = 0.5f,
                bodyLength = 0.6f,
            };
        }

        private static FurZones Wide(FurZones zones, float headHalfWidth)
        {
            zones.headHalfWidth = headHalfWidth;
            return zones;
        }

        private static FurZones NoFur()
        {
            FurZones zones = Zones(0.85f);
            zones.enabled = false;
            return zones;
        }

        private static FurLook Look(float length, float spacing, float darkCutoff)
        {
            return new FurLook { length = length, strandSpacing = spacing, darkCutoff = darkCutoff, tint = Color.white };
        }
    }
}
