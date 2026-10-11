using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SILocalAI
{
    public static class LocalApiKeys
    {
        public static string FilePath(string state) { return Path.Combine(state, "local-api-key.txt"); }
        public static string LoadOrCreate(string state)
        {
            string path = FilePath(state);
            if (!File.Exists(path))
            {
                var bytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
                Paths.AtomicWrite(path, "si-local-" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant() + "\r\n");
            }
            string key = File.ReadAllText(path).Trim();
            // Exactly one bearer token; never silently accept a comment/empty key file.
            if (!Regex.IsMatch(key, @"\A[A-Za-z0-9_-]{32,160}\z"))
                throw new InvalidDataException("로컬 API 키 파일 형식이 올바르지 않습니다. state/local-api-key.txt를 확인하세요. 키를 자동 교체하지 않았습니다.");
            return key;
        }
        public static void AddArguments(List<string> args, Settings settings, Capabilities caps, string keyPath)
        {
            if (!settings.ApiKeyRequired) return;
            if (!caps.Has("--api-key-file")) throw new InvalidOperationException("이 실행기는 API 키 파일 인증을 지원하지 않습니다. 인증을 끄지 않고 시작을 중단했습니다. 제공된 llama.cpp 실행기를 사용하세요.");
            args.AddRange(new[] { "--api-key-file", keyPath });
        }
    }

    // An in-memory snapshot of a ready server, never persisted as connection.json.
    public sealed class ClientConnection
    {
        public string BaseUrl, ModelId, ModelName, ApiKey, Thinking;
        public int Context;
        public bool ApiKeyRequired;
    }
    public static class ClientConfig
    {
        public const string ProviderId = "si-local";
        public static string OpenCode(ClientConnection c)
        {
            if (c == null || String.IsNullOrEmpty(c.ModelId)) throw new InvalidOperationException("모델을 먼저 시작하세요.");
            var options = new Dictionary<string, object> { { "baseURL", c.BaseUrl } };
            // With authentication enabled, /connect owns the credential. No secret in exported JSON.
            if (!c.ApiKeyRequired) options["apiKey"] = "si-local-no-key";
            var model = new Dictionary<string, object> {
                { "name", c.ModelName + " (local)" },
                { "limit", new { context = c.Context, output = Math.Max(128, Math.Min(4096, c.Context / 4)) } }
            };
            var provider = new Dictionary<string, object> {
                { "npm", "@ai-sdk/openai-compatible" }, { "name", "Local AI" },
                { "options", options }, { "models", new Dictionary<string, object> { { c.ModelId, model } } }
            };
            var config = new Dictionary<string, object> {
                { "$schema", "https://opencode.ai/config.json" },
                { "provider", new Dictionary<string, object> { { ProviderId, provider } } },
                { "model", ProviderId + "/" + c.ModelId }
            };
            return Pretty(Json.Write(config));
        }
        static string Pretty(string json)
        {
            var b = new System.Text.StringBuilder(); int depth = 0; bool quoted = false, escaped = false;
            foreach (char ch in json)
            {
                if (quoted) { b.Append(ch); if (escaped) escaped = false; else if (ch == '\\') escaped = true; else if (ch == '"') quoted = false; continue; }
                if (ch == '"') { quoted = true; b.Append(ch); }
                else if (ch == '{' || ch == '[') { b.Append(ch).Append("\r\n"); depth++; b.Append(' ', depth * 2); }
                else if (ch == '}' || ch == ']') { b.Append("\r\n"); depth--; b.Append(' ', depth * 2).Append(ch); }
                else if (ch == ',') b.Append(",\r\n").Append(' ', depth * 2);
                else if (ch == ':') b.Append(": "); else b.Append(ch);
            }
            return b.Append("\r\n").ToString();
        }
        public static string Guide(ClientConnection c)
        {
            return "OpenCode 연결\r\n\r\n" +
                "1. 내보낸 JSON을 opencode.json으로 저장합니다. 기존 설정에는 provider.si-local을 병합합니다.\r\n" +
                (c.ApiKeyRequired ? "2. /connect → Other → si-local에 로컬 API 키를 입력합니다.\r\n" : "2. API 키 인증: 사용 안 함\r\n") +
                "3. /models에서 Local AI 모델을 선택합니다.\r\n\r\n" +
                "기본 주소: " + c.BaseUrl + "\r\n모델 ID: " + c.ModelId + "\r\n사고 모드: " + c.Thinking + "\r\n\r\n" +
                "문맥 부족 오류가 나면 16K 설정을 적용하세요. 메모리 사용량이 늘어납니다.\r\n" +
                "모델·포트·문맥 변경 후에는 설정을 다시 내보냅니다.\r\n";

        }
    }
}
