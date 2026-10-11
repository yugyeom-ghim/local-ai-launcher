# Third-party notices and distribution scope

This package combines independently licensed components. It does not relicense NVIDIA or Microsoft binaries under MIT or Apache-2.0. Preserve this directory and the original runtime notices when making approved internal copies.

| Component | Terms included | Scope |
|---|---|---|
| llama.cpp b11429 | llama-LICENSE.txt, llama-source-notices/ | MIT; source commit d81235049384534c167caea52b85a694f6103d14 |
| LLVM OpenMP runtime | LICENSE-LLVM-OpenMP.txt and original in each runtime directory | Keep the full upstream notice, including its exceptions |
| Qwen3.5-4B / Unsloth GGUF | Qwen-LICENSE.txt | Model card identifies Apache-2.0; original model license copied from Qwen revision 851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a. GGUF bytes are unchanged |
| CUDA 12.4 DLLs | CUDA-12.4-EULA.html / .txt | NVIDIA SDK EULA + CUDA supplement and third-party obligations |
| CUDA 13.4 DLLs | CUDA-13.4-EULA.html / .txt | NVIDIA v13.4 current EULA snapshot retrieved 2026-10-08; its archive/13.4.0 URL was unavailable |
| Microsoft VC++ v14 installer | VC-REDIST-LICENSE.docx / .txt; VC-REDIST-TERMS.html | Official installer, version 14.51.36247.0; distribution and use remain subject to Microsoft terms |

The NVIDIA Attachment A lists the CUDA runtime and cuBLAS DLL families, including filename variations for version/architecture. This package contains only the release-matched cudart, cublas, cublasLt libraries for those families, within the application's runtime directories. Their bytes are not modified. CUDA's application distribution requirements and authorized employee/contractor provisions remain applicable; this is not a standalone general CUDA toolkit redistribution grant. NVIDIA drivers and unrelated SDK tools are not redistributed here.

Microsoft documents redistribution eligibility for licensed Visual Studio users. The organization must retain its applicable license rights when redistributing that prerequisite. The installer is not automatically executed; its own terms remain available to the authorized installer. Windows Authenticode verification was not performed in the Linux build environment.

Official reference URLs:
- https://github.com/ggml-org/llama.cpp/releases/tag/b11429
- https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/tree/e87f176479d0855a907a41277aca2f8ee7a09523
- https://huggingface.co/Qwen/Qwen3.5-4B/tree/851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a
- https://docs.nvidia.com/cuda/archive/12.4.0/eula/index.html
- https://docs.nvidia.com/cuda/eula/index.html
- https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist
- https://aka.ms/VCRedistLicense

The launcher source came from the user-provided v1.1 package. These third-party notices do not assert ownership or grant additional rights over that user-provided source. v1.2 source changes are described in README_KO.md.

## Optional model packs

Qwen3.5-2B, Qwen3.5-9B and Qwen3.8-27B: Alibaba Qwen, Apache-2.0. GGUF quantization: Unsloth. Exact revisions, hashes and original license URLs are recorded in `src/Resources/ModelCatalog.json`. Each released pack includes the verified original license. The quantized weights are distributed without further tensor modification.
