# Telemetry - Event Schema

## 1. 공통 필드
- eventName: string
- eventTimeUtc: ISO-8601
- userId: string
- profileId: string
- appVersion: string
- buildChannel: string
- stageId: string
- classId: string
- runId: string

## 2. 핵심 이벤트

### run_start
- riskScore: int
- selectedModifiers: string[]
- seed: int

### level_up
- level: int
- offeredChoices: string[]
- selectedChoice: string
- rerollUsed: bool

### weapon_evolution
- fromWeaponId: string
- toWeaponId: string
- isAbyss: bool
- riskScore: int

### boss_spawn
- bossId: string
- elapsedSec: int

### boss_kill
- bossId: string
- elapsedSec: int
- hpRatioOnKill: float

### run_end
- result: enum(win, lose)
- elapsedSec: int
- levelReached: int
- kills: int
- deathReasonCode: string
- rewardCurrency: int
- endingType: string

## 3. KPI 계산 매핑
- 보스 도달율: `boss_spawn / run_start`
- 보스 클리어율: `boss_kill / run_start`
- 진화 경험율: `weapon_evolution(unique run) / run_start`
- 평균 런 길이: `avg(run_end.elapsedSec)`
- 리스크 선택률: `selectedModifiers 분포`

## 4. 데이터 품질 규칙
- run_start 없이 run_end 금지
- elapsedSec 음수 금지
- result=win이면 rewardCurrency > 0
- endingType은 Stage06 승리 시에만 값 허용

## 5. 이벤트 버전
- schemaVersion: 1
- 필드 추가 시 minor 증가
- 필드 제거/의미 변경 시 major 증가
