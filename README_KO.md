# 사용법

## 서버 실행

1. Local AI Launcher에서 모델과 실행기를 선택합니다.
2. 사고 모드를 선택하고 **시작**을 누릅니다.
3. **준비 완료** 후 대화창 또는 **API 연결**을 사용합니다.

자동 실행기는 CUDA 12.4 → CUDA 13.4 → 기존 CUDA 13.1 → CPU 순으로 검사합니다. 직접 선택한 실행기는 다른 종류로 전환되지 않습니다.

## 모델 추가

`offline-assets/ai/models`에 GGUF를 넣으면 자동으로 목록을 갱신합니다. 분할 모델은 모든 조각을 같은 폴더에 둡니다.
복사 중인 파일은 `.partial`로 두고 완료 후 `.gguf`로 바꿀 수 있습니다. 모델을 선택한 뒤 시작하면 해당 모델로 전환됩니다.

## API와 사고 모드

**API 연결**에서 API 키 인증을 설정하고 OpenCode 설정을 내보냅니다. [연결 방법](README_API_UPDATE_KO.md)

사고 ON/OFF는 서버 기본값이며 변경 후 재시작해야 합니다. 모델 템플릿과 클라이언트 요청 옵션에 따라 적용됩니다.

## 설정과 로그

PC 사양 확인: **설정 → PC 정보 → PC 정보 확인**.

- 설정: `state/settings.json`
- 키: `state/local-api-key.txt`
- 로그 위치: **로그 → 로그 폴더 열기**
- 설치 폴더에 쓰기 권한이 없으면 `%LOCALAPPDATA%/SI.LocalAI/<설치경로 해시>` 사용

기존 설정과 상대경로, OpenCode 제공자 ID `si-local`은 유지합니다.

## 포함 자산

| 항목 | 버전 / 경로 |
|---|---|
| llama.cpp | b11429 (`d81235049384534c167caea52b85a694f6103d14`) |
| CPU | `offline-assets/ai/bin/win-cpu-x64` |
| CUDA | `offline-assets/ai/bin/win-cuda-12.4-x64`, `win-cuda-13.4-x64` |
| 모델 | `offline-assets/ai/models/Qwen3.5-4B-Q4_K_M.gguf` |
| VC++ 런타임 | `prerequisites/vc_redist.x64.exe` |

출처·크기·SHA256: `ASSET_MANIFEST.json`, `scripts/ASSET_LOCK.json`. 무결성 검사: `Verify-Package.cmd`.

사양별 추가 팩: [모델 선택·설치](MODEL_PACKS.md). 앱의 **모델 추가 → 모델 팩 설치**에서 반입한 팩을 선택합니다.
