# Ops - Art/Audio Pipeline

## 1. 목적
아트/오디오 에셋 제작과 반영 규칙을 통일해 누락과 충돌을 방지한다.

## 2. 폴더 규칙
- Art: `Assets/_Game/Art/{stage|ui|vfx}/...`
- Audio: `Assets/_Game/Audio/{bgm|sfx|voice}/...`

## 3. 파일 네이밍
- 스프라이트: `spr_{category}_{name}_{variant}`
- VFX: `vfx_{context}_{name}`
- BGM: `bgm_{stage}_{mood}`
- SFX: `sfx_{event}_{name}`
- Voice: `vo_{lang}_{actor}_{lineId}`

## 4. 포맷/길이 기준
- BGM: .wav 원본 / 런타임 .ogg, loop 지원, 2~4분
- SFX: .wav, -3dB normalize, 0.1~2.0초
- Voice: .wav, -6dB headroom, 노이즈 제거 필수

## 5. 이벤트-오디오 매핑 필수 목록
- run.start
- level.up
- chest.open
- evolution.trigger
- boss.spawn
- boss.kill
- run.victory
- run.defeat

## 6. 품질 체크
- 클리핑 없음
- 루프 클릭 노이즈 없음
- VFX 과다 점멸 없음
- 색각 이상 모드 대비 UI 명도 확보
