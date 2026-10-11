# 사양별 모델

| PC 사양 | 모델 | 다운로드 | 시작 문맥 |
|---|---|---:|---:|
| CPU · RAM 8GB 이상 | Qwen3.5 2B Q4_K_M | 1.28GB | 4K |
| GPU 6–8GB · 4060 Ti 8GB | Qwen3.5 4B Q4_K_M (기본 포함) | 2.74GB | 4K |
| GPU 12–16GB · 4060 Ti 16GB | Qwen3.5 9B Q4_K_M | 5.68GB | 8K |
| GPU 24GB 이상 · RAM 32GB 이상 | Qwen3.8 27B UD-Q4_K_M | 16.46GB | 8K |

단일 GPU와 문맥 4K–8K 기준의 메모리 추정입니다. GPU 추론 성능을 실측한 값은 아닙니다.
9B는 8GB GPU에서도 문맥 4K로 시도할 수 있지만 다른 프로그램의 GPU 사용량에 따라 부족할 수 있습니다.
24GB 이상 GPU에서는 27B를, 속도가 더 중요하면 9B를 선택하세요.

## 추가

1. 인터넷 PC에서 [모델 다운로드 도구](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest/download/Internet-PC-Download-Models.exe)를 실행해 모델을 선택합니다. `0`은 모두 받기입니다.
2. 완성된 모델 팩 폴더를 반입합니다.
3. Launcher에서 **모델 팩 설치** → 팩의 `.modelpack.json` 선택 → 설치된 모델 선택 → 시작.

설치에는 모델 크기만큼의 빈 공간이 추가로 필요합니다. 팩은 현재 설정된 모델 폴더에 설치되며, SHA256 검사가 끝난 모델만 목록에 표시됩니다. 설치가 끝난 팩 파일은 보관할 필요가 없으면 삭제해도 됩니다.
문맥은 **경로 · 고급 설정**에서 변경합니다. 사고 모드는 모델 시작/재시작 때 적용합니다.

## 출처

2026-10-11 확인. 원본은 [Qwen3.5 2B](https://huggingface.co/Qwen/Qwen3.5-2B), [Qwen3.5 9B](https://huggingface.co/Qwen/Qwen3.5-9B), [Qwen3.8 27B](https://huggingface.co/Qwen/Qwen3.8-27B), GGUF 변환은 Unsloth입니다. 라이선스는 Apache-2.0입니다.
이 팩은 텍스트/API용 모델이며 이미지 인코더는 포함하지 않습니다.

고정 revision·파일 크기·SHA256·라이선스 URL: [ModelCatalog.json](src/Resources/ModelCatalog.json).
실제 다운로드·Windows 설치 검증 기록은 릴리스의 `MODEL_PACK_VALIDATION.json`에 있습니다.
