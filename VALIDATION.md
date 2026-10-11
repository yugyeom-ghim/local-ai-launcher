# 검증

| 항목 | 결과 |
|---|---|
| Windows Release 빌드 | [WINDOWS_CI.md](validation/WINDOWS_CI.md) |
| 코어 / API / 다운로드 로직 | 62 / 7 / 9개 검사 통과 |
| 실제 설치·재설치·제거, 설정 보존 | [INSTALLER_VALIDATION.md](validation/INSTALLER_VALIDATION.md) |
| 모델·실행기 다운로드 및 SHA256 | [FULL_PACKAGE_VALIDATION.json](validation/FULL_PACKAGE_VALIDATION.json) |
| 공개 다운로드 | [RELEASE_DOWNLOAD_CHECKS.md](validation/RELEASE_DOWNLOAD_CHECKS.md) |
| GUI/DPI, CPU·CUDA 추론, 사내 OpenCode/MCP | 미검증 |

설치 검사는 Windows Server 2022에서 실행했습니다. 네트워크 차단·패킷 캡처 검사는 수행하지 않았습니다.
바이너리는 코드 서명되지 않았습니다.
