# S1-05: ProgressionSystem 분할 설계 RFC

**Status:** Approved (2026-04-19)
**Author:** `everything-claude-code:architect` agent (via session)
**Implementation target:** Sprint 2, task S2-01
**Supersedes:** 초기 태스크 브리프의 "4 모듈 분할" 지시 (XPCollection·LevelUpQueue·UpgradeApply·EvolutionGate)

## 1. Purpose

`Assets/NightDash/Scripts/ECS/Systems/ProgressionSystem.cs` (642줄)는 프로젝트 최대 시스템 파일로, CLAUDE.md §3.2 "시스템 파일 600줄 초과 시 분할 필수" 규정을 위반한다. 본 RFC는 **Sprint 2의 S2-01 실행 담당 agent**가 별도 설계 없이 곧바로 코드 이관을 시작할 수 있도록, 책임 경계·모듈 계약·마이그레이션 순서를 확정한다.

**중요 정정 (실제 코드 확인 결과)**: 사전 태스크 브리프에 명시된 4개 모듈 중 세 개는 이미 별도 시스템에 존재한다.

| 모듈 (브리프 지시) | 실제 현재 위치 | 분할 대상 여부 |
|---|---|---|
| XPCollectionSystem | `CombatSystem.cs:303` (드롭 처리) → `GameLoopSystem.cs:31-48` (소비) | **아니오. 이미 분리되어 있음** |
| LevelUpQueueSystem | `GameLoopSystem.cs:31-48` (PendingLevelUps 증가) | **아니오. 이미 분리되어 있음** |
| UpgradeApplySystem | `ProgressionSystem.cs` 내부 | **예. 본 분할 대상** |
| EvolutionGateSystem | `EvolutionSystem.cs` (별도 시스템) | **아니오. 이미 분리되어 있음** |

따라서 본 RFC는 **`ProgressionSystem` 내부를 4개 모듈이 아닌 3개 시스템 + 1개 유틸리티**로 재분할하는 설계로 조정한다.

## 2. Current State (642줄 책임 맵)

모든 내용은 `public partial struct ProgressionSystem : ISystem` 단일 타입 내부. `SimulationSystemGroup`, `[UpdateAfter(typeof(GameLoopSystem))]`.

```
ProgressionSystem.cs (642 lines)
├─ [1–23]    OnCreate + using/namespace — RequireForUpdate 6종
├─ [25–52]   OnUpdate entry — Status 전이 (Playing → LevelUpSelection)
├─ [54–74]   Reroll 처리 — RerollsRemaining 차감 + GenerateOptions 재호출
├─ [76–88]   빈 옵션 초기 생성
├─ [90–112]  옵션이 여전히 비었을 때 PendingLevelUps 소진 경로
├─ [114–148] 선택 반영 — ApplySelection + RuntimeBalanceUtility 재계산 + 상태 복귀
├─ [151–272] GenerateOptions — 후보 풀 구성 + 종류/신규해금/프레시 보장 (122 lines)
├─ [274–345] ApplySelection — 무기·패시브 레벨업 또는 신규 슬롯 추가 (72 lines)
├─ [347–397] Contains* / AddCandidate 헬퍼
├─ [399–425] AddPreferredOptions — 우선 채움 로직
├─ [427–523] EnsureOptionKindPresence / EnsureFreshUnlockPresence — 종류·신규 보장
├─ [525–575] ContainsOption overloads / FindCandidateIndex
└─ [577–640] HasFreshUnlock / HasOptionKind / TryReplaceOption / ContainsOption overload
```

**외부 참조**: `ProgressionSystem` 타입 자체는 외부 C# 코드에서 `[UpdateAfter(typeof(ProgressionSystem))]` 등으로 참조되지 않는다 (Grep 결과 자기 파일 외에는 문서·브리프만 언급). 따라서 타입 이름을 교체해도 컴파일 영향 없음.

**소비/생산 관계**:

| 데이터 | 생산자 | 소비자 | 현재 파일 역할 |
|---|---|---|---|
| `GameLoopState.PendingLevelUps` | `GameLoopSystem` | `ProgressionSystem` | RO + 감소 |
| `GameLoopState.Status` | `GameLoopSystem`/`ProgressionSystem` | 전역 | RW (전이) |
| `UpgradeSelectionRequest` | `LevelUpSelectionUI`/`RunSelectionOverrideSystem` | `ProgressionSystem` | RO + 클리어 |
| `UpgradeOptionElement` 버퍼 | `ProgressionSystem` | `LevelUpSelectionUI` | RW (생성·클리어) |
| `OwnedWeaponElement`/`OwnedPassiveElement` 버퍼 | `ProgressionSystem` | `WeaponSystem`/`CombatSystem`/`EvolutionSystem` | RW (추가·레벨업) |
| `PlayerProgressionState.RerollsRemaining` | `ProgressionSystem` | UI | RW |
| `AvailableWeaponElement`/`AvailablePassiveElement` | `DataBootstrapSystem` | `ProgressionSystem` | RO |

## 3. Proposed Split

### 3.1 설계 원칙

- **하나의 `SimulationSystemGroup` 순차 체인**으로 프레임 내 결정성 확보 (`[UpdateBefore]`/`[UpdateAfter]` 명시).
- `PlayerProgressionState`/`UpgradeSelectionRequest`/`UpgradeOptionElement` 싱글턴은 **각각 단일 writer**로 제약.
- 순수 알고리즘(후보 풀, 선호 채움)은 `static` 유틸 클래스로 추출해 EditMode 단위 테스트 가능하도록.

### 3.2 모듈 목록

| # | 파일 | 타입 | UpdateGroup / 순서 | 예상 라인 |
|---|---|---|---|---|
| M1 | `Assets/NightDash/Scripts/ECS/Systems/Progression/LevelUpSelectionGateSystem.cs` | `ISystem` (partial struct) | `[UpdateInGroup(SimulationSystemGroup)] [UpdateAfter(GameLoopSystem)]` | ~90 |
| M2 | `Assets/NightDash/Scripts/ECS/Systems/Progression/UpgradeOptionGeneratorSystem.cs` | `ISystem` (partial struct) | `[UpdateAfter(LevelUpSelectionGateSystem)]` | ~120 |
| M3 | `Assets/NightDash/Scripts/ECS/Systems/Progression/UpgradeApplySystem.cs` | `ISystem` (partial struct) | `[UpdateAfter(UpgradeOptionGeneratorSystem)]` | ~140 |
| H1 | `Assets/NightDash/Scripts/ECS/Systems/Progression/UpgradeOptionUtility.cs` | `static class` (managed) | — | ~260 |

총합 ~610 lines (각 파일 300 미만 충족). H1은 `List<UpgradeOptionElement>`를 사용하는 managed 유틸이므로 구조체가 아닌 `static class` 유지 — Burst 최적화 원한다면 S2-01에서 `NativeList<T>`로 리팩터링을 추가 검토.

### 3.3 M1 — LevelUpSelectionGateSystem

- **책임**: `PendingLevelUps > 0 && Status == Playing` 이면 `Status = LevelUpSelection`. 완료 조건(옵션 소비·선택 완료)은 M3가 책임.
- **Read**: `GameLoopState.PendingLevelUps`, `GameLoopState.Status`
- **Write**: `GameLoopState.Status` (Playing → LevelUpSelection 한 방향만)
- **Require**: `GameLoopState`
- **브리프 매핑**: "LevelUpQueueSystem" 역할의 **상태 전이 부분**. 큐 증가는 `GameLoopSystem`에 이미 존재하므로 여기서는 게이트만.

### 3.4 M2 — UpgradeOptionGeneratorSystem

- **책임**: `Status == LevelUpSelection` 이면서 `options.Length == 0` 또는 `RerollRequested == 1` 일 때만 옵션 3종 채움. 옵션이 끝내 비면 `PendingLevelUps` 감쇠 + 상태 복귀.
- **Read**: `GameLoopState` (Status/Level/PendingLevelUps), `AvailableWeaponElement`, `AvailablePassiveElement`, `OwnedWeaponElement`, `OwnedPassiveElement` (RO), `PlayerProgressionState` (SlotLimit/RerollsRemaining RO/RW)
- **Write**: `UpgradeOptionElement` 버퍼, `UpgradeSelectionRequest.RerollRequested` 클리어, `PlayerProgressionState.RerollsRemaining` 차감, `GameLoopState.PendingLevelUps`/`Status` (옵션 없을 때 감쇠)
- **Require**: `OwnedWeaponElement`, `OwnedPassiveElement`, `UpgradeOptionElement`, `UpgradeSelectionRequest`, `PlayerProgressionState`, `GameLoopState`
- **위임**: 후보 구성·선호 채움은 `UpgradeOptionUtility.BuildOptions(...)` 호출 단 한 번.

### 3.5 M3 — UpgradeApplySystem

- **책임**: `HasSelection == 1`일 때 선택된 옵션을 `OwnedWeapon/Passive` 버퍼에 반영, `RuntimeBalanceUtility.RefreshPlayerRuntime` 호출, 옵션 버퍼 클리어, `PendingLevelUps` 감쇠·Status 복귀.
- **Read**: `UpgradeSelectionRequest` (SelectedOptionIndex/HasSelection), `RunSelection.ClassId`, `UpgradeOptionElement` 버퍼
- **Write**: `OwnedWeaponElement`, `OwnedPassiveElement`, `UpgradeSelectionRequest` 클리어, `UpgradeOptionElement.Clear()`, `GameLoopState.PendingLevelUps`/`Status`
- **Require**: 현재와 동일 6종
- **위임**: `UpgradeOptionUtility.ApplySelection(...)` (274–345 블록 그대로 이관).
- **순서 주의**: 이 시스템이 M2보다 **뒤**에서 돌아야 reroll 시 옵션이 먼저 채워지고 같은 프레임에 실수로 "빈 옵션 선택" 분기를 타지 않는다.

### 3.6 H1 — UpgradeOptionUtility (static class)

- 이관 대상 메서드 (원본 라인 범위):
  - `GenerateOptions` (151–272) → `BuildOptions`
  - `ApplySelection` (274–345) → `ApplySelection`
  - `ContainsWeapon`/`ContainsPassive`/`ContainsOption`×3 (347–384, 525–536, 629–640)
  - `AddCandidate` (386–397), `AddPreferredOptions` (399–425)
  - `EnsureOptionKindPresence`/`EnsureFreshUnlockPresence` (427–523)
  - `FindCandidateIndex` (538–575), `HasFreshUnlock`/`HasOptionKind`/`TryReplaceOption` (577–627)
- `internal static` + `[InternalsVisibleTo("NightDash.Tests.EditMode")]`로 단위 테스트 노출 (S1-03에서 이미 설정됨).

## 4. Module Contracts & Ownership

### 4.1 Writer 단일 소유권 표

| 자원 | 단독 Writer | 보조 Reader | 비고 |
|---|---|---|---|
| `GameLoopState.Status` | `GameLoopSystem`, `M1`, `M2`, `M3` | 전 시스템 | **복수 writer 허용** (전이 방향별로 경합 없음 — 모두 `[UpdateAfter]` 체인 내) |
| `GameLoopState.PendingLevelUps` | `GameLoopSystem`(+), `M2`(-), `M3`(-) | | 증가/감소 방향이 분리되어 안전 |
| `PlayerProgressionState.RerollsRemaining` | `M2` 단독 | UI (RO) | |
| `UpgradeOptionElement` 버퍼 | `M2`(Add/Clear), `M3`(Clear) | `LevelUpSelectionUI` (RO) | |
| `UpgradeSelectionRequest` | `M2`(RerollRequested 클리어), `M3`(HasSelection/SelectedOptionIndex 클리어) | UI (Set) | 필드별 분리 — 경합 없음 |
| `OwnedWeaponElement` / `OwnedPassiveElement` | `M3` 단독 | `WeaponSystem`, `CombatSystem`, `EvolutionSystem` | |
| `AvailableWeaponElement` / `AvailablePassiveElement` | `DataBootstrapSystem` | `M2` (RO) | |

### 4.2 프레임 내 실행 순서

```
GameLoopSystem
  → M1 LevelUpSelectionGateSystem       (Status 게이트)
  → M2 UpgradeOptionGeneratorSystem     (reroll·옵션 생성·빈 옵션 감쇠)
  → M3 UpgradeApplySystem               (선택 반영·상태 복귀)
  → EvolutionSystem (after CombatSystem, 기존 유지)
```

### 4.3 Race Condition 회피

- **3개 시스템이 모두 `GameLoopState.Status`를 쓰므로 순서 경합 우려**: `[UpdateAfter]` 체인으로 같은 프레임 내 직렬화 보장. 메인 스레드 `ISystem.OnUpdate` 이므로 잡 경합 없음.
- **M2 reroll 분기와 M3 선택 분기가 같은 프레임 재진입**: 원본 로직상 reroll 직후에는 `HasSelection=0`으로 클리어하므로 M3는 no-op. 계약 유지됨.
- **`PendingLevelUps` 이중 감쇠 금지**: M2는 "옵션 생성 실패시에만" 감쇠, M3는 "선택 적용 완료시에만" 감쇠. 두 경로는 상호 배타 (옵션 없으면 선택 불가능). 원본과 동일 계약.

## 5. Migration Plan

S2-01 실행 담당 agent(`refactor-cleaner`)가 다음 순서대로 진행:

| 단계 | 작업 | 검증 방법 |
|---|---|---|
| 1 | `Assets/NightDash/Scripts/ECS/Systems/Progression/` 폴더 + `.meta` 생성 | Unity 컴파일 그린 |
| 2 | `UpgradeOptionUtility.cs` 생성 — 원본 151–272, 274–345, 347–640 정적 메서드 복사 (아직 원본 유지) | 컴파일 그린, 기존 `ProgressionSystem`은 내부 헬퍼를 `UpgradeOptionUtility.X` 위임으로 교체 |
| 3 | `UpgradeApplySystem.cs` 생성 — `ApplySelection` 호출 경로(원본 114–148)만 담당하는 ISystem 구현. 원본 파일 해당 블록 제거 | EditMode: 기존 Progression 회귀 테스트 통과 (확인 필요: `ProgressionSystemTests` 존재 여부) |
| 4 | `UpgradeOptionGeneratorSystem.cs` 생성 — 원본 54–112(reroll·초기 생성·빈 옵션 경로) 이관 | Manual: `NightDashManualPlaytestRunner`에서 레벨업 → 리롤 → 선택 플로우 실행 |
| 5 | `LevelUpSelectionGateSystem.cs` 생성 — 원본 44–47 상태 전이만 이관. `ProgressionSystem.cs` 삭제 | PlayMode: `ManualFlowPlayModeTests` 통과 |
| 6 | 문서 업데이트 — `Docs/Architecture/`에 모듈 다이어그램, CLAUDE.md §5 경로 추가 | `doc-updater` agent 실행 |
| 7 | `code-reviewer` 자체 리뷰 — 4파일 모두 400줄 이하 확인, CLAUDE.md 규약 준수 | 리뷰 보고서 |

각 단계 종료 시 커밋 (`feat(progression): split <module>`). 단계 3–5 중간 커밋도 컴파일이 성립해야 함 — 이를 위해 2단계에서 원본을 먼저 위임 형태로 만들어 두는 것이 핵심.

## 6. Risks & Mitigations

| 리스크 | 영향 | Detection | 완화 |
|---|---|---|---|
| **R1. 프레임 내 상태 전이 순서 뒤바뀜** — M1/M2/M3 `[UpdateAfter]` 누락 시 reroll·선택 분기 누락 | 레벨업 선택 UI가 안 뜨거나 이중 차감 | Manual 테스트로 "한 프레임에 PendingLevelUps 2회 증가" 시나리오 재현, 카운터 로깅 | `[UpdateAfter]` 체인 명시 + EditMode에서 시스템 등록 순서 단위 테스트 추가 |
| **R2. `UpgradeOptionElement` 버퍼 Clear 타이밍 어긋남** — M3가 Clear하는데 M2가 같은 프레임에 재채움 경합 | 선택 직후 옵션이 즉시 사라지지 않거나, 이미 적용된 옵션이 다시 표시 | `LevelUpSelectionUI` 렌더 로그 비교 (분할 전/후) | M3의 Clear는 반드시 `PendingLevelUps > 1` 분기 **이후**에 수행하도록 원본 131–144 순서 그대로 유지 |
| **R3. `RuntimeBalanceUtility.RefreshPlayerRuntime(ref state, ...)` 호출 주체 모호** — `SystemState`를 넘기므로 어느 시스템 컨텍스트에서 호출하느냐가 성능/결정성에 영향 | 스탯 재계산이 누락되거나 이중 호출 | Manual에서 레벨업 후 이동 속도/공격력 즉시 반영 확인 | M3 `UpgradeApplySystem.OnUpdate` 내부에서만 호출. M2에서는 호출 금지 (리롤은 소유 변화 없음) |

## 7. Open Questions

1. **확인 필요**: `ProgressionSystem`에 대한 EditMode/PlayMode 테스트가 존재하는가? 존재한다면 분할 후 시스템 이름 참조 업데이트 필요.
2. **확인 필요**: `NightDash.Tests.EditMode` asmdef의 `[InternalsVisibleTo]` 설정은 S1-03에서 이미 완료됨 → `UpgradeOptionUtility` 단위 테스트 허용.
3. **Burst 고려**: 현재 `GenerateOptions`는 `List<UpgradeOptionElement>` (managed heap) 사용으로 Burst 대상이 아님. S2-01 범위를 "동등 분할"로 한정할지, `NativeList<T>` 전환까지 포함할지 스프린트 계획 시 결정 필요.
4. **브리프 4모듈 vs 실제 3+1모듈 괴리**: 본 RFC는 브리프에 기술된 `XPCollectionSystem`/`EvolutionGateSystem`이 **이미 다른 파일에 존재**함을 근거로 생성하지 않는다. 이 결정을 Sprint 2 킥오프에서 명시 승인받을 것.

---

## 관련 파일 (모두 절대 경로)

- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/ECS/Systems/ProgressionSystem.cs` (분할 대상, 642줄)
- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/ECS/Systems/GameLoopSystem.cs` (XP/레벨업 감지 담당 — 분할 대상 아님)
- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/ECS/Systems/EvolutionSystem.cs` (진화 판정 담당 — 분할 대상 아님)
- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/ECS/Systems/CombatSystem.cs` (XP 드롭·획득)
- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/ECS/Components/NightDashComponents.cs` (`UpgradeSelectionRequest`, `GameLoopState` 등 정의)
- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/ECS/Components/NightDashBuffers.cs` (버퍼 정의)
- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/Runtime/LevelUpSelectionUI.cs` (UI 소비자)
- `/Users/ihyeongju/Develop/Private/NightDash/Assets/NightDash/Scripts/ECS/Systems/RuntimeBalanceUtility.cs` (M3가 호출)
