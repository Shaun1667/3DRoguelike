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