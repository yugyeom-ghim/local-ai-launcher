# Windows GitHub Actions validation

Version: 1.6.0
Commit: 7954443fc55e88cf342c8e1ecd6ae053989edd06
Run: https://github.com/yugyeom-ghim/local-ai-launcher/actions/runs/38150494774

- Windows Server 2022 runner: native .NET Framework x64 Release build succeeded.
- Core regression checks: 62 passed.
- API request/credential logic checks: 7 passed.
- Download/assembly checks: 9 passed, including HTTP Range resume and ignored-Range restart.
- No GGUF, llama-server, CUDA runtime or model inference was executed in this job.
- Native WinForms startup, idle CPU, UI heartbeat, fixed-size renders, simulated scaling and local file change checks passed.
- Physical DPI transitions, CPU/CUDA inference and company OpenCode/MCP integration remain unverified.
- This ZIP contains the Windows-built EXE. Previous Linux reports are historical records.
