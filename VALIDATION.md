# 현재 Launcher v1.3 검증

최신 결과는 `validation/API_v1.3.md`를 확인하세요. 아래는 기존 v1.2 자산·빌드의 보존 기록입니다. 이번 v1.3 Windows 실행을 증명하는 결과가 아닙니다.

# 검증 기록 — 2026-10-08

## 결과 구분

| 범위 | 결과 | 증거 / 한계 |
|---|---|---|
| 실제 자산 다운로드 | 완료 | CPU ZIP, CUDA 12.4 실행기/DLL ZIP, CUDA 13.4 실행기/DLL ZIP, Qwen GGUF를 전체 다운로드 |
| 게시자 SHA256 대조 | 완료 | 5개 GitHub 릴리스 ZIP의 API digest, Hugging Face 모델 LFS SHA256과 로컬 전체 해시 일치 |
| VC++ 설치 파일 | 완료 | Microsoft 공식 링크의 실제 파일 14.51.36247.0 포함. 로컬 SHA256 계산. 게시자 SHA256 및 Windows Authenticode 검증은 미실행 |
| 압축 해제 | 완료 | 경로 이탈/중복 대상 금지, CRC 읽기 검사, 파일 크기 확인. EXTRACTION.json |
| 자산 준비 재현 | 완료 | 동봉 스크립트의 offline-cache 모드로 별도 폴더 재구성, 실제 배포 파일 169개와 해시 일치. ASSET_REPRODUCTION.txt |
| PE / DLL 검사 | 완료(정적) | 실행기와 DLL 모두 AMD64. 각 CUDA 폴더별 DLL/외부 의존성 조사. PE_DEPENDENCIES.json |
| Release EXE | 완료(교차 빌드) | Linux + .NET SDK 8.0.425 Roslyn + Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3. 최적화 x64/C# 5. RELEASE_BUILD.txt |
| Windows 테스트 코드 컴파일 | 완료(교차 빌드) | 실제 .NET Framework 4.8 참조로 CoreTests 빌드. WINDOWS_TEST_BUILD.txt. 실행하지 않음 |
| 핵심 로직 테스트 | 52 통과 / 0 실패 | Linux/.NET 8 실행. CORE_TESTS_LINUX.txt. 테스트 전용 JSON 어댑터 사용, 실제 Framework 직렬화 검증이 아님 |
| 실제 모델 파일 구조 | 완료 | 포함된 2.74GB GGUF에 실제 Core.cs 파서를 적용. qwen35 및 텐서 데이터 경계 검사 통과. 추론하지 않음 |
| Windows GUI | 미검증 | Windows 세션 없음. 화면 배치, DPI, 한글 폰트, 트레이 동작 실측 불가 |
| Windows CPU/CUDA 추론 | 미검증 | Windows/NVIDIA GPU 환경 없음. --help, --list-devices, 모델 로딩, 실제 생성 응답 실행 불가 |
| SI Copilot VSIX | 범위 밖 | 기존 소스 미제공. 제작·수정·설치 검증하지 않음 |

최종 ZIP 내 ASSET_MANIFEST.json은 각 자산의 출처·버전·게시자 해시 대조 여부 및 펼친 파일별 크기/SHA256을 기록합니다. SHA256SUMS.txt는 자신을 제외한 배포 파일 전체를 대상으로 합니다. 실제 Windows 동작 성공이나 추론 품질/속도를 보증하는 기록은 아닙니다.

## Linux에서 확인한 세부 범위

- 모델 0/1/다수의 탐색, mmproj/인코더/OCR 경로 제외, 분할 조각 누락/완비, 불안정한 복사 파일 제외.
- 실제 패키지 GGUF, HTML 오인 파일, 잘린 텐서 데이터 검출. 낯선 양자화 형식은 최소 경계만 검사하며 전체 검증은 llama.cpp에 위임합니다.
- 상대경로/한글/공백/따옴표, 폴더 이동 후 모델 ID 유지, Windows UNC·드라이브 경로 거부.
- 기본 자동 시작 false, 이전 자동 시작 true 설정 보존, CUDA 13.1 경로 보존과 CUDA 13.4 기본값.
- 자동 실행기 순서, 명시적 CUDA에서 CPU 강제 전환 금지, CPU fallback opt-out.
- 옵션 정확 일치, reasoning-format과 reasoning 구분, 지원하는 옵션만 생성, router 자동 로딩 비활성화.

이 테스트는 Windows 파일 공유 잠금, WinForms 이벤트 순서, 실제 .NET Framework JavaScriptSerializer, Windows Job Object 동작을 검증하지 않습니다. CORE_TESTS_SCOPE.txt를 함께 보세요.

## 승인된 Windows 환경에서 남은 확인

아래는 이번 환경에서 **수행하지 않은** 항목입니다. 외부망 PC에서 LM Studio나 모델을 실행하라는 준비 조건이 아닙니다.

1. 실행 정책을 그대로 둔 채 EXE 시작, 화면/DPI/트레이 확인. VC++/.NET/드라이버 전제 확인.
2. CPU 강제 모드 → 모델 로딩 → 준비 완료 → 연결 테스트. CUDA 12.4, CUDA 13.4 각각 실제 장치에서 반복.
3. GPU 비호환 시 자동 모드의 CPU fallback, fallback 해제, 명시적 CUDA 실패 상태 확인.
4. models 파일 추가/삭제/부분 복사/잘림/분할 모델/다수 모델을 확인. 선택만으로 실행 모델이 바뀌지 않는지 확인.
5. ‘한글 폴더 이름/공백’ 위치와 폴더 이동, 읽기 전용 위치에서 설정/로그 fallback 확인.
6. 다른 프로그램이 포트를 점유한 상태에서 시작 실패하는지 확인. 기존 프로그램이 그대로 살아 있어야 함.
7. 실제 생성 중 모델 전환 취소/승인, 시작 작업 취소, 반복 시작·중지, 정상 종료·작업 관리자 강제 종료. 본 Launcher가 시작한 프로세스 트리만 정리되는지 확인.
8. v1.1 설정 파일 적용과 경로 변경 유지. 외부 인터넷 접근이 불가능한 실제 업무망에서 운영 확인.

## 환경 제한 및 실패 기록

- 초기 환경: Ubuntu 24.04, Windows 세션/Visual Studio/MSBuild/.NET SDK 없음. 초기 여유 디스크 약 29GiB.
- `apt-get update && apt-get install -y mono-mcs mono-runtime ...`: 컨테이너 setgroups/seteuid 제약으로 실패. 권한·보안 정책을 바꾸지 않았습니다.
- 공식 .NET SDK 8.0.425를 작업 폴더에 내려받아 게시자 SHA512를 확인하고, 공식 .NET Framework 참조 NuGet을 사용하여 Roslyn으로 교차 빌드했습니다. SDK는 직원 배포본에 포함하지 않습니다.
- 첫 C# 5 빌드: 기존 WindowsHost.cs의 await-in-catch로 CS1985 발생. 제어 흐름을 바꿔 재빌드 성공.
- dotnet CLI 재실행 일부가 이 컨테이너의 Process.GetStat 오류로 실패. 같은 SDK의 csc.dll을 직접 호출하여 컴파일하고 테스트 DLL을 직접 실행했습니다. 정책 변경 없음.
- CUDA 13.4 고정 아카이브 EULA URL은 404. NVIDIA의 당시 v13.4 현행 EULA 전체를 내려받고 해시/확인일로 고정했습니다. 12.4는 12.4.0 아카이브 EULA 보존.
