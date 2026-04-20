# NightDash Audio

## 목적
플레이스홀더 단계의 오디오 파이프라인. 실제 오디오 클립은 S4 또는 사운드 의뢰 시점에 채워진다.
아래 구조·네이밍·이벤트 매핑은 `Docs/GDD/ops/01_art_audio_pipeline.md` 를 따른다.

## 폴더 구조
```
Assets/NightDash/Audio/
├── bgm/        # 스테이지별 배경음 (.wav 원본, .ogg 런타임, 2~4분, loop 지원)
├── sfx/        # 효과음 (.wav, -3dB normalize, 0.1~2.0초)
└── voice/      # 보이스 (.wav, -6dB headroom, 노이즈 제거 필수)
```

## 네이밍 규칙
- BGM: `bgm_{stage}_{mood}` 예: `bgm_stage_01_boss`
- SFX: `sfx_{event}_{name}` 예: `sfx_levelup_chime`
- Voice: `vo_{lang}_{actor}_{lineId}` 예: `vo_ko_narrator_intro`

## 필수 이벤트 매핑 (GDD 8종)

| 이벤트 ID | 용도 | 기본 경로 |
|---|---|---|
| `run.start` | 런 시작 | `sfx/sfx_run_start.wav` |
| `level.up` | 레벨업 | `sfx/sfx_levelup.wav` |
| `chest.open` | 보스 상자 오픈 | `sfx/sfx_chest_open.wav` |
| `evolution.trigger` | 진화 발동 | `sfx/sfx_evolution.wav` |
| `boss.spawn` | 보스 등장 | `sfx/sfx_boss_spawn.wav` |
| `boss.kill` | 보스 처치 | `sfx/sfx_boss_kill.wav` |
| `run.victory` | 런 승리 | `sfx/sfx_run_victory.wav` |
| `run.defeat` | 런 패배 | `sfx/sfx_run_defeat.wav` |

## 런타임 연결

1. `AudioLibrary` ScriptableObject — 8 이벤트별 `AudioClip` 슬롯 보유.
   생성 경로: `Assets/NightDash/Data/Audio/audio_library.asset` (에디터 메뉴 `NightDash/Data/Audio Library`).

2. `NightDashAudioBridge` MonoBehaviour — 씬에 1개 배치. 내부 `AudioSource`를 통해
   `PlayOneShot(AudioEventId)` 퍼블릭 API 제공. 현재는 `OnEnemyKilled(boss=true)` 시그널을
   자동 구독해 `boss.kill` 슬롯을 재생한다. 나머지 7 이벤트는 게임 로직에서 직접 호출.

## 품질 체크 (GDD §6)
- 클리핑 없음
- 루프 클릭 노이즈 없음
- 포맷/길이 기준 준수 (§4)
