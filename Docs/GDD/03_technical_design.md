# Layer 2 - Technical Design (개발 기준서)

이 문서는 `02_core_gdd.md`의 규칙을 실제 구현 가능한 형태로 풀어쓴 기술 기준서다.

## 1. 런타임 상태 머신

## 상태
- Loading
- Playing
- Paused
- Victory
- Defeat
- Result

## 전이 규칙
- Loading -> Playing: StageData 로드 성공 + 플레이어 스폰 완료
- Playing -> Paused: 유저 입력(ESC) 또는 포커스 이탈(옵션)
- Paused -> Playing: 재개 입력
- Playing -> Victory: 보스 처치 + 클리어 조건 충족
- Playing -> Defeat: 플레이어 HP <= 0
- Victory/Defeat -> Result: 리워드 계산 완료
- Result -> Loading: 다음 런 시작

## 필수 이벤트
- OnRunStarted
- OnRunPaused
- OnRunResumed
- OnRunEnded
- OnResultConfirmed

## 2. 전투 프레임 파이프라인
1. 입력 수집
2. 이동 계산
3. 적 스폰
4. 무기 발사 트리거 계산
5. 투사체 이동 및 히트 판정
6. 데미지 적용
7. 사망 처리/드랍 처리
8. 경험치 반영/레벨업 체크
9. UI 갱신 이벤트 발행

## 업데이트 순서 고정 규칙
- 한 프레임 내에서 시스템 순서가 바뀌지 않도록 StageRunner에서 단일 오케스트레이션
- 시스템 간 직접 참조 대신 이벤트/쿼리 인터페이스 사용

## 3. 스탯 계산 규칙

## 스탯 카테고리
- Base: 클래스/레벨 기반 기본값
- Flat: 고정값 가감
- PercentAdd: 합연산 퍼센트
- PercentMul: 곱연산 퍼센트

## 최종 계산식
`Final = (Base + Sum(Flat)) * (1 + Sum(PercentAdd)) * Product(1 + PercentMul)`

## 우선순위
1. 클래스 기본값
2. 무기/패시브
3. 난이도 체크리스트
4. 메타 트리
5. 일시 버프/디버프

## 4. 데미지 공식
`Damage = max(1, (AttackerPower * SkillCoeff * CritMultiplier * TypeMultiplier) - DefenderArmorReduction)`

## 보조 규칙
- 치명타 기본 배율: 1.5
- 치명타 확률 상한: 75%
- 쿨타임 하한: 기본 쿨의 20%
- 이동속도 하한: 기본 이속의 40%

## 5. 경험치/레벨업 규칙

## 경험치 요구량 공식
`XPRequired(level) = floor(20 + 8 * level + 1.6 * level^2)`

## 레벨업 보상 선택지
- 기본 3개 노출
- 중복 방지 규칙: 현재 만렙 항목 제외
- 희귀 선택지 등장률: 기본 10%
- 리롤 1회 기본 제공(점성술사 제외 규칙은 클래스 문서 우선)

## 6. 무기/패시브 슬롯 규칙
- 무기 슬롯 최대: 6
- 패시브 슬롯 최대: 6
- 이미 보유한 항목 선택 시 레벨업 처리
- 만렙 항목은 후보에서 제거

## 7. 진화 트리거 상세

## 기본 트리거
- 보스 상자 오픈 시 진화 체크
- 엘리트 상자 오픈 시 낮은 확률 진화 체크

## 체크 순서
1. 진화 가능한 후보 리스트 생성
2. stage/난이도 조건 필터링
3. 심연 진화 제한(판당 1개) 적용
4. 후보 1개 확정(우선순위 규칙 또는 랜덤)

## 우선순위 규칙
- 심연 진화 가능 후보가 있으면 심연 우선
- 동일 우선순위면 최근 획득 무기 우선

## 8. 난이도 보상 계산

## 위험도 점수 합산
`RiskScore = Sum(SelectedModifier.RiskPoint)`

## 보상 배율
`RewardMultiplier = 1.0 + 0.1 * RiskScore`

## 하드 캡
- MVP 기준 최대 배율 2.5
- 추후 라이브에서 밸런스 데이터로 조정

## 9. 세이브 데이터 버전 정책
- SaveVersion 필수
- 마이그레이션 함수 체인 방식
- 하위 호환 불가 버전은 경고 후 백업 생성

## 권장 Save 루트 필드
- profileId
- saveVersion
- unlockedStages
- unlockedBonusMaps
- classProgress
- currency
- settings

## 10. 성능 예산 (MVP)
- 동시 적 수 목표: 180 (피크 250)
- 동시 투사체 목표: 220 (피크 300)
- 메인 스레드 프레임 예산: 16.6ms(60FPS 기준)
- GC Alloc: 전투 중 1초 평균 2KB 이하 목표

## 11. 에러/예외 처리 정책
- 핵심 룹 시스템 예외는 런 중단 대신 해당 액션만 실패 처리
- 세이브 실패 시 재시도 1회 + 로컬 백업
- 데이터 로드 실패 시 안전 기본값 적용 후 에러 로그 수집

## 12. 구현 우선순위
- Tier 1: 전투 루프 안정화
- Tier 2: 성장/진화/난이도
- Tier 3: 메타/보너스 맵/고급 연출
