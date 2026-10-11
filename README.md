# SI Local AI Launcher

Windows에서 GGUF 모델을 선택하고 로컬 llama.cpp 서버를 실행하는 도구입니다.
**SI코파일럿은 Visual Studio에 설치하는 별도 VSIX**이며, 이 저장소는 Launcher 소스와 배포 파일을 관리합니다.

## 업무망용 오프라인 설치

**[GitHub에서 전체 설치 파일 받기](https://github.com/yugyeom-ghim/si_local_model/releases/latest)**

`SI_Local_AI_Offline_Setup.exe`와 **모든 `SI_Local_AI_Offline_Setup-*.bin`**을 같은 폴더에 받으세요. **EXE 하나만으로는 설치할 수 없습니다.**

1. 인터넷이 승인된 PC에서 위 설치 파일 세트를 다운로드합니다.
2. 회사의 승인된 반입 절차로 업무망에 옮깁니다.
3. 업무망에서 **Offline Setup EXE**를 실행합니다. 설치 중 GitHub 접속이나 파일 다운로드가 없습니다.

쎄트렉아이 로고를 적용한 설치 화면, 설치 위치 선택, 시작 메뉴·선택적 바탕화면 바로가기, 제거 기능을 제공합니다. 모델·CPU·CUDA 12.4/13.4 실행기와 DLL이 모두 포함됩니다. 앱·설치 화면에는 버전을 표시하지 않습니다.

인터넷 PC에서는 [다운로드 편의 도구](https://github.com/yugyeom-ghim/si_local_model/releases/latest/download/Internet-PC-Download-Installer.exe)로 전체 세트를 자동으로 이어받을 수 있습니다. **이 도구만 인터넷에 연결하며 업무망 설치용이 아닙니다.**

[설치 안내](README_FULL_DOWNLOAD_KO.md) · [기존 포터블용 업데이트](https://github.com/yugyeom-ghim/si_local_model/releases/download/v1.4.0/SI_Local_AI_Update_v1.4.0_Windows.zip) · [소스 전체 ZIP](https://github.com/yugyeom-ghim/si_local_model/archive/refs/heads/main.zip)

기존 포터블 폴더에 덮어쓰는 Update ZIP과 Source code ZIP에는 대용량 모델이 없습니다. 전체 설치에는 위 Setup과 BIN이 필요합니다. .NET Framework 4.8 및 GPU 사용 시 호환 드라이버는 회사가 승인한 환경으로 준비합니다. 설치 파일에는 VC++ 오프라인 재배포 파일이 포함됩니다.

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
- 앱 화면·창 제목에는 버전 숫자를 표시하지 않으며 내부 메타데이터와 GitHub 릴리스에서 관리

## 소스 빌드와 검증

승인된 Windows .NET Framework 4.8 환경에서 `Build.cmd`, 또는 Visual Studio의 `SI.LocalAI.Launcher.sln`을 사용합니다. 외부 NuGet 라이브러리 없이 빌드합니다.

- `Run-Core-Tests.cmd`: 설정·모델 검색·명령 인자·내보내기 회귀 검사
- `Run-API-Checks.cmd`: API 요청 헤더·키 처리 등 로직 검사
- [Windows 빌드와 검사 결과](validation/WINDOWS_CI.md): GitHub의 Windows Server 2022 환경에서 네이티브 Release 빌드 및 코어/API 로직 **69개**, 다운로드/합치기 **9개** 검사 통과. 분할 경계·기존 파일 보호 검사 2개도 통과했습니다.
- [전체 패키지 검증 결과](validation/FULL_PACKAGE_VALIDATION.json): 실제 자산 187개 크기·SHA256 확인.
- [오프라인 설치 검증 결과](validation/INSTALLER_VALIDATION.md): Windows에서 한글 경로 설치·바로가기·재설치·제거·개인 파일 보존 검사 15개 통과, 설치된 270개 파일 SHA256 확인.
- [공개 다운로드 확인](validation/RELEASE_DOWNLOAD_CHECKS.md): Setup·BIN 3개 게시 및 매니페스트와 GitHub SHA256 일치 확인.
- [성공한 Windows 작업](https://github.com/yugyeom-ghim/si_local_model/actions/runs/38128563074) · [이전 Linux 검증 범위](validation/API_v1.3.md)
- 설치 프로그램은 실제 Windows에서 무인 설치·재설치·제거를 실행했습니다. 대화형 설치 화면·Launcher GUI/DPI, 모델 추론, 사내 OpenCode/MCP 연동은 미검증입니다. 네트워크를 물리적으로 끊거나 패킷을 캡처한 검사는 수행하지 않았으며, 설치 스크립트에 다운로드 동작이 없음을 확인했습니다.
- 새 버전은 `release-version.txt`를 올려 게시합니다. 이미 게시한 Release 파일은 자동으로 덮어쓰지 않습니다.

전체 배포 과정에서 고정된 실행기·모델을 다운로드하고 기존 SHA256과 대조합니다. 다운로드 프로그램은 이미 GitHub에 게시된 전체 패키지만 받습니다. 외부망 PC에서 모델을 실행할 필요는 없습니다. 서버 주소는 `127.0.0.1`이며 회사 보안 정책·방화벽·실행 정책을 바꾸지 않습니다.

공식 참고: [OpenCode MCP](https://opencode.ai/docs/mcp-servers/) · [OpenCode llama.cpp](https://opencode.ai/docs/providers/#llamacpp) · [llama.cpp b11429](https://github.com/ggml-org/llama.cpp/tree/b11429).
