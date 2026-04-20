# NightDash — Steam 빌드 & 릴리스 가이드 (S4-08)

## 1. 빌드 소스

| 경로 | 설명 |
|---|---|
| `.github/workflows/smoke-build.yml` | CI 자동 빌드 (수동 트리거 + 매주 월요일) |
| `Application.buildGUID` | Unity 기본 빌드 식별자 |
| `ProjectSettings/ProjectVersion.txt` | Unity 버전 잠금 (6000.3.8f1) |

## 2. 로컬 빌드 절차

### 2.1 사전 체크

```bash
# 데이터 무결성
UNITY_PATH=/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  ./scripts/run-data-validation.sh

# EditMode 테스트
UNITY_PATH=/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  ./scripts/run-editmode-tests.sh
```

두 스크립트 모두 exit 0 확인 후 다음 단계로.

### 2.2 Unity 에디터에서 빌드

1. Unity Editor 열기 (Samples Scene 로드됨)
2. File → Build Settings
3. 타겟 플랫폼 선택 (PC/Mac/Linux Standalone)
4. Architecture: `x86_64`
5. Development Build: **체크 해제** (릴리스 빌드)
6. Server Build: 해제
7. IL2CPP + .NET Standard 2.1 권장
8. Build 경로: `build/{platform}/NightDash.exe` (또는 .app)

### 2.3 CI 빌드 아티팩트

`.github/workflows/smoke-build.yml` 실행 후 Artifacts에서 다운로드:
- `nightdash-StandaloneWindows64` — Steam Windows 빌드 소스
- `nightdash-StandaloneOSX` — Steam macOS 빌드 소스

## 3. Steam 업로드 체크리스트

### 3.1 사전 검증

- [ ] 데이터 검증 exit 0 (`run-data-validation.sh`)
- [ ] EditMode 테스트 96%+ 통과 (사전 PlayMode·밸런스 실패 제외)
- [ ] `NightDashDebugVisualBridge` 릴리스 빌드 미포함 (S4-07 #if 가드 확인)
- [ ] `NightDashRuntimeToggles.verboseRuntimeLogs` 기본값 false (S4-07)
- [ ] mono_crash·.blob 파일 루트 미포함 (pre-commit 훅이 차단 — S1-07)

### 3.2 빌드 메타데이터

- [ ] 빌드명: `NightDash` (공백 없음)
- [ ] 버전: `ProjectSettings/ProjectSettings.asset` 의 `bundleVersion` 확인
- [ ] 앱 ID: Steamworks에서 발급받은 AppID 기록
- [ ] 아이콘: 256×256 ICO(Win) / ICNS(macOS) 포함

### 3.3 Steam 업로드

1. SteamPipe GUI 또는 `steamcmd` 사용
2. 빌드 브랜치:
   - `default` — 스테이블 (검수 완료 후 push)
   - `beta` — 내부 QA
   - `dev` — 개발자 전용
3. 업로드 후 Steamworks 대시보드에서 "Set Build Live on Default"는 **QA 승인 후**

### 3.4 빌드 후 스모크

- [ ] Windows 빌드 실행 → 런 시작 → 15분 런 완주 1회
- [ ] macOS 빌드 실행 → 런 시작 → 5분 런 안정성 확인
- [ ] 세이브 데이터 로드 (`ConquestPoints` 영속성, S3-07 체크섬 보호 확인)

## 4. 릴리스 노트 템플릿

```markdown
## NightDash vX.Y.Z (YYYY-MM-DD)

### Added
- (신규 기능)

### Changed
- (기존 기능 변경)

### Fixed
- (버그 수정)

### Known Issues
- (미해결 이슈)

### Technical
- Unity 6000.3.8f1
- DOTS Entities vX.Y
```

## 5. 롤백 절차

1. Steamworks → Builds → 이전 버전 선택
2. "Set Build Live on Default" → 승인
3. 변경 사항은 `git revert` 로 코드 반영

## 6. 참고

- CI 설정: `.github/workflows/README.md`
- 데이터 정책: `Docs/GDD/ops/01_art_audio_pipeline.md`
- 보안 체크: `Assets/NightDash/Scripts/ECS/Systems/SaveDataHelper.cs` (S3-07)
- 프로젝트 규약: `CLAUDE.md`
