using System.Collections.Generic;
using Game.Rules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// E 개체(두 발) 적의 리깅 모델 · 동작 · 애니메이터 컨트롤러를 만든다. <see cref="EnemySetup"/>이 부른다(Setup Test Room). 여러 번 실행해도 결과가 같다.
    /// 모델: Art/Enemies/Models/Rigged/{종}_E.fbx(Mixamo 오토리그, T-포즈)를 Humanoid로 가져와 Body/Model 아래에 넣는다(재질은 정적 모델과 같은 것).
    /// 동작: Art/Enemies/Animation의 Mixamo FBX(Y Bot 뼈대만)를 고라니 동작과 같은 방식으로 Humanoid로 가져오고, 아래 <see cref="SequencesFor"/> 표의
    /// 구간마다 클립을 잘라 낸다. 이동은 고라니 폴더의 Mutant Walk · Mutant Run을 읽기만 한다(가져오기 설정은 건드리지 않음).
    /// 컨트롤러: Art/Enemies/Animation/Controllers/{종}_E.controller — 대기/이동 블렌드(Speed, m/s) + 표의 상태들 + 맞은 동작 레이어(더하기).
    /// 상태 길이 = 수치 에셋의 시간(예고 · 간격 · 빈틈)에 맞춘 재생 빠르기. 그래서 예고 상태의 끝 프레임(= 손이 닿는 프레임)이 코드가 피해를 넣는 순간과 맞는다.
    /// 수치 에셋의 시간을 바꾸면 Setup Test Room을 다시 실행한다. 새 동작 = <see cref="EnemyAnim"/> 이름 한 줄 + 표 한 줄.
    /// 루트 모션은 쓰지 않는다(이동은 NavMeshAgent · 코드): 회전 · 높이는 포즈에 굽고, 앞뒤 이동은 굽지 않아 제자리에서 논다(죽는 동작만 쓰러지는 쪽으로 굽는다).
    /// Mutant(네 발): Blender에서 만든 Generic 리그 — 모델 Models/Rigged/{종}_Mutant_Rigged.fbx, 동작 Animation/Mutant/{종}_Mutant@{상태 이름}.fbx(파일 하나 = 클립 하나, 뼈는 경로로 맞는다).
    /// 컨트롤러 Controllers/{종}_Mutant.controller는 E 개체와 같은 구조(<see cref="TryBuildMutant"/>). 걷기 · 달리기 · 돌진 재생 빠르기는 클립에서 발이 땅을 미는 빠르기를 재서 이동 빠르기에 맞춘다.
    /// </summary>
    public static class EnemyAnimationSetup
    {
        private const string RiggedFolder = "Assets/Game/Art/Enemies/Models/Rigged";
        private const string ClipFolder = "Assets/Game/Art/Enemies/Animation";
        private const string ControllerFolder = ClipFolder + "/Controllers";
        // 고라니 쪽 에셋(읽기만): 몸 숙인 괴물 걸음 + 달리기 후보(느린 것부터). 종마다 이동 빠르기에 맞는 것을 고른다(PickRun)
        private const string WalkClipPath = "Assets/Game/Art/Gorani/Animation/Mutant Walk.fbx";
        private static readonly string[] RunClipPaths =
        {
            // 몸 숙인 괴물 달리기(Mutant Run)는 빼다(결정 2026-11-19, 사용자): 발을 오래 디디고 몸이 멈췄다 나가는 동작이라 일정한 빠르기로 밀면 발이 끌렸다. 빠르기가 고른 클립만 둔다
            "Assets/Game/Art/Gorani/Animation/Jog Forward.fbx", // 2.4 m/s
            "Assets/Game/Art/Gorani/Animation/Run.fbx",         // 4.2 m/s
            "Assets/Game/Art/Gorani/Animation/Fast Run.fbx",    // 4.9 m/s
        };

        // 동작 파일(Art/Enemies/Animation/{이름}.fbx)
        private const string Idle = "E_Idle_Mutant";
        private const string HitReaction = "E_Hit_Zombie";
        private const string DeathBack = "E_Death_Mutant";    // 뒤로 넘어짐
        private const string DeathFront = "E_Death_Zombie";   // 비틀거리다 앞으로 쓰러짐
        private const string Dizzy = "E_Stun_Dizzy";
        private const string Swipe1 = "Badger_Swipe1_MutantSwiping"; // 몸을 틀었다가 왼손으로 크게 — 39프레임에 닿음
        private const string Swipe2 = "Badger_Swipe2_MutantPunch";   // 오른손 짧게 — 9프레임
        private const string Swipe3 = "Badger_Swipe3_ZombieSwipe";   // 왼손 들었다가 오른손 — 31프레임
        private const string Roar = "Boar_ChargeWindup_MutantRoaring"; // 몸을 젖혔다가(30~42) 앞으로 숙임(54)
        private const string Overhead = "Boar_Slam_ZombieOverhead";    // 두 손 들고(24~40) 내려찍음 — 47프레임
        private const string Hop = "Hare_Hop_ForwardJump";       // 웅크림(9~18) · 공중(21~36) · 착지(36~)
        private const string JumpAttack = "Hare_JumpAttack_Mutant"; // 웅크림(9~15) · 높이 뜀(16~47) · 찍으며 착지(47~)
        private const string Scream = "Toad_Spit_ZombieScream";  // 젖혔다가(12~24) 앞으로 내지름(30)

        /// <summary>잇는 구간 사이 · 대기로 돌아갈 때 섞는 시간(초).</summary>
        private const float ChainBlend = 0.06f;
        private const float ReturnBlend = 0.25f;
        /// <summary>맞은 동작 길이(초) — 원본 1.07초를 빠르게.</summary>
        private const float HitSeconds = 0.5f;
        /// <summary>죽는 동작이 끝나는 때(사라질 때까지 시간의 비율). 나머지는 쓰러진 채.</summary>
        private const float DeathFallShare = 0.7f;
        /// <summary>달리기 클립 재생 빠르기 범위(발 미끄러짐을 줄이려 이동 빠르기에 맞추되 이 안에서만).</summary>
        private const float MinRunRate = 0.75f;
        private const float MaxRunRate = 1.8f;

        /// <summary>컨트롤러를 만드는 동안만: 땅 빠르기를 잴 리깅 모델 · 그 배율(TryBuild가 넣는다. 없으면 재지 않고 어림값).</summary>
        private static GameObject measureModel;
        private static float measureScale = 1f;
        /// <summary>동작 파일의 한 구간(프레임, 30fps) → 클립 하나. seconds 동안 재생(0이면 원래 빠르기).</summary>
        private struct Part
        {
            public string file;
            public int first;
            public int last;
            public float seconds;
            public bool loop;
            /// <summary>높이를 포즈에 굽지 않는다(높이 뜨는 구간 — 뜨는 높이는 코드가 준다).</summary>
            public bool freeHeight;
            /// <summary>앞뒤 · 좌우 이동을 포즈에 굽는다(쓰러지는 동작).</summary>
            public bool bakeMove;
            /// <summary>더하기 레이어용(첫 프레임이 기준 자세).</summary>
            public bool additive;

            public string ClipName => $"{file}_{first}_{last}{(loop ? "_Loop" : "")}{(freeHeight ? "_Air" : "")}{(bakeMove ? "_Fall" : "")}{(additive ? "_Add" : "")}";
        }

        /// <summary>상태 하나(= EnemyAnim 이름). 구간이 여럿이면 차례로 저절로 이어진다. 끝나면 대기/이동으로 돌아간다(returns).</summary>
        private sealed class Sequence
        {
            public string state;
            public Part[] parts;
            public bool returns = true;
            /// <summary>동작 파일 구간 대신 쓰는 바깥 클립(고라니 달리기) · 재생 빠르기.</summary>
            public AnimationClip clip;
            public float speed = 1f;
        }

        private static Part P(string file, int first, int last, float seconds = 0f)
        {
            return new Part { file = file, first = first, last = last, seconds = seconds };
        }

        private static Part Looping(Part part) { part.loop = true; return part; }
        private static Part Air(Part part) { part.freeHeight = true; return part; }
        private static Part Fall(Part part) { part.bakeMove = true; return part; }
        private static Part Additive(Part part) { part.additive = true; return part; }
        private static Sequence S(string state, params Part[] parts) => new Sequence { state = state, parts = parts };

        private static Part IdlePart => Looping(P(Idle, 0, 121));
        private static Part HitPart => Additive(P(HitReaction, 8, 40, HitSeconds));

        /// <summary>
        /// 종마다 동작 표: 상태 ← 동작 파일 구간(프레임) · 길이(수치 에셋의 시간).
        /// 예고 상태는 "손이 닿는 프레임"에서 끝나게 자르고 길이를 예고 시간으로 맞춘다 → 코드가 피해를 넣는 순간 = 닿는 순간.
        /// </summary>
        private static List<Sequence> SequencesFor(EnemySpecies species, EnemyTuning tuning, float stride)
        {
            var list = new List<Sequence>();
            float fall = tuning.evolvedCorpseTime * DeathFallShare;
            Part death = Fall(P(DeathBack, 20, 84, fall));
            switch (species)
            {
                case EnemySpecies.Badger:
                {
                    var t = (BadgerTuning)tuning;
                    float gap = t.comboInterval;
                    list.Add(S(EnemyAnim.AttackState, P(Swipe1, 6, 39, t.windupTime)));
                    list.Add(S(EnemyAnim.AttackRecoverState, P(Swipe1, 39, 78, t.recoverTime)));
                    // 3연속: 첫 번째는 Attack과 같은 예고. 두 번째 = 오른손 짧게, 세 번째 = 오른손 크게
                    list.Add(S(EnemyAnim.Combo2State, P(Swipe1, 39, 48, gap * 0.45f), P(Swipe2, 0, 9, gap * 0.55f)));
                    list.Add(S(EnemyAnim.Combo3State, P(Swipe2, 9, 15, gap * 0.27f), P(Swipe3, 15, 31, gap * 0.73f)));
                    list.Add(S(EnemyAnim.ComboRecoverState, P(Swipe3, 31, 78, t.comboRecoverTime)));
                    break;
                }
                case EnemySpecies.Boar:
                {
                    var t = (BoarTuning)tuning;
                    list.Add(S(EnemyAnim.ChargeWindupState, P(Roar, 18, 54, t.windupTime)));
                    AnimationClip chargeRun = PickRun(t.chargeSpeed / stride, 1f, 2f, out float chargeRate, stride);
                    // 돌진은 빠르기가 커서 어림(클립 평균 빠르기 × 걸음 길이)으로는 발이 많이 미끄러졌다 → 이 모델에서 디딘 발이 몸(엉덩이) 기준으로 뒤로 가는 빠르기를 재서 맞춘다
                    float chargeGround = GroundSpeed(measureModel, chargeRun, "Hips") * measureScale;
                    if (chargeGround > 0.5f) chargeRate = Mathf.Clamp(t.chargeSpeed / chargeGround, 0.75f, 2f);
                    list.Add(new Sequence { state = EnemyAnim.ChargeState, clip = chargeRun, speed = chargeRate });
                    // 두 손을 드는 구간 + 내려찍는 구간(가운데 들고 버티는 16프레임은 뺀다 — 예고가 0.5초라)
                    list.Add(S(EnemyAnim.SlamState, P(Overhead, 18, 27, t.slamWindupTime * 0.54f), P(Overhead, 40, 47, t.slamWindupTime * 0.46f)));
                    list.Add(S(EnemyAnim.SlamRecoverState, P(Overhead, 47, 84, t.slamRecoverTime)));
                    list.Add(S(EnemyAnim.StunState, Looping(P(Dizzy, 0, 128))));
                    break;
                }
                case EnemySpecies.Hare:
                {
                    var t = (HareTuning)tuning;
                    list.Add(S(EnemyAnim.CrouchState, P(Hop, 3, 21, t.crouchTime)));
                    list.Add(S(EnemyAnim.LeapState, P(Hop, 21, 36, t.leapTime)));
                    list.Add(S(EnemyAnim.LandState, P(Hop, 36, 60, t.recoverTime)));
                    list.Add(S(EnemyAnim.SlamCrouchState, P(JumpAttack, 0, 16, t.slamCrouchTime)));
                    list.Add(S(EnemyAnim.SlamLeapState, Air(P(JumpAttack, 16, 47, t.slamLeapTime))));
                    list.Add(S(EnemyAnim.SlamLandState, P(JumpAttack, 47, 90, t.slamRecoverTime)));
                    death = Fall(P(DeathFront, 8, 85, fall));
                    break;
                }
                case EnemySpecies.Toad:
                {
                    var t = (ToadTuning)tuning;
                    list.Add(S(EnemyAnim.SpitWindupState, P(Scream, 4, 30, t.windupTime)));
                    // 내지른 자세로 잠깐 + 일어서기(가운데 오래 버티는 구간은 뺀다 — 빈틈이 1초라)
                    list.Add(S(EnemyAnim.SpitState, P(Scream, 30, 36, t.recoverTime * 0.2f), P(Scream, 58, 84, t.recoverTime * 0.8f)));
                    death = Fall(P(DeathFront, 8, 85, fall));
                    break;
                }
                case EnemySpecies.RaccoonDog:
                {
                    // 오소리와 같은 할퀴기 동작(더 짧게). 연속은 두 번째부터 매번 Combo2(오른손 짧게)
                    var t = (RaccoonDogTuning)tuning;
                    float gap = t.comboInterval;
                    list.Add(S(EnemyAnim.AttackState, P(Swipe1, 6, 39, t.windupTime)));
                    list.Add(S(EnemyAnim.AttackRecoverState, P(Swipe1, 39, 78, t.recoverTime)));
                    list.Add(S(EnemyAnim.Combo2State, P(Swipe1, 39, 48, gap * 0.45f), P(Swipe2, 0, 9, gap * 0.55f)));
                    list.Add(S(EnemyAnim.ComboRecoverState, P(Swipe2, 9, 33, t.comboRecoverTime)));
                    death = Fall(P(DeathFront, 8, 85, fall));
                    break;
                }
                case EnemySpecies.Fox:
                {
                    // 웅크림(예고) + 앞으로 뛰기(달려들기) → 끝이 무는 순간. 물러나기는 착지 구간. 연속 달려들기 = 짧은 웅크림 + 달려들기
                    var t = (FoxTuning)tuning;
                    list.Add(S(EnemyAnim.AttackState, P(Hop, 3, 21, t.windupTime), P(Hop, 21, 36, t.lungeTime)));
                    list.Add(S(EnemyAnim.AttackRecoverState, P(Hop, 36, 60, t.retreatTime)));
                    list.Add(S(EnemyAnim.Combo2State, P(Hop, 3, 21, t.chainWindupTime), P(Hop, 21, 36, t.lungeTime)));
                    break;
                }
            }
            list.Add(new Sequence { state = EnemyAnim.DeathState, parts = new[] { death }, returns = false });
            return list;
        }

        /// <summary>EnemySetup이 프리팹에 넣을 것.</summary>
        internal struct Rig
        {
            public Animator animator;
            /// <summary>머리 뼈(두꺼비 독 나가는 곳을 여기에 붙인다).</summary>
            public Transform head;
            /// <summary>입 앞(머리 앞쪽 끝, Body 좌표).</summary>
            public Vector3 mouth;
        }

        /// <summary>
        /// 동작 FBX를 Humanoid로 가져오고 표의 구간대로 클립을 자른다(모든 종의 표를 모아 파일마다 한 번). 폴더가 없으면 아무것도 안 한다.
        /// </summary>
        internal static void ImportClips(params (EnemySpecies species, EnemyTuning tuning)[] entries)
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder)) return;
            ImportMutantClips();
            ModelPrefabSetup.UseGoraniAvatarForClips(ClipFolder, false); // 아래 Mutant 폴더(Generic)는 건드리지 않는다

            var byFile = new Dictionary<string, List<Part>>();
            void Need(Part part)
            {
                if (!byFile.TryGetValue(part.file, out List<Part> parts)) byFile[part.file] = parts = new List<Part>();
                if (!parts.Exists(p => p.ClipName == part.ClipName)) parts.Add(part);
            }
            Need(IdlePart);
            Need(HitPart);
            foreach (var (species, tuning) in entries)
            {
                foreach (Sequence sequence in SequencesFor(species, tuning, 1f))
                {
                    if (sequence.parts == null) continue;
                    foreach (Part part in sequence.parts) Need(part);
                }
            }

            foreach (KeyValuePair<string, List<Part>> pair in byFile)
            {
                string path = $"{ClipFolder}/{pair.Key}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || importer.defaultClipAnimations.Length == 0)
                {
                    Debug.LogWarning($"[EnemyAnimationSetup] 동작 파일이 없거나 동작이 들어 있지 않습니다: {path}");
                    continue;
                }
                pair.Value.Sort((a, b) => string.CompareOrdinal(a.ClipName, b.ClipName));
                var wanted = new ModelImporterClipAnimation[pair.Value.Count];
                for (int i = 0; i < wanted.Length; i++)
                {
                    Part part = pair.Value[i];
                    ModelImporterClipAnimation clip = importer.defaultClipAnimations[0]; // 부를 때마다 새 것
                    clip.name = part.ClipName;
                    clip.firstFrame = part.first;
                    clip.lastFrame = part.last;
                    clip.loopTime = part.loop;
                    clip.loopPose = false;
                    clip.lockRootRotation = true;
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = !part.freeHeight;
                    clip.keepOriginalPositionY = true;
                    clip.heightFromFeet = false;
                    clip.lockRootPositionXZ = part.bakeMove;
                    clip.keepOriginalPositionXZ = part.bakeMove;
                    clip.hasAdditiveReferencePose = part.additive;
                    clip.additiveReferencePoseFrame = part.additive ? part.first : 0f;
                    wanted[i] = clip;
                }
                if (SameClips(importer.clipAnimations, wanted)) continue;
                importer.clipAnimations = wanted;
                importer.SaveAndReimport();
            }
        }

        private static bool SameClips(ModelImporterClipAnimation[] a, ModelImporterClipAnimation[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                ModelImporterClipAnimation x = a[i], y = b[i];
                if (x.name != y.name || x.firstFrame != y.firstFrame || x.lastFrame != y.lastFrame || x.loopTime != y.loopTime || x.loopPose != y.loopPose
                    || x.lockRootRotation != y.lockRootRotation || x.keepOriginalOrientation != y.keepOriginalOrientation
                    || x.lockRootHeightY != y.lockRootHeightY || x.keepOriginalPositionY != y.keepOriginalPositionY || x.heightFromFeet != y.heightFromFeet
                    || x.lockRootPositionXZ != y.lockRootPositionXZ || x.keepOriginalPositionXZ != y.keepOriginalPositionXZ
                    || x.hasAdditiveReferencePose != y.hasAdditiveReferencePose || x.additiveReferencePoseFrame != y.additiveReferencePoseFrame) return false;
            }
            return true;
        }

        /// <summary>
        /// Body 아래에 리깅 모델을 <c>Model</c>(크기 · 회전 · 위치 보정) → 모델 인스턴스(Animator)로 넣는다. 키(T-포즈 발바닥~머리 끝)를 height에 맞춘다.
        /// 리깅 파일 · 아바타 · 대기 클립이 없으면 아무것도 넣지 않고 false(EnemySetup이 정적 모델로 대신한다).
        /// </summary>
        /// <param name="modelPath">리깅 모델 자리. null이면 Models/Rigged/{file}.fbx(숲 E 개체 Mixamo). 다른 리깅 모델로 바꿀 때는 EnemySetup 표의 이 칸만 바꾼다</param>
        internal static bool TryBuild(EnemySpecies species, string file, string modelPath, float height, float yaw, Vector3 offset, Material material, EnemyTuning tuning, Transform body, out Rig rig)
        {
            rig = default;
            string path = string.IsNullOrEmpty(modelPath) ? $"{RiggedFolder}/{file}.fbx" : modelPath;
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || material == null || tuning == null) return false;
            ConfigureModel(importer);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar found) avatar = found;
            }
            if (model == null || avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning($"[EnemyAnimationSetup] {file}: Humanoid 아바타를 만들지 못해 정적 모델을 씁니다: {path}");
                return false;
            }
            if (LoadClip($"{ClipFolder}/{Idle}.fbx", IdlePart.ClipName) == null)
            {
                Debug.LogWarning($"[EnemyAnimationSetup] {file}: 대기 동작이 없어 정적 모델을 씁니다: {ClipFolder}/{Idle}.fbx");
                return false;
            }

            var holder = new GameObject("Model").transform;
            holder.SetParent(body, false);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = avatar;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (renderers.Length == 0 || head == null || !Measure(body, renderers, head, out float bottom, out float top, out Vector3 mouth))
            {
                Object.DestroyImmediate(holder.gameObject);
                return false;
            }
            foreach (SkinnedMeshRenderer r in renderers)
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.updateWhenOffscreen = false;
            }

            // Humanoid 동작이 몸을 모델 원점(발밑) 위에 세운다 — 앞뒤 · 좌우는 옮기지 않고 T-포즈 발바닥만 바닥에 맞춘다(주민과 같은 방식)
            float scale = height / Mathf.Max(0.001f, top - bottom);
            holder.localScale = Vector3.one * scale;
            holder.localPosition = new Vector3(0f, -bottom * scale, 0f) + offset;

            float stride = animator.humanScale * scale; // Y Bot 걸음 → 이 모델 걸음 길이
            measureModel = model;
            measureScale = scale;
            animator.runtimeAnimatorController = BuildController(species, tuning, stride);
            measureModel = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.updateMode = AnimatorUpdateMode.Normal;

            rig = new Rig { animator = animator, head = head, mouth = holder.localPosition + mouth * scale };
            return true;
        }

        /// <summary>리깅 FBX를 Humanoid(이 모델로 아바타 만들기)로. T-포즈 한 장뿐이라 동작 · 재질은 가져오지 않는다(주민 리깅 모델과 같은 설정).</summary>
        private static void ConfigureModel(ModelImporter importer)
        {
            bool changed = importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.importAnimation || importer.importCameras || importer.importLights || importer.optimizeGameObjects
                || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.addCollider;
            if (!changed) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false; // 머리 뼈에 독 나가는 곳을 붙인다
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.SaveAndReimport();
        }

        /// <summary>스키닝한 T-포즈 정점(space 좌표)으로 발바닥 · 머리 끝 높이와 입 앞(머리 뼈보다 위 · 가운데 줄에서 가장 앞)을 잰다.</summary>
        private static bool Measure(Transform space, SkinnedMeshRenderer[] renderers, Transform head, out float bottom, out float top, out Vector3 mouth)
        {
            var points = new List<Vector3>();
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r.sharedMesh == null) continue;
                r.BakeMesh(baked, true);
                Matrix4x4 toSpace = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                foreach (Vector3 v in baked.vertices) points.Add(toSpace.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(baked);
            bottom = float.MaxValue;
            top = float.MinValue;
            mouth = Vector3.zero;
            if (points.Count == 0) return false;
            foreach (Vector3 p in points)
            {
                bottom = Mathf.Min(bottom, p.y);
                top = Mathf.Max(top, p.y);
            }
            float headY = space.InverseTransformPoint(head.position).y;
            float halfWidth = (top - bottom) * 0.1f;
            float front = float.MinValue;
            foreach (Vector3 p in points)
            {
                if (p.y < headY || Mathf.Abs(p.x) > halfWidth || p.z <= front) continue;
                front = p.z;
                mouth = new Vector3(0f, p.y, p.z);
            }
            return top > bottom && front > float.MinValue;
        }

        // ---------- 컨트롤러 ----------

        /// <summary>
        /// 종 컨트롤러를 같은 에셋에 다시 만든다(프리팹 연결 유지). Base: Locomotion(대기 ↔ 걷기 ↔ 달리기, Speed m/s) + 표의 상태들. Hit 레이어: 더하기.
        /// 걷기 · 달리기 문턱은 클립의 원래 빠르기 × 이 모델 걸음 길이(stride)로 잡아 발이 덜 미끄러지게 하고, 달리기는 이동 빠르기에 맞춰 재생 빠르기를 조금 바꾼다.
        /// </summary>
        private static RuntimeAnimatorController BuildController(EnemySpecies species, EnemyTuning tuning, float stride)
        {
            if (!AssetDatabase.IsValidFolder(ControllerFolder)) AssetDatabase.CreateFolder(ClipFolder, "Controllers");
            string path = $"{ControllerFolder}/{species}_E.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.layers = new AnimatorControllerLayer[0];
            controller.parameters = new AnimatorControllerParameter[0];
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub != null && sub != controller) Object.DestroyImmediate(sub, true);
            }
            controller.AddParameter(EnemyAnim.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddLayer("Base");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // 대기 ↔ 걷기 ↔ 달리기
            AnimationClip idle = LoadClip($"{ClipFolder}/{Idle}.fbx", IdlePart.ClipName);
            AnimationClip walk = LoadClip(WalkClipPath, null);
            AnimationClip run = PickRun(tuning.moveSpeed / stride, MinRunRate, MaxRunRate, out float runRate, stride);
            AnimatorState locomotion = controller.CreateBlendTreeInController(EnemyAnim.LocomotionState, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = EnemyAnim.SpeedParameter;
            tree.useAutomaticThresholds = false;
            var children = new List<ChildMotion> { new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f } };
            float walkSpeed = NaturalSpeed(walk, stride);
            float runNatural = NaturalSpeed(run, stride);
            float runSpeed = tuning.moveSpeed;
            if (run != null && runNatural > 0.01f)
            {
                runSpeed = runNatural * runRate;
                if (walk != null && walkSpeed > 0.05f && walkSpeed < runSpeed * 0.7f) children.Add(new ChildMotion { motion = walk, threshold = walkSpeed, timeScale = 1f });
                children.Add(new ChildMotion { motion = run, threshold = runSpeed, timeScale = runRate });
            }
            else
            {
                Debug.LogWarning($"[EnemyAnimationSetup] 이동 클립을 찾지 못했습니다(이동 중에도 대기 동작): {RunClipPaths[0]}");
            }
            tree.children = children.ToArray();
            locomotion.iKOnFeet = false;
            machine.defaultState = locomotion;

            var report = new System.Text.StringBuilder();
            foreach (Sequence sequence in SequencesFor(species, tuning, stride))
            {
                AnimatorState previous = null;
                if (sequence.clip != null || sequence.parts == null)
                {
                    previous = machine.AddState(sequence.state);
                    previous.motion = sequence.clip;
                    previous.speed = sequence.speed;
                    previous.iKOnFeet = false;
                    report.Append($" · {sequence.state} ×{sequence.speed:0.00}");
                    continue; // 바깥 클립은 도는 동작 — 코드가 다음 상태로 넘긴다
                }
                bool loops = false;
                for (int i = 0; i < sequence.parts.Length; i++)
                {
                    Part part = sequence.parts[i];
                    AnimationClip clip = LoadClip($"{ClipFolder}/{part.file}.fbx", part.ClipName);
                    if (clip == null) Debug.LogWarning($"[EnemyAnimationSetup] {species}: {sequence.state} 동작 클립이 없습니다: {part.ClipName}");
                    AnimatorState state = machine.AddState(i == 0 ? sequence.state : $"{sequence.state}_{i + 1}");
                    state.motion = clip;
                    state.speed = clip != null && part.seconds > 0.01f ? clip.length / part.seconds : 1f;
                    state.iKOnFeet = false;
                    if (previous != null) Link(previous, state, ChainBlend);
                    previous = state;
                    loops = part.loop;
                    report.Append($" · {(i == 0 ? sequence.state : "+")} {part.file} {part.first}~{part.last} ×{state.speed:0.00}");
                }
                if (sequence.returns && !loops && previous != null) Link(previous, locomotion, ReturnBlend);
            }

            // 맞은 동작: 더하기 레이어 — 하던 동작 위에 움찔만 얹는다
            var hitMachine = new AnimatorStateMachine { name = EnemyAnim.HitLayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(hitMachine, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = EnemyAnim.HitLayerName,
                stateMachine = hitMachine,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
            });
            AnimatorState none = hitMachine.AddState("None");
            hitMachine.defaultState = none;
            AnimationClip hitClip = LoadClip($"{ClipFolder}/{HitReaction}.fbx", HitPart.ClipName);
            AnimatorState hit = hitMachine.AddState(EnemyAnim.HitState);
            hit.motion = hitClip;
            hit.speed = hitClip != null ? hitClip.length / HitSeconds : 1f;
            hit.iKOnFeet = false;
            Link(hit, none, 0.12f);

            EditorUtility.SetDirty(controller);
            Debug.Log($"[EnemyAnimationSetup] {species}_E: 걸음 길이 ×{stride:0.00} · 걷기 {walkSpeed:0.00} m/s · 달리기 {runSpeed:0.00} m/s({(run != null ? System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(run)) : "없음")} 재생 ×{runRate:0.00}, 이동 {tuning.moveSpeed:0.0} m/s){report}");
            return controller;
        }

        /// <summary>앞 상태가 끝나면(끝 프레임) 다음 상태로 넘어간다.</summary>
        private static void Link(AnimatorState from, AnimatorState to, float blend)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = blend;
            transition.offset = 0f;
            transition.canTransitionToSelf = false;
        }

        /// <summary>
        /// 달리기 클립 고르기. wanted = 이 모델이 내야 하는 빠르기를 Y Bot 크기로 환산한 값(m/s ÷ 걸음 길이).
        /// 괴물 달리기로 될 만하면 그것, 아니면 재생 빠르기가 1에 가장 가까운 후보. rate = 재생 빠르기(범위 안으로 자른다).
        /// </summary>
        private static AnimationClip PickRun(float wanted, float minRate, float maxRate, out float rate, float stride = 1f)
        {
            AnimationClip best = null;
            float bestCost = float.MaxValue;
            rate = 1f;
            for (int i = 0; i < RunClipPaths.Length; i++)
            {
                AnimationClip clip = LoadClip(RunClipPaths[i], null);
                float natural = NaturalSpeed(clip, stride) / stride;
                if (natural < 0.01f) continue;
                float needed = wanted / natural;
                float cost = Mathf.Abs(Mathf.Log(Mathf.Max(0.01f, needed)));
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = clip;
                rate = Mathf.Clamp(needed, minRate, maxRate);
            }
            return best;
        }

        /// <summary>
        /// 이동 클립이 이 모델에서 어울리는 이동 빠르기(m/s). 컨트롤러를 만드는 중이면(measureModel) 이 모델에 클립을 샘플해 디딘 발이 엉덩이 기준으로 뒤로 가는 빠르기를 잰다
        /// (클립 평균 빠르기 × 걸음 길이 어림은 이 모델들에서 실제보다 작게 나와 발이 헛돌았다). 못 재면 어림값.
        /// </summary>
        private static float NaturalSpeed(AnimationClip clip, float stride)
        {
            if (clip == null) return 0f;
            float estimate = Mathf.Abs(clip.averageSpeed.z) * stride;
            if (measureModel == null) return estimate;
            float measured = GroundSpeed(measureModel, clip, "Hips") * measureScale;
            return measured > 0.2f ? measured : estimate;
        }


        // ---------- Mutant(네 발, Generic 리그) ----------

        private const string MutantClipFolder = ClipFolder + "/Mutant";
        private const string MutantIdle = "Idle";
        private const string MutantWalk = "Walk";
        private const string MutantRun = "Run";
        /// <summary>도는 동작(마지막 프레임 = 첫 프레임). 나머지는 한 번.</summary>
        private static readonly string[] MutantLoops = { MutantIdle, MutantWalk, MutantRun, EnemyAnim.ChargeState, EnemyAnim.StunState };
        /// <summary>달리기 · 돌진 클립 재생 빠르기 범위(이동 빠르기에 맞추되 이 안에서만).</summary>
        private const float MinMutantRunRate = 0.6f;
        private const float MaxMutantRunRate = 2f;
        private const float MaxChargeRate = 2.5f;

        /// <summary>
        /// 종마다 Mutant 상태 표: 상태(= 클립 이름) · 길이(수치 에셋의 시간, 0이면 원래 빠르기) · 맞히는 프레임(0이면 끝 프레임).
        /// 예고 클립은 그 프레임이 닿는 · 뱉는 순간 — 길이는 처음부터 그 프레임까지를 맞추고, 남은 프레임은 같은 빠르기로 이어 튼다(빈틈 동작이 따로 없는 너구리).
        /// </summary>
        private static List<(string state, float seconds, int hitFrame)> MutantStatesFor(EnemySpecies species, EnemyTuning tuning)
        {
            var list = new List<(string state, float seconds, int hitFrame)>();
            switch (species)
            {
                case EnemySpecies.Badger:
                {
                    var t = (BadgerTuning)tuning;
                    list.Add((EnemyAnim.AttackState, t.windupTime, 0));
                    list.Add((EnemyAnim.AttackRecoverState, t.recoverTime, 0));
                    break;
                }
                case EnemySpecies.Boar:
                {
                    var t = (BoarTuning)tuning;
                    list.Add((EnemyAnim.ChargeWindupState, t.windupTime, 0));
                    list.Add((EnemyAnim.ChargeState, 0f, 0)); // 재생 빠르기는 돌진 빠르기에 맞춘다
                    list.Add((EnemyAnim.StunState, 0f, 0));
                    break;
                }
                case EnemySpecies.Hare:
                {
                    var t = (HareTuning)tuning;
                    list.Add((EnemyAnim.CrouchState, t.crouchTime, 0));
                    list.Add((EnemyAnim.LeapState, t.leapTime, 0));
                    list.Add((EnemyAnim.LandState, t.recoverTime, 0));
                    break;
                }
                case EnemySpecies.Toad:
                {
                    var t = (ToadTuning)tuning;
                    list.Add((EnemyAnim.SpitWindupState, t.windupTime, 0));
                    list.Add((EnemyAnim.SpitState, t.recoverTime, 0));
                    break;
                }
                case EnemySpecies.RaccoonDog:
                {
                    // Attack 24프레임 한 벌: 0~7 몸을 일으킴 → 8~11 앞으로 덮치며 무는 순간(11) → 12~24 제자리(빈틈 동안 이어 튼다)
                    var t = (RaccoonDogTuning)tuning;
                    list.Add((EnemyAnim.AttackState, t.windupTime, 11));
                    break;
                }
                case EnemySpecies.Fox:
                {
                    // Attack 30프레임: 0~11 웅크림 → 11~15 달려들어 무는 순간(15, 턱을 닫음) / AttackRecover: 뒤로 뛰어 물러나기
                    var t = (FoxTuning)tuning;
                    list.Add((EnemyAnim.AttackState, t.windupTime + t.lungeTime, 15));
                    list.Add((EnemyAnim.AttackRecoverState, t.retreatTime, 0));
                    break;
                }
            }
            list.Add((EnemyAnim.DeathState, tuning.evolvedCorpseTime * DeathFallShare, 0));;
            return list;
        }

        /// <summary>
        /// Mutant 동작 FBX({종}_Mutant@{클립}.fbx)를 Generic으로 가져온다: 클립 이름 = @ 뒤(= 상태 이름), 도는 동작은 루프, Hit는 더하기 기준 프레임 0.
        /// 아바타 없이 뼈 경로(Armature/Root/…)로 맞는다 — 리깅 모델과 계층이 같다. 루트 모션 없음(루트 뼈 움직임은 그대로 포즈에 남는다 — 죽는 동작이 쓰러지는 쪽).
        /// </summary>
        private static void ImportMutantClips()
        {
            if (!AssetDatabase.IsValidFolder(MutantClipFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MutantClipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                int at = fileName.LastIndexOf('@');
                if (at < 0 || !(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                string clipName = fileName.Substring(at + 1);
                bool loop = System.Array.IndexOf(MutantLoops, clipName) >= 0;
                bool additive = clipName == EnemyAnim.HitState;

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
                    Debug.LogWarning($"[EnemyAnimationSetup] 동작이 들어 있지 않습니다: {path}");
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

        private static AnimationClip MutantClip(string file, string clip) => LoadClip($"{MutantClipFolder}/{file}@{clip}.fbx", clip);

        /// <summary>
        /// Body 아래에 네 발 리깅 모델({file}_Rigged.fbx)을 <c>Model</c>(크기 · 회전 · 위치 보정) → 모델 인스턴스(Animator)로 넣는다.
        /// 정적 모델과 같은 기준: 앞뒤 길이를 length에 맞추고 바닥 가운데를 발밑 피벗에. 리깅 파일 · 대기 클립이 없으면 false(EnemySetup이 정적 모델로 대신한다).
        /// </summary>
        /// <param name="modelPath">리깅 모델 자리. null이면 Models/Rigged/{file}_Rigged.fbx</param>
        /// <param name="footSpeed">발이 안 미끄러지는 걷기(x) · 달리기(y) 빠르기(m/s, 모델 길이 1 기준 — 모델 배율을 곱한다). 0이면 클립을 샘플해 잔다</param>
        internal static bool TryBuildMutant(EnemySpecies species, string file, string modelPath, Vector2 footSpeed, float length, float yaw, Vector3 offset, Material material, EnemyTuning tuning, Transform body, out Rig rig)
        {
            rig = default;
            string path = string.IsNullOrEmpty(modelPath) ? $"{RiggedFolder}/{file}_Rigged.fbx" : modelPath;
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || material == null || tuning == null) return false;
            ConfigureMutantModel(importer);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) return false;
            if (MutantClip(file, MutantIdle) == null)
            {
                Debug.LogWarning($"[EnemyAnimationSetup] {file}: 대기 동작이 없어 정적 모델을 씁니다: {MutantClipFolder}/{file}@{MutantIdle}.fbx");
                return false;
            }

            var holder = new GameObject("Model").transform;
            holder.SetParent(body, false);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            List<Vector3> points = SkinnedPoints(body, renderers);
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
            // 입 앞: 가운데 줄에서 가장 앞
            float strip = (max.x - min.x) * 0.15f;
            float front = float.MinValue;
            Vector3 mouth = new Vector3(center.x, center.y, max.z);
            foreach (Vector3 p in points)
            {
                if (Mathf.Abs(p.x - center.x) > strip || p.z <= front) continue;
                front = p.z;
                mouth = new Vector3(center.x, p.y, p.z);
            }
            foreach (SkinnedMeshRenderer r in renderers)
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.updateWhenOffscreen = false;
            }

            float scale = length / Mathf.Max(0.001f, max.z - min.z);
            holder.localScale = Vector3.one * scale;
            holder.localPosition = new Vector3(-center.x * scale, -min.y * scale, -center.z * scale) + offset;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar avatar && avatar.isValid) animator.avatar = avatar;
            }
            animator.runtimeAnimatorController = BuildMutantController(species, file, tuning, model, scale, footSpeed);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.updateMode = AnimatorUpdateMode.Normal;

            rig = new Rig { animator = animator, head = FindDeep(instance.transform, "Head"), mouth = holder.localPosition + mouth * scale };
            return true;
        }

        /// <summary>네 발 리깅 FBX를 Generic(이 모델로 아바타 만들기, 루트 모션 뼈 없음)으로. 동작 · 재질은 가져오지 않는다.</summary>
        private static void ConfigureMutantModel(ModelImporter importer)
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
            importer.optimizeGameObjects = false; // 뼈를 경로로 찾는다 · 머리 뼈에 독 나가는 곳을 붙인다
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.SaveAndReimport();
        }

        /// <summary>스키닝한 기본 자세 정점(space 좌표).</summary>
        private static List<Vector3> SkinnedPoints(Transform space, SkinnedMeshRenderer[] renderers)
        {
            var points = new List<Vector3>();
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer r in renderers)
            {
                if (r.sharedMesh == null) continue;
                r.BakeMesh(baked, true);
                Matrix4x4 toSpace = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                foreach (Vector3 v in baked.vertices) points.Add(toSpace.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(baked);
            return points;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        /// <summary>
        /// 제자리 이동 클립에서 발이 땅을 미는 빠르기(모델 단위/초) = 이 클립이 어울리는 이동 빠르기.
        /// 발 뼈(FrontFoot_ · HindFoot_)가 낮게 있으면서(자기 높이 범위의 아래 1/4) 뒤로 가는 구간의 거리 ÷ 시간. 못 재면 0.
        /// </summary>
        private static float GroundSpeed(GameObject model, AnimationClip clip, string reference = null)
        {
            if (model == null || clip == null || clip.length < 0.01f) return 0f;
            GameObject probe = Object.Instantiate(model);
            probe.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var feet = new List<Transform>();
                foreach (Transform t in probe.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("FrontFoot_") || t.name.StartsWith("HindFoot_") || t.name.EndsWith("LeftToeBase") || t.name.EndsWith("RightToeBase")) feet.Add(t);
                }
                if (feet.Count == 0) return 0f;
                int steps = Mathf.Max(8, Mathf.RoundToInt(clip.length * clip.frameRate));
                float dt = clip.length / steps;
                var positions = new Vector3[feet.Count, steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    clip.SampleAnimation(probe, dt * i);
                    // Humanoid 클립은 샘플할 때 몸이 앞으로 나간다 → 기준 뼈(엉덩이)에서 본 발 위치로 잰다(높이는 그대로)
                    Vector3 origin = Vector3.zero;
                    if (reference != null)
                    {
                        foreach (Transform bone in probe.GetComponentsInChildren<Transform>(true))
                        {
                            if (!bone.name.EndsWith(reference)) continue;
                            origin = new Vector3(bone.position.x, 0f, bone.position.z);
                            break;
                        }
                    }
                    for (int f = 0; f < feet.Count; f++) positions[f, i] = feet[f].position - origin; // 프로브는 원점 · 회전 없음(루트 모션으로 프로브가 움직여도 기준 뼈를 빼면 같다)
                }
                float moved = 0f, time = 0f;
                for (int f = 0; f < feet.Count; f++)
                {
                    float low = float.MaxValue, high = float.MinValue;
                    for (int i = 0; i <= steps; i++)
                    {
                        low = Mathf.Min(low, positions[f, i].y);
                        high = Mathf.Max(high, positions[f, i].y);
                    }
                    float limit = low + Mathf.Max((high - low) * 0.25f, 0.002f);
                    for (int i = 0; i < steps; i++)
                    {
                        Vector3 a = positions[f, i], b = positions[f, i + 1];
                        if (a.y > limit || b.y > limit || b.z >= a.z) continue;
                        moved += a.z - b.z;
                        time += dt;
                    }
                }
                return time > 0.0001f ? moved / time : 0f;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Mutant 종 컨트롤러를 같은 에셋에 다시 만든다(E 개체와 같은 구조). Base: Locomotion(대기 ↔ 걷기 ↔ 달리기, Speed m/s) + 종 상태들 + Death. Hit 레이어: 더하기.
        /// 걷기 문턱 = 걷기 클립의 땅 빠르기 × 모델 배율, 달리기는 이동 빠르기에 맞춰 재생 빠르기를 바꾼다(발 미끄러짐 줄이기).
        /// </summary>
        private static RuntimeAnimatorController BuildMutantController(EnemySpecies species, string file, EnemyTuning tuning, GameObject model, float scale, Vector2 footSpeed)
        {
            if (!AssetDatabase.IsValidFolder(ControllerFolder)) AssetDatabase.CreateFolder(ClipFolder, "Controllers");
            string path = $"{ControllerFolder}/{species}_Mutant.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.layers = new AnimatorControllerLayer[0];
            controller.parameters = new AnimatorControllerParameter[0];
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub != null && sub != controller) Object.DestroyImmediate(sub, true);
            }
            controller.AddParameter(EnemyAnim.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddLayer("Base");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimationClip idle = MutantClip(file, MutantIdle);
            AnimationClip walk = MutantClip(file, MutantWalk);
            AnimationClip run = MutantClip(file, MutantRun);
            // 발 빠르기를 알고 있으면(Blender에서 잔 값) 그것, 아니면 샘플해 잔다
            float walkSpeed = (footSpeed.x > 0f ? footSpeed.x : GroundSpeed(model, walk)) * scale;
            float runNatural = (footSpeed.y > 0f ? footSpeed.y : GroundSpeed(model, run)) * scale;
            // 발을 디딘 채 뛰는(발이 뒤로 밀리지 않는) 깡충 동작은 땅 빠르기가 거의 0으로 나온다 → 못 잰 것으로 보고 원래 빠르기로 튼다
            bool measured = runNatural > tuning.moveSpeed * 0.25f;
            float runRate = measured ? Mathf.Clamp(tuning.moveSpeed / runNatural, MinMutantRunRate, MaxMutantRunRate) : 1f;
            float runSpeed = measured ? runNatural * runRate : tuning.moveSpeed;
            AnimatorState locomotion = controller.CreateBlendTreeInController(EnemyAnim.LocomotionState, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = EnemyAnim.SpeedParameter;
            tree.useAutomaticThresholds = false;
            var children = new List<ChildMotion> { new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f } };
            bool usesWalk = walk != null && walkSpeed > 0.05f && walkSpeed < runSpeed * 0.7f;
            if (usesWalk) children.Add(new ChildMotion { motion = walk, threshold = walkSpeed, timeScale = 1f });
            if (run != null) children.Add(new ChildMotion { motion = run, threshold = runSpeed, timeScale = runRate });
            else Debug.LogWarning($"[EnemyAnimationSetup] {file}: 달리기 클립이 없습니다(이동 중에도 대기 · 걷기 동작)");
            tree.children = children.ToArray();
            locomotion.iKOnFeet = false;
            machine.defaultState = locomotion;

            var report = new System.Text.StringBuilder();
            foreach ((string name, float seconds, int hitFrame) in MutantStatesFor(species, tuning))
            {
                AnimationClip clip = MutantClip(file, name);
                if (clip == null) Debug.LogWarning($"[EnemyAnimationSetup] {file}: {name} 동작 클립이 없습니다");
                AnimatorState state = machine.AddState(name);
                state.motion = clip;
                state.iKOnFeet = false;
                bool loop = System.Array.IndexOf(MutantLoops, name) >= 0;
                float speed = 1f;
                if (clip != null && seconds > 0.01f) speed = (hitFrame > 0 ? Mathf.Min(clip.length, hitFrame / clip.frameRate) : clip.length) / seconds;
                else if (name == EnemyAnim.ChargeState && tuning is BoarTuning boar)
                {
                    float natural = GroundSpeed(model, clip) * scale;
                    if (natural > 0.01f) speed = Mathf.Clamp(boar.chargeSpeed / natural, MinMutantRunRate, MaxChargeRate);
                    report.Append($" (돌진 클립 땅 빠르기 {natural:0.00} m/s)");
                }
                state.speed = speed;
                if (!loop && name != EnemyAnim.DeathState) Link(state, locomotion, ReturnBlend);
                report.Append($" · {name} {(clip != null ? clip.length : 0f):0.00}초 ×{speed:0.00}{(loop ? " 루프" : "")}");
            }

            var hitMachine = new AnimatorStateMachine { name = EnemyAnim.HitLayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(hitMachine, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = EnemyAnim.HitLayerName,
                stateMachine = hitMachine,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
            });
            AnimatorState none = hitMachine.AddState("None");
            hitMachine.defaultState = none;
            AnimatorState hit = hitMachine.AddState(EnemyAnim.HitState);
            hit.motion = MutantClip(file, EnemyAnim.HitState);
            hit.speed = 1f;
            hit.iKOnFeet = false;
            Link(hit, none, 0.12f);

            EditorUtility.SetDirty(controller);
            Debug.Log($"[EnemyAnimationSetup] {species}_Mutant: 모델 배율 ×{scale:0.00} · 걷기 {walkSpeed:0.00} m/s{(usesWalk ? "" : "(블렌드에 안 넣음)")} · 달리기 클립 {runNatural:0.00} m/s → 재생 ×{runRate:0.00} = {runSpeed:0.00} m/s(이동 {tuning.moveSpeed:0.0} m/s){report}");
            return controller;
        }

        /// <summary>FBX 안의 클립. name이 null이면 미리보기용이 아닌 첫 클립.</summary>
        private static AnimationClip LoadClip(string path, string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                if (name == null || clip.name == name) return clip;
            }
            return null;
        }
    }
}
