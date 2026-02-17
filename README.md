# NightDash

다크 판타지 분위기의 Survivor-like Roguelite를 **Unity DOTS(ECS + Burst)** 기반으로 구현하는 프로젝트입니다.  
현재는 핵심 게임 루프와 전투/스폰/메타 진행의 MVP 시스템을 ECS로 구성한 상태입니다.

## 프로젝트 개요

- 장르: 다크 판타지 Survivor-like Roguelite
- 플랫폼 목표: PC (Steam)
- 기술 스택: Unity, Entities(DOTS), Burst, Mathematics, Collections
- 설계 방향: 1인 개발 기준의 시스템 중심, 데이터 지향 아키텍처

핵심 루프:
1. 지역 선택
2. 클래스 선택
3. 난이도 체크리스트 선택
4. 전투 진행
5. 레벨업 및 성장 선택
6. 보스/진화
7. 정복 포인트 획득
8. 메타 트리 강화

## 현재 구현 범위 (ECS MVP)

- `GameLoopSystem`: 경과 시간, 레벨업(경험치 임계치 증가) 처리
- `DifficultySystem`: 체크리스트 버퍼 기반 위험도(`RiskScore`) 계산
- `StageTimelineSystem`: 시간대별 스폰 계수/보너스 반영
- `EnemySpawnSystem`: 플레이어 주변 적 스폰(ECB + 랜덤)
- `WeaponSystem`: 자동 투사체 발사
- `CombatSystem`: 적 추적 이동, 투사체 수명/충돌/피해 처리
- `EvolutionSystem`: 보스 처치 + 위험도 조건 시 심연 진화 플래그
- `StageProgressSystem`: 보스 스폰 시점/클리어 시점 기반 런 종료
- `MetaProgressionSystem`: 런 종료 시 정복 포인트 보상 지급
- `SaveSystem`: 메타 진행도 `PlayerPrefs` 저장

## ECS 아키텍처 요약

### 주요 컴포넌트

- 게임 상태
  - `GameLoopState`, `StageRuntimeConfig`, `EvolutionState`, `MetaProgress`
- 전투/개체
  - `CombatStats`, `PlayerTag`, `EnemyTag`, `BossTag`
- 스폰/무기
  - `EnemySpawnConfig`, `WeaponRuntimeData`, `ProjectileData`, `PhysicsVelocity2D`
- 버퍼 데이터
  - `StageTimelineElement`, `DifficultyModifierElement`

### Authoring/Baking

- `NightDashBootstrapAuthoring`
  - 게임 루프 초기값, 스폰 설정, 타임라인, 난이도 버퍼, RNG 시드 구성
- `PlayerAuthoring`
  - 플레이어 전투 스탯 및 무기 런타임 데이터 구성
- `EnemyAuthoring`
  - 적/보스 태그 및 스탯 구성
- `ProjectileAuthoring`
  - 투사체 기본 컴포넌트 구성

## 폴더 구조

```text
Assets/
  NightDash/
    Docs/
      SETUP.md
    Scripts/
      Data/
      ECS/
        Authoring/
        Components/
        Systems/
NightDash_GDD.md
README.md
```

## 실행/세팅 방법

자세한 설정: `Assets/NightDash/Docs/SETUP.md`

기본 순서:
1. 빈 GameObject 생성 후 `NightDashBootstrapAuthoring` 추가
2. 플레이어 프리팹에 `PlayerAuthoring` 추가
3. 적 프리팹에 `EnemyAuthoring` 추가
4. `NightDashBootstrapAuthoring`에 프리팹/타임라인/난이도 데이터 연결
5. 플레이 모드에서 스폰, 자동 공격, 레벨업 루프 확인

## 데이터 설계 (ScriptableObject)

`Assets/NightDash/Scripts/Data`에 다음 데이터 정의가 포함되어 있습니다.

- `ClassData`
- `WeaponData`
- `PassiveData`
- `EvolutionData`
- `DifficultyModifierData`
- `MetaTreeData`
- `StageData`

현재 ECS 런타임 MVP와 병행해, 콘텐츠 중심 확장을 위한 데이터 레이어를 준비한 상태입니다.

## 로드맵 (예정)

- 플레이어 이동 입력/회피/피격 시스템 고도화
- 보스 전투 패턴과 정교한 충돌 처리
- 무기/패시브 선택 UI 및 인게임 성장 트리
- 클래스별 고유 트리와 메타 강화 연동
- Save/Load 슬롯화 및 밸런싱 파이프라인

## 문서

- 게임 디자인 문서: `NightDash_GDD.md`
- 초기 환경/씬 세팅: `Assets/NightDash/Docs/SETUP.md`

## 라이선스

- 코드 라이선스: [MIT](./LICENSE)
- 에셋/콘텐츠 라이선스: [All Rights Reserved](./LICENSE-ASSETS)

정리:
- `Assets/NightDash/Scripts` 및 코드 파일은 MIT 라이선스를 따릅니다.
- 게임 에셋/콘텐츠(그래픽, 오디오, 스토리, 브랜드, 게임 콘텐츠 데이터 등)는 `LICENSE-ASSETS` 기준으로 보호됩니다.
