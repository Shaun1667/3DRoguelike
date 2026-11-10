# BALANCE.md

## 0. 데이터 에셋 위치
| 번호 | 에셋 | 클래스 |
|---|---|---|
| 1~5 | `Assets/Game/Data/PlayerTuning.asset` | `PlayerTuning` |
| 7 · 8 · 10 · 11 | `Assets/Game/Data/FloorTuning.asset` · `RewardTable.asset` | `FloorTuning` → `FloorLayoutSettings` · `EncounterSettings` · `RewardSettings`(Game.Rules), `RewardTable` |
| 9 | `Assets/Game/Data/MutantTuning.asset` | `MutantTuning` |

## 1. 플레이어 조작
재미 검증 전 출발값이다. 값을 바꾸면 `PlayerTuningTests`도 함께 고친다. 걷기 배속은 바꾼 뒤 `Tools/Game/Setup Test Room`을 다시 실행해야 컨트롤러에 들어간다.

| 항목 | 값 | 근거 |
|---|---|---|
| 이동 속도 `moveSpeed` | 4 m/s | 까치 보스전 9 m/s ÷ 캐릭터 배율 2.75 = 3.3에서 방 전투를 위해 올림 |
| 달리기 배율 `sprintMultiplier` | 1.6 (6.4 m/s) | 사용자 요청 2026-11-10 "shift는 달리기" — Mutant 3 m/s의 2배쯤으로 거리를 벌림. 걷기 동작도 이 배속으로, 첫 시도 |
| 중력 `gravity` | -20 | 까치 보스전 값 |
| 마우스 감도 `lookSensitivity` | 0.08 도/픽셀 | 첫 시도 |
| 시점 위아래 `minPitch` · `maxPitch` | -30 ~ 60 도 | 첫 시도 |
| 동작 전환 `animationBlendSpeed` | 8 /초 | 까치 보스전 값 |
| 걷기 배속 `walkPlaybackSpeed` | 2.4 | 까치 보스전 2배에서 이동 속도 증가만큼 올림 |

## 2. 맨몸 근접
값을 바꾸면 `PlayerTuningTests`도 함께 고친다. 재생 배속 · 잠금 시간은 바꾼 뒤 `Tools/Game/Setup Test Room`을 다시 실행한다.

| 항목 | 값 | 근거 |
|---|---|---|
| 피해 `meleeDamage` | 10 | 까치 보스전 값 |
| 사거리 `meleeRange` | 2.2 m | 캐릭터 키 1.85m 기준 첫 시도 |
| 각도 `meleeAngle` | 110 도 | 첫 시도 |
| 재생 배속 `meleePlaybackSpeed` | 3 | 까치 보스전에서 사용자가 고른 값 |
| 명중 시점 `meleeHitDelay` | 0.35 초 | 손이 가장 앞인 클립 1.05초 ÷ 배속 3 |
| 잠금 `meleeLockTime` | 0.75 초 | 까치 보스전 값 |
## 3. 권총 · 조준
값을 바꾸면 `PlayerTuningTests`도 함께 고친다. 전부 재미 검증 전 첫 시도 값이다.

| 항목 | 값 | 근거 |
|---|---|---|
| 피해 `pistolDamage` | 6 | 근접 10보다 약하고 멀리서 안전 |
| 발사 간격 `pistolFireInterval` | 0.22 초 | 초당 약 4.5발, 초당 피해 27 (근접은 10 ÷ 0.75 = 약 13) |
| 탄창 `pistolMagazine` | 12 | 탄창 한 번에 피해 72 |
| 재장전 `pistolReloadTime` | 1.2 초 | 탄창 소모 2.6초 + 재장전 → 평균 초당 약 19 |
| 사거리 `pistolRange` | 40 m | 방 크기보다 김 |
| 탄도 선 `tracerTime` | 0.05 초 | 첫 시도 |
| 조준 중 이동 `aimMoveMultiplier` | 0.6 | 사용자 결정 2026-11-09 |
| 카메라 거리 `cameraDistance` · `aimCameraDistance` | 3 → 1.7 m | 첫 시도 |
| 조준 전환 `aimBlendSpeed` | 6 /초 | 약 0.17초 |
| 조준 팔 길이 `aimArmLength` | 0.55 m | 고라니 팔 길이 |

## 4. 구르기
값을 바꾸면 `PlayerTuningTests`도 함께 고친다. 구르는 시간을 바꾸면 `Tools/Game/Setup Test Room`을 다시 실행한다.

| 항목 | 값 | 근거 |
|---|---|---|
| 속도 `rollSpeed` | 6 m/s | 달리기 4 m/s보다 빠름, 한 번에 4.8m |
| 시간 `rollDuration` | 0.8 초 | 까치 보스전에서 사용자가 늘린 길이 |
| 재사용 `rollCooldown` | 1 초 | 시작 기준, 끝나고 0.2초 뒤 가능 |
| 무적 비율 `rollInvulnerableRatio` | 0.5 | 사용자 결정 2026-11-09 — 앞쪽 0.4초 |

## 5. 플레이어 체력
값을 바꾸면 `PlayerTuningTests`도 함께 고친다.

| 항목 | 값 | 근거 |
|---|---|---|
| 최대 체력 `playerMaxHealth` | 100 | 사용자 결정 2026-11-09 — 강화로 올리기 쉬운 크기 |
| 맞은 뒤 무적 `hitInvulnerableTime` | 0.5 초 | 사용자 결정 2026-11-09 |
| 번쩍임 `hitFlashTime` | 0.08 초 | 첫 시도 |

## 6. 표적 적 (시험용) — 지움
2-4에서 진짜 적(Mutant, 9번)이 생겨 `TargetEnemy` · `TargetEnemyTuning.asset`을 지웠다(CLAUDE.md "진짜 적이 생기면 지운다"). 마지막 값은 git 이력(99b1231)에 있다.

## 7. 층 방 배치
`FloorTuning`이 `FloorLayoutSettings`로 넘기는 값. 값을 바꾸면 `PlayerTuningTests.FloorTuning_MatchesBalance`와 `FloorGeneratorTests.BalanceSettings`도 함께 고친다(고정 시드 배치도 바뀐다).

| 항목 | 값 | 근거 |
|---|---|---|
| 격자 `gridWidth` × `gridHeight` | 9 × 9 칸 | 방 12개가 가운데에서 사방으로 뻗어도 남는 크기, 첫 시도 |
| 방 수 `minRooms` ~ `maxRooms` | 8 ~ 12 개 | GDD 층 구성(사용자 결정 2026-11-09). 큰 방도 1개로 센다 |
| 방 모양 무게 | 1×1: 7, 2×1: 1, 1×2: 1, 2×2: 1 | 첫 시도. 무게로는 큰 방 30%지만 큰 방은 자리를 못 찾는 일이 많아 실제로는 약 17%(시드 3000개, 시작 방 제외: 2×1 · 1×2 각 6.5%, 2×2 3.9%). 시작 방은 늘 1×1 |

## 8. 방 크기
`FloorTuning`의 값. 값을 바꾸면 `PlayerTuningTests.FloorTuning_MatchesBalance`도 함께 고친다.

| 항목 | 값 | 근거 |
|---|---|---|
| 격자 한 칸 `cellSize` | 24 m | 시험 방과 같은 크기 — 1-1 ~ 1-5 조작을 이 크기에서 확인함. 큰 방은 48 m |
| 벽 높이 `wallHeight` · 두께 `wallThickness` | 4 m · 1 m | 시험 방과 같음 |
| 문 폭 `doorWidth` | 4 m | 플레이어(폭 0.7 m)가 구르거나 카메라를 돌리며 지나가도 넉넉하게, 첫 시도 |
| 방에 들어선 거리 `enterMargin` | 1.5 m | 문틀에 선 채로 잠겨 갇히지 않게, 첫 시도 |

## 9. 숲 Mutant
`MutantTuning`의 값. 값을 바꾸면 `PlayerTuningTests.MutantTuning_MatchesBalance`도 함께 고치고, 할퀴기 거리 · 각도를 바꾸면 `Tools/Game/Setup Test Room`을 다시 실행한다(바닥 예고 크기).

| 항목 | 값 | 근거 |
|---|---|---|
| 체력 `maxHealth` | 30 | 근접 3대, 권총 5발 — 방에 2~4마리라 표적(60)의 절반 |
| 이동 속도 `moveSpeed` | 3 m/s | 플레이어 4 m/s보다 느려 걸으며 쏠 수 있고, 조준(×0.6 = 2.4 m/s) 중에는 따라잡힘 |
| 회전 `turnSpeed` | 540 도/초 | 쫓을 때 바로 돌아섬, 첫 시도 |
| 길 다시 찾기 `repathInterval` | 0.2 초 | 첫 시도 |
| 나타나기 예고 `spawnTime` | 1 초 | 들어서자마자 맞지 않게 |
| 예고 시작 거리 `attackStartRange` | 1.8 m | 맞는 거리보다 짧아 예고 중 조금 물러나도 맞음 |
| 예고 `windupTime` | 1 초 | 사용자 요청 2026-11-10 "선딜레이가 없고 너무 빠름"(0.6 → 1). 몸을 젖히고 바닥 범위가 차오르는 걸 보고 피할 시간 |
| 맞는 거리 `hitRange` · 각도 `hitAngle` | 2.2 m · 100 도 | 플레이어 근접(2.2 m · 110 도)과 비슷하게 |
| 피해 `damage` | 10 | 플레이어 10대. 한 방에 4마리가 동시에 붙으면 위험 |
| 빈틈 `recoverTime` | 1.3 초 | 사용자 요청 2026-11-10 후딜레이(0.9 → 1.3). 할퀸 뒤 근접 2~3대를 넣을 시간 |
| 젖히기 `windupLeanAngle` · 숙이기 `strikeLeanAngle` | 25 도 · 20 도 | 연출. 예고 동안 뒤로 젖히고, 할퀼 때 앞으로 숙였다가 빈틈 동안 일어섬 |
| 번쩍임 `hitFlashTime` | 0.08 초 | 플레이어와 같음 |
| 사라짐 `corpseTime` | 1 초 | 첫 시도 |

## 10. 방마다 적
`FloorTuning`이 `EncounterSettings`로 넘기는 값. 값을 바꾸면 `PlayerTuningTests.FloorTuning_MatchesBalance`와 `EncounterPlannerTests.BalanceSettings`도 함께 고친다.

| 항목 | 값 | 근거 |
|---|---|---|
| 한 칸 방 적 수 `minEnemies` ~ `maxEnemies` | 2 ~ 4 | 첫 시도 |
| 큰 방 더하기 `extraEnemiesPerCell` | 칸 하나에 +1 | 2×1 · 1×2는 3~5, 2×2는 5~7 |
| 벽에서 `spawnWallMargin` | 4 m | 벽에 붙어 나오지 않게 |
| 적끼리 `spawnSpacing` | 3 m | 겹쳐 나오지 않게 |
| 문에서 `spawnDoorClearance` | 6 m | 들어선 자리(문에서 1.5 m) 바로 옆에 나오지 않게 |

## 11. 보상
`FloorTuning`이 `RewardSettings`로 넘기는 값과 `Data/RewardTable.asset`의 성향 양. 값을 바꾸면 `PlayerTuningTests.FloorTuning_MatchesBalance`도 함께 고친다.

| 항목 | 값 | 근거 |
|---|---|---|
| 전투 방 보상물 확률 `rewardChance` | 0.5 | 사용자 결정 "보상이 안 나올 수도 있음", 첫 시도 |
| 선택지 수 `rewardChoices` | 3 | 사용자 결정 "완전 무작위 3개" |
| 보물방 | 층마다 1개(보스 아닌 막다른 방) · 희귀 보상 반드시 | 사용자 결정, 수는 첫 시도 |
| 성향 양 | 일반 +1 · 희귀 +2 | 첫 시도. 실제 효과는 3-2 강화 표 |
