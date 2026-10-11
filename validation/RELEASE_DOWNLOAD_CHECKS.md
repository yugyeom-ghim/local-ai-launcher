# GitHub Release 다운로드 확인

대상: https://github.com/yugyeom-ghim/si_local_model/releases/tag/v1.3.1

- 전체 ZIP: 4,070,155,634 bytes (3.79 GiB), 3개 조각.
- 전체 ZIP SHA256: c6fb7c2e042ea43431cc047f875df12154993832192e7604f792850eb30fe8f6
- 세 조각 모두 GitHub 상태 uploaded, 크기 및 GitHub 저장 SHA256이 full-download-manifest.json과 일치.
- 세 조각 공개 다운로드 URL에서 HTTP 206 Range 응답 및 첫 4096바이트 수신 확인.
- 다운로드 EXE, 업데이트 ZIP, 검사 로그·출처·매니페스트는 공개 URL에서 실제 내려받아 크기 및 SHA256 확인.
- 업데이트 ZIP CRC와 내부 파일별 SHA256 확인. App.cs / Dashboard.cs에 화면 버전 문자열이 없고 AssemblyInfo 내부 버전이 1.3.1임을 확인.
- 전체 ZIP 내부 자산 해시와 CRC는 Windows 빌드 작업에서 검증. 이 후속 점검에서는 4GB 전체를 다시 내려받지는 않았습니다.
- Windows GUI/DPI·실제 모델 추론·사내 OpenCode/MCP 연동 미검증. 다운로드 프로그램의 실제 GitHub 전체 수신은 직원 Windows PC에서 별도 확인 필요하며 Windows 로컬 HTTP 이어받기 검사는 통과했습니다.
