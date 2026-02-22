# Specs - System Interfaces (개발 계약)

## 1. IRunStateMachine
```csharp
public interface IRunStateMachine {
    RunState Current { get; }
    void Enter(RunState next);
    event Action<RunState, RunState> OnStateChanged;
}
```

## 2. IStatResolver
```csharp
public interface IStatResolver {
    float Get(StatType stat);
    void AddModifier(StatModifier modifier);
    void RemoveModifier(string sourceId);
}
```

## 3. IWeaponSystem
```csharp
public interface IWeaponSystem {
    void Tick(float dt);
    void AddWeapon(string weaponId, int level = 1);
    bool TryLevelUp(string weaponId);
}
```

## 4. IExpLevelSystem
```csharp
public interface IExpLevelSystem {
    int Level { get; }
    int CurrentXp { get; }
    void AddXp(int amount);
    event Action<int> OnLevelUp;
}
```

## 5. IEvolutionSystem
```csharp
public interface IEvolutionSystem {
    bool TryEvolve(EvolutionTrigger trigger);
    bool HasAbyssEvolutionThisRun { get; }
}
```

## 6. IDifficultySystem
```csharp
public interface IDifficultySystem {
    int RiskScore { get; }
    float RewardMultiplier { get; }
    void Apply(RunConfig config);
}
```

## 7. ISaveSystem
```csharp
public interface ISaveSystem {
    bool Save(ProfileSaveData data);
    bool TryLoad(string profileId, out ProfileSaveData data);
}
```

## 8. 이벤트 이름 표준
- `run.started`
- `run.paused`
- `run.ended`
- `combat.enemy_killed`
- `combat.player_hit`
- `progress.level_up`
- `progress.evolution`
- `loot.chest_opened`

## 9. 로그 카테고리 표준
- `[RUN]`
- `[COMBAT]`
- `[PROGRESSION]`
- `[DIFFICULTY]`
- `[SAVE]`
- `[DATA]`

## 10. 인터페이스 변경 규칙
- 시그니처 변경 시 `appendices/02_change_log.md` 기록 필수
- 하위 호환이 깨지는 경우 major 버전 상승
