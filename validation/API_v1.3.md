# v1.3 검증 기록 — 2026-10-11

추가 검증: GitHub Windows Server 2022에서 네이티브 빌드 및 로직 69개 검사가 완료되었습니다. [WINDOWS_CI.md](WINDOWS_CI.md)와 함께 보세요. 아래는 GitHub 게시 전의 Linux 작업 기록이며 GUI/DPI·실제 추론·사내 MCP 연동은 여전히 미검증입니다.

## 빌드 및 다운로드 구분

- Release EXE: 성공. .NET SDK 8.0.425 Roslyn, Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3, C# 5, x64, optimize+, .NET Framework 4.8 대상. 소스·솔루션·Build.cmd 포함. 새 NuGet 런타임 종속성 없음.
- 기존 도구 체인으로 Linux에서 Windows EXE를 교차 빌드함. Windows 자체 빌드/실행과 다름.
- 모델 / llama.cpp / CUDA / VC++ 바이너리를 이번 업데이트에서 다운로드하지 않음. 기존 v1.2 배포 자산을 사용하며 원래 출처·고정 버전·크기·해시·라이선스 기록을 유지함.
- API 플래그는 공식 llama.cpp **b11429**의 server README 및 common/arg.cpp, server-http.cpp 등 소스를 내려받아 확인함. 원래 배포 런타임 버전과 같음.
- 공식 OpenCode providers 문서의 llama.cpp/custom-provider 규격에 따라 JSON을 생성함. OpenCode 최신 JSON Schema 다운로드는 HTTP 403으로 실패하여 스키마 자동 검사는 미실행. JSON 파싱·필수 필드·키 비포함 검사는 통과함.

## 실행한 검사

`CORE_TESTS_API.txt`: **62 passed, 0 failed**. 실제 Core.cs / Library.cs / ApiIntegration.cs를 net48 참조로 컴파일 후 Linux Mono에서 실행. 이전 버전의 serializer shim을 사용하지 않음.

범위: 기존 모델 검색·상대경로·분할 GGUF·파일 안정화·선택 유지·실행기 순서 회귀검사, 기존 설정의 ThinkingOff/포트/경로 및 새 인증 기본값 호환, 사고 ON/OFF의 명시적 인자, 구형 템플릿 옵션 대체, 키 지속성·서로 다른 설치의 키 구분·오염 키 거부, 미지원 인증의 실패 처리, OpenCode 내보내기의 실제 모델 ID/포트/문맥 및 키 비포함.

`API_LOGIC_CHECKS.txt`: **7 passed, 0 failed**. 배포 Release EXE의 실제 클래스 참조.

범위: health/models/load/chat 요청 생성의 Bearer 헤더와 IPv4 loopback 목적지, 인증 해제 시 헤더 생략, 외부/잘못된 경로 거부, 상속 환경 변수의 API 키 제거, 파일·이벤트 로그의 키 마스킹, 공개 상태 JSON의 키 비포함, 중지된 서버에서 오래된 연결 정보 내보내기 거부. **요청 객체 검사이며 네트워크 송수신 시험이 아님.**

## 수행하지 못한 검사

- 새 UI를 Mono + Xvfb에서 실행하려 했으나 X 디스플레이 연결이 환경에서 차단됨. `UI_ATTEMPT_API.txt`에 실패 기록. v1.2.1 UI 검사는 이전 버전의 역사적 기록일 뿐 v1.3 UI 검증으로 계산하지 않음.
- Windows Forms UI / 실제 100–200% DPI / 모니터 이동: 미검증.
- Windows Job Object 프로세스 소유·정리, CPU/CUDA DLL 로딩 및 Qwen 추론: 이번 업데이트 미검증.
- 실제 서버의 401 인증 거부, 정상 키 생성 API 응답, 사고 ON/OFF 출력 차이: 이 환경에서 미검증. 코드에는 직원 PC에서 서버 시작 시 인증과 생성 응답을 확인하는 검사를 추가함.
- 사내 OpenCode와 실제 VSIX의 연결, 스트리밍 및 코딩 도구 호출: 미검증. VSIX 소스를 새로 만들거나 수정하지 않음.

## 동작상 경계

- 사고 스위치는 서버 기본값. 모델 템플릿이 지원해야 하고 클라이언트가 요청별로 덮어쓸 수 있음. reasoning-format none을 사고 OFF로 사용하지 않음.
- API 키는 로컬 서버용으로 별도 생성하고 파일로 전달. 기존 키는 보존. 키 사용을 끄거나 지원하지 않는 실행기로 인증을 우회하는 자동 폴백은 없음.
- 내보내기는 준비된 현재 서버의 스냅샷을 사용. 사용자가 아직 적용하지 않은 포트·키 설정을 혼합하지 않음.
- 기존 state/settings.json, 모델, 실행기 및 상대경로를 업데이트 ZIP에서 덮어쓰지 않음.
- 인증 파일은 사용자 상태 폴더의 평문 파일이며 암호화 저장을 구현했다고 주장하지 않음. 로그 폴더를 배포할 때 개인 키를 포함하지 않아야 함.
- OpenCode의 설치·패키지 추가·회사 정책 변경·원격 포트 개방 없음. 기본 loopback 주소 유지.

## 공식 참고

- https://github.com/ggml-org/llama.cpp/blob/b11429/tools/server/README.md
- https://github.com/ggml-org/llama.cpp/blob/b11429/common/arg.cpp
- https://github.com/ggml-org/llama.cpp/blob/b11429/tools/server/server-http.cpp
- https://opencode.ai/docs/providers/#llamacpp
- https://opencode.ai/docs/providers/#custom-provider
- https://huggingface.co/Qwen/Qwen3.5-4B
