# Local API contract — v1.3

SI Local AI Launcher starts a local llama.cpp process. SI Copilot is a separate Visual Studio VSIX; its source was not supplied and no VSIX was created or changed.

Default endpoint: `http://127.0.0.1:1234/v1`. The selected port may differ. Copy the **running** model ID from “로컬 API 연결 정보”; the model ID includes a stable relative-path hash and is not just the GGUF filename.

- `GET /health`: process readiness only; this alone does not establish model usability.
- `GET /v1/models`: verify that the running alias is returned.
- `POST /v1/chat/completions`: actual text generation. The launcher sends a small diagnostic request before displaying 준비 완료. A manual 연결 테스트 repeats the request.
- Default single-model operation keeps all other models unloaded. File addition and UI selection do not restart the server. Explicit start/restart cancels the owned process's in-flight requests after confirmation.
- Diagnostic requests disable thinking for that request only, use `stream: false`, and require nonempty `choices[0].message.content`. They do not measure recommendation quality, token throughput or VSIX behavior.

Example request (replace the model value with the displayed running ID):

```json
{
  "model": "DISPLAYED_RUNNING_MODEL_ID",
  "messages": [{"role": "user", "content": "Reply with OK only."}],
  "stream": false,
  "max_tokens": 24,
  "chat_template_kwargs": {"enable_thinking": false},
  "reasoning_effort": "none"
}
```

The launcher binds only to IPv4 loopback. Optional `Settings.ApiKeyRequired` enables bearer authentication through the runtime's `--api-key-file`. Existing settings default to false. The key is randomly generated once in the effective state directory as `local-api-key.txt`, never in settings.json. Internal health/models/load/chat requests include `Authorization: Bearer <local key>` when enabled. /health is public in the pinned server; it is not an authentication test. Before ready, the launcher requires unauthenticated and incorrect-key GET /v1/models requests to return 401, followed by successful authenticated model listing and generation. Unsupported key-file authentication or failed checks stop the server; authentication is never silently dropped.

`ThinkingOff` remains the persisted compatibility field. true emits `--reasoning off`, false now emits `--reasoning on`; older supported runtimes receive chat-template kwargs. The switch is a server default for supporting templates, not an enforcement policy. A request may override it. Unsupported runtimes are labeled accordingly. The diagnostic request remains explicitly non-thinking and does not validate reasoning output.

The API connection UI uses an in-memory snapshot from the ready, owned process, rather than unsaved form fields. Stopped/busy servers cannot export credentials/configuration. OpenCode exports use `@ai-sdk/openai-compatible`, `provider.si-local`, actual base URL/model ID/context, and a bounded output limit. Auth-enabled exports omit the key; use OpenCode `/connect` for provider ID si-local. Auth-disabled exports use the non-secret placeholder si-local-no-key. Existing OpenCode files are not automatically edited.

Connection state adds ApiKeyRequired, Thinking and Context; it never includes the bearer token. API keys are not remote-company credentials. Remote/WSL/container clients are not configured by this launcher.

`state/connection.json` is diagnostic state, not an agreed VSIX integration protocol. Its content can become stale after an abrupt termination. No consumer should trust it for process ownership, authentication, or liveness. The effective state directory is beside the EXE when writable, otherwise `%LOCALAPPDATA%/SI.LocalAI/<installation-path-hash>`. The actual directory opens through 진단 로그 → 로그 폴더 열기.
