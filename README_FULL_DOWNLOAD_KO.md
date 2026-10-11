# 전체 배포본 다운로드

**[전체 다운로드 프로그램 받기](https://github.com/yugyeom-ghim/si_local_model/releases/latest/download/Download-SI-Local-AI.exe)**

1. 인터넷 사용이 승인된 Windows PC에서 위 EXE를 빈 폴더에 저장하고 실행합니다. .NET Framework 4.8 환경을 사용합니다.
2. 프로그램이 GitHub의 전체 배포 파일을 이어받아 검사하고 **전체 ZIP 하나**를 같은 폴더에 만듭니다. 중단되면 같은 위치에서 다시 실행하세요.
3. 완성된 ZIP을 승인된 반입 절차로 사내 PC에 옮겨 압축을 완전히 풀고 `SI.LocalAI.Launcher.exe`를 실행합니다.

다운로드와 합치기만 수행합니다. 외부망 PC에서 LM Studio나 모델을 실행하지 않습니다. 다운로드 중에는 ZIP 크기의 약 두 배의 여유 공간이 필요하고 압축 해제에는 추가 공간이 필요합니다. 20GB 이상의 여유 공간을 권장합니다. FAT32 대신 4GB가 넘는 파일을 저장할 수 있는 NTFS/exFAT를 사용하세요.

## 포함 파일

- Windows에서 빌드한 Launcher Release EXE와 전체 소스
- llama.cpp b11429 Windows CPU 실행기
- CUDA 12.4 및 13.4 실행기와 각각 일치하는 DLL
- Qwen3.5-4B-Q4_K_M.gguf 실제 모델 (2,740,937,888 bytes)
- Visual C++ 재배포 설치 파일, 출처·고정 버전·크기·SHA256·라이선스

CUDA Toolkit이나 LM Studio를 별도로 설치하지 않습니다. GPU 사용에는 해당 CUDA를 지원하는 NVIDIA 드라이버가 필요합니다. 프로그램은 드라이버·VC 재배포 파일을 자동 설치하지 않으며 회사 정책에 따라 담당자와 진행합니다. CPU 모드도 포함됩니다.

## 브라우저로 직접 받기

GitHub Releases는 파일당 2GiB 미만만 허용하므로 전체 ZIP은 `.part01`, `.part02` 등으로 올립니다. 복구 전용 파일은 없습니다. [최신 Release](https://github.com/yugyeom-ghim/si_local_model/releases/latest)에서 모든 `SI_Local_AI_Full_*.zip.part*`와 `Download-SI-Local-AI.exe`를 같은 폴더에 받은 후 EXE를 실행하면 검증하고 합칩니다. 직접 받은 조각을 모두 사용하면 네트워크에 다시 접속할 필요가 없습니다. 명령줄의 `--assemble-only`로 네트워크 다운로드를 금지할 수도 있습니다.

개별 조각은 압축 해제하지 않습니다. 완성된 ZIP만 압축 해제하세요. `Source code (zip)`과 작은 `Update` ZIP에는 대용량 모델이 없습니다.

`full-download-manifest.json`은 전체 ZIP과 각 조각의 크기·SHA256을 기록합니다. 다운로드 EXE에도 같은 값을 내장하며 임의 최신 링크의 내용으로 바꾸지 않습니다. `SHA256SUMS.txt`는 GitHub 첨부 파일을, 패키지 내부 `SHA256SUMS.txt`는 압축 해제한 파일을 검증합니다. 다운로드 프로그램은 코드 서명되지 않았으며 실행·네트워크 정책을 바꾸지 않습니다.

Windows 빌드·로직 검사와 자산 무결성 검증 결과는 Release의 `WINDOWS_CI.md`, `FULL_PACKAGE_VALIDATION.json`에 있습니다. 실제 Windows GUI/DPI, CPU/CUDA 추론, 사내 OpenCode/MCP 연동은 별도 실환경 확인이 필요합니다.

공식 제한: https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases
