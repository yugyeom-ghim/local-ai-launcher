# 다운로드와 설치

[GitHub Release](https://github.com/yugyeom-ghim/si_local_model/releases/latest)

Setup EXE와 BIN 파일을 같은 폴더에 저장한 뒤 업무망으로 반입해 `Local_AI_Offline_Setup.exe`를 실행합니다. 설치 중 다운로드는 없습니다.

인터넷 PC용 [다운로드 도구](https://github.com/yugyeom-ghim/si_local_model/releases/latest/download/Internet-PC-Download-Installer.exe)는 이어받기와 SHA256 검사를 지원합니다. 완료된 `Local_AI_Offline_Installer` 폴더를 옮기면 됩니다.

## 환경

Windows 10/11 x64, .NET Framework 4.8+, 여유 공간 6GB 이상. CUDA 사용 시 호환 NVIDIA 드라이버가 필요합니다.
VC++ 런타임 설치 파일은 `prerequisites/vc_redist.x64.exe`에 있습니다.

## 업데이트

기존 설치 위치에 설치하면 설정·API 키·추가 모델을 유지합니다. 포터블 사용자는 Update ZIP을 기존 폴더에 덮어쓴 뒤 `LocalAI.Launcher.exe`를 실행합니다.

제거 후에도 개인 설정과 추가 모델은 남습니다.

[파일별 SHA256](https://github.com/yugyeom-ghim/si_local_model/releases/latest/download/SHA256SUMS.txt) · [검증 결과](VALIDATION.md)
