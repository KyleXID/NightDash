# Ops - Localization Policy

## 1. 지원 언어
- MVP: ko-KR, en-US
- 출시 후 확장: ja-JP, zh-CN

## 2. 키 네이밍 규칙
`{domain}.{subdomain}.{id}`
예: `stage01.start.line01`, `ui.result.retry`

## 3. 문자열 관리
- 원본 소스: `Docs/GDD/localization/strings_master.csv` (추후 생성)
- 필드: key, ko-KR, en-US, note, maxLen

## 4. 문체 가이드
- UI: 짧고 명령형
- 스토리: 다크 판타지 톤
- 튜토리얼: 명확하고 간결

## 5. 길이 제한
- 버튼 텍스트: 12자 이내(ko 기준)
- HUD 라벨: 10자 이내
- 튜토리얼 문구: 40자 이내

## 6. 검수 프로세스
1. 기획 작성
2. 번역 1차
3. 인게임 길이 검수
4. QA 문맥 검수
5. 락
