# BALANCE.md

## 0. 데이터 에셋 위치
| 번호 | 에셋 | 클래스 |
|---|---|---|
| 1~5 | `Assets/Game/Data/PlayerTuning.asset` | `PlayerTuning` |
| 6 | `Assets/Game/Data/TargetEnemyTuning.asset` | `TargetEnemyTuning` |
| 7 · 8 | `Assets/Game/Data/FloorTuning.asset` | `FloorTuning` → `FloorLayoutSettings`(Game.Rules) |

## 1. 플레이어 조작
재미 검증 전 출발값이다. 값을 바꾸면 `PlayerTuningTests`도 함께 고친다. 걷기 배속은 바꾼 뒤 `Tools/Game/Setup Test Room`을 다시 실행해야 컨트롤러에 들어간다.

| 항목 | 값 | 근거 |
|---|---|---|
| 이동 속도 `moveSpeed` | 4 m/s | 까치 보스전 9 m/s ÷ 캐릭터 배율 2.75 = 3.3에서 방 전투를 위해 올림 |
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

## 6. 표적 적 (시험용)
진짜 적이 생기면 없어진다. 값을 바꾸면 `PlayerTuningTests`도 함께 고치고, 범위를 바꾸면 Setup 메뉴를 다시 실행한다.

| 항목 | 값 | 근거 |
|---|---|---|
| 체력 `maxHealth` | 60 | 근접 6대, 권총 10발 |
| 충격파 피해 `shockwaveDamage` | 15 | 사용자 결정 2026-11-09 — 플레이어 7대 |
| 대기 `attackInterval` | 3 초 | 사용자 결정 2026-11-09 |
| 예고 `warningTime` | 0.8 초 | 구르기 무적 0.4초를 맞출 시간 |
| 범위 `shockwaveRadius` | 3.5 m | 근접 사거리 2.2m보다 넓어 근접은 위험을 짐 |
| 다시 나타남 `respawnTime` | 2 초 | 첫 시도 |
| 번쩍임 `hitFlashTime` | 0.08 초 | 첫 시도 |

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
