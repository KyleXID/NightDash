# Stage 1 MVP Multi-Agent Execution

## Goal

Stage 1 MVP vertical slice를 기준으로 `Title -> Run Selection -> Playing -> Level Up -> Boss/Evolution -> Result -> Meta Save -> Retry` 루프를 끊김 없이 완성한다.

현재 코드베이스에는 이미 `RunStatus`, 기본 ECS singleton, upgrade buffer, bootstrap/data wiring의 골격이 있으므로, 병렬 작업 전에는 이 골격을 Stage 1 계약으로 명시적으로 잠그는 것이 우선이다.

## Shared Contracts Lock

### 1. Run state machine

단일 truth source는 ECS singleton `GameLoopState`이다.

`RunStatus`
- `Loading`
- `Playing`
- `Paused`
- `LevelUpSelection`
- `Victory`
- `Defeat`
- `Result`

상태 전이 규칙
- `Loading -> Playing`: `DataBootstrapSystem` 완료 시.
- `Playing -> LevelUpSelection`: XP 누적으로 `PendingLevelUps > 0`일 때.
- `LevelUpSelection -> Playing`: 카드 선택 또는 reroll 후 선택 완료 시.
- `Playing -> Victory`: 보스 스폰 이후 보스 처치 완료 시.
- `Playing -> Defeat`: 플레이어 HP가 0이 되면.
- `Victory -> Result`: 결과 스냅샷 고정 후.
- `Defeat -> Result`: 결과 스냅샷 고정 후.
- `Result -> Loading`: 재도전 또는 로비 복귀로 새 run bootstrap 시작 시.

명시 규칙
- UI는 `RunStatus`를 직접 계산하지 않는다.
- UI는 command/request singleton만 쓰고, gameplay singleton을 직접 수정하지 않는다.
- `IsRunActive`는 legacy compatibility 플래그로 유지하되, 화면/게임플레이 분기는 `Status`를 우선 기준으로 본다.

### 2. Core ECS singleton/contracts

기존 파일 기준 anchor
- `Assets/NightDash/Scripts/ECS/Components/NightDashComponents.cs`
- `Assets/NightDash/Scripts/ECS/Components/NightDashBuffers.cs`

Stage 1에서 메인 에이전트가 잠그는 계약
- `GameLoopState`
  - run timer
  - level/xp/next xp
  - pending level up count
  - current `RunStatus`
- `StageRuntimeConfig`
  - stage duration
  - boss spawn time
  - map bounds
  - stage clear flag
- `BossSpawnState`
  - boss spawned
  - boss killed
  - chest pending 여부를 여기에 확장 가능
- `RunResultStats`
  - kills
  - gold
  - souls
  - reward committed flag
- `PlayerProgressionState`
  - weapon slot limit
  - passive slot limit
  - rerolls remaining
- `UpgradeSelectionRequest`
  - selected option index
  - has selection
  - reroll requested

Buffer contracts
- `OwnedWeaponElement`
- `OwnedPassiveElement`
- `UpgradeOptionElement`
- `AvailableWeaponElement`
- `AvailablePassiveElement`

Stage 1 추가 확장 포인트
- chest/evolution/result snapshot 관련 singleton 또는 buffer는 Agent B가 추가하되, 기존 singleton/buffer 이름을 재사용하지 않고 새 타입으로 추가한다.
- combat drop을 orb 대신 instant XP로 고정하면 pickup entity contract는 후순위로 둔다.

### 3. Data contracts actually consumed at runtime

Anchor files
- `Assets/NightDash/Scripts/Data/ClassData.cs`
- `Assets/NightDash/Scripts/Data/WeaponData.cs`
- `Assets/NightDash/Scripts/Data/PassiveData.cs`
- `Assets/NightDash/Scripts/Data/EvolutionData.cs`
- `Assets/NightDash/Scripts/Data/StageData.cs`

Stage 1에서 실제 소비해야 하는 필드

`ClassData`
- `id`
- `baseHp`
- `baseMoveSpeed`
- `basePower`
- `startWeaponId`
- `uniquePassiveId`

`WeaponData`
- `id`
- `weaponType`
- `maxLevel`
- `baseCooldown`
- `basePowerCoeff`
- `baseRange`
- `baseProjectileSpeed`
- `includeInUpgradePool`
- `levelCurves`
- `specialFlags`

`PassiveData`
- `id`
- `category`
- `maxLevel`
- `effects`
- `condition`

`EvolutionData`
- `id`
- `resultWeaponId`
- `requiredWeaponId`
- `requiredWeaponLevel`
- `requiredPassiveIds`
- `requiredRiskScoreMin`
- `isAbyss`
- `priority`

`StageData`
- `id`
- `durationSec`
- `bossSpawnSec`
- `boundsCenter`
- `boundsSize`
- `spawnPhases`
- `bossId`
- `baseRewardPoints`

### 4. UI boundary

Anchor files
- `Assets/NightDash/Scripts/Runtime/RunSelectionLobbyUI.cs`
- `Assets/NightDash/Scripts/Runtime/NightDashTitleScreenUI.cs`
- `Assets/NightDash/Scripts/Runtime/NightDashHudResultUI.cs`

규칙
- `RunSelectionLobbyUI` 책임은 stage/class 선택과 run start 요청까지만.
- HUD/Result UI는 ECS read-model만 소비.
- LevelUpSelection UI는 신규 파일로 분리.
- 버튼 action은 ECS request singleton을 통해 전달.
- retry/menu/meta action은 direct gameplay mutation 대신 run reset command를 발행하는 방향으로 통일.

## File Ownership

dirty worktree 기준으로 shared hotspot 파일은 메인 에이전트만 수정한다.

메인 에이전트 전용
- `Assets/NightDash/Scripts/ECS/Components/NightDashComponents.cs`
- `Assets/NightDash/Scripts/ECS/Components/NightDashBuffers.cs`
- `Assets/NightDash/Scripts/ECS/Authoring/NightDashBootstrapAuthoring.cs`
- `Assets/NightDash/Scripts/ECS/Systems/NightDashRuntimeBootstrapSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/DataBootstrapSystem.cs`
- `Docs/GDD/production/05_stage1_mvp_multi_agent_execution.md`

Agent A: ECS Combat Core
- `Assets/NightDash/Scripts/ECS/Systems/WeaponSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/CombatSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/EnemySpawnSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/StageTimelineSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/StageProgressSystem.cs`
- `Assets/NightDash/Scripts/ECS/Authoring/ProjectileAuthoring.cs`
- `Assets/NightDash/Scripts/ECS/Authoring/EnemyAuthoring.cs`
- 신규 combat support file가 필요하면 `Assets/NightDash/Scripts/ECS/Systems/` 아래에 추가

Agent B: Progression and Meta
- `Assets/NightDash/Scripts/ECS/Systems/GameLoopSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/ProgressionSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/EvolutionSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/MetaProgressionSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/SaveSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/RuntimeBalanceUtility.cs`
- 신규 progression/evolution command file가 필요하면 `Assets/NightDash/Scripts/ECS/Systems/` 아래에 추가

Agent C: UI/Flow
- `Assets/NightDash/Scripts/Runtime/RunSelectionLobbyUI.cs`
- `Assets/NightDash/Scripts/Runtime/NightDashTitleScreenUI.cs`
- `Assets/NightDash/Scripts/Runtime/NightDashHudResultUI.cs`
- 신규 `Assets/NightDash/Scripts/Runtime/LevelUpSelectionUI.cs`
- UI helper 신규 파일은 `Assets/NightDash/Scripts/Runtime/` 아래에 추가

Agent D: Data and Content Slice
- `Assets/NightDash/Scripts/Data/ClassData.cs`
- `Assets/NightDash/Scripts/Data/WeaponData.cs`
- `Assets/NightDash/Scripts/Data/PassiveData.cs`
- `Assets/NightDash/Scripts/Data/EvolutionData.cs`
- `Assets/NightDash/Scripts/Data/StageData.cs`
- `Assets/NightDash/Scripts/Editor/DataValidator.cs`
- `Assets/NightDash/Data/**`

Agent E: QA/Integration
- write scope 없음
- 통합 시 verification, acceptance, perf notes만 담당

## Worker Prompts

### Agent A prompt

목표
- Stage 1에서 플레이어가 실제 공격으로 일반 적과 보스를 처치 가능해야 한다.

제약
- 너는 혼자 작업하지 않는다. 다른 에이전트도 동시에 작업 중이므로, 네 소유 범위 밖 파일은 revert하지 말고, 공유 파일 변경이 필요하면 메인 에이전트에 요청 사항만 남겨라.
- `NightDashComponents.cs`, `NightDashBuffers.cs`, bootstrap singleton 계약은 메인 에이전트 소유다.
- XP orb 대신 instant XP 지급을 기본값으로 구현한다.

완료 기준
- `WeaponSystem` placeholder 제거
- projectile 또는 melee hit spawn 구현
- 적 이동, projectile 이동, 충돌, 피해, 사망 처리
- kill count 증가
- boss 최소 1패턴
- boss 처치 시 `BossSpawnState`와 `RunStatus` 계약에 맞는 상태 반영 준비

### Agent B prompt

목표
- 레벨업 카드 선택, 무기/패시브 슬롯 성장, 진화 판정, 결과 보상 커밋 1회 보장을 완성한다.

제약
- 너는 혼자 작업하지 않는다. 다른 에이전트가 같은 저장소에서 동시에 작업 중이다. 네 소유 범위 밖 변경은 revert하지 말고, 공유 계약 수정이 필요하면 메인 에이전트에 요청만 남겨라.
- run state source는 `GameLoopState.Status`이다.
- UI는 직접 gameplay singleton을 바꾸지 않고 request singleton을 통해 들어온다고 가정한다.

완료 기준
- XP 공식 정리
- `LevelUpSelection` 상태 전환 안정화
- 3카드 후보 생성 규칙 강화
- 무기 6 / 패시브 6 슬롯 상한 준수
- 보스 상자 기반 진화 체크
- result 진입 시 보상 커밋 1회 고정

### Agent C prompt

목표
- 마우스만으로 title부터 result까지 루프가 가능해야 한다.

제약
- 너는 혼자 작업하지 않는다. 다른 에이전트도 동시에 작업 중이다. 네 소유 범위 밖 파일은 revert하지 말고, 공유 ECS 계약이 바뀌면 메인 에이전트가 조정한다.
- gameplay 로직은 UI에서 직접 계산하지 않는다.
- `RunSelectionLobbyUI`는 시작 로비 역할까지만 유지한다.

완료 기준
- title/lobby/HUD/result 책임 정리
- ECS 상태 기반 HUD 바인딩
- LevelUpSelection UI 추가
- reroll 1회 반영
- result action 버튼이 retry/menu 루프에 연결될 준비가 되어 있어야 함

### Agent D prompt

목표
- Stage 1에서 실제 쓰는 데이터가 fallback 하드코딩 없이 소비되도록 정리한다.

제약
- 너는 혼자 작업하지 않는다. 다른 에이전트도 동시에 작업 중이다. 네 소유 범위 밖 코드는 revert하지 말고, 필요한 런타임 필드가 누락되면 메인 에이전트에 계약 요청만 남겨라.
- 이번 범위는 Stage 1 vertical slice용 최소 데이터다.

완료 기준
- 클래스 1~2종
- 무기 3종
- 패시브 6종
- 진화 2종
- Stage 1 보스 1종
- `DataValidator`에 Stage 1 필수 누락 검사 추가

## Integration Order

1. 메인 에이전트가 shared contracts와 ownership 잠금.
2. Agent A/B/C/D 병렬 구현.
3. 메인 에이전트가 contract drift와 merge 충돌 정리.
4. Agent E가 acceptance/perf 검증.
5. blocker만 원 담당 에이전트 재투입.

## Known Hotspots

병렬 충돌 위험이 높은 파일
- `Assets/NightDash/Scripts/ECS/Components/NightDashComponents.cs`
- `Assets/NightDash/Scripts/ECS/Components/NightDashBuffers.cs`
- `Assets/NightDash/Scripts/ECS/Systems/DataBootstrapSystem.cs`
- `Assets/NightDash/Scripts/ECS/Systems/NightDashRuntimeBootstrapSystem.cs`
- `Assets/NightDash/Scripts/Runtime/RunSelectionLobbyUI.cs`

규칙
- hotspot은 메인 에이전트가 마지막에 통합한다.
- worker는 hotspot 변경이 꼭 필요하면 직접 수정 대신 요구 변경 목록을 보고한다.
