# 검증

| 항목 | 결과 |
|---|---|
| Windows Release 빌드 | [WINDOWS_CI.md](validation/WINDOWS_CI.md) |
| 코어 / API / 다운로드 로직 | 62 / 7 / 9개 검사 통과 |
| 단일 EXE 합치기·SHA256·외부 연결 차단 설치 | [SINGLE_INSTALLER_VALIDATION.json](validation/SINGLE_INSTALLER_VALIDATION.json) |
| 실제 설치·재설치·제거, 설정 보존 | [INSTALLER_VALIDATION.md](validation/INSTALLER_VALIDATION.md) |
| 2B·9B 실제 다운로드·SHA256·Windows 팩 설치 | [MODEL_PACK_VALIDATION.json](validation/MODEL_PACK_VALIDATION.json) |
| 모델 팩 로직 | [12개 검사 통과](validation/MODEL_PACK_CHECKS.txt) |
| 기본 모델·실행기 다운로드 및 SHA256 | [FULL_PACKAGE_VALIDATION.json](validation/FULL_PACKAGE_VALIDATION.json) |
| 공개 다운로드 | [SINGLE_INSTALLER_DOWNLOAD_CHECKS.json](validation/SINGLE_INSTALLER_DOWNLOAD_CHECKS.json) |
| 실제 배포 파일로 Windows UI·대기 검사 | [REAL_ASSET_UI_CHECKS.json](validation/REAL_ASSET_UI_CHECKS.json) |
| Windows 시작·대기 성능 비교 | [UI_STARTUP_COMPARISON.json](validation/UI_STARTUP_COMPARISON.json) |
| 파일 변경 감지, 1000/800px 화면·150% 배율 모의 렌더 | [Windows 검사](https://github.com/yugyeom-ghim/local-ai-launcher/actions/workflows/ui-checks.yml) |
| 실제 모니터 DPI 전환, CPU·CUDA 추론, 사내 OpenCode/MCP | 미검증 |

설치 검사는 Windows Server 2022에서 실행했습니다. 설치 프로그램의 외부 연결을 차단해 검증했습니다. 패킷 캡처는 수행하지 않았습니다.
바이너리는 코드 서명되지 않았습니다.
