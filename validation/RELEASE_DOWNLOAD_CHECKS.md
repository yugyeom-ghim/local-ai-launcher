# GitHub Release 다운로드 확인

대상: https://github.com/yugyeom-ghim/si_local_model/releases/tag/v1.4.0
빌드 및 설치 검사: https://github.com/yugyeom-ghim/si_local_model/actions/runs/38128563074
빌드 커밋: fcd11908de928e37eda5bbb64f860c671734a507

- 전체 오프라인 설치 세트: 4,068,264,969 bytes (약 3.79 GiB), Setup EXE 1개와 BIN 3개.
- 네 파일 모두 GitHub 상태 uploaded, 크기 및 GitHub 저장 SHA256이 installer-download-manifest.json과 일치.
- 세 BIN의 공개 다운로드 URL에서 HTTP 206 Range 응답 및 첫 4096바이트 수신 확인.
- Setup EXE, 인터넷 PC용 다운로드 EXE, 업데이트 ZIP, 검사 로그·출처·매니페스트는 공개 URL에서 실제 내려받아 크기 및 SHA256 확인.
- 전체 Release SHA256SUMS.txt와 개별 파일·GitHub 저장 SHA256 일치 확인.
- 업데이트 ZIP CRC와 내부 파일별 SHA256, 내부 버전 1.4.0 확인.
- Windows 빌드 작업에서 실제 모델·실행기·DLL 등 187개 자산, 설치된 전체 270개 파일 SHA256 검사 통과.
- Windows Server 2022에서 한글 경로 설치·바로가기·재설치·제거 및 개인 설정/API 키/추가 파일 보존 검사 15개 통과.
- 인터넷 PC용 다운로드 도구의 내장 매니페스트로 전체 설치 파일의 SHA256 확인 통과. 이 후속 다운로드 점검에서는 4GB 데이터 전체를 다시 내려받지는 않았습니다.
- Setup 스크립트에 인터넷 다운로드·보안 정책 변경 동작이 없습니다. 네트워크 차단 및 패킷 캡처 검사는 수행하지 않았습니다.
- 대화형 설치 화면·Launcher GUI/DPI·실제 모델 추론·사내 OpenCode/MCP 연동은 미검증입니다.
- 직원 Windows PC에서 인터넷 다운로드 도구로 전체 GitHub 파일을 받는 종단간 검사는 미실행입니다. 동일 다운로드 로직의 Windows 로컬 HTTP 이어받기 검사는 통과했습니다.

파일별 정확한 크기·해시·검증 범위: [INSTALLER_DOWNLOAD_CHECKS.json](INSTALLER_DOWNLOAD_CHECKS.json).
설치 테스트 기록: [INSTALLER_VALIDATION.md](INSTALLER_VALIDATION.md).
