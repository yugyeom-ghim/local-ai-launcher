# 모델 추가

**모델 추가 → 폴더 열기**에서 GGUF를 복사하면 목록에 나타납니다. 사용할 모델을 선택해 시작하세요.

| 메모리 기준 | 모델 | 크기 |
|---|---|---:|
| CPU · RAM 8GB+ | Qwen3.5 2B Q4_K_M | 1.28GB |
| VRAM 6–8GB · RAM 16GB+ | Qwen3.5 4B Q4_K_M (기본 포함) | 2.74GB |
| VRAM 12–16GB · RAM 16GB+ | Qwen3.5 9B Q4_K_M | 5.68GB |

문맥 4K–8K 기준 추정치입니다. 다른 GGUF도 직접 추가할 수 있습니다.

## 다운로드

인터넷 PC에서 [모델 다운로드 도구](https://github.com/yugyeom-ghim/local-ai-launcher/releases/latest/download/Internet-PC-Download-Models.exe)로 2B 또는 9B를 선택합니다.
완성된 `Local_AI_Models/models`의 GGUF를 앱의 모델 폴더로 복사합니다. 라이선스는 `Local_AI_Models/licenses`에 보관됩니다.
다운로드와 합치기에 모델 크기의 약 2배 공간이 필요합니다.

출처·고정 버전·SHA256·라이선스: [ModelCatalog.json](src/Resources/ModelCatalog.json). Qwen3.5 모델은 Apache-2.0이며 텍스트/API용 GGUF를 제공합니다.
