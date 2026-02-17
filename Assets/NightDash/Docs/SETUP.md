# NightDash Unity DOTS 초기 환경

## 1. 권장 Unity 패키지
- Entities (DOTS)
- Burst
- Mathematics
- Collections

## 2. 씬 배치 순서
1. 빈 GameObject 생성 후 `NightDashBootstrapAuthoring` 추가
2. 플레이어 프리팹에 `PlayerAuthoring` 추가
3. 적 프리팹에 `EnemyAuthoring` 추가
4. `NightDashBootstrapAuthoring`에 플레이어/적 프리팹 연결
5. 타임라인 포인트, 난이도 체크리스트 기본값 입력

## 3. 현재 포함된 시스템
- GameLoopSystem
- StageTimelineSystem
- EnemySpawnSystem
- WeaponSystem
- CombatSystem
- EvolutionSystem
- DifficultySystem
- MetaProgressionSystem
- SaveSystem

## 4. MVP 연결 포인트
- 이동 + 적 스폰: EnemySpawnSystem + EnemyChase
- 자동 공격 1종: WeaponSystem + Projectile
- 레벨업: GameLoopSystem
- 체크리스트 위험도: DifficultySystem
- 보스 후 진화 플래그: EvolutionSystem
