# NightDash

다크 판타지 분위기의 Survivor-like Roguelite를 **Unity DOTS(ECS + Burst)** 기반으로 구현하는 프로젝트입니다.

2026-04-20 기준 **Stage 1 MVP 콘텐츠·안정성·CI 파이프라인**이 정착된 상태이며, Steam 릴리스 후보로 진입하기 위한 플레이테스트 단계에 있습니다.

## 프로젝트 개요

- 장르: 다크 판타지 Survivor-like Roguelite
- 플랫폼 목표: PC (Steam)
- 기술 스택: Unity 6000.3.8f1, Entities(DOTS), Burst, Mathematics, Collections
- 설계 방향: 1인 개발 기준의 시스템 중심, 데이터 지향 아키텍처

핵심 루프:
1. 스테이지 / 클래스 선택 (난이도 모디파이어 토글)
2. 15분 전투 + 성장 / 진화 / 보스
3. 정복 포인트 적립 → 메타트리 강화

## 현재 구현 범위

### ECS 시스템 (Assets/NightDash/Scripts/ECS/Systems)

- `GameLoopSystem` — 경과 시간, 레벨업 임계치 처리
- `DifficultySystem` — 체크리스트 버퍼 기반 위험도(`RiskScore`) 계산
- `StageTimelineSystem` — 시간대별 스폰 계수 반영
- `EnemySpawnSystem` — 플레이어 주변 적 스폰 (랜덤 + ECB)
- `WeaponSystem` — 자동 투사체 발사
- `CombatSystem` (300줄, S2-02 분할) — 피해·투사체·충돌 처리 + 헬퍼/이벤트 분리
  - `Combat/CombatHelpers.cs`, `Combat/CombatEvents.cs`
- `Progression/` 3 시스템 + 1 유틸 (S2-01 분할)
  - `LevelUpSelectionGateSystem`, `UpgradeOptionGeneratorSystem`, `UpgradeApplySystem`
  - `UpgradeOptionUtility` (pure-static helpers)
- `EvolutionSystem`, `StageProgressSystem`, `MetaProgressionSystem`
- `SaveSystem` + `SaveDataHelper` (S3-07, 체크섬·version·range fallback)

### 데이터 레이어 (Assets/NightDash/Data)

- **Classes 7종** (S3-02): warrior, mage, astrologer, paladin, priest, archer, gunslinger
- **Stages**: stage_01 실사 (S3-01) + stage_02~06 스켈레톤 + 보너스 3종
- **Weapons 10종**: demon_greatsword, demon_orb, starfall, hellflame_slash + 진화 2종 + 클래스 전용 4종
- **Passives 10종**: stat 3종 + 클래스 고유 7종
- **Evolutions 2종** (S3-04): void_starfall, hellflame_slash
- **Meta Trees 3종** (S3-04): warrior, mage, astrologer (각 7 nodes)
- **Difficulty Modifiers 5종** (S3-03): enemy_hp_up, enemy_speed_up, enemy_surge, no_heal, on_kill_explosion

### 브리지 (Assets/NightDash/Scripts/Runtime)

ECS ↔ UI/렌더/외부 시스템 어댑터. 경계는 `Docs/Architecture/bridges.md` 참고.

- `RunSelectionLobbyUI` (S2-03 분할, 333줄) + `RunSelectionLobbyWorldBridge` + `RunSelectionLobbyOptions` + `NightDashButtonFrameStyle`
- `NightDashAudioBridge` (S3-05) + `AudioLibrary` SO
- `NightDashTutorialBridge` (S3-06, GDD T0~T5 6 트리거)
- `LocalizationService` (S4-03, ko/en) + `LocalizationTable` SO
- `NightDashVFXBridge` / `NightDashDamageNumberUI` (S4-05 GC 최적화 완료)
- `NightDashDebugVisualBridge` (S4-07 Editor/Dev 빌드 전용 가드)

### 검증·테스트 (Assets/NightDash/Tests/EditMode)

115 테스트 / 113 pass / 0 fail / 2 skipped(PlayMode batchmode 한계, Editor Test Runner에서 실행).

- `RuntimeBalanceUtilityTests` (S1-04) — 플레이어/무기 밸런스 ±20% 회귀
- `CombatHelpersTests` (S2-04) — 전투 헬퍼 단위 테스트
- `UpgradeOptionUtilityTests` (S2-05) — 옵션 생성 로직
- `SaveDataHelperTests` (S3-07) — 체크섬·범위·버전 fallback
- `Stage1ContentRegressionTests` (S3-08) — 7 class × 5 modifier 수치 잠금
- `TutorialConfigTests`, `AudioLibraryTests`, `LocalizationServiceTests`, `RunSelectionSessionNormalizeTests`

## CI / 릴리스

- GitHub Actions 3 워크플로 (`.github/workflows/`, S4-01)
  - `editmode-tests.yml` — EditMode 전체 (~15분)
  - `data-validation.yml` — DataValidator batchmode (~8분)
  - `smoke-build.yml` — Win64/macOS Standalone (수동 + 주간 월요일)
  - 현재 `workflow_dispatch`만 활성 — `UNITY_LICENSE` 시크릿 설정 후 PR 게이트 활성화
- 데이터 검증: `scripts/run-data-validation.sh` (S2-06)
- EditMode 로컬: `scripts/run-editmode-tests.sh` (S1-03)
- Git pre-commit 훅: `scripts/git-hooks/pre-commit` (S1-07, mono_crash·임시파일 차단)

## 문서

| 경로 | 내용 |
|---|---|
| `NightDash_GDD.md`, `Docs/GDD/` | 게임 디자인 문서 (6 지역·7 클래스·15 모디파이어 사양) |
| `Docs/Architecture/bridges.md` | MonoBehaviour ↔ ECS 브리지 경계 (S2-07) |
| `Docs/Architecture/progression-split-rfc.md` | ProgressionSystem 분할 RFC (S1-05) |
| `Docs/Balance/s4_04_integration_audit.md` | 통합 밸런스 1차 감사 (S4-04) |
| `Docs/Profiling/s4_05_memory_gc_guide.md` | Memory·GC 프로파일 절차 (S4-05) |
| `Docs/Release/steam-build-guide.md` | Steam 빌드·업로드 체크리스트 (S4-08) |
| `Docs/Codemap/system-index.md` | ECS 시스템·브리지·데이터 맵 (S4-06) |
| `Assets/NightDash/Audio/README.md` | 오디오 파이프라인 (S3-05) |
| `Assets/NightDash/Docs/SETUP.md` | 초기 환경/씬 세팅 |

## 프로젝트 규약

작업 규약은 `CLAUDE.md` 참고.
- ECC 플러그인 agent(architect·planner·code-reviewer·tdd-guide 등) 우선 사용
- 한국어 응답 / 영어 커밋 메시지 허용
- 파일 크기: 시스템 400줄, UI 500줄, 초과 시 분할 검토
- 신규 시스템은 EditMode 단위 테스트 필수
- mono_crash·임시 파일 커밋 금지 (pre-commit 훅이 강제)

## 로드맵

**완료** (Sprint 1-4, 2026-04-19~20):
- ✅ Stage 1 MVP 플레이어블 루프 (S1)
- ✅ 대형 시스템 분할 + 단위 테스트 스윗 (S2)
- ✅ Stage 1 실사 콘텐츠 + 테스트 회귀 게이트 (S3)
- ✅ CI 파이프라인 + 보안/메모리 패스 + 릴리스 문서 (S4)

**다음 단계** (S5+):
- 플레이테스트 5 연속 런 → S4-04 재검토 포인트 4건 조정
- Unity 라이선스 시크릿 등록 → CI 자동 트리거 활성화
- Stage 2~6 실사 콘텐츠
- 모디파이어 카테고리 커버리지 확장 (Survival 4 / Mechanic 4)
- 메타트리 노드 확장 (현재 7 → GDD 15~18 목표)
- Localization strings_master.csv 임포트 툴링

## 라이선스

- 코드: [MIT](./LICENSE)
- 에셋/콘텐츠: [All Rights Reserved](./LICENSE-ASSETS)
