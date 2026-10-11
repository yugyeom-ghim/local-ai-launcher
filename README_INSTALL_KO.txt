SI Local AI — 업무망 오프라인 설치

인터넷 PC에서 준비
  GitHub Release의 SI_Local_AI_Offline_Setup.exe와 모든 SI_Local_AI_Offline_Setup-*.bin을 받습니다.
  편의 도구 Internet-PC-Download-Installer.exe는 인터넷 PC 전용이며 GitHub에 연결합니다.
  이 도구는 설치하지 않고 설치 파일 세트만 받습니다.
  업무망에서는 다운로드 도구를 실행할 필요가 없습니다.

업무망에서 설치
  1. Setup EXE와 모든 BIN 파일을 같은 폴더에 모아 회사가 승인한 절차로 반입합니다.
  2. SI_Local_AI_Offline_Setup.exe를 실행합니다.
  3. 설치 위치와 바탕화면 바로가기 여부를 선택하고 설치합니다.
  4. 시작 메뉴의 SI Local AI Launcher를 엽니다.

오프라인 Setup은 인터넷에 접속하여 파일을 다운로드하지 않습니다.
모델과 실행기·DLL이 설치 파일에 모두 포함되어 있습니다. BIN 파일은 압축 해제하거나 합치지 마세요.
모든 BIN이 Setup EXE 옆에 있어야 설치가 완료됩니다. EXE 하나만 반입하면 안 됩니다.

설치 환경: Windows x64 / .NET Framework 4.8 이상 / 설치 위치에 약 6GB 이상 여유 공간.
다운로드 파일과 설치된 앱을 함께 보관하려면 12GB 이상의 여유 공간을 권장합니다.
VC++ 런타임이 없다면 동봉된 prerequisites\vc_redist.x64.exe를 회사 담당자를 통해 설치합니다.
GPU 사용에는 호환되는 NVIDIA 드라이버가 필요하며 CPU 모드도 제공됩니다.
드라이버·런타임을 인터넷에서 받거나 보안 정책을 변경하지 않습니다.

기존 사용자
  기존 설치 폴더를 선택하면 모델·실행기와 앱을 갱신합니다.
  state/settings.json, 로컬 API 키, 사용자가 추가한 별도 파일은 유지합니다.
  기존 Launcher의 서버와 트레이 앱을 종료한 뒤 설치하세요.
  기본 설치 위치를 새로 선택하면 기존 포터블 폴더의 설정을 자동 이전하지 않습니다.

사용
  모델 라이브러리에서 모델과 사고 OFF/ON을 선택하고 시작합니다.
  추가 GGUF는 offline-assets\ai\models에 넣으면 목록에 나타납니다.
  API 연결 정보는 앱의 API 연결 탭에서 확인합니다.
  OpenCode의 기존 MCP 설정을 유지하고 provider.si-local만 추가합니다.
  SI코파일럿 VSIX는 별도이며 이 설치 프로그램에서 만들거나 수정하지 않습니다.

제거
  앱과 서버를 종료한 뒤 Windows 설정 → 앱 → SI Local AI Launcher에서 제거합니다.
  설치된 앱과 기본 모델은 제거되며 개인 설정·키와 사용자가 추가한 파일은 남습니다.

버전은 앱 화면에 표시하지 않으며 release-version.txt, EXE 속성, GitHub에서 관리합니다.
검증 결과는 Release의 INSTALLER_VALIDATION.md와 WINDOWS_CI.md를 확인하세요.
