# v1.3 검증 기록

Linux에서 .NET SDK 8.0.425 Roslyn으로 net48 / C# 5 / x64 EXE를 교차 빌드하고 Mono로 검사했습니다.

- 코어: 62개 통과 — `CORE_TESTS_API.txt`
- API 요청·키 처리: 7개 통과 — `API_LOGIC_CHECKS.txt`
- 모델·실행기: 기존 배포 자산 사용
- UI: X 디스플레이 연결 실패 — `UI_ATTEMPT_API.txt`
- Windows 프로세스·DLL·추론, 실제 API 인증·사고 출력, 사내 MCP·VSIX 연동: 미검증
- OpenCode JSON Schema 수신은 HTTP 403으로 실패. JSON 파싱·필수 필드·키 제외 검사는 통과.

후속 Windows 빌드 기록: [WINDOWS_CI.md](WINDOWS_CI.md).

참고: [llama.cpp b11429](https://github.com/ggml-org/llama.cpp/blob/b11429/tools/server/README.md), [OpenCode](https://opencode.ai/docs/providers/#llamacpp).
