# NightDash CI Workflows (S4-01)

## 현재 상태: 시크릿 설정 대기 (수동 실행만 허용)

`UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` 시크릿이 아직 리포에 없어서,
자동 PR·push 트리거는 비활성화한 상태. 각 워크플로는 **Actions 탭에서 수동
(workflow_dispatch)** 으로만 실행 가능.

시크릿 설정 후 아래 트리거 주석을 해제해 PR 게이트·정기 실행을 복원한다.

## 구성

| Workflow | 현재 트리거 | 설정 후 트리거 | 소요 | 목적 |
|---|---|---|---|---|
| `editmode-tests.yml` | 수동 | PR, push to main | ~15분 | EditMode 테스트 전체 실행 |
| `data-validation.yml` | 수동 | Data/ 변경 PR | ~8분 | DataValidator batchmode |
| `smoke-build.yml` | 수동 + 주간 월요일 | 동일 | ~60분 | Windows64·macOS StandaloneBuild 스모크 |

## 선행 설정

각 워크플로는 [game-ci](https://game.ci) 액션을 사용한다.

### 1. Unity 라이선스 발급

Personal License(무료):
```bash
# WSL/Linux/Mac 어디서든
unity-request-activation-file \
  -batchmode -nographics -quit \
  -createManualActivationFile
# 생성된 Unity_v6000.x.alf 파일을 https://license.unity3d.com/manual 에 업로드
# 반환받은 Unity_v6000.x.ulf 파일의 전체 내용을 UNITY_LICENSE 시크릿으로 등록
```

또는 Unity Pro 시리얼 번호가 있으면 `UNITY_SERIAL` 시크릿을 대신 사용.

### 2. GitHub 시크릿 설정

리포 Settings → Secrets and variables → Actions → New repository secret:
- `UNITY_LICENSE` — ULF 파일 전체 내용 (Personal)
- `UNITY_EMAIL` — Unity 계정 이메일
- `UNITY_PASSWORD` — Unity 계정 비밀번호

또는:
- `UNITY_SERIAL` — Unity Pro 시리얼 (Personal 미사용 시)

### 3. 자동 트리거 활성화

`editmode-tests.yml`, `data-validation.yml` 파일 상단의 `on:` 블록에서
주석 처리된 `pull_request`/`push` 트리거를 주석 해제.

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
