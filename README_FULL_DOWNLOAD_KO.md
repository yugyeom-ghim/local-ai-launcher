# 다운로드와 설치

[GitHub Release](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest)

1. 인터넷 PC에서 [다운로드 도구](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest/download/Internet-PC-Download-Installer.exe)를 실행합니다.
2. `Local_AI_Offline_Installer`에 만들어진 `Local_AI_Offline_Setup.exe` 하나를 업무망으로 옮깁니다.
3. EXE를 실행해 설치합니다. 설치 중 인터넷 연결은 필요하지 않습니다.

다운로드·합치기에 여유 공간 9GB가 필요합니다. 이어받기와 SHA256 검사를 지원하며, 완료 후 조각 파일은 정리됩니다.
GitHub의 파일당 2GiB 제한으로 다운로드 단계에서만 분할 데이터를 사용합니다.

## 환경

Windows 10/11 x64, .NET Framework 4.8+, 여유 공간 6GB 이상. CUDA 사용 시 호환 NVIDIA 드라이버가 필요합니다.
VC++ 런타임 설치 파일은 `prerequisites/vc_redist.x64.exe`에 있습니다.

## 업데이트

기존 설치 위치에 설치하면 설정·API 키·추가 모델을 유지합니다. 포터블 사용자는 Update ZIP을 기존 폴더에 덮어쓴 뒤 `LocalAI.Launcher.exe`를 실행합니다.

제거 후에도 개인 설정과 추가 모델은 남습니다.

[파일별 SHA256](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest/download/SHA256SUMS.txt) · [검증 결과](VALIDATION.md)
