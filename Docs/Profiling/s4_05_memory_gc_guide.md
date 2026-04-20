# S4-05 장기 세션 메모리·GC 프로파일 가이드

**작성일**: 2026-04-20
**관련 태스크**: S4-05 (P1·M·Medium)

## 1. 목적

Stage 1 15분 런 × 5회 연속(약 75분) 세션에서 **GC alloc/s 증가**와 **메모리
누수**를 탐지하기 위한 표준 절차. 배치모드 CI는 Profiler 런타임을 지원하지
않으므로 본 문서는 **Unity Editor 수동 실행** 전제로 작성됐다.

## 2. 정적 감사 결과 (완료 분)

### 2.1 ECS NativeArray/NativeList 처리

| 파일 | 지점 | 할당자 | Disposal | 판정 |
|---|---|---|---|---|
| `CombatSystem.cs:67-69` | deadEnemies/Flags/Positions | Temp | `using` | OK |
| `SpawnPrefabResolveSystem.cs:33,46,62,75` | ToEntityArray | Temp | `using` | OK |
| `RuntimeBalanceUtility.cs:156` | playerEntities | Temp | `using` | OK |

전부 `using` 구문으로 프레임 경계 내 안전 해제. 누수 위험 없음.

### 2.2 Managed GC 감사 — Runtime Bridge 수정 완료

**S4-05 이번 패스에서 수정**:

| 파일 | 이전 | 수정 후 | 영향 |
|---|---|---|---|
| `NightDashVFXBridge.cs` | `new HashSet<Entity>()` / `new List<Entity>()` 매 `LateUpdate` | 필드 캐시 + `Clear()` | 60 FPS 기준 초당 120개 객체 할당 제거 |
| `NightDashDamageNumberUI.cs` | 동일 패턴 | 필드 캐시 + `Clear()` | 동일 |

**수정 전**:
```csharp
private void LateUpdate() {
    var alive = new HashSet<Entity>();   // 프레임마다 힙 할당
    ...
    var dead = new List<Entity>();       // 프레임마다 힙 할당
    ...
}
```

**수정 후**:
```csharp
private readonly HashSet<Entity> _aliveBuffer = new();   // 1회 할당
private readonly List<Entity> _deadBuffer = new();

private void LateUpdate() {
    _aliveBuffer.Clear();
    ...
    _deadBuffer.Clear();
    ...
}
```

### 2.3 Editor-guarded 영역

`NightDashDebugVisualBridge.cs` (S4-07에서 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
가드 적용): per-frame HashSet/List 생성 패턴 존재하나 릴리스 빌드 미포함.
Editor 실행 중 Profiler 노이즈 감안 — 디버그 켠 상태 GC 측정은 신뢰 낮음.

## 3. 실측 프로파일 절차 (Unity Editor)

### 3.1 준비

1. Unity 에디터에서 프로젝트 열기
2. `Window → Analysis → Profiler`
3. Profiler 창 상단 `Deep Profile` 비활성 (초기 측정은 빠른 경로로)
4. Record(⦿) ON
5. Profiler Module: Memory, GC Alloc 활성화

### 3.2 베이스라인 측정 (5분 단일 런)

1. SampleScene Play
2. `stage_01` + `class_warrior` 선택 → Start Run
3. 5분 런 (보스 직전까지)
4. Profiler 에서 아래 메트릭 기록:
   - `GC Allocated In Frame` 평균 (KB)
   - `Total Reserved Memory` 추세 (우상향 → 누수 의심)
   - `Managed Heap` 추세

### 3.3 장기 세션 측정 (5 연속 런)

1. 런 종료 후 lobby로 복귀 → 다시 시작 반복 5회
2. 각 런 시작 시 `Total Reserved Memory` 기록
3. 런 5회 후 lobby 유지 5분 → **유휴 상태 메모리 감소/안정화 확인**

### 3.4 측정 기준 (권고)

| 메트릭 | 허용치 | 경고 |
|---|---|---|
| `GC Allocated In Frame` 평균 | < 2 KB | > 5 KB 지속 |
| 5 연속 런 후 `Managed Heap` 증가 | < 10 MB | > 30 MB 누수 의심 |
| 유휴 5분 후 GC | 안정화 (SKB 단위 진동) | 단조 증가 |

### 3.5 결과 저장

- Profiler → Save → `Docs/Profiling/runs/<date>_<scenario>.data`
- 회귀 발견 시 원인 시스템과 PR 링크 남길 것

## 4. 주요 의심 지점 (실측 시 우선 확인)

1. **`NightDashVFXBridge._tracked` 딕셔너리**: Entity 수명 끝난 후 Remove 호출됨. OnDestroy Clear 있지만 씬 전환 간 race 가능성. Profiler로 5 런 후 Dictionary 크기 확인.
2. **`DataRegistry._classById / _weaponById`**: 초기화 시 1회 build, 클리어 경로 부재. DDOL 싱글톤이라 의도적이지만 씬 reload 시 중복 등록 가드 확인.
3. **`AudioLibrary` / `LocalizationTable`**: SO 런타임 caching 없음 — 각 Get 호출 시 O(n) 순회. `LocalizationTable`은 LocalizationService Dictionary 캐싱 완료(S4-03). `AudioLibrary.Resolve`는 switch 분기 상수 시간 OK.
4. **IMGUI `OnGUI` 호출**: `RunSelectionLobbyUI`, `NightDashTutorialBridge` 등 IMGUI 사용. Unity IMGUI는 매 프레임 문자열 할당 발생 가능 — 장기적으로 uGUI/UI Toolkit 이전 권고 (S4 이후 별도 태스크).

## 5. 자동화 계획

본 태스크 범위에는 포함하지 않으나, 향후 CI 확장 후보:

- `smoke-build.yml` 에 런타임 메모리 텔레메트리 추가 (game-ci의 build 단계에서
  Player.log 분석 후 GC 스파이크 검출)
- Headless run 모드 도입 시 (Nightly) 5 연속 런 자동화

## 6. 참고

- Unity Profiler 매뉴얼: https://docs.unity3d.com/Manual/Profiler.html
- ECS allocator 가이드: https://docs.unity3d.com/Packages/com.unity.collections@latest
- S4-01 CI 구성: `.github/workflows/README.md`
- S4-07 릴리스 가드: `Docs/Release/steam-build-guide.md`
