# Story - Endings & Cue Sheet

## 1. 엔딩 분기 조건
- Normal Ending: Stage06 클리어
- Abyss Ending: Stage06 클리어 + RiskScore 10+ + 심연 진화 획득

## 2. 일반 엔딩 스크립트
- KR: "문은 열렸고, 나는 돌아갈 수 있다. 하지만 이 균열은 누가 닫지?"
- 연출: 붉은 하늘이 옅어지고 균열이 천천히 봉합
- 결과 카드: `Ending_Normal_Unbound`

## 3. 심연 엔딩 스크립트
- KR: "나는 돌아가지 않는다. 순환 자체를 끊는다."
- 연출: 마왕성 코어 붕괴, 주인공 실루엣 잔류
- 결과 카드: `Ending_Abyss_Breaker`

## 4. 크레딧 전 큐시트
1. 0~8s: 정적 + 심박 SFX
2. 8~20s: 엔딩 내레이션
3. 20~45s: 지역 회상 이미지 6장
4. 45~60s: 타이틀/크레딧 진입

## 5. 로컬라이즈 키
- `ending.normal.line01`
- `ending.abyss.line01`
- `ending.result.normal`
- `ending.result.abyss`
