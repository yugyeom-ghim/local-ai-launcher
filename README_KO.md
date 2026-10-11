# SI Local AI Launcher v1.3 / Offline 자산 v1.2

**v1.3 업데이트 ZIP 적용 방법과 API 키 / OpenCode / 사고 모드 사용법은 [README_API_UPDATE_KO.md](README_API_UPDATE_KO.md)를 먼저 확인하세요. 업데이트 ZIP은 기존 전체 패키지에 덮어쓰는 파일이며 아래 자산은 재포함하지 않습니다.**

기존 v1.2 전체 패키지에는 CPU, CUDA 12.4, CUDA 13.4 실행기·DLL과 Qwen 모델이 포함되어 있습니다. Windows x64 / .NET Framework 4.8용 Release EXE를 Linux에서 교차 빌드했습니다. **Windows UI와 실제 CPU/GPU 추론은 아직 검증하지 못했습니다.** 검증 범위는 VALIDATION.md를 확인하세요.

## 직원 사용

1. 전체 ZIP을 압축 해제합니다. 압축 내부에서 EXE만 따로 실행하지 마세요.
2. `SI.LocalAI.Launcher.exe` 또는 `Start.cmd`를 실행합니다. 최초 실행에 소스 컴파일이나 인터넷 연결이 필요하지 않습니다.
3. 모델 목록의 `Qwen3.5-4B-Q4_K_M`을 선택하고 **선택 모델로 서버 시작**을 누릅니다. 실행기는 기본 **자동 검사**를 사용합니다.
4. 실행기 검사 → 모델 로딩 → 실제 생성 API 검사를 통과하면 **준비 완료**가 됩니다.
5. **로컬 API 연결 정보**에서 기본 주소와 실제 실행 모델 ID를 복사합니다. SI Copilot의 별도 VSIX에서 지원하는 설정에 입력합니다. 이 패키지는 VSIX를 설치·수정하지 않습니다.

외부망 PC에서 LM Studio나 모델을 실행하는 준비 절차는 없습니다. 다운로드·해시 검증·압축 해제만으로 패키지를 준비할 수 있습니다. 실제 사용/추론 검증은 회사에서 허용한 Windows 환경에서 진행합니다.

## 실제 포함 파일

| 항목 | 고정 버전 / 위치 |
|---|---|
| Launcher | 1.3.0.0, x64, C# 5, .NET Framework 4.8, 최적화 Release EXE |
| llama.cpp | b11429, commit `d81235049384534c167caea52b85a694f6103d14` (GitHub의 pre-release 표기) |
| CPU | `offline-assets/ai/bin/win-cpu-x64`, 공식 CPU ZIP 전체 |
| CUDA 12.4 | `offline-assets/ai/bin/win-cuda-12.4-x64`, 같은 릴리스의 실행기 ZIP 전체 + CUDA 12.4 DLL ZIP 전체 |
| CUDA 13.4 | `offline-assets/ai/bin/win-cuda-13.4-x64`, 같은 릴리스의 실행기 ZIP 전체 + CUDA 13.4 DLL ZIP 전체 |
| 모델 | `offline-assets/ai/models/Qwen3.5-4B-Q4_K_M.gguf`, 2,740,937,888 bytes |
| 모델 저장소 | `unsloth/Qwen3.5-4B-GGUF`, revision `e87f176479d0855a907a41277aca2f8ee7a09523` |
| VC++ 런타임 | `prerequisites/vc_redist.x64.exe`, 파일 버전 14.51.36247.0 |
| 기록 | ASSET_MANIFEST.json, SHA256SUMS.txt, licenses/, validation/, 소스 및 재현 스크립트 |

빈 실행기 폴더나 PLACE_FILES_HERE 파일로 대체한 항목은 없습니다. 이전 CUDA 13.1 설정 키와 선택 항목은 호환용으로 유지하되, 13.1 바이너리는 배포하지 않습니다. 13.4 파일을 13.1 폴더에 넣지 않았습니다. 모델용 mmproj/OCR 파일을 추가하지 않았습니다. 공식 실행기 ZIP은 전체 보존하므로 그 안의 일반 CLI/멀티모달 실행 파일은 존재하지만 Launcher는 텍스트 서버만 실행합니다.

## 어떤 실행기를 선택하나요?

- 기본 **자동**: CUDA 12.4 → CUDA 13.4 → 기존 CUDA 13.1(직접 등록한 경우) → CPU 순으로 실제 실행 가능 여부를 확인합니다. CPU 전환 허용이 꺼져 있으면 CPU로 넘어가지 않습니다.
- **CPU**: NVIDIA GPU가 없거나 GPU 사용을 원하지 않을 때 선택합니다.
- **CUDA 12.4 / 13.4 직접 선택**: 해당 버전만 사용합니다. 실패해도 CPU로 임의 전환하지 않습니다.
- CUDA 13.4는 추가 호환 선택지입니다. 모든 GPU에서 12.4보다 더 빠르거나 반드시 필요한 것은 아닙니다. 드라이버·GPU·모델의 실제 로딩 결과로 판단합니다.
- GPU 메모리가 부족하고 GPU 레이어가 자동이면 같은 실행기로 16개 레이어만 한 번 재시도합니다. 이후에도 실패하면 자동 모드의 다음 실행기를 확인합니다.

Windows x64, .NET Framework 4.8 이상, 충분한 RAM/디스크가 필요합니다. NVIDIA GPU 사용에는 회사에서 승인한 호환 드라이버가 이미 설치되어 있어야 합니다. CUDA Toolkit 전체나 드라이버 설치 파일은 포함하지 않습니다. VC++ 런타임이 없다는 오류가 발생하면 동봉된 설치 파일을 회사의 승인 절차에 따라 사용하세요. 자동 설치, 관리자 권한 강제 요청, 방화벽·백신·실행 정책 변경은 하지 않습니다. CPU 사용도 VC++ 런타임이 필요합니다.

## 모델 추가와 기존 설정

- 새 GGUF를 `offline-assets/ai/models`에 넣으면 약 3초 간격으로 목록을 갱신합니다. 파일 크기/수정 시각이 안정되고 헤더·텐서 위치 검사를 통과해야 표시됩니다.
- 큰 파일은 `.partial` 이름으로 복사를 완료한 다음 `.gguf`로 바꾸는 방식을 권장합니다. `.partial` 파일은 실행 대상에 포함되지 않습니다.
- 모델 추가, 새로고침, 행 선택만으로 서버를 재시작하지 않습니다. 선택한 모델로 시작/전환할 때만 로딩합니다. 전환 확인창은 진행 중 요청이 끊어진다는 점을 알립니다.
- **선택 모델**과 **실제 실행 모델/API 모델 ID**는 별도입니다. 목록의 발견 상태는 추론 준비 완료를 의미하지 않습니다.
- 분할 GGUF는 모든 조각이 있어야 한 모델로 표시됩니다. 파일 크기/구조 검사만으로 모든 손상을 검출하지는 못합니다. 배포본 전체는 `Verify-Package.cmd`로 SHA256을 검사할 수 있습니다. 회사 정책이 스크립트를 차단하면 정책을 변경하지 말고 승인된 담당자에게 검사를 요청하세요.
- v1.1의 Schema 1 설정과 상대경로는 유지됩니다. 기존 `state/settings.json`을 같은 위치에 두면 사용합니다. 새 설정 `Cuda134Path`만 추가되었습니다.
- 자산은 EXE 위치 기준 상대경로로 찾습니다. 폴더 전체를 함께 옮기세요. EXE만 이동하지 마세요.
- 설정/로그를 쓸 수 없으면 `%LOCALAPPDATA%/SI.LocalAI/<설치경로 해시>`로 전환합니다. 원래 폴더의 기존 설정은 읽어서 이어갑니다. 자산은 복사하지 않습니다.
- 기본 자동 시작은 false입니다. 기존에 저장한 자동 시작 true 설정은 보존됩니다.

## 소스 변경 요약

- ‘SI코파일럿 연결’ → ‘로컬 API 연결 정보’.
- CUDA 13.4 경로/선택/자동 검사 추가, CUDA 13.1 설정 유지.
- 기본 auto 모드는 사용자가 선택한 한 모델만 실행. 명시적 router 모드도 선택 모델만 등록하며 지원 시 자동 로딩을 끕니다.
- `/health`와 `/v1/models` 확인 이후 실제 생성 응답까지 확인해야 준비 완료 표시.
- GGUF 파서가 architecture 항목에서 조기 반환하던 부분을 수정. 전체 메타데이터, 텐서 디렉터리, 알려진 양자화 형식의 데이터 크기를 검사하여 중단된 복사를 더 잘 검출합니다.
- 기존 C# 5에서 허용되지 않는 catch 블록의 await 두 곳을 수정하여 실제 빌드 가능하게 했습니다.
- Start.cmd에서 직원 PC 최초 소스 컴파일 제거. EXE 누락 시 명확히 실패합니다.
- 읽기 전용 설치 위치에서 기존 설정을 이어 읽도록 보완했습니다.

## 담당자 재현

- 승인된 Windows 빌드 PC: `Build.cmd` 또는 VS2022에서 제공 솔루션 Release/x64 빌드. `.NET Framework 4.8` 개발 환경이 필요합니다.
- 핵심 테스트: `Run-Core-Tests.cmd` (Windows에서는 아직 실행하지 않음).
- 자산 재현: `python scripts/prepare_assets.py --cache <승인된 캐시> --destination <새 빈 폴더>`.
- 인터넷 없이 캐시만 사용: 위 명령에 `--offline-cache` 추가. 모델/서버/설치 파일을 실행하지 않습니다. 전체 바이트 크기와 고정 SHA256이 다르면 중단합니다.
- 릴리스 압축: `python scripts/package_release.py --root <완성 폴더> --output <폴더 밖 ZIP 경로>`.
- 실행 코드에 다운로드 기능은 없습니다. 온라인 자산 준비 스크립트는 배포 담당자용이며 Launcher가 호출하지 않습니다.

런처는 SI Copilot VSIX와 별개입니다. API_CONTRACT.md를 참고하세요.
