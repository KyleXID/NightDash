# Specs - Unlock & Progression Rules

## 1. 스테이지 해금
- Stage01: 기본 해금
- Stage02: Stage01 클리어
- Stage03: Stage02 클리어
- Stage04: Stage03 클리어
- Stage05: Stage04 클리어
- Stage06: Stage05 클리어

## 2. 보너스맵 해금
- 금지된 도서관: RiskScore 6+로 Stage01 클리어
- 폐허 투기장: Stage03 클리어 + 엘리트 50처치 누적
- 심연 제단: RiskScore 8+ 클리어 3회

## 3. 클래스 해금
- 시작: 전사, 마법사, 점성술사
- 성전사: Stage02 클리어
- 사제: Stage03 클리어
- 궁수: Stage04 클리어
- 건슬링어: Stage05 클리어

## 4. 메타 트리 해금
- 트리 1단계: 해당 클래스 첫 클리어
- 트리 2단계: 해당 클래스로 RiskScore 5+ 클리어
- 트리 3단계: 해당 클래스로 Stage04 이상 클리어
- 엔드 노드: Stage06 클리어 + 해당 클래스 누적 승리 5회

## 5. 엔딩 해금
- 일반 엔딩: Stage06 클리어
- 심연 엔딩: Stage06 클리어 + RiskScore 10+ + 심연 진화 달성

## 6. 세이브 플래그 키
- `unlocked_stages[]`
- `unlocked_bonus_maps[]`
- `unlocked_classes[]`
- `meta_tree_tiers[classId]`
- `ending_flags[]`

## 7. 예외 규칙
- 하위 스테이지 재클리어로도 누적 업적 카운트 반영
- 디버그 모드 해금은 저장 분리(profile_debug)
