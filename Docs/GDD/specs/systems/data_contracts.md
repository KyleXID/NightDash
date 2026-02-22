# Specs - Data Contracts (ScriptableObject/런타임 모델)

이 문서는 기획 데이터가 코드에 어떻게 들어가는지 정의한다.

## 1. ClassData
```yaml
id: string
name: string
baseHp: int
baseMoveSpeed: float
basePower: float
startWeaponId: string
uniquePassiveId: string
ultimateSkillId: string
tags: [string]
```

## 2. WeaponData
```yaml
id: string
name: string
weaponType: enum # melee, projectile, aoe, summon
maxLevel: int
baseCooldown: float
basePowerCoeff: float
baseRange: float
baseProjectileSpeed: float
levelCurves:
  - level: int
    powerCoeff: float
    cooldown: float
    range: float
specialFlags:
  canPierce: bool
  canCrit: bool
  hasKnockback: bool
```

## 3. PassiveData
```yaml
id: string
name: string
category: enum # stat, mechanic, risk, evolution_key
maxLevel: int
effects:
  - stat: string
    op: enum # flat, percent_add, percent_mul
    value: float
condition:
  type: enum # always, low_hp, on_kill, on_hit, timed
  arg: string
```

## 4. EvolutionData
```yaml
id: string
resultWeaponId: string
requiredWeaponId: string
requiredWeaponLevel: int
requiredPassiveIds: [string]
requiredRiskScoreMin: int
requiredModifiers: [string]
isAbyss: bool
priority: int
```

## 5. StageData
```yaml
id: string
name: string
durationSec: int
bossSpawnSec: int
environmentHazards: [string]
spawnPhases:
  - fromSec: int
    toSec: int
    entries:
      - enemyId: string
        weight: int
        spawnPerMin: int
eliteEvents:
  - atSec: int
    enemyId: string
    count: int
bossId: string
rewardTableId: string
```

## 6. DifficultyModifierData
```yaml
id: string
name: string
riskPoint: int
category: enum # combat, survival, mechanic
enemyModifiers:
  hpPct: float
  moveSpeedPct: float
  spawnRatePct: float
playerModifiers:
  healRatePct: float
  cooldownPct: float
runtimeEffects:
  hazardMultiplier: float
  onKillExplosion: bool
rewardBonusPct: float
```

## 7. MetaTreeData
```yaml
classId: string
nodes:
  - nodeId: string
    name: string
    cost: int
    prereqNodeIds: [string]
    effects:
      - stat: string
        op: enum
        value: float
```

## 8. RunConfig
```yaml
runId: string
stageId: string
classId: string
selectedModifierIds: [string]
riskScore: int
rewardMultiplier: float
seed: int
startedAtUtc: string
```

## 9. 밸리데이션 룰
- 모든 `id`는 프로젝트 내 유일해야 함
- 참조 필드(`startWeaponId`, `requiredPassiveIds`)는 존재 검증 필수
- 위험도 음수 금지
- 무기/패시브 maxLevel은 1 이상
- StageData `bossSpawnSec` <= `durationSec`

## 10. 데이터 검증 자동화 체크리스트
- 빌드 전 DataValidator 실행
- 누락 참조, 중복 ID, 범위 오류 검사
- 실패 시 CI 차단
