# Systems - ScriptableObject Template Workflow

## 1. 목적
기획 문서의 데이터 계약을 Unity 자산(.asset)으로 빠르게 반영하기 위한 작업 규칙.

## 2. 코드 위치
- Data 클래스: `Assets/NightDash/Scripts/Data/`
- Editor 생성기: `Assets/NightDash/Scripts/Editor/DataTemplateGenerator.cs`
- Editor 검증기: `Assets/NightDash/Scripts/Editor/DataValidator.cs`

## 3. 생성 규칙
- 파일명: `{type}_{id}.asset` 권장
  - 예: `class_warrior.asset`, `stage_01.asset`
- 저장 위치:
  - Class: `Assets/NightDash/Data/Classes`
  - Weapon: `Assets/NightDash/Data/Weapons`
  - Passive: `Assets/NightDash/Data/Passives`
  - Evolution: `Assets/NightDash/Data/Evolutions`
  - Stage: `Assets/NightDash/Data/Stages`
  - Difficulty: `Assets/NightDash/Data/Difficulty`
  - Meta: `Assets/NightDash/Data/Meta`

## 4. Unity 메뉴
- 템플릿 생성: `NightDash/Data/Create All Template Assets`
- 데이터 검증: `NightDash/Validation/Run Data Validation`

## 5. 작업 순서
1. 템플릿 생성 메뉴 실행
2. 생성된 `tpl_*.asset`를 복제해 실제 id로 이름 변경
3. 문서(`Docs/GDD/specs/...`) 기준으로 필드 입력
4. 데이터 검증 실행
5. 오류 0 확인 후 커밋

## 6. 필수 검증 항목
- ID 중복 없음
- 참조 ID 존재함
- Stage 시간 범위 유효
- Evolution 요구 조건 유효
- Meta 노드 선행 조건 유효

## 7. 주의사항
- `id` 변경 시 참조 문서와 자산 동시 변경
- 런타임에서 문자열 참조를 사용하므로 오탈자 관리 필수
