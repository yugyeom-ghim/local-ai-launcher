# Offline installer validation

Run: https://github.com/yugyeom-ghim/si_local_model/actions/runs/38128563074
Commit: fcd11908de928e37eda5bbb64f860c671734a507
Windows Server 2022: real setup/reinstall/uninstall executed in a temporary Korean path.

PASS running Launcher blocks installation without being terminated
PASS native Windows unattended installation succeeds
PASS Launcher EXE installed
PASS Satrec Initiative icon installed
PASS all 270 installed payload files match SHA256
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

- Installer payload contains the actual Qwen model, CPU and both CUDA runtimes. No model inference executed.
- Setup script has no network/download operation, firewall change or execution-policy change.
- This test did not disconnect the runner's network and does not claim packet-capture verification.
- Interactive wizard/DPI, Windows desktop model inference and company OpenCode/MCP remain unverified.
- Installer/compiler and Launcher binaries are not company-code-signed; existing execution policy applies.
