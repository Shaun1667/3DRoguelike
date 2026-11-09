# BALANCE.md

## 0. 데이터 에셋 위치
| 번호 | 에셋 | 클래스 |
|---|---|---|
| 1 | `Assets/Game/Data/PlayerTuning.asset` | `PlayerTuning` |

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
