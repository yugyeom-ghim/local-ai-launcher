# Local AI Launcher

Windows에서 GGUF 모델을 실행하고 OpenAI 호환 API를 제공하는 llama.cpp 런처입니다.

[다운로드](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest) · [사용법](README_KO.md) · [API 연결](README_API_UPDATE_KO.md)

## 설치

Setup EXE와 BIN 파일을 같은 폴더에 받은 뒤 Setup을 실행합니다. 설치는 오프라인으로 진행됩니다.
인터넷 PC에서는 [다운로드 도구](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest/download/Internet-PC-Download-Installer.exe)로 전체 파일을 이어받을 수 있습니다.

- 포함: Qwen3.5-4B-Q4_K_M, llama.cpp CPU·CUDA 12.4/13.4, VC++ 재배포 파일
- 환경: Windows 10/11 x64, .NET Framework 4.8+, 설치 공간 6GB 이상
- CUDA 사용 시 호환 NVIDIA 드라이버 필요

## 사용

1. 모델과 실행기, 사고 모드를 선택하고 서버를 시작합니다.
2. **API 연결**에서 주소·모델 ID·키를 복사하거나 OpenCode 설정을 내보냅니다.
3. 모델 추가: `offline-assets/ai/models`에 GGUF 파일을 넣으면 목록에 표시됩니다.

사고 모드 변경은 서버 재시작 후 적용됩니다. 기존 OpenCode 설정에는 `provider.si-local`을 병합해 MCP 설정을 유지합니다.

## 사양별 추가 모델

CPU용 2B, GPU 12–16GB용 9B, GPU 24GB 이상용 Qwen3.8 27B를 제공합니다.
[모델 선택·추가 방법](MODEL_PACKS.md) · [모델 다운로드 도구](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest/download/Internet-PC-Download-Models.exe)

## 개발

`Build.cmd` 또는 `LocalAI.Launcher.sln`으로 빌드합니다. 대상은 .NET Framework 4.8 / x64입니다.

[검증 결과](VALIDATION.md) · [API 규격](API_CONTRACT.md) · [라이선스](licenses/THIRD_PARTY_NOTICES.md)
