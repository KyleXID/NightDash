# MonoBehaviour ↔ ECS 브리지 레이어

> Ticket: S2-07 — 브리지별 책임·경계·비허용 패턴 문서화.
> 위치: `Assets/NightDash/Scripts/Runtime/*Bridge.cs`

## 1. 브리지 레이어의 역할

NightDash는 DOTS/ECS로 게임 로직을 구현하지만, **렌더링·입력·오디오·씬 계층구조는 여전히 MonoBehaviour 기반**이다. 브리지 레이어는 이 두 세계 사이의 **단방향 또는 양방향 어댑터**로 기능한다.

- ECS → Mono: 시각적 표현(스프라이트·VFX·UI) 및 외부 시스템 호출
- Mono → ECS: 씬 배치된 오브젝트(장애물·용암 등)를 ECS가 소비할 수 있는 데이터로 변환

**절대 금지 사항**:
1. 브리지에서 **게임플레이 로직** 수행 금지 (밸런스·전투·레벨업 등은 ECS 시스템 소관)
2. 브리지 간 **직접 참조** 금지 — 공유 필요 시 ECS 싱글턴/이벤트 경유
3. `EntityManager.SetComponentData`를 **ReadOnly 쿼리**로 호출 금지 (S1-08 리그레션)
4. **파괴된 엔티티** 접근 전 `em.Exists(entity)` 가드 누락 금지 (S1-09 리그레션)
5. `FindFirstObjectByType<T>()` **매 프레임 호출** 금지 — `AutoCreate`에서 1회만

## 2. 브리지 카탈로그 (2026-04-19 스냅샷)

### 2.1 NightDashDebugVisualBridge
**파일**: `Runtime/NightDashDebugVisualBridge.cs`
**방향**: ECS → Mono (표현)
**책임**:
- 플레이어·적·보스·투사체 엔티티를 SpriteRenderer GameObject에 싱크
- 아키타입별 스프라이트 매핑 (`EnemyArchetypeMap`) + 무기별 VFX 매핑 (`WeaponVfxMap`)
- 투사체 회전 (속도 벡터 기준)
**입력**: `LocalTransform`, `EnemyArchetypeData`, `ProjectileData`, `PhysicsVelocity2D`
**출력**: 씬의 `Sprite`/`Transform` GameObject
**허용되지 않는 것**:
- 데미지 계산·피격 처리 (그건 `CombatSystem` 몫)
- 엔티티 생성/파괴
- 씬 재로드 처리 (World 재초기화는 허용)

### 2.2 NightDashVFXBridge
**파일**: `Runtime/NightDashVFXBridge.cs`
**방향**: ECS → Mono (이펙트)
**책임**:
- 적 체력 변화·사망 감지 시 hit-flash / death VFX 스프라이트 스폰
- 사망 위치를 `NightDashXPDropBridge`에 **이벤트 방식으로 알림** (직접 참조 X, 싱글턴/정적 큐 경유)
**입력**: `CombatStats.CurrentHealth` 변화 스냅샷
**출력**: 씬의 임시 VFX GameObject (애니메이션 후 파괴)
**허용되지 않는 것**:
- XP 오브 생성 (`XPDropBridge` 몫)
- 이펙트로 인한 게임 로직 개입 (폭발 데미지 등 → ECS 시스템 별도)

### 2.3 NightDashXPDropBridge
**파일**: `Runtime/NightDashXPDropBridge.cs`
**방향**: Mono → ECS (픽업 이벤트)
**책임**:
- 적 사망 위치에 XP gem / HP orb 스프라이트 생성
- 자석 효과 (일정 시간 후 플레이어 방향 가속)
- 픽업 반경 진입 시 ECS 플레이어 상태에 XP/HP 추가
**입력**: `VFXBridge`의 사망 알림
**출력**: `GameLoopState.ExperiencePoints` 증가, `CombatStats.CurrentHealth` 복구
**허용되지 않는 것**:
- 엔티티 오너십 변경 — XP gem은 순수 Mono 오브젝트
- 레벨업 트리거 판단 (`ProgressionSystem` 몫 — XP 누적만 담당)

### 2.4 NightDashObstacleBridge
**파일**: `Runtime/NightDashObstacleBridge.cs`
**방향**: Mono → ECS (지형 데이터)
**책임**:
- 씬에 배치된 `Prop_*` 스프라이트를 장애물로 수집 (주기적 rescan)
- 플레이어·적 엔티티 `LocalTransform`을 장애물 외곽으로 밀어냄 (push)
- Y-sort 업데이트 (Prop sorting order)
**입력**: 씬 계층구조의 `Prop_*` GameObject
**출력**: `LocalTransform` 위치 조정 (ReadWrite 쿼리, S1-08 참조)
**허용되지 않는 것**:
- 장애물 파괴 (지형은 정적 가정)
- 엔티티 생성
- `Prop_*`이 아닌 이름 패턴의 오브젝트 접근

### 2.5 NightDashLavaDamageBridge
**파일**: `Runtime/NightDashLavaDamageBridge.cs`
**방향**: Mono → ECS (환경 데미지)
**책임**:
- 씬의 `deco_lava_crack` 데코 위치 수집
- 플레이어가 용암 반경에 있으면 초당 데미지 적용
**입력**: 씬의 `deco_lava_crack` GameObject 위치
**출력**: `CombatStats.CurrentHealth` 감소 (플레이어만)
**허용되지 않는 것**:
- 적에게 데미지 (환경 대칭성은 ECS 전담)
- 용암 시각 효과 제어 (VFXBridge 몫)

## 3. 공통 패턴

### 3.1 자동 생성 (AutoCreate)
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void AutoCreate()
{
    // 중복 인스턴스 가드 (S1-08 파생 권장)
    var existing = FindFirstObjectByType<MyBridge>(FindObjectsInactive.Include);
    if (existing != null) return;

    var go = new GameObject("[NightDash] MyBridge");
    go.AddComponent<MyBridge>();
    DontDestroyOnLoad(go);
}
```

### 3.2 World·EntityQuery 초기화
```csharp
private bool EnsureInitialized()
{
    if (_initialized) return true;
    _world = World.DefaultGameObjectInjectionWorld;
    if (_world == null || !_world.IsCreated) return false;
    _query = _world.EntityManager.CreateEntityQuery(
        ComponentType.ReadOnly<PlayerTag>(),
        ComponentType.ReadWrite<LocalTransform>());  // 쓰기 필요 시 반드시 ReadWrite
    _initialized = true;
    return true;
}
```

### 3.3 Stale entity 방어 (S1-09)
```csharp
if (em.Exists(entity) && em.HasComponent<T>(entity))
{
    var data = em.GetComponentData<T>(entity);
    // ... safe to use
}
```

## 4. 새 브리지 도입 체크리스트

- [ ] **단방향 원칙**: ECS→Mono 또는 Mono→ECS 중 하나만 담당하는가?
- [ ] **로직 분리**: 게임 로직(데미지·XP·레벨업)은 ECS 시스템에 있는가?
- [ ] **중복 가드**: `AutoCreate`에 `FindFirstObjectByType` 가드가 있는가?
- [ ] **초기화**: `EnsureInitialized` 또는 동등 패턴으로 World 재초기화 가능한가?
- [ ] **쿼리 권한**: 쓰기 시 `ReadWrite`, 읽기 전용 시 `ReadOnly`로 정확히 선언되었는가?
- [ ] **Stale 방어**: 엔티티 접근 전 `Exists` 가드가 있는가?
- [ ] **브리지 간 독립성**: 다른 브리지를 직접 참조하지 않고 이벤트/싱글턴 경유하는가?
- [ ] **테스트**: 브리지 동작을 EditMode/PlayMode 테스트로 커버 가능한가?

## 5. 향후 로드맵

- **AudioBridge** (S3-05): 효과음·BGM 이벤트 라우팅. 본 문서 §4 체크리스트 준수.
- **InputBridge 검토**: 현재 `NightDashPlayerInputRuntime`이 직접 ECS 싱글턴에 쓰는 형태. 브리지 레이어로 정식 승격 여부 Sprint 3 이후 재검토.
- **SpriteRenderer → Hybrid Renderer 마이그레이션**: 퍼포먼스 측정 후 장기 과제. DebugVisualBridge가 첫 대상.
