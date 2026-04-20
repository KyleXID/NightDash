# NightDash CI Workflows (S4-01)

## 구성

| Workflow | 트리거 | 소요 | 목적 |
|---|---|---|---|
| `editmode-tests.yml` | PR, push to main | ~15분 | EditMode 테스트 전체 실행 |
| `data-validation.yml` | Data/ 변경 PR | ~8분 | DataValidator batchmode (데이터 무결성 게이트) |
| `smoke-build.yml` | 수동 + 주간 월요일 | ~60분 | Windows64·macOS StandaloneBuild 스모크 |

## 선행 설정

각 워크플로는 [game-ci](https://game.ci) 액션을 사용한다. 최초 1회 시크릿 설정 필요:

1. Unity Personal 라이선스 발급: https://license.unity3d.com/manual (ULF 파일)
2. GitHub 리포 Settings → Secrets and variables → Actions 에 추가:
   - `UNITY_LICENSE` — ULF 파일 전체 내용
   - `UNITY_EMAIL` — Unity 계정 이메일
   - `UNITY_PASSWORD` — Unity 계정 비밀번호

시크릿 미설정 시 라이선스 단계에서 실패하므로 PR 머지 정책은 시크릿 설정 후에 활성화.

## 로컬 실행

CI와 동일한 스크립트를 로컬에서 실행 가능:

```bash
UNITY_PATH=/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  ./scripts/run-editmode-tests.sh

UNITY_PATH=/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  ./scripts/run-data-validation.sh
```

## 캐시 전략

- `Library/` 디렉토리를 `Assets`·`Packages`·`ProjectSettings` 해시 기반으로 캐싱 → 재빌드 10-20분 단축.
- 캐시 무효화는 Unity 패키지 추가/변경 시 자동.

## 아티팩트

- EditMode 결과: `Logs/editmode-artifacts` (14일 보관)
- Standalone 빌드: `build/{platform}` (14일 보관)

## 확장 계획

- S4-05 메모리 프로파일 잡 추가 (nightly)
- PlayMode 테스트는 batchmode scene-load 한계로 CI 제외, Unity Editor 수동 실행 권장.
