# CLAUDE.md

## 프로젝트 개요
폐기된 고라니 융합체 E-07-V.D.가 되어 넓은 월드의 진행 길을 따라가며 던전(방이 연결된 맵)을 깨고 진화하는 3D TPS 로그라이크. 구조 참고: Binding of Isaac(방 연결), Hades(방 보상 선택), MoaDungeon(시드 생성 · 판 구조).
기획은 GDD.md, 작업 순서와 일지는 TASKS.md, 수치는 BALANCE.md(첫 수치가 코드에 들어갈 때 만든다).
- **새 세션을 시작하면** 이 문서 → TASKS.md 맨 위 "진행 요약" 순서로 읽고 다음 항목을 확인한 뒤 시작한다. 다른 PC면 Docs/진행상황.md도. "다음 작업: 미정"이면 후보를 먼저 묻는다.
- 수치는 BALANCE.md와 데이터 에셋 기준이며 코드에 임의로 넣지 않는다.

## 참조 문서
| 문서 | 역할 | 상태 |
|---|---|---|
| `GDD.md` | 기획, 결정, 열린 항목 | 사용 중 |
| `TASKS.md` | 진행 요약, 작업 순서, 일지 | 사용 중 |
| `BALANCE.md` | 수치 표 | 첫 수치 때 생성 |
| Notion 「세계관 기획」 (id 56181c0935cc4f7aaef99c7ce3d251d8) | 세계관 원문, 사용자 확정 2026-11-09 | 고치지 않음 |
| `Docs/Source/MY_DEV_GUIDE.md` | 작업 방식 원본 | 고치지 않음 |
| `Docs/Source/ROGUELIKE_STRUCTURE.md` | 로그라이크 구조 원본 | 고치지 않음 |

## 협업 방식
- 한국어. 1인 개발. 쉬운 말로 설명하고, 오래 걸리는 작업은 지금 무엇을 하는지 알린다.
- 설계 결정은 선택지 + 추천안으로 묻고 `결정 (날짜, 사용자): … — 이유`로 TASKS에 남긴다. 사소한 것은 정한 뒤 "정한 것 (Claude, 확인 필요)"로 보고한다.
- 한 항목 = 작업 브랜치(`phaseN-…`) 커밋 + 보고. 지시 없이 main에 커밋 · 푸시하지 않는다. "main에 병합하고 푸시" = fast-forward 병합 · 푸시 · 작업 브랜치 삭제.
- 보고: 한 일 · 정한 것 · 확인한 것 · 확인 못 한 것 · 확인 방법 · 커밋 상태.
- 테스트 실행은 사용자가 지시할 때만 (결정 2026-11-09) — Claude는 작성만 하고 "테스트 작성, 실행 안 함"을 적는다. 몇 항목마다 돌릴 때가 됐다고 알린다.

## 작업 원칙
- 한 번에 기능 하나 (TASKS 현재 항목만). 요청받지 않은 시스템을 미리 만들지 않는다 — 단 핵심 설계 규칙의 구조는 처음부터.
- 구현 전 문서를 읽고 구현 후 문서를 고친다. 다르면 문서를 실제 기준으로 고치고 알린다.
- 에디터 수작업은 목록으로 안내, 반복 생성은 여러 번 실행해도 결과가 같은 Setup 메뉴로.
- 완료 = 테스트 통과 + 확인 방법 + 사용자 플레이 확인. 구현만 = `— 플레이 확인 대기`.
- 직접 본 것만 됐다고 말한다. 실패 · 생략 · 미확인은 그대로 보고한다.

## 기술 스택
| 항목 | 값 | 상태 |
|---|---|---|
| 플랫폼 | PC (Windows) | 2026-11-09 |
| 엔진 | Unity 6000.3.25f1 (6.3 LTS) | 2026-11-09 |
| 렌더 | URP 17.3 | 2026-11-09 |
| 카메라 | Cinemachine 3.1.7 | 2026-11-09 |
| 입력 | Input System 1.20 | 프로젝트 기존 값 |
| UI | uGUI + TextMeshPro, 한글 글꼴 Noto Sans KR(SIL OFL) | 2026-11-09, 글꼴 2026-11-10 |
| 3D 에셋 | VARCO 3D, 동작은 Mixamo Humanoid | 2026-11-09 |

## 폴더 구조
저장소 루트에 문서, Unity 프로젝트는 `3DRoguelike/` 하위 폴더.
```
(저장소 루트)                  CLAUDE.md, GDD.md, TASKS.md, Docs/
└─ 3DRoguelike/Assets/Game/
   ├─ Scripts/Rules/           Game.Rules: 엔진을 모르는 판 규칙 (noEngineReferences)
   ├─ Scripts/Runtime/         Game.Runtime: MonoBehaviour, 이동, 판정, 적 AI, 화면
   ├─ Scripts/Editor/          Game.Editor: Setup 메뉴
   ├─ Tests/EditMode/          Game.Tests.EditMode: 판 규칙 테스트
   ├─ Data/                    수치 · 콘텐츠 데이터 에셋
   ├─ Art/  Prefabs/  Scenes/
```

## 코딩 규칙
- 클래스 · 메서드 PascalCase, 필드 camelCase.
- 수치 · 콘텐츠는 데이터 에셋으로. 새 콘텐츠 = 표 한 줄, 종류별 분기 코드 금지. enum은 끝에만 추가한다.
- 난수는 주입하고 용도별로 나눈다 (맵 ≠ 보상 ≠ 전투 ≠ 연출).
- 결합은 이벤트로, "바뀐 것만" 알린다. Find/GetComponent는 캐시한다.
- 모델 표시용 프리팹은 `ModelPrefabSetup` 한 곳(메뉴 `Tools/Game/Setup Model Prefabs`), 새 모델 = `Models` 표 한 줄 + 메뉴 다시 실행.
- 플레이어 이동 · 시점은 `PlayerController` 한 곳, 데이터 `Data/PlayerTuning.asset`. 시험 방 · 플레이어 프리팹 · 컨트롤러는 `TestRoomSetup`(메뉴 `Tools/Game/Setup Test Room`)이 만든다.
- 맨몸 근접은 `PlayerMelee` 한 곳, 수치는 `PlayerTuning`. 맞을 수 있는 대상은 `IDamageable`을 구현한다.
- 권총 · 조준은 `PlayerGun` 한 곳(디버그 `DebugForceAim`), 팔은 `PlayerAimIK`, 카메라 줌 · 조준점은 `AimCameraZoom`. 키는 `InputSystem_Actions`의 Player 맵. 코드로 넣은 키 입력은 전달되지 않으니 확인은 공개 메서드(`TryAttack` · `TryFire`)와 `EditorApplication.Step`으로 한다.
- 구르기 · 무적 판정은 `PlayerRoll` 한 곳(`TryRoll`, `IsInvulnerable`).
- 손 모양은 `PlayerHandPose` 한 곳(애니메이터 `Hands` 레이어가 손가락만 덮음: 평소 손 · 조준 중 권총 쥔 손 · 근접 중 할퀴는 손), 값은 `Data/HandPoses.asset`(`HandPoseTable`, 조준 손목 각 `gripRoll` 포함), 포즈 클립 · 마스크 · 레이어는 `HandPoseSetup`(Setup Test Room이 부름 — 값을 바꾸면 다시 실행). 동작 클립의 손가락 커브는 쓰이지 않는다.
- 체력 계산은 `HealthPool`(Game.Rules), 무적 시간 · 이벤트는 `Health`, 맞은 표시는 `HitFlash`. 플레이어 사망 · 재시작은 `PlayerVitals`. 시험용 표적 `TargetEnemy`는 2-4에서 지움.
- 적 행동은 종류마다 클래스 한 곳(`Badger` 오소리 · `Boar` 멧돼지 · `Hare` 멧토끼 · `Toad` 두꺼비), 공통(체력 · 나타나기 · 쫓기 · 예고/빈틈 · 플레이어 맞히기 · 죽음 · 목록)은 `Enemy`, 데이터는 종마다 `Data/Enemies/{종}Tuning.asset` 하나(Mutant · E 개체 값 같이). 모습(Mutant / E 개체)은 프리팹(`Prefabs/Enemies/{종}_Mutant|E.prefab`, 모양은 `Body` 아래 — 모델은 그 아래만 바꿈), 프리팹 · 수치 에셋은 `EnemySetup`(Setup Test Room이 부름). 어느 종이 얼마나 나오나는 구성 표 `Data/EnemyRoster.asset`(단계 = 던전 순서마다 무게 · E 비율, 종마다 자리 · 떼) → 규칙 `EnemyMix` · `EnemyMixPlanner`. 새 종 = `EnemySpecies` 끝에 한 줄 + 클래스 · 튜닝 + 구성 표 한 줄 + `EnemySetup` 모양 표 한 줄. 이동은 NavMesh(`NavMeshAgent`). 층은 `FloorRun`이 세운 뒤 굽고, 시험 방은 `BuildNavMeshOnAwake`. 방마다 적 수 · 자리 · 종은 규칙 `EncounterPlanner`가 층 시작 때 적 난수로 모두 정한다(길 위 무리는 `road` 난수). 격자 ↔ 층 좌표 변환은 `FloorSpace` 한 곳. E 개체 동작(2026-11-19): Mixamo 리깅 모델 `Art/Enemies/Models/Rigged` + 클립 `Art/Enemies/Animation`, 컨트롤러 · 구간 · 재생속도는 `EnemyAnimationSetup` 한 곳(튜닝 시간으로 재생속도를 계산해 컨트롤러에 굽는다 — 튜닝 시간을 바꾸면 Setup Test Room 다시 실행), 상태 이름은 `EnemyAnim`, 코드는 `Enemy.PlayAnimation` · `PlayLocomotion`으로 상태에 바로 CrossFade(네 발 Mutant도 2026-11-19부터 Blender 리그 `Models/Rigged/{종}_Mutant_Rigged.fbx` + 클립 `Animation/Mutant/{종}_Mutant@{상태}.fbx`(Generic) — 걷기 · 달리기 재생속도는 이 모델에 클립을 샘플해 디딘 발이 엉덩이 기준 뒤로 가는 속도로 재서 이동 속도에 맞춤(E 개체 · 멧돼지 E 돌진도 같은 방식). E 개체 달리기에는 몸 속도가 고르지 않은 Mutant Run을 쓰지 않는다 — 제자리 재생 + 일정 속도 이동에서 발이 끌림(결정 2026-11-19, 사용자: 고른 달리기 Jog Forward · Run · Fast Run 중 재생속도가 1에 가장 가까운 것). 리깅 파일이 없으면 정적 모델로 대체되고 애니메이터 null → 건너뜀). URP Lit 발광을 코드로 켤 때는 `globalIlluminationFlags`를 `RealtimeEmissive`로(`None`이면 URP가 `_EMISSION`을 바로 끔 — `TestRoomSetup.EnableFlash`).
- 난수는 `RunRandom.Create(용도, 층)` 한 곳에서 만들고(`Game.Rules`, PCG32), 받는 쪽은 `IRandom`만 안다. 판 시드는 `RunSeed`(화면에는 8글자 코드). 새 용도 = `RandomPurpose`에 한 줄, 한 번 쓴 키 글자는 바꾸지 않는다. `RunRandomTests.FixedSeed_GivesFixedValues`가 깨지면 모든 시드의 결과가 바뀐 것이다.
- 층 방 배치는 `FloorGenerator.Generate(설정, 맵 난수)` 한 곳(`Game.Rules`), 결과는 `FloorLayout`(방 · 문 · 막다른 방 · 보스 방), 수치는 `FloorLayoutSettings`(BALANCE 7번). 새 방 모양 = 모양 목록에 한 줄, 새 방 종류 = `RoomKind` 끝에 추가하고 꾸미기 난수로 막다른 방에 배정한다(맵 모양은 그대로).
- 층 진행(방 들어가기 · 문 잠금 · 방 깨기)은 규칙 `FloorProgress`, 화면은 `FloorRun`(시드 선택 · 생성 · 적 내보내기) + `FloorGeometry`(회색 도형 바닥 · 벽 · 문). 수치 `Data/FloorTuning.asset`. 층 씬은 `FloorSetup`(메뉴 `Tools/Game/Setup Floor`)이 만들고, 방은 플레이할 때 세워진다. 빌드 씬 목록은 `BuildSceneList.Ensure`로만 바꾼다.
- HUD는 `GameHud`(체력 · 탄 · 미니맵, 바뀐 것만 고침), 결과 화면은 `RunResultScreen`(R 새 층 · T 같은 층), 판 기록은 규칙 `RunStats`. 둘 다 `HudSetup`이 Setup 메뉴 안에서 만든다. 사망 원인은 `Health.TakeDamage(피해, "공격 이름")` — 새 적 = 데이터에 이름 · 공격 이름. 미니맵에 보이는 방은 `FloorProgress.Visibility`. 한글은 `Art/Fonts/NotoSansKR SDF`(동적 글꼴, SIL OFL).
- 디버그 도구는 `DebugPanel` 한 곳(`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, 씬마다 저절로 생김 — 씬 · 프리팹에 넣지 않음). F1 창 · F2 원펀맨(한 마리 맞히면 살아 있는 적 전부 처치) · F3 모두 처치. 새 디버그 기능 = 이 창에 한 줄. 적 목록은 `Enemy.Alive`(종 무관), 종 · 모습을 골라 앞에 소환(시험 방은 `EnemyRosterSource`가 구성 표를 알려 줌).
- 보상은 규칙 `RewardPlanner`(층 시작 때 방마다 선택지, 보상 난수) · `SpecialRoomPlanner`(보물방, 꾸미기 난수), 판 성향은 `RunStats.TakeReward`. 보상 목록 겸 강화 표는 `Data/RewardTable.asset` — 새 보상 = 표 한 줄(성향 + 능력치 효과 `effects`, 기술이 없으면 `comingLater`). 강화 합계 · 조회는 규칙 `UpgradeSheet` 한 곳(`RunStats.Upgrades`), 플레이어 쪽은 `PlayerUpgrades.Scale(능력치)`만 묻는다. 새 능력치 = `UpgradeStat` 끝에 한 줄 + 이름 + 쓰는 곳 한 군데. 화면은 `RewardChoiceScreen`, 보상물은 `FloorRun`이 놓는다. 이동 · 달리기는 `PlayerController`(Shift 달리기 — 앞으로 달릴 때 `Fast Run` 동작, Space 점프, Ctrl 구르기는 `PlayerRoll`). 고라니 동작 FBX는 고라니 아바타로 가져온다(`ModelPrefabSetup`).
- 월드는 `WorldRun` 한 곳(고정 지형 · 진행 길 · 길 위 적 무리 · 던전 입구 · 관문), 데이터 `Data/WorldTuning.asset`(BALANCE 13번), 씬은 `WorldSetup`(메뉴 `Tools/Game/Setup World`, 지형 · 나무 · NavMesh까지 굽는다 — 값을 바꾸면 다시 실행). 게임은 `World` 씬에서 시작. 판 상태 한 곳은 규칙 `RunSession`(시드 · `RunStats` · `WorldProgress` · 있는 곳 · 체력), 씬을 오가는 동안 `CurrentRun`이 든다. 던전 = 층 씬(`FloorRun`이 판을 이어받고 난수 층 번호 = 던전 순서). 새 던전 = `dungeons`에 한 줄(길 순서), 새 적 무리 = `encounters`에 한 줄, **새 지역 = `regions` 한 줄**(+ 길 점 — 지형 타일 · 바닥 레이어 · 길가 배치 · 경계 벽 · 검문소 · 분위기까지, 종류별 코드 없음, BALANCE 13-1. 숲 나무 자리는 고정 기준 `treeReference`(예전 숲 길 · 관문 · 비운 자리)로 골라 길 · 던전 · 무리를 바꿔도 남는 나무 자리가 그대로). 마을은 `VillageTuning.placeInWorld`로 켜고 끈다(2026-11-25 끔). 결과 화면 다시 시작은 `IRunRestart`.
- 숲 나무는 `TreeSetup` 한 곳(메뉴 `Tools/Game/Setup Trees`, Setup World가 부름): VARCO 줄기 + 잎 카드 FBX(`Art/Trees`) → 텍스처 설정 · 머티리얼 · 프리팹 `Prefabs/World/Tree_{id}`(줄기 캡슐 충돌 · LODGroup). 섞는 비율은 `WorldTuning.treeKinds`. 새 나무 = `TreeSetup` 모델 표 한 줄 + `treeKinds` 한 줄.
- 폐허 도시 소품은 `CitySetup` 한 곳(메뉴 `Tools/Game/Setup City Props`): VARCO FBX(`Art/City/{id}`, 1 = 1 m · 피벗 바닥 가운데 · 정면 -Z) → 텍스처 설정 · 금속/매끄러움 맵(알파 From Input) · 차폐 맵(ORM R → 회색) · 머티리얼 `City_{id}.mat` · 프리팹 `Prefabs/World/City/City_{id}`(BoxCollider · 정적 · 큰 건물 LODGroup 컬링), 바닥 지형 레이어 `Art/City/Ground/City_{이름}.terrainlayer`. 새 소품 = `Models` 표 한 줄, 새 바닥 = `Grounds` 표 한 줄 + 메뉴 다시 실행. 월드 배치는 `WorldTuning.regions`의 도시 줄(Setup World가 `CitySetup.Run`을 부른 뒤 놓음, 3-8 ④).
- (시스템이 생길 때마다 "이 시스템은 `클래스` 한 곳, 데이터는 `에셋`, 새 항목 = 표 한 줄"로 한 줄씩 더한다)

## 핵심 설계 규칙 (변경 금지)
바꾸고 싶어지면 영향 범위를 표로 정리하고 사용자 결정을 받은 뒤에만.
1. **판 규칙은 순수 코드.** 맵 생성 · 보상 · 성향 · 해금 · 저장은 엔진을 모르는 어셈블리에 둔다. 이동 · 피해 판정 · 적 AI는 Unity 물리로 만든다 (결정 2026-11-09).
2. **같은 시드 = 같은 맵 · 보상.** 전투 결과까지 같을 필요는 없다.
3. **저장할 판 상태는 한 곳.** 방을 깰 때 저장하고, 전투 중 상태는 저장하지 않는다. 판 밖 데이터(영구 재화 · 해금)는 따로 저장한다.
4. **Beast/Human 수치는 고른 강화로만 오르고 매 판 초기화된다.**

## 아트 · 그래픽 규칙
- 고라니만 실제 모델, 나머지는 회색 도형으로 시작한다. 한 판이 돌면 VARCO로 교체한다 (결정 2026-11-09).
- 그래픽은 표현 레이어만 바꾼다. 데이터는 비주얼을 ID로 참조하고, 프리팹이 없으면 도형으로 그린다.
- 기존 에셋은 `main` 브랜치에 있다. 필요한 파일만 메타와 함께 가져온다.

## Git · 환경
- 커밋 메시지 `MM.DD/Phase-항목/PascalCase요약`, 수정은 `…Fix`. 커밋 전 `git status` · `git diff --cached --stat`.
- 파일을 바꾼 직후 Unity MCP가 끊기기 쉽다 → 콘솔 읽기로 깨우고, 안 되면 Unity 창 클릭을 요청한다.
- 빌드는 요청이 시간 초과돼도 계속된다 → `Logs/Editor.log`의 "Build Finished"로 확인한다.
