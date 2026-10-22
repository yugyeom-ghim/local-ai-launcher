# Local API

Local AI Launcher runs llama.cpp on `127.0.0.1`. Default base URL: `http://127.0.0.1:1234/v1`.
Use the running model ID shown in the app; it includes a stable relative-path hash.

| Endpoint | Purpose |
|---|---|
| `GET /health` | Process readiness |
| `GET /v1/models` | Running model alias |
| `POST /v1/chat/completions` | Text generation |

The Launcher reports ready after a nonempty generation response. Diagnostic requests set `stream: false`, `max_tokens: 24`, `chat_template_kwargs.enable_thinking: false`, and `reasoning_effort: none`.

## Authentication

`Settings.ApiKeyRequired` enables bearer authentication with `--api-key-file`. The key is stored in `state/local-api-key.txt`.
Startup checks require 401 responses for missing/incorrect keys, then successful authenticated model listing and generation. `/health` is public in the pinned server.
Unsupported key-file authentication stops startup. Logs, exported configuration and `connection.json` exclude the key.

## Browser clients / CORS

The launcher no longer forces `--cors-origins localhost`. The pinned llama.cpp b11429 server's default allows any origin, including a Confluence page. For supported runtimes, `--cors-headers "*, Content-Type, Authorization"` also allows JSON requests and explicit bearer authentication; `Authorization` is not covered by the header wildcard.
The listener remains `127.0.0.1`, and optional API-key authentication remains enforced. Other websites can now call the local API too; enable the API key when access should require a token. Restart the server after updating the launcher. Inherited `LLAMA_ARG_*` settings are removed as before.
Preflight OPTIONS and HTTP responses are checked against the pinned Windows server without loading a model. The actual Confluence page, browser local-network permissions, CSP and model inference are separate checks.

## Thinking

Persisted `ThinkingOff` maps to `--reasoning off/on`, with template kwargs for older supported runtimes. This is a server default; request options can override it. Restart to apply changes.

## OpenCode

Exports use `@ai-sdk/openai-compatible`, the running base URL/model/context and provider ID `si-local` for compatibility. Authenticated exports omit the key; register it through `/connect`. Keyless exports use `si-local-no-key`.
Merge `provider.si-local` into existing configuration to retain MCP settings.

## Context

Presets cover 4K through 256K; custom integer context accepts 512 through Int32.MaxValue. The default remains 4096 and saved values are preserved. GGUF `[architecture].context_length` is displayed as a model reference, not a hardware capacity guarantee; missing or unusable metadata leaves it unknown. No automatic RoPE/YaRN expansion is added.

The selected context is passed as `-c` and, where supported, `--fit-ctx` to prevent silent reduction by memory fitting. Restart to apply, then re-export OpenCode configuration. Exported `limit.context` uses the active server configuration, not unsaved UI edits. Large-context model inference still requires validation on the target PC.

## State

State is stored beside the EXE, falling back to `%LOCALAPPDATA%/SI.LocalAI/<installation-path-hash>` when unwritable.
`connection.json` is diagnostic data and can be stale after an abrupt exit. Use the live endpoints for readiness.
