# NightDash Codemap (S4-06)

**작성**: 2026-04-20
**목적**: 주요 ECS 시스템·브리지·데이터 자산을 한 페이지에서 탐색.

## 1. ECS 시스템 (Assets/NightDash/Scripts/ECS/Systems)

### 1.1 게임 루프 / 진행

| 시스템 | 책임 | 주요 컴포넌트 |
|---|---|---|
| `GameLoopSystem` | 경과 시간, 레벨업, 런 상태 전이 | `GameLoopState`, `RunStatus` |
| `RunSelectionOverrideSystem` | 스테이지/클래스 선택 반영 | `RunSelection`, `DataLoadState` |
| `RunNavigationSystem` | Retry/ReturnToLobby 네비게이션 | `RunNavigationRequest` |
| `StageTimelineSystem` | 시간 구간별 스폰 계수 | `StageTimelineElement` |
| `StageProgressSystem` | 보스 스폰/클리어 판정 | `BossSpawnState`, `StageRuntimeConfig` |
| `MetaProgressionSystem` | 런 종료 보상 계산 | `RunResultStats`, `MetaProgress` |
| `SaveSystem` + `SaveDataHelper` | PlayerPrefs 저장 (체크섬·version·range, S3-07) | `MetaProgress`, `SaveState` |

### 1.2 전투 / 스폰 / 이동

| 시스템 | 책임 | 주요 컴포넌트 |
|---|---|---|
| `PlayerMovementSystem` | 플레이어 이동 입력 처리 | `PlayerTag`, `LocalTransform` |
| `EnemySpawnSystem` | 주변 적 스폰 (ECB) | `EnemySpawnConfig`, `SpawnArchetypeElement` |
| `WeaponSystem` | 자동 투사체 발사 | `WeaponRuntimeData`, `ProjectileData` |
| `CombatSystem` (300줄, S2-02) | 추적·충돌·피해 (CombatHelpers/Events 분리) | `CombatStats`, `PhysicsVelocity2D` |
| `SpawnPrefabResolveSystem` | 적 아키타입 해석 | `SpawnPrefabLookup` |
| `DifficultySystem` | 난이도 버퍼 → 리스크/스케일 | `DifficultyState`, `DifficultyModifierElement` |
| `EvolutionSystem` | 진화 조건 판정 | `EvolutionState` |

### 1.3 성장 (S2-01 분할)

`Assets/NightDash/Scripts/ECS/Systems/Progression/`

| 시스템 | 책임 |
|---|---|
| `LevelUpSelectionGateSystem` | LevelUp 선택 화면 게이트 |
| `UpgradeOptionGeneratorSystem` | 3+리롤 옵션 생성 |
| `UpgradeApplySystem` | 선택된 옵션 효과 적용 |
| `UpgradeOptionUtility` (pure static) | 가중 추첨·중복 제거 헬퍼 |

### 1.4 부트스트랩

| 시스템 | 책임 |
|---|---|
| `NightDashRuntimeBootstrapSystem` | 씬 진입 시 ECS 월드 기본값 |
| `NightDashPlayableFallbackSystem` | 베이킹 누락 시 최소 플레이어블 복구 |
| `DataBootstrapSystem` | `DataRegistry` → ECS 싱글톤/버퍼 주입 |

## 2. MonoBehaviour 브리지 (Assets/NightDash/Scripts/Runtime)

ECS ↔ 외부 시스템 어댑터. 상세 경계: `Docs/Architecture/bridges.md`.

### 2.1 UI / IMGUI

- `RunSelectionLobbyUI` (333줄, S2-03) — 로비 IMGUI 코디네이터
  - `RunSelectionLobbyOptions` — stage/class ID 리스트 헬퍼
  - `RunSelectionLobbyWorldBridge` — ECS 월드 조작 (TryApply·SetActive·TryGetPendingNavigation)
  - `NightDashButtonFrameStyle` — 버튼 프레임 텍스처/스타일 유틸 (TitleScreen UI와 공유)
- `NightDashTitleScreenUI` — 타이틀 화면 (Canvas UI)
- `NightDashHudResultUI` — 런 결과/리트라이/로비 복귀 UI
- `LevelUpSelectionUI` — 레벨업 카드 선택 UI
- `NightDashTutorialBridge` (S3-06) — GDD T0~T5 6 트리거 IMGUI 토스트
- `RunSelectionSession` — 정적 세션 캐시 + PlayerPrefs 영속 (S4-07 guard)

### 2.2 렌더 / VFX / 오디오

- `NightDashVFXBridge` (S4-05 GC 최적화) — hit/death VFX
- `NightDashDamageNumberUI` (S4-05 GC 최적화) — 월드 공간 피해 숫자
- `NightDashObstacleBridge` (S1-08 ReadWrite guard) — 장애물 → ECS 인스턴스
- `NightDashDebugVisualBridge` (S4-07 Editor/Dev 전용) — 디버그 비주얼
- `NightDashAudioBridge` (S3-05) + `AudioLibrary` SO — 8 이벤트 슬롯
- `LocalizationService` + `LocalizationTable` SO (S4-03, ko/en)

### 2.3 시스템 유틸

- `DataRegistry` — ScriptableObject 런타임 lookup 허브
- `NightDashLog` — `NightDashRuntimeToggles.verboseRuntimeLogs` 기반 로깅 (S4-07 기본 false)

## 3. 데이터 자산 (Assets/NightDash/Data)

### 3.1 현황 (2026-04-20)

| 카테고리 | 파일 수 | S3 실사화 태스크 |
|---|---:|---|
| Classes | 7 | S3-02 |
| Stages | 9 (main 6 + bonus 3) | S3-01 (stage_01) |
| Weapons | 10 | S3-02 (4 신규 클래스 전용) |
| Passives | 10 | S3-02 (4 고유 패시브) |
| Evolutions | 2 | S3-04 |
| Meta Trees | 3 | S3-04 |
| Difficulty Modifiers | 5 | S3-03 |
| AudioLibrary | 1 슬롯만 (클립 미할당) | S3-05 |
| LocalizationTable | 0 (추후 strings_master.csv 임포트) | S4-03 |

### 3.2 레지스트리 구조

`data_catalog.asset` (DataCatalog SO) → `DataRegistry` 경유 ECS 주입.
`Classes`·`Weapons`·`Passives`·`Evolutions`·`Stages`·`DifficultyModifiers`·`MetaTrees` 리스트 보유.

## 4. 테스트 맵 (Assets/NightDash/Tests/EditMode)

| 파일 | 대상 | 케이스 수 |
|---|---|---:|
| `RuntimeBalanceUtilityTests` (S1-04) | 플레이어/무기 ±20% 회귀 | 9 |
| `EnemySpawnInvariantTests` (S1-10) | 플레이어·적 속도 불변식 | ~5 |
| `HarnessSmokeTests` | 하네스 스모크 | 1 |
| `CombatHelpersTests` (S2-04) | 전투 헬퍼 단위 | 12 |
| `UpgradeOptionUtilityTests` (S2-05) | 옵션 가중 추첨 | ~13 |
| `SaveDataHelperTests` (S3-07) | 체크섬·버전·범위 fallback | 9 |
| `Stage1ContentRegressionTests` (S3-08) | 7 class × 5 modifier 수치 잠금 | 23 |
| `TutorialConfigTests` (S3-06) | 튜토리얼 6 트리거 | 3 |
| `AudioLibraryTests` (S3-05) | 오디오 슬롯 구조 | 10 |
| `LocalizationServiceTests` (S4-03) | ko/en 조회·fallback | 12 |
| `RunSelectionSessionNormalizeTests` (S4-07) | FixedString64 guard | 7 |

**PlayMode** (Assets/NightDash/Tests/PlayMode, batchmode에서 `Assert.Ignore`):

- `ManualFlowPlayModeTests` (S1) — Stage1 vertical slice
- `Stage1ContentPlayModeTests` (S4-02) — 신규 클래스 load·SaveData roundtrip·RunSession 영속

## 5. 도구 스크립트 (scripts/)

| 파일 | 역할 |
|---|---|
| `run-editmode-tests.sh` (S1-03) | Unity batchmode EditMode 실행 |
| `run-data-validation.sh` (S2-06) | DataValidator batchmode |
| `git-hooks/pre-commit` (S1-07) | mono_crash·임시파일·scratch 차단 |

## 6. CI / 배포 (.github/workflows/)

| 파일 | 트리거 | 설명 |
|---|---|---|
| `editmode-tests.yml` (S4-01) | 수동 | EditMode 전체 (Unity 라이선스 시크릿 필요) |
| `data-validation.yml` (S4-01) | 수동 | DataValidator batchmode |
| `smoke-build.yml` (S4-01) | 수동 + 주간 월요일 | Win64/macOS Standalone |
| `README.md` | — | 시크릿 설정·트리거 복원 절차 |

## 7. 문서 트리

```
Docs/
├── Architecture/
│   ├── bridges.md                      (S2-07)
│   └── progression-split-rfc.md        (S1-05)
├── Balance/
│   └── s4_04_integration_audit.md      (S4-04)
├── Codemap/
│   └── system-index.md                 (이 문서, S4-06)
├── GDD/                                (6 지역·7 클래스·15 모디파이어 사양)
│   ├── specs/
│   ├── production/
│   ├── ops/
│   └── ...
├── Profiling/
│   └── s4_05_memory_gc_guide.md        (S4-05)
└── Release/
    └── steam-build-guide.md            (S4-08)
```

## 8. 변경 이력

- 2026-04-20: S4-06 초기 작성 — Sprint 2·3·4 반영
