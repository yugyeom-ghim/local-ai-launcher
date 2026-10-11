# SI Local AI Launcher

Windows에서 GGUF 모델을 선택하고 로컬 llama.cpp 서버를 실행하는 도구입니다.
**SI코파일럿은 Visual Studio에 설치하는 별도 VSIX**이며, 이 저장소는 Launcher 소스와 배포 파일을 관리합니다.

## 다운로드

**[v1.3 업데이트 ZIP 다운로드 — 163 KB](https://github.com/yugyeom-ghim/si_local_model/raw/refs/heads/main/downloads/SI_Local_AI_Update_v1.3.zip)**

[소스 전체 ZIP](https://github.com/yugyeom-ghim/si_local_model/archive/refs/heads/main.zip) · [다운로드 폴더와 SHA256](downloads/) · [사용 안내](README_API_UPDATE_KO.md)

- 업데이트 ZIP에는 빌드된 Windows x64 Release EXE가 들어 있습니다. 직원 PC에서 빌드할 필요가 없습니다.
- 서버와 Launcher를 종료하고, ZIP 내용물 전체를 **기존 v1.2 / v1.2.1 설치 폴더**에 덮어쓰세요.
- 기존 모델·CPU/CUDA 실행기·개인 설정을 그대로 사용합니다. 빈 폴더에 이 ZIP만 풀면 모델 서버를 실행할 수 없습니다.
- 대용량 GGUF / CUDA 바이너리는 Git 소스 이력에 넣지 않았습니다. 기존 배포 자산의 고정 버전·원본 URL·크기·SHA256·라이선스는 [ASSET_LOCK.json](scripts/ASSET_LOCK.json)과 [ASSET_MANIFEST.json](ASSET_MANIFEST.json)에 기록되어 있습니다.
- 복구 조각과 합치기 작업은 이번 업데이트 다운로드에 필요하지 않습니다.

## OpenCode의 기존 MCP도 쓸 수 있나요?

**기존 OpenCode에서 모델 제공자만 SI Local AI로 추가하면 기존 MCP 설정·인증·권한을 유지하며 사용할 수 있는 구조입니다.**

OpenCode가 MCP에 연결하고 도구를 실행합니다. 로컬 모델은 OpenCode가 보내는 문맥과 도구 목록을 받아 다음 응답이나 도구 호출을 생성합니다. Launcher는 모델 API를 제공합니다.

1. 앱의 **모델 라이브러리**에서 모델과 사고 모드를 선택합니다.
2. **API 연결 → 로컬 API 키 사용 → 저장하고 서버에 적용**을 누릅니다.
3. 준비 완료 후 OpenCode 설정을 내보냅니다.
4. **기존 `opencode.json` / `opencode.jsonc`를 통째로 바꾸지 마세요.** 내보낸 설정의 `provider.si-local` 항목을 추가하고 모델을 선택합니다. 기존 `mcp`, `permission`, `agent`, 다른 제공자 설정을 유지합니다.
5. 키를 켰다면 OpenCode `/connect → Other → si-local`에 복사한 키를 입력하고 `/models`에서 SI Local AI 모델을 선택합니다.

MCP 연결 유지와 모델의 도구 호출 정확도는 별개입니다. Qwen 4B의 복잡한 다단계 도구 호출은 실환경 확인이 필요합니다. MCP 도구 설명도 문맥을 사용하므로 문맥 부족 시 앱의 **문맥을 16K로 설정**을 검토하세요. 메모리 사용량이 늘어납니다.

Launcher의 llama-ui 대화창이나 SI코파일럿에 OpenCode의 MCP 설정이 자동 복제되지는 않습니다. 기존 OpenCode 안에서 로컬 모델을 선택할 때의 설명입니다.

## 사고 ON / OFF

- 현재 Launcher는 **ON 또는 OFF 중 하나를 선택한 뒤 모델을 시작**합니다.
- 선택을 바꾸고 서버를 재시작하면 반영됩니다. ON용·OFF용 모델 파일을 따로 받지 않습니다.
- 한 번에 선택한 모델 하나를 실행합니다. ON/OFF 서버 두 개를 동시에 실행하는 기능은 없습니다.
- 지원 템플릿에 전달하는 서버 기본값입니다. 클라이언트가 요청별 사고 옵션을 보내면 달라질 수 있습니다.

## 기능

- GGUF 자동 검색과 선택한 모델 실행
- CPU / CUDA 12.4 / CUDA 13.4 선택, 기존 CUDA 13.1 설정 호환
- 로컬 API 키 생성·복사와 실행 중 연결 정보 표시
- 사고 모드 ON/OFF, OpenCode 설정 JSON 내보내기
- 긴 CPU 이름 줄바꿈, 작은 화면의 세로 배치와 스크롤, SI 로고, 모델 추가 안내

## 소스 빌드와 검증

승인된 Windows .NET Framework 4.8 환경에서 `Build.cmd`, 또는 Visual Studio의 `SI.LocalAI.Launcher.sln`을 사용합니다. 외부 NuGet 라이브러리 없이 빌드합니다.

- `Run-Core-Tests.cmd`: 설정·모델 검색·명령 인자·내보내기 회귀 검사
- `Run-API-Checks.cmd`: API 요청 헤더·키 처리 등 로직 검사
- [현재 검증 범위](validation/API_v1.3.md): Linux 교차 빌드 및 로직 69개 통과. 실제 Windows 모델 추론·사내 OpenCode/MCP 연동은 미검증입니다.

다운로드한 기존 v1.2 실행기·모델의 SHA256을 유지합니다. 새 모델 다운로드나 서버 실행을 외부망 PC에서 요구하지 않습니다. 서버 주소는 `127.0.0.1`이며 회사 보안 정책·방화벽·실행 정책을 바꾸지 않습니다.

공식 참고: [OpenCode MCP](https://opencode.ai/docs/mcp-servers/) · [OpenCode llama.cpp](https://opencode.ai/docs/providers/#llamacpp) · [llama.cpp b11429](https://github.com/ggml-org/llama.cpp/tree/b11429).
