# BALANCE.md

## 0. 데이터 에셋 위치
| 번호 | 에셋 | 클래스 |
|---|---|---|
| 1~5 | `Assets/Game/Data/PlayerTuning.asset` | `PlayerTuning` |
| 7 · 8 · 10 · 11 · 12 | `Assets/Game/Data/FloorTuning.asset` · `RewardTable.asset` | `FloorTuning` → `FloorLayoutSettings` · `EncounterSettings` · `RewardSettings`(Game.Rules), `RewardTable` |
| 9 | `Assets/Game/Data/Enemies/{Badger,Boar,Hare,Toad}Tuning.asset` | `BadgerTuning` · `BoarTuning` · `HareTuning` · `ToadTuning` (공통 `EnemyTuning`) |
| 13 | `Assets/Game/Data/WorldTuning.asset` | `WorldTuning` |
| 14 | `Assets/Game/Data/EnemyRoster.asset` | `EnemyRoster` → `EnemyMix`(Game.Rules) |

## 1. 플레이어 조작
재미 검증 전 출발값이다. 값을 바꾸면 `PlayerTuningTests`도 함께 고친다. 걷기 배속은 바꾼 뒤 `Tools/Game/Setup Test Room`을 다시 실행해야 컨트롤러에 들어간다.

| 항목 | 값 | 근거 |
|---|---|---|
| 이동 속도 `moveSpeed` | 4 m/s | 까치 보스전 9 m/s ÷ 캐릭터 배율 2.75 = 3.3에서 방 전투를 위해 올림 |
| 달리기 배율 `sprintMultiplier` | 1.6 (6.4 m/s) | 사용자 요청 2026-11-10 "shift는 달리기" — Mutant 3 m/s의 2배쯤으로 거리를 벌림. 걷기 동작도 이 배속으로, 첫 시도 |
| 중력 `gravity` | -20 | 까치 보스전 값 |
| 점프 높이 `jumpHeight` | 1.2 m (공중 약 0.69초) | 사용자 요청 2026-11-10 "스페이스바는 점프" — 키 1.85m 캐릭터가 허리 높이 상자를 넘을 정도, 첫 시도. 점프 동작 배속은 Setup이 공중 시간에 맞춘다 |
| 마우스 감도 `lookSensitivity` | 0.08 도/픽셀 | 첫 시도 |
| 시점 위아래 `minPitch` · `maxPitch` | -30 ~ 60 도 | 첫 시도 |
| 동작 전환 `animationBlendSpeed` | 8 /초 | 까치 보스전 값 |
| 밀림 줄어듦 `pushDecay` | 5 /초 | 멧돼지 돌진 밀림(3-3). 밀리는 거리 = 처음 속도 ÷ 이 값(12 m/s → 약 2.4 m), 첫 시도 |
| 걷기 배속 `walkPlaybackSpeed` | 2.4 | 까치 보스전 2배에서 이동 속도 증가만큼 올림 |
| 달리기 동작 배속 | 계산값 (Setup 콘솔에 출력) | 걷기 배속 × 클립 속도 비(Walk ÷ Fast Run) — 6.4 m/s에서 발이 맞게. 앞으로 달릴 때만, 2026-11-10 |

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

## 9. 숲 적 4종
종마다 `Data/Enemies/{종}Tuning.asset` 하나에 Mutant(네 발, 본능)와 E 개체(두 발) 값을 같이 둔다. 결정 2026-11-12(사용자): 오소리 · 멧돼지 · 멧토끼 · 두꺼비, E 개체 = 체력 ×1.8 + 공격 하나 더 — 결정에 없는 값은 첫 시도. 값을 바꾸면 `EnemyTuningTests`도 함께 고치고, 할퀴기 범위 · 돌진 거리 · 부딪힘 반지름 · 내려찍기 반지름 · 부채꼴 발 수를 바꾸면 `Tools/Game/Setup Test Room`을 다시 실행한다(바닥 예고 크기). 예전 `MutantTuning`(2-4)의 값은 오소리로 옮겼다.

공통(`EnemyTuning`)
| 항목 | 값 | 근거 |
|---|---|---|
| E 개체 체력 `evolvedHealthMultiplier` | ×1.8 (반올림) | 결정 2026-11-12 |
| E 개체 추가 공격 `extraAttackEvery` | 2 — 공격 두 번에 한 번은 추가 공격 | 정한 것 (Claude, 확인 필요) |
| 회전 `turnSpeed` | 540 도/초 (멧돼지 360) | 2-4 값, 멧돼지는 둔하게 |
| 길 다시 찾기 · 나타나기 예고 · 번쩍임 · 사라짐 | 0.2초 · 1초 · 0.08초 · 1초 | 2-4 값 |
| 젖히기 `windupLeanAngle` · 숙이기 `strikeLeanAngle` | 25 · 20 도 (멧토끼 0 · 25, 두꺼비 25 · 10) | 연출 |

오소리(`BadgerTuning`) — 기본 근접. Mutant 값은 2-4 Mutant 그대로
| 항목 | 값 | 근거 |
|---|---|---|
| 체력 `maxHealth` | 30 (E 54) | 근접 3대, 권총 5발 |
| 이동 `moveSpeed` | 3 m/s | 플레이어 4 m/s보다 느림 |
| 예고 시작 거리 · 예고 · 맞는 거리 · 각도 | 1.8 m · 1초 · 2.2 m · 100도 | 2-4(사용자 피드백 2026-11-10 반영) |
| 피해 `damage` · 빈틈 `recoverTime` | 10 · 1.3초 | 2-4 |
| E: 3연속 할퀴기 `comboHits` · 간격 · 피해 · 내딛기 · 빈틈 | 3번 · 0.55초 · 8 · 0.6 m · 1.6초 | 같은 예고 뒤 세 번. 간격을 플레이어 맞은 뒤 무적(0.5초)보다 길게 해 다 맞으면 24 |

멧돼지(`BoarTuning`) — 돌진 탱커
| 항목 | 값 | 근거 |
|---|---|---|
| 체력 | 70 (E 126) | 근접 7대, 권총 12발 — 탱커 |
| 이동 | 2.6 m/s | 느린 대신 돌진 |
| 돌진 시작 거리 `chargeStartRange` | 12 m, 앞이 트였을 때 | 돌진(15 m)보다 짧아 지나쳐 벽에 박을 여지 |
| 예고 · 돌진 속도 · 거리 | 1.2초 · 9 m/s · 15 m | 결정 2026-11-12. 예고 동안 바닥에 돌진 길 |
| 피해 · 부딪힘 반지름 | 20 · 1.2 m | 피해는 결정, 반지름 첫 시도(예고 폭 2.4 m) |
| 밀림 `knockbackSpeed` | 12 m/s (약 2.4 m, 1번 `pushDecay`) | 결정 "밀림", 크기 첫 시도 |
| 벽 · 나무에 박으면 기절 `stunTime` | 2초 | 결정 2026-11-12. 머리 위 노란 별 |
| 박지 않았을 때 빈틈 | 1초 | 첫 시도 |
| E: 돌진 끝 내려찍기 예고 · 반지름 · 피해 · 빈틈 | 0.5초 · 3 m · 15 · 1.2초 | 벽에 박으면 내려찍기 없음 |

멧토끼(`HareTuning`) — 빠른 떼(떼 크기는 14번)
| 항목 | 값 | 근거 |
|---|---|---|
| 체력 | 12 (E 22) | 근접 2대, 권총 2발 — 떼라 약하게 |
| 이동 | 4.2 m/s | 걷기(4)로는 못 떨치고 달리기(6.4)로 떨침 |
| 도약 시작 거리 · 웅크림 | 5 m · 0.5초 | 웅크림은 결정. 뛸 곳은 웅크릴 때 정해져 바닥에 보임 |
| 최대 거리 · 공중 · 높이 · 부딪힘 반지름 | 6 m · 0.4초 · 0.8 m · 0.9 m | 첫 시도 |
| 피해 · 착지 빈틈 | 6 · 0.8초 | 결정 2026-11-12 |
| E: 높이 뛰어 내려찍기 웅크림 · 거리 · 공중 · 높이 | 0.8초 · 8 m · 0.8초 · 3 m | 첫 시도 |
| E: 내려찍기 반지름 · 피해 · 빈틈 | 2 m · 10 · 1초 | 첫 시도 |

두꺼비(`ToadTuning`) — 원거리
| 항목 | 값 | 근거 |
|---|---|---|
| 체력 | 24 (E 43) | 근접 3대, 권총 4발 |
| 이동 | 2.2 m/s | 물러나도 플레이어가 따라잡음 |
| 거리 유지 `keepMin` ~ `keepMax` | 8 ~ 14 m, 물러날 때 4 m씩 | 결정 8~14 m. 사이에 막힌 게 있으면 다가감 |
| 예고 · 날아가는 시간 · 꼭대기 높이 | 1.2초 · 0.9초 · 3 m | 예고는 결정. 떨어질 곳이 예고 때 정해져 바닥에 보임 |
| 피해 · 맞는 반지름 | 8 · 1.5 m | 피해는 결정 |
| 독 웅덩이 반지름 · 시간 · 초당 피해 · 간격 | 2 m · 4초 · 4 · 1초 | 시간 · 초당 피해는 결정 |
| 쏜 뒤 빈틈 · 다음 공격까지 | 1초 · 2.5초 | 첫 시도 |
| E: 부채꼴 발 수 · 각도 | 3발 · 가운데에서 ±25도 | 결정 3발, 각도 첫 시도 |

## 10. 방마다 적
`FloorTuning`이 `EncounterSettings`로 넘기는 값. 3-3부터 적 수는 '자리'이고 종마다 쓰는 자리 수는 14번(멧토끼 떼는 2자리에 3~5마리). 값을 바꾸면 `PlayerTuningTests.FloorTuning_MatchesBalance`와 `EncounterPlannerTests.BalanceSettings`도 함께 고친다.

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

## 12. 강화 효과
`Data/RewardTable.asset`의 보상마다 붙은 효과(`effects`). 퍼센트는 기본값(1 · 3번 수치) 기준이고, 같은 보상을 다시 고르면 더해진다(상한 없음). 값을 바꾸면 `UpgradeSheetTests.RewardTable_MatchesBalance`도 함께 고친다. 결정 2026-11-11(사용자): 일반 +20% · 희귀 +40%, 쌓임, 기술 보상은 나중.

| 보상 | 효과 | 1번 고르면 | 근거 |
|---|---|---|---|
| 근력 | 근접 피해 +20% | 10 → 12 | 일반 크기 |
| 이동 | 이동 속도 +10% | 4 → 4.4 m/s (달리기 7.0) | 일반의 절반 — 빨라지면 적(3 m/s)과 거리 벌리기가 너무 쉬워짐. 동작 배속도 같이 올라감 |
| 재생 | 방을 깰 때 최대 체력의 20% 회복 | 100 기준 20 | 기본 회복이 0이라 "방을 깰 때"로 줌 |
| 권총 손질 | 권총 피해 +20% | 6 → 7 (반올림) | 일반 크기. 두 번이면 6 × 1.4 = 8 |
| 탄창 | 탄창 +20% | 12 → 14 (반올림) | 늘어난 만큼 바로 채움 |
| 조준 | 조준 전환 +20%, 조준 중 이동 +20% | 0.17 → 0.14초, 이동 배율 0.6 → 0.72 | 이름대로 조준만. 이동 배율은 1을 넘지 않음 |
| 회피 (희귀) | 구르기 거리 +40%, 구르기 재사용 +40% | 4.8 → 6.7 m, 대기 1 → 0.71초 | 희귀 크기. 대기는 구르는 시간 0.8초보다 짧아지지 않음 |
| 돌진 · 산탄총 · 폭발물 (희귀) | 없음 — 준비 중(`comingLater`), 선택지에 안 나옴 | — | 기술은 따로 항목에서. 그동안 보물방은 회피 + 일반 2장 |

정수 값(피해 · 탄창)은 반올림(.5는 올림)하고 1 아래로 내려가지 않는다.

## 13. 숲 월드
`Data/WorldTuning.asset`(`WorldTuning`). 월드는 판마다 같고 `Tools/Game/Setup World`가 이 값으로 씬을 세운다 — 값을 바꾸면 Setup World를 다시 실행하고 `WorldTests.WorldTuning_MatchesBalance`도 함께 고친다. 결정 2026-11-11(사용자): 고정 월드 + 던전 안만 무작위, 길 위에 적 무리, 숲 지역 + 던전 3개, 죽으면 길 시작부터. 나머지 값은 첫 시도.

| 항목 | 값 | 근거 |
|---|---|---|
| 진행 길 `route` | 꺾은선 9점, 남 → 북 약 1,055 m (S자) | 걷기 4 m/s면 약 4분 반, 달리기 약 2분 45초. 길이 남→북으로만 나아가서 관문의 보이지 않는 벽이 다른 골짜기를 자르지 않음 |
| 골짜기 반폭 `valleyHalfWidth` | 55 m (폭 110 m) | "광활하게" — 길 옆으로 돌아다닐 여유 |
| 관문 고개 반폭 · 길이 · 좁아지는 거리 | 6 m · 30 m · 45 m | 관문으로만 지나가게 골짜기를 좁힘 |
| 절벽 높이 · 가로 `cliffHeight` · `cliffWidth` | 28 m · 10 m | 평균 경사 약 70° — 플레이어 · 적 오르기 한계 45°를 넘음 |
| 흙길 반폭 `pathHalfWidth` | 4 m | 눈에 띄는 길 |
| 바닥 굴곡 `groundBumps` | 1.5 m | 길 · 입구 근처(14 m)는 평평 |
| 바닥 레이어 타일 `forestFloorTile` · `dirtTrailTile` · `pollutedSoilTile` | 숲 바닥 5 m · 흙길 4 m · 오염 토양 5 m (절벽 바위는 단색) | VARCO 1024² 이음매 없는 텍스처(`Art/World/Ground`). 정한 것 (감독 지시 2026-11-17) |
| 바닥 매끄러움 `groundSmoothness` · `trailSmoothness` | 0.05 · 흙길 0.1 (상수, 금속 0) | 매끄러움을 텍스처 알파에서 읽으면(Unity 기본) 1이 되어 낮은 시점에서 하늘이 비쳐 바닥이 청백색으로 보였음 |
| 오염 토양 `pollutedRadius` · `pollutedFade` | 입구에서 28 m · 가장자리 8 m | 오염된 둥지 둘레. 길 위는 흙길이 먼저 |
| 오염 경계 노이즈 `pollutedEdgeNoise` · `pollutedNoiseScale` | ±6 m · 0.08 | 경계가 둥글지 않고 들쭉날쭉하게 |
| 숲 바닥 흙 얼룩 `forestDirtMix` · `forestDirtNoiseScale` | 최대 15% · 0.05 (약 20 m 크기 얼룩) | 숲 바닥 단조로움 줄이기 |
| 분위기 `ambientColor` · `fogColor` · `fogStart`~`fogEnd` · `skyExposure` | 앰비언트 (0.42, 0.42, 0.38) · 안개 (0.50, 0.55, 0.48) 70~320 m · 하늘 밝기 1.1 | 푸른 기를 줄인 따뜻한 숲. 월드 씬만(시험 방은 그대로) |
| 나무 `treeCount` · 간격 · 길에서 비움 | 900그루 · 4 m · 9 m | 숲 느낌, 고정 시드 7 |
| 나무 종류 `treeKinds` 무게 (신갈나무 · 소나무 · 돌연변이) | 50 · 35 · 15 | 결정 2026-11-17(사용자 "추천안대로"): 신갈나무 많이, 소나무 중간, 돌연변이 드문드문. 지금 배치 결과 424 · 306 · 170그루 |
| 던전 입구 둘레 무게 `weightNearDungeon` · 반경 `treeNearDungeonRadius` | 40 · 25 · 35 · 45 m | 오염된 둥지 둘레에 돌연변이 나무가 더 많게. 정한 것 (Claude, 확인 필요) |
| 나무 크기 `treeScaleRange` · 회전 | 0.85 ~ 1.15배 · Y축 아무 각도 | 모델 높이 소나무 12 m · 신갈나무 9 m · 돌연변이 8 m. 나무마다 난수 3번(종류 · 크기 · 회전)이라 자리는 도형 나무 때와 같음 |
| 던전 입구 (길 거리 · 옆) | 300 m 오른쪽 18 m · 600 m 왼쪽 18 m · 900 m 오른쪽 18 m | 길을 셋으로 나눔. 마지막(3번) 던전을 깨면 숲 클리어 |
| 관문 `gateAfterEntrance` | 입구에서 길 따라 70 m 앞 | 입구가 좁아지는 구간 밖에 있게(45 + 15 m 넘게) |
| 입장 거리 `entranceRadius` | 2.5 m | 입구 앞 파란 원에 들어서면 바로 입장 |
| 적 무리 | 6곳(110 · 210 · 420 · 510 · 700 · 800 m), 자리 2 · 3 · 2 · 3 · 3 · 4(종 · 떼는 14번) | 던전 사이마다 두 번 |
| 무리 나타나는 거리 `encounterTriggerRange` | 22 m | 시야 안에서 바닥 예고가 보이게 |
| 던전 → 월드로 돌아가기 | 마지막 방을 깨고 2초 뒤(보상 고르는 중이면 고른 뒤) | `FloorRun.returnDelayAfterClear` |

던전 안 층은 판 시드 + 던전 순서(난수의 층 번호)로 만들어져, 같은 시드면 같은 던전 셋이 나온다.

## 14. 적 구성 (종 비중 · E 개체 · 떼)
`Data/EnemyRoster.asset`(`EnemyRoster` → 규칙 `EnemyMix` · `EnemyMixPlanner`). 방 적 수(10번)와 길 위 무리 수(13번)는 '자리'로 쓰고, 종마다 정해진 만큼 자리를 쓴다. 결정 2026-11-16(사용자): 종 구성은 단계별로 늘리기 — 던전 1까지 오소리 위주 + 멧토끼 떼, 던전 2부터 멧돼지 · 두꺼비. 결정 2026-11-12(사용자): 길 위 무리는 Mutant만, 던전 2 · 3은 E 개체 약 25% · 50%. 나머지는 첫 시도. 값을 바꾸면 `EnemyTuningTests.EnemyRoster_MatchesBalance`와 `EnemyMixTests.BalanceMix`도 함께 고친다. 같은 시드면 같은 구성(던전은 적 난수, 길 위는 `road` 난수).

| 종 | 쓰는 자리 `slots` | 마릿수 `groupMin` ~ `groupMax` | 떼 반지름 `groupSpread` | 근거 |
|---|---|---|---|---|
| 오소리 | 1 | 1 | — | 기준 |
| 멧돼지 | 2 | 1 | — | 탱커라 둘 몫 |
| 멧토끼 | 2 | 3 ~ 5 | 1.5 m | 떼 3~5는 결정, 한 마리가 약해 자리 2 |
| 두꺼비 | 1 | 1 | — | |

| 단계 | 쓰는 곳 | 무게: 오소리 · 멧토끼 · 멧돼지 · 두꺼비 | E 개체 `evolvedChance` |
|---|---|---|---|
| 1 | 숲 던전 1 + 그 앞 길 무리(110 · 210 m) | 3 · 1 · 0 · 0 | 0 |
| 2 | 숲 던전 2 + 그 앞 길 무리(420 · 510 m) | 3 · 1 · 1 · 1 | 0.25 |
| 3 | 숲 던전 3 + 그 앞 길 무리(700 · 800 m) | 2 · 1 · 2 · 2 | 0.5 |

- 한 번 뽑을 때마다 종 → E 개체 여부 → 떼 크기. 떼는 다 같은 모습. 남은 자리로 쓸 수 있는 종만 뽑으므로(1자리 남으면 오소리 · 두꺼비) 실제 비중은 무게와 조금 다르다. 오소리가 늘 1자리라 자리는 남김없이 쓴다.
- 길 위 무리는 그 무리 다음 던전의 단계를 쓰되 E 개체 없음. 층 씬만 Play하면 단계 1. 표보다 뒤 던전은 마지막 단계.
