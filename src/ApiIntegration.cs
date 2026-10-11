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
                { "npm", "@ai-sdk/openai-compatible" }, { "name", "SI Local AI" },
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
            return "OpenCode 로컬 모델 연결\r\n\r\n" +
                "1. Launcher에서 모델을 시작하고 ‘준비 완료’를 확인합니다.\r\n" +
                "2. 새 프로젝트는 내보낸 설정을 프로젝트 폴더의 opencode.json으로 사용합니다.\r\n" +
                "   기존 opencode.json / opencode.jsonc가 있으면 provider.si-local 항목을 병합하고 사용할 모델을 선택하세요. 기존 사내 설정을 덮어쓰지 마세요.\r\n" +
                (c.ApiKeyRequired ? "3. OpenCode에서 /connect → Other → 제공자 ID si-local → 앱의 ‘API 키 복사’ 값을 입력합니다.\r\n" : "3. API 키 인증이 꺼져 있습니다. 별도 /connect 등록 없이 사용할 수 있습니다.\r\n") +
                "4. OpenCode를 다시 열고 /models → SI Local AI 모델을 선택합니다.\r\n\r\n" +
                "기본 주소: " + c.BaseUrl + "\r\n모델 ID: " + c.ModelId + "\r\n사고 모드 기본값: " + c.Thinking + "\r\n\r\n" +
                "Launcher와 클라이언트를 같은 Windows PC에서 실행하세요. 127.0.0.1은 현재 PC를 가리킵니다. WSL/컨테이너/원격 환경은 별도 네트워크 구성이 필요하며 이 앱에서 자동 변경하지 않습니다.\r\n" +
                "사내 배포 OpenCode의 사용자 제공자 허용 여부와 승인된 패키지 구성이 필요합니다. 이 앱은 OpenCode를 설치하거나 패키지를 다운로드하지 않습니다.\r\n" +
                "API 연결과 코딩 도구 실행은 별개입니다. 도구 호출 품질은 모델·문맥 크기·OpenCode 버전에 따라 달라집니다. 4,096 토큰으로 문맥 부족 오류가 나면 경로·고급 설정에서 16,384 이상을 검토하세요. 메모리 사용량이 늘어납니다.\r\n" +
                "사고 ON/OFF는 서버 기본값입니다. 지원되는 템플릿에서 적용되며 클라이언트가 요청별 옵션을 보내면 달라질 수 있습니다. 연결 테스트는 짧은 응답을 위해 사고 OFF로 한 번 검사합니다.\r\n\r\n" +
                "다른 OpenAI 호환 클라이언트 / SI코파일럿: 기본 주소·모델 ID·API 키를 해당 확장 설정에 입력합니다. SI코파일럿은 별도 VSIX이며 이 Launcher는 VSIX를 설치하거나 수정하지 않습니다.\r\n" +
                "로컬 키는 이 PC의 llama-server 인증용입니다. 사내 서버의 API 키를 재사용할 필요가 없습니다.\r\n";
        }
    }
}
