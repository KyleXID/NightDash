# NightDash — 프로젝트 작업 규약

> 이 파일은 Claude Code 세션마다 자동 로드됩니다. 모든 지시는 다른 규칙보다 우선합니다.

## 1. 프로젝트 개요

- **장르**: 다크 판타지 Survivor-like Roguelite
- **엔진**: Unity (DOTS = Entities + Burst + Mathematics + Collections)
- **플랫폼 목표**: PC (Steam)
- **개발 체제**: 1인 개발, 시스템/데이터 지향
- **현재 단계**: Stage 1 MVP (플레이어블 루프 완성, 콘텐츠/테스트/안정성 보강 단계)

## 2. 필수 작업 규칙

### 2.1 everything-claude-code 플러그인 필수 사용

이 프로젝트의 모든 작업은 **반드시 `/everything-claude-code` 플러그인의 agent·skill·command를 우선 사용**합니다.

- 신규 기능/리팩터링/버그픽스/리뷰/문서화 작업 시작 전, 아래 역할 분담표에서 해당 agent를 먼저 호출합니다.
- 범용 `general-purpose` agent는 ECC agent가 커버하지 못하는 경우에만 사용합니다.
- 순수 파일 조회(Read/Glob/Grep)·단일 편집(Edit)은 agent 없이 직접 수행해도 됩니다.

### 2.2 Agent 역할 분담

| 작업 유형 | 사용 Agent | 트리거 예시 |
|---|---|---|
| 아키텍처/시스템 설계 | `everything-claude-code:architect` | "새 시스템 추가", "ECS 구조 개선", "MonoBehaviour 브리지 경계 재정의" |
| 복합 작업 계획 수립 | `everything-claude-code:planner` | "기능 구현 계획", "리팩터링 로드맵", "마일스톤 쪼개기" |
| C# 코드 리뷰 | `everything-claude-code:code-reviewer` | 모든 코드 변경 후 (C# 전용 리뷰어 부재로 범용 사용) |
| 대형 파일 분할/데드코드 | `everything-claude-code:refactor-cleaner` | `ProgressionSystem`(642줄) 등 분할, 미사용 코드 제거 |
| 테스트 설계/TDD | `everything-claude-code:tdd-guide` | EditMode/PlayMode 테스트 작성, 커버리지 확보 |
| 빌드/컴파일 오류 | `everything-claude-code:build-error-resolver` | Unity/C# 컴파일 실패 시 |
| 보안 점검 | `everything-claude-code:security-reviewer` | `SaveSystem` PlayerPrefs·세이브 무결성·데이터 검증 |
| 문서 동기화 | `everything-claude-code:doc-updater` | GDD/README/codemap 업데이트 |
| 코드베이스 탐색 | `Explore` | 3회 이상 쿼리가 필요한 광범위 탐색 |

### 2.3 워크플로우 표준 순서

1. **계획**: `planner` agent로 작업 분해 (규모가 크거나 파일 3개 이상 수정 시)
2. **설계**: 아키텍처 변경이 있으면 `architect` 검토
3. **구현 전 테스트**: `tdd-guide`로 실패 테스트 먼저 작성
4. **구현**: 직접 Edit/Write
5. **리뷰**: `code-reviewer` agent로 자체 리뷰 (필수)
6. **문서/코드맵**: 영향 시 `doc-updater` 호출

### 2.4 응답 언어

- 사용자 대화·설명·상태 업데이트: **한국어**
- 코드 주석·커밋 메시지·PR 설명: 영어 허용
- 기술 용어는 영어 병기 가능

## 3. 아키텍처 원칙

### 3.1 ECS 우선

- 게임 로직은 `Assets/NightDash/Scripts/ECS/Systems/`의 `SystemBase`/`ISystem`으로 구현
- 데이터는 `Components/`의 `IComponentData`/`IBufferElementData`
- MonoBehaviour 브리지(`Runtime/*Bridge.cs`)는 **ECS ↔ 렌더/UI/외부 시스템 어댑터 용도로만 제한**

### 3.2 파일 크기 가이드

- 시스템 파일: 400줄 이하 권장, 600줄 초과 시 분할 검토 필수
- UI 파일: 500줄 이하 권장
- Components 파일: 논리적 응집 단위로 분리

### 3.3 데이터 레이어

- `ScriptableObject` (`Assets/NightDash/Scripts/Data/`)로 정의, `Assets/NightDash/Data/`에 에셋 저장
- 베이킹은 `Authoring/` 에서만 수행
- 런타임 참조는 `DataRegistry`/`DataCatalog` 경유

## 4. 테스트 정책

- 신규 시스템은 **EditMode 단위 테스트 동반 필수**
- PlayMode 테스트 리플렉션 의존 최소화 — `internal` API + `[InternalsVisibleTo]` 사용
- 밸런스 수식(`RuntimeBalanceUtility`)은 수치 회귀 테스트 필수 (GDD 기준 ±20% 규칙)

## 5. 주요 경로

- GDD: `NightDash_GDD.md`, `Docs/GDD/`
- ECS 시스템: `Assets/NightDash/Scripts/ECS/Systems/`
- ECS 컴포넌트: `Assets/NightDash/Scripts/ECS/Components/`
- Authoring/Baking: `Assets/NightDash/Scripts/ECS/Authoring/`
- MonoBehaviour 브리지·UI: `Assets/NightDash/Scripts/Runtime/`
- Data SO 정의: `Assets/NightDash/Scripts/Data/`
- Data 에셋: `Assets/NightDash/Data/`
- 테스트: `Assets/NightDash/Tests/PlayMode/`
- 메인 씬: `Assets/Scenes/SampleScene.unity`

## 6. 금지 사항

- mono_crash 블롭·임시 파일 커밋 금지 (루트의 `mono_crash.*` 정리 대상)
- Git 커밋에 미검토 바이너리 에셋 일괄 추가 금지 — 경로별 개별 스테이징
- `--no-verify` 등 훅 우회 금지
- CLAUDE.md/메모리에 명시된 역할분담을 건너뛰고 작업 시작 금지
