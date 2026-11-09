# TASKS.md

## 진행 요약 (2026-11-09 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-09): 1-2 맨몸 근접 구현 — 브랜치 `phase1-controls`(`phase0-setup` 위), main 병합 전, 테스트 2개(실행 안 함), 플레이 확인 대기: 1-2. 다음: 1-3 권총.**

기존 까치 보스전은 `main`(2adecb2)에 그대로 있다. 제출물 저장소는 별도(GGachi).

---

## Phase 0. 세팅
- [x] **0-1. 프로젝트 문서** (2026-11-09 사용자 "착수 질문 시작")
  - 결정 (2026-11-09, 사용자): 3DRoguelike 프로젝트를 유지하고 빈 프로젝트 커밋 10b853a에서 새 브랜치로 시작 — 판 규칙을 분리한 구조로 처음부터 짜기 위해. 고라니 에셋은 main에서 가져온다.
  - 결정 내용은 GDD "규칙" 표. 세계관은 Notion 원문대로.
  - 정한 것 (Claude, 확인 필요): 핵심 재미 3 문구, 브랜치 이름 `phase0-setup`, 폴더 `Assets/Game/`
- [x] **0-2. 첫날 구조 세팅** (2026-11-09 사용자 "A로 진행해줘")
  - 결정 (2026-11-09, 사용자): UI는 uGUI + TextMeshPro — 까치 보스전에서 이미 쓴 방식이라 익숙함
  - 구현: 어셈블리 `Game.Rules`(엔진 참조 없음) · `Game.Runtime` · `Game.Editor` · `Game.Tests.EditMode`, 폴더 `Assets/Game/`, `.gitignore`를 루트 기준 경로로, `.gitattributes`로 Unity 텍스트 에셋 줄바꿈 LF 고정, 시작 샘플(TutorialInfo · Readme) 삭제
  - 정한 것 (Claude, 확인 필요): 빈 어셈블리가 빌드되도록 표시 클래스 3개(`RulesAssembly` · `RuntimeAssembly` · `EditorAssembly`)를 둠 — 뒤의 둘은 첫 스크립트가 생기면 지운다. 빈 폴더는 `.gitkeep`으로 유지. MCP용 `com.unity.ai.assistant` 2.20.0-pre.1을 main과 같은 버전으로 추가
  - 확인: 어셈블리 4개 컴파일 성공 · `Game.Rules.dll` 참조는 netstandard뿐 · MCP 연결 · 콘솔 오류 0. 경고 3개는 assistant 패키지의 실행 파일 서명 수집 경고로 게임 코드와 무관
  - 확인 못 함: 저장소를 새로 받았을 때 Unity가 경고 없이 열리는지
  - 테스트 작성, 실행 안 함: `RulesAssemblyTests`
  - 확인 방법: Unity에서 Window ▸ General ▸ Test Runner ▸ EditMode → `RulesAssembly_DoesNotReferenceUnity` 실행 → 통과하면 정상
- [x] **0-3. 고라니 가져오기** (2026-11-09 사용자 "0-3 진행")
  - 구현: main에서 `Gorani.fbx` · 재질 · 텍스처 3장 · 동작 9개(Idle · Walk · Walking Backward · 좌우 Strafe · Attack · Jump · Stand To Roll · Death From Right)를 메타째 `Art/Gorani/`로 가져옴. 메뉴 `Tools/Game/Setup Model Prefabs`(`ModelPrefabSetup`)가 `Prefabs/Gorani.prefab` 생성
  - 정한 것 (Claude, 확인 필요): 까치용 `Flying.fbx`와 예전 `Gorani.controller`는 가져오지 않음 — 컨트롤러는 1-1부터 새 상태 구조로 만든다. FBX에 재질 슬롯이 없어 재질은 프리팹에서 입힘
  - 확인: 아바타 유효 · Humanoid, 동작 9개 모두 Humanoid, 재질 `Gorani_Mat`, 키 약 1.85m, 포즈 6개 렌더 정상, 콘솔 오류 0. 메뉴는 1번 실행
  - 확인 못 함: 동작을 실제 재생(플레이 모드)한 모습 — 프레임을 찍어 본 것뿐. 메뉴 재실행 결과 비교
  - 확인 방법: Project 창 `Assets/Game/Prefabs/Gorani.prefab` 선택 → Inspector 미리 보기에서 옷 색이 보이고, 프리팹을 씬에 끌어다 놓으면 T자 자세로 서 있으면 정상

## Phase 1. 핵심 조작 (회색 방 하나)
- [x] **1-1. TPS 이동 · 카메라** (2026-11-09 사용자 "1-1 진행") — 플레이 확인 완료 (2026-11-09 사용자 "확인했어")
  - 결정 (2026-11-09, 사용자): 몸은 항상 카메라 방향 — 가져온 방향별 걷기 동작과 맞고 총을 쏘며 움직이기 좋음. 카메라는 Cinemachine 3인칭 따라가기 — 벽 충돌 피하기가 기본 기능. 달리기 동작 없이 걷기 동작을 빠르게 재생 — 전투 속도가 정해지면 교체
  - 구현: `PlayerController`(입력 · 이동 · 시점 · 동작 값) · `PlayerTuning` 에셋 `Data/PlayerTuning.asset` · 메뉴 `Tools/Game/Setup Test Room`(`TestRoomSetup`)이 컨트롤러 · `Prefabs/Player.prefab` · `Scenes/TestRoom.unity` 생성. 패키지 `com.unity.cinemachine` 6.6.0 추가
  - 정한 것 (Claude, 확인 필요): 입력은 프로젝트 기본 `InputSystem_Actions`의 Player/Move · Look 사용. 카메라 어깨 오른쪽 0.5m · 거리 3m · 눈높이 1.5m. 브랜치 `phase1-controls`를 `phase0-setup` 끝에서 시작
  - 수치: BALANCE 1번
  - 확인: 컴파일 오류 0 · 콘솔 오류 0 · 플레이 모드 진입 시 바닥에 서고 커서 잠김 · 카메라 위치 정상(캡처) · 메뉴 2회 실행 시 프리팹 · 컨트롤러 · 수치 에셋 파일 동일, 씬은 내용이 같아도 내부 ID가 달라 파일이 바뀜
  - 확인 못 함: **WASD 이동 · 마우스 시점 · 벽 앞 카메라 · 걷기 동작 재생을 실제로 못 봄.** Unity 창이 비활성이면 입력이 게임으로 가지 않고, 코드로 넣은 키 입력도 이동 입력에 전달되지 않았음. 조작감도 플레이 필요
  - 테스트 작성, 실행 안 함: `PlayerTuningTests`
  - 확인 방법: `Assets/Game/Scenes/TestRoom` 열기 → Play → Game 창 클릭 → WASD로 이동(A/D 옆걸음 · S 뒷걸음), 마우스로 시점 회전, 기둥과 벽 사이로 뒷걸음쳐 카메라가 벽을 뚫지 않으면 정상. 바닥에서 발이 미끄러지면 BALANCE 1번 조정
- [x] **1-2. 맨몸 근접** (2026-11-09 사용자 "확인했어" 후 착수 질문) — 플레이 확인 대기
  - 결정 (2026-11-09, 사용자): 좌우 번갈아 할퀴기 — 가져온 동작 좌우 반전으로 바로 만들 수 있고 발차기는 Beast 강화로 미룸. 공격 중 잠깐 멈춤 — 온몸 동작이라 발이 안 미끄러짐. 손이 뻗는 순간 앞쪽 부채꼴 검사 — 수치로 다루기 쉬움. 조작은 좌클릭 근접, 우클릭 누른 채 좌클릭 사격(1-3에서 구현) — 무기 전환 없이 두 방식을 섞음
  - 구현: `PlayerMelee`(공격 · 명중 · `Hit` 이벤트) · `IDamageable` · `PlayerController`가 공격 중 이동 입력 무시. `TestRoomSetup`이 AttackRight · AttackLeft 상태 추가
  - 정한 것 (Claude, 확인 필요): 공격 중에도 시점 회전은 됨. 맞는 대상이 아직 없어 시험 방에서는 맞아도 아무 일이 없음 — 체력 · 반응은 1-5
  - 수치: BALANCE 2번
  - 확인: 플레이 모드에서 `TryAttack`을 코드로 호출 → 오른손 → 왼손 번갈아 재생, 명중 0.36초, 잠금 중 재공격 거부, 잠금 0.75초 후 재공격 가능. 시험 큐브 5개 중 앞 1.5m · 45도만 맞고, 80도 · 뒤 · 4m는 안 맞음, 스윙당 1회. 콘솔 오류 0
  - 확인 못 함: 실제 좌클릭(코드로 넣은 입력이 전달되지 않음) · 타격감 · 왼손 동작이 자연스러운지(정지 화면 한 장만 봄) · 공격 중 이동이 실제로 멈추는지(입력을 못 넣음)
  - 테스트 작성, 실행 안 함: `PlayerTuningTests`에 근접 수치 추가
  - 확인 방법: `TestRoom` Play → Game 창 클릭 → 좌클릭 연타 시 오른손 · 왼손 번갈아 나오고, 누르는 동안 WASD가 멈추면 정상
- [ ] 1-3. 권총 (조준 · 사격)
- [ ] 1-4. 회피
- [ ] 1-5. 체력 · 피해 · 표적 적 1종

## Phase 2. 한 층이 돈다
- [ ] 2-1. 시드 · 용도별 난수 (순수 코드)
- [ ] 2-2. 방 격자 생성 (순수 코드, 시드 수백 개로 연결 테스트)
- [ ] 2-3. 방 표시 · 문 잠금 · 방 이동
- [ ] 2-4. 적 1종 AI · 방 클리어
- [ ] 2-5. HUD · 사망 · 결과 화면(사망 원인)

## Phase 3. 숲 한 지역 + 보스
- [ ] 3-1. 보상 선택 · Beast/Human 성향
- [ ] 3-2. 강화 표 (효과 조회 한 곳)
- [ ] 3-3. 적 3~4종 (AI 하나 + 특성 표)
- [ ] 3-4. 층 이동 · 첫 층 권총 보장
- [ ] 3-5. 숲 보스
- [ ] 3-6. 방 클리어 저장 · 이어 하기
- [ ] 3-7. 재생 · 영구 재화 · 작은 트리

## Phase G. 그래픽 (한 판이 돈 뒤)
- [ ] 숲 방 · 적 VARCO 교체

## Phase 4. 재미 검증
- [ ] 멈추고 30분 플레이 → 진단 → 기획 수정

## 다른 PC에서 할 일
- 없음
