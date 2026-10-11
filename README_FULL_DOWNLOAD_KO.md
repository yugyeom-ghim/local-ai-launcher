# 업무망용 오프라인 인스톨러

**[GitHub에서 전체 설치 파일 받기](https://github.com/yugyeom-ghim/si_local_model/releases/latest)**

`SI_Local_AI_Offline_Setup.exe`와 **모든 `SI_Local_AI_Offline_Setup-*.bin`**을 같은 폴더에 받으세요. EXE 하나만으로는 설치할 수 없습니다.

1. 인터넷이 승인된 PC에서 설치 파일 세트를 받습니다.
2. 같은 폴더에 모아 회사의 승인된 반입 절차로 업무망에 옮깁니다.
3. 업무망에서 `SI_Local_AI_Offline_Setup.exe`를 실행합니다. 이 설치 프로그램은 인터넷에서 파일을 다운로드하지 않습니다. BIN은 압축을 풀거나 합칠 필요가 없습니다.

설치 위치 선택, 시작 메뉴 등록, 선택적 바탕화면 바로가기, Windows 앱 목록의 제거 기능을 제공합니다. 설치 중 기존 Launcher나 모델을 자동 실행하지 않습니다.

## 인터넷 PC에서 한 번에 받기

**[인터넷 PC 전용 다운로드 도구](https://github.com/yugyeom-ghim/si_local_model/releases/latest/download/Internet-PC-Download-Installer.exe)**는 GitHub에 접속해 EXE와 모든 BIN을 자동으로 이어받고 SHA256을 확인합니다. 완성된 `SI_Local_AI_Offline_Installer` 폴더를 업무망에 옮기세요.

이 편의 도구는 설치 프로그램이 아닙니다. **업무망에서는 다운로드 도구를 실행하지 말고 Offline Setup을 실행하세요.** 외부망 PC에서 모델·LM Studio를 실행할 필요는 없습니다.

## 포함 및 필요 환경

- 포함: 실제 Qwen3.5-4B-Q4_K_M 모델, llama.cpp b11429 CPU·CUDA 12.4/13.4 실행기와 일치 DLL, VC++ 재배포 파일, 소스·라이선스·출처·SHA256.
- Windows x64와 .NET Framework 4.8 이상이 필요합니다. 없는 경우 회사 담당자가 승인된 오프라인 설치본으로 준비해야 하며 이 Setup은 다운로드하지 않습니다.
- CUDA 사용 시 호환 NVIDIA 드라이버가 필요합니다. CPU도 선택할 수 있습니다.
- VC++ 런타임이 없다면 동봉된 `prerequisites/vc_redist.x64.exe`를 회사 정책에 따라 설치합니다. 드라이버·런타임 설치 및 보안 정책 변경을 자동 수행하지 않습니다.
- 설치 폴더에 약 6GB 이상, 설치 파일 세트까지 함께 보관하면 12GB 이상 여유 공간을 권장합니다.

GitHub 파일당 2GiB 제한 때문에 Setup과 BIN 데이터로 나누었습니다. `installer-download-manifest.json`과 `SHA256SUMS.txt`에 각 파일의 크기와 SHA256을 기록합니다. 복구 조각은 없습니다.

## 기존 설치와 제거

기존 폴더를 설치 위치로 지정하면 개인 `state`와 API 키, 사용자가 추가한 별도 파일을 보존하며 갱신합니다. 새 위치로 설치하면 기존 포터블 폴더의 설정은 자동 이전하지 않습니다. 제거 시 설치된 앱과 기본 모델은 삭제하고 개인 설정·키·추가 파일은 남깁니다.

기존 포터블 사용자는 작은 `Update` ZIP을 덮어써도 됩니다. `Source code (zip)`과 `Update` ZIP에는 대용량 모델이 없습니다. 이전 ZIP 배포본은 [v1.3.1 Release](https://github.com/yugyeom-ghim/si_local_model/releases/tag/v1.3.1)에 보존됩니다.

## 검증과 표시

Windows 설치·재설치·제거·설정 보존·설치된 전체 파일 SHA256 검사 결과는 Release의 `INSTALLER_VALIDATION.md`를 확인하세요. 실제 모델 추론과 사내 OpenCode/MCP, 대화형 설치 화면의 DPI 검증은 구분하여 기록합니다. 회사 코드 서명 인증서는 제공되지 않았으며 기존 실행 정책을 따릅니다.

로고: [쎄트렉아이 공식 CI](https://www.satreci.com/kr/page/13). 설치 도구: Inno Setup 6.7.3, 고정 URL·크기·SHA256·라이선스는 `provenance/installer-build-tool.json`에 기록했습니다. 앱과 설치 화면에는 버전 숫자를 표시하지 않습니다.
