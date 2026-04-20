# S4-04 통합 밸런싱 감사 (7 클래스 × 5 모디파이어)

**작성일**: 2026-04-20
**관련 태스크**: S4-04 (P0·L·High)
**선행**: S1-12 서베이 (bb122026), S3-02 신규 클래스 4종, S3-03 모디파이어 5종

## 1. 요약

실제 플레이테스팅은 별도(S4 이후) 세션에서 수행한다. 본 감사는 **수치 정합성
· 카테고리 커버리지 · 의도된 차별화**를 정적 분석으로 점검한다.

결론: **신규 콘텐츠 4 클래스 + 5 모디파이어는 S1-12 서베이 체계와 정합.
즉시 재조정 필요 수치는 없음.** 플레이테스트 후 재검토할 포인트 4건을
§4 에 기록.

## 2. 클래스 스탯 감사 (7종)

### 2.1 GDD 베이스 대비 편차

GDD 베이스 (`Docs/GDD/specs/08_combat_math_and_balance.md §1`): HP 100 / MS 5.0 / POW 10
±20% 허용: HP 80~120 / MS 4.0~6.0 / POW 8~12

| Class | HP | MS | POW | 편차(HP/MS/POW) | 판정 |
|---|---:|---:|---:|---|---|
| warrior | 125 | 3.5 | 11 | +25 / −30 / +10 | HP 경계 초과, MS 초과 |
| mage | 85 | 3.7 | 13 | −15 / −26 / +30 | MS 초과, POW 초과 |
| astrologer | 95 | 3.8 | 12 | −5 / −24 / +20 | MS 초과 |
| paladin (신) | 115 | 3.8 | 11 | +15 / −24 / +10 | MS 초과 |
| priest (신) | 90 | 4.2 | 9 | −10 / −16 / −10 | 전부 OK |
| archer (신) | 95 | 4.5 | 12 | −5 / −10 / +20 | 전부 OK |
| gunslinger (신) | 90 | 4.0 | 13 | −10 / −20 / +30 | POW 초과 |

### 2.2 편차 해석

- **MS 전반 하향** (−16~−30%): S1-12 커밋(4c89d940 `balance(stage1): 플레이어 속도 하향 + 적 속도 상향`)의 의도된 조정. 플레이어 5.0은 Stage 1 난이도 체감상 너무 빠름으로 확정. **유지**.
- **warrior HP +25%**: 탱커 컨셉 의도된 편차. S1-12 survey 기록. **유지**.
- **mage / gunslinger POW +30%**: Glass cannon / Burst DPS 컨셉 의도된 편차. **유지**.

### 2.3 역할 차별화

| 역할 | 담당 클래스 | HP | MS | POW | 구분 포인트 |
|---|---|---:|---:|---:|---|
| Tank | warrior | 125 | 3.5 | 11 | 최고 HP |
| 반탱 | paladin | 115 | 3.8 | 11 | HP/MS 균형, 피격 폭발 패시브 |
| Glass cannon | mage | 85 | 3.7 | 13 | 최저 HP + 고 POW |
| Mid DPS | astrologer | 95 | 3.8 | 12 | 크리 기반 (리롤 무료) |
| Burst DPS | gunslinger | 90 | 4.0 | 13 | mage 동급 POW + 재장전 메커니즘 |
| Mobile DPS | archer | 95 | 4.5 | 12 | 최고 MS + 이동 중 공격 증가 |
| Support | priest | 90 | 4.2 | 9 | 최저 POW + 주기 회복 |

**경미 이슈**:
- `warrior` vs `paladin`: POW 동일(11), HP 차이 10, MS 차이 0.3. 수치 차별화 미미 → 패시브/궁극기 차별화에 의존 (warrior 근접 스택, paladin 피격 폭발).
- `mage` vs `gunslinger`: POW 동일(13). 구분 포인트는 시작 무기 (Projectile/AoE vs Projectile 단발) + 패시브.

## 3. 모디파이어 감사 (5종)

### 3.1 리스크-리워드 정합성

규칙: `rewardBonusPct = riskPoint × 0.1`

| Modifier | Category | Risk | Reward | 검증 |
|---|---|---:|---:|---|
| mod_enemy_hp_up | Combat | 2 | 0.2 | OK |
| mod_enemy_speed_up | Combat | 2 | 0.2 | OK |
| mod_enemy_surge | Combat | 3 | 0.3 | OK |
| mod_no_heal | Survival | 2 | 0.2 | OK |
| mod_on_kill_explosion | Mechanic | 2 | 0.2 | OK |

**위험도 구간**: 총 risk 합 11 (= `2+2+3+2+2`) → GDD `04_difficulty_checklist.md`
§"위험도 구간" 기준 **지옥 구간** (10+). 5종 전부 활성화 시 극한 난이도
시나리오로 정상 설계.

### 3.2 카테고리 커버리지

| Category | 구현 수 | GDD 대표 항목 수 | 비고 |
|---|---:|---:|---|
| Combat | 3 | 5 | GDD #1·2·3 구현, #4 엘리트 2배·#5 보스 2체 미구현 |
| Survival | 1 | 5 | #6 회복 제거만 구현, #7~10 미구현 |
| Mechanic | 1 | 5 | #11 처치 폭발만 구현, #12~15 미구현 |

**현황**: GDD 15종 중 5종 실사화(33%). S3-03 MVP 범위로 의도된 축소.
플레이테스트에서 단조로움 확인 시 S4 이후 확장 태스크.

## 4. 플레이테스트 후 재검토 포인트

S4-04 차기(플레이테스트 동반) 세션에서 다음을 확인한다.

1. **warrior vs paladin 체감 차별화**: HP 10·MS 0.3 차이가 실제 생존 시간에 유의미한지. 차별화 부족 시 paladin HP 115→110, MS 3.8→4.0 조정 고려.
2. **mage vs gunslinger POW 중복**: 동일 POW 13이 "번개 빠름 vs 묵직 한 방" 플레이 스타일로 충분히 느껴지는지. 부족 시 mage POW 유지 + gunslinger baseCooldown 0.9 상향(현재)으로 단발형 강화.
3. **priest POW 9 생존성**: 서포트 역할이지만 solo 클리어 가능해야. 5분 DPS 요구(110~150) 미달 시 주기 회복 수치 상향 또는 passive effect 추가.
4. **모디파이어 전부 켠 지옥 구간 클리어 가능성**: risk 11로 클리어율 5% 이하면 보상 배율(총 100%+110%=210%)만으론 매력 부족 → 희귀 상자 확률 추가 보장 필요.

## 5. 회귀 게이트 상태

`Stage1ContentRegressionTests` (S3-08) fixture는 **현재 확정 수치와 일치**.
이번 패스에서 수치 변경이 없으므로 fixture 업데이트 불필요.

`RuntimeBalanceUtilityTests` (S1-04) 는 fixture 커브(powerCoeff 1.0→1.4)로
`_weaponData.levelCurves` 검증. 실사 weapon 에셋이 아직 levelCurves 비어있는
상태 — 에셋 커브 주입은 별도 데이터 태스크.

## 6. 참고

- `Docs/GDD/specs/08_combat_math_and_balance.md` — GDD 밸런스 베이스
- `Docs/GDD/specs/01_classes.md` — 클래스 사양
- `Docs/GDD/specs/04_difficulty_checklist.md` — 난이도 모디파이어 15종
- S1-12 survey: `bb122026 docs(s1-12): 밸런스 기준표 재정렬`
- S3-02 클래스 4종: `9f5a8791 feat(s3-02)`
- S3-03 모디파이어 5종: `b873eb85 feat(s3-03)`
