# TASKS.md

## 진행 요약 (2026-11-09 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-09): 0-2 첫날 구조 세팅 끝 — 브랜치 `phase0-setup`(10b853a에서 시작), main 병합 전, 테스트 1개(실행 안 함). 다음: 0-3 고라니 가져오기.**

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
- [ ] **0-3. 고라니 가져오기** — 다음 작업. main에서 모델 · 동작만, Humanoid 설정 확인

## Phase 1. 핵심 조작 (회색 방 하나)
- [ ] 1-1. TPS 이동 · 카메라
- [ ] 1-2. 맨몸 근접
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
