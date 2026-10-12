# Offline installer validation

Run: https://github.com/yugyeom-ghim/local-ai-launcher/actions/runs/38187867066
Commit: 2357be9224a8dcec379645bd397b5c006e0fedfd
Windows Server 2022: single EXE setup/reinstall/uninstall executed in a temporary Korean path with outbound network blocked.

PASS network probe works before isolation
PASS installer/test outbound connections blocked; GitHub control connection retained
PASS installer source folder contains only one EXE
PASS running Launcher blocks installation without being terminated
PASS native Windows unattended installation succeeds
PASS Launcher EXE installed
PASS legacy executable and shortcuts removed
PASS application icon installed
PASS all 305 installed payload files match SHA256
PASS Start menu shortcut created
PASS optional desktop shortcut created
PASS shortcut targets the installed Launcher
PASS reinstallation succeeds at the existing location
PASS reinstallation preserves existing settings, local key and added files
PASS native Windows uninstall succeeds
PASS uninstall removes the Launcher
PASS uninstall removes the bundled model
PASS uninstall preserves personal settings, local key and user-added files
PASS uninstall removes created shortcuts
PASS runner network policy restored after installation checks

- Installer payload contains the actual Qwen model, CPU and both CUDA runtimes. No model inference executed.
- Setup script has no network/download operation, firewall change or execution-policy change.
- Installer/test outbound connections were blocked by temporary CI firewall policy; only GitHub runner control processes (HTTPS) and the DNS service were allowed. The original policy was restored in finally. A TCP probe checked connectivity before, during and after isolation. No packet capture performed.
- Interactive wizard/DPI, Windows desktop model inference and company OpenCode/MCP remain unverified.
- Installer/compiler and Launcher binaries are not company-code-signed; existing execution policy applies.
