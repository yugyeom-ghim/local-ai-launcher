// Local AI Launcher. C# 5 / .NET Framework 4.8. No NuGet packages.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace SILocalAI
{
    public sealed class Settings
    {
        public int Schema = 1;
        public string AssetRoot = "offline-assets/ai";
        public string ModelsPath = "models";
        public string CpuPath = "bin/win-cpu-x64";
        public string Cuda12Path = "bin/win-cuda-12.4-x64";
        public string Cuda13Path = "bin/win-cuda-13.1-x64";
        public string Cuda134Path = "bin/win-cuda-13.4-x64";
        public string Backend = "auto";
        public string Mode = "auto";
        public string Model = "";
        public int Port = 1234;
        public int Context = 4096;
        public int Threads = 0;
        public int LoadTimeout = 180;
        public int GpuLayers = -1; // -1 = runtime auto / full offload on old builds.
        public bool AutoStart = false; // v1.1: first use is select -> start; saved v1.0 value is retained.
        public bool ThinkingOff = true; // Existing settings key retained. false now explicitly requests ON.
        public bool ApiKeyRequired = false; // Preserve existing clients until the user enables authentication.
        public bool OpenBrowser = false;
        public bool CpuFallback = true;
        public Settings Clone() { return Json.Read<Settings>(Json.Write(this)); }
        public void Validate()
        {
            if (Schema != 1) throw new InvalidDataException("지원하지 않는 설정 버전입니다: " + Schema);
            if (Port < 1024 || Port > 65535) throw new ArgumentException("포트는 1024~65535 범위여야 합니다.");
            if (Context < 512) throw new ArgumentException("문맥은 512 토큰 이상이어야 합니다.");
            if (Threads < 0 || Threads > 512) throw new ArgumentException("CPU 스레드는 0~512 범위여야 합니다.");
            if (LoadTimeout < 30 || LoadTimeout > 900) throw new ArgumentException("모델 로딩 제한은 30~900초 범위여야 합니다.");
            if (GpuLayers < -1 || GpuLayers > 999) throw new ArgumentException("GPU 레이어는 -1(자동) 또는 0~999여야 합니다.");
            if (!new[] { "auto", "cpu", "cuda12", "cuda13", "cuda134" }.Contains(Backend)) throw new ArgumentException("실행기 설정을 확인하세요.");
            if (!new[] { "auto", "single", "router" }.Contains(Mode)) throw new ArgumentException("서버 모드 설정을 확인하세요.");
            foreach (string p in new[] { AssetRoot, ModelsPath, CpuPath, Cuda12Path, Cuda13Path, Cuda134Path }) Paths.CheckRelative(p);
        }
    }

    public static class Json
    {
        public static string Write(object value) { return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(value); }
        public static T Read<T>(string text) { return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Deserialize<T>(text); }
        public static Dictionary<string, object> Object(string text) { return Read<Dictionary<string, object>>(text); }
        public static string Text(Dictionary<string, object> o, string key)
        { object v; return o != null && o.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : ""; }
        public static IEnumerable<Dictionary<string, object>> Rows(string text)
        {
            var root = Object(text); object data;
            if (root == null || !root.TryGetValue("data", out data)) throw new InvalidDataException("모델 API 응답에 data가 없습니다.");
            var arr = data as System.Collections.IEnumerable;
            if (arr == null) throw new InvalidDataException("잘못된 모델 목록 형식입니다.");
            foreach (var row in arr) { var dict = row as Dictionary<string, object>; if (dict != null) yield return dict; }
        }
    }

    public static class Paths
    {
        public static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        public static void CheckRelative(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Any(Char.IsControl) || Path.IsPathRooted(value) || value.StartsWith("\\") || value.Contains(":") || value.Contains("\""))
                throw new ArgumentException("경로는 드라이브 이름 없이 상대경로로 지정하세요: " + value);
        }
        public static string Full(string root, string relative)
        {
            CheckRelative(relative);
            return Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        }
        public static string Relative(string root, string path)
        {
            if (String.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return ".";
            var baseUri = new Uri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var target = new Uri(Path.GetFullPath(path));
            if (!String.Equals(baseUri.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("같은 드라이브의 폴더를 선택하세요.");
            string r = Uri.UnescapeDataString(baseUri.MakeRelativeUri(target).ToString());
            CheckRelative(r);
            if (r.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("실행 도구와 자산 폴더는 같은 드라이브에 두세요.");
            return r;
        }
        public static string Runtime(string root, string path)
        { try { return Relative(root, path); } catch (ArgumentException) { return Path.GetFullPath(path); } }
        public static string Quote(string arg)
        {
            // Windows CommandLineToArgvW/CRT quoting. No cmd.exe or shell evaluation.
            if (arg == null) arg = "";
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1); b.Append('"'); slashes = 0; continue; }
                b.Append('\\', slashes); slashes = 0; b.Append(c);
            }
            b.Append('\\', slashes * 2); b.Append('"'); return b.ToString();
        }
        public static string Command(IEnumerable<string> args) { return String.Join(" ", args.Select(Quote)); }
        public static void AtomicWrite(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static IEnumerable<string> Files(string root, string pattern, int depth, Action<string> warn)
        {
            if (!Directory.Exists(root)) yield break;
            var queue = new Queue<Tuple<string, int>>(); queue.Enqueue(Tuple.Create(root, 0)); int count = 0;
            while (queue.Count > 0)
            {
                var item = queue.Dequeue(); string[] files = new string[0], dirs = new string[0];
                try { files = Directory.GetFiles(item.Item1, pattern); if (item.Item2 < depth) dirs = Directory.GetDirectories(item.Item1); }
                catch (Exception e) { if (warn != null) warn("폴더 읽기 실패: " + item.Item1 + " / " + e.Message); }
                foreach (string file in files)
                {
                    if (++count > 10000) throw new InvalidDataException("파일이 너무 많습니다. 자산 폴더를 좁혀 주세요(최대 10,000개).");
                    yield return file;
                }
                foreach (string dir in dirs)
                {
                    try
                    {
                        string name = Path.GetFileName(dir).ToLowerInvariant();
                        if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                        if (new[] { "ocr", "runtime", "cache", ".git", "node_modules" }.Contains(name)) continue;
                        queue.Enqueue(Tuple.Create(dir, item.Item2 + 1));
                    }
                    catch (Exception e) { if (warn != null) warn("하위 폴더 제외: " + dir + " / " + e.Message); }
                }
            }
        }
    }

    public sealed class ModelInfo
    {
        public string Id, Name, FullPath, RelativePath, Architecture;
        public long Bytes;
        public int? ContextLength; // GGUF training context; not a hardware capacity limit.
        public override string ToString() { return Name + "  (" + (Bytes / 1073741824.0).ToString("0.00") + " GB)"; }
    }
    public sealed class BackendInfo
    {
        public string Id, Label, Folder, Exe, Error;
        public bool IsGpu { get { return Id != "cpu"; } }
        public override string ToString() { return Label; }
    }
    public sealed class Inventory
    {
        public List<ModelInfo> Models = new List<ModelInfo>();
        public List<BackendInfo> Backends = new List<BackendInfo>();
        public List<string> Warnings = new List<string>();
        public List<string> PendingFiles = new List<string>();
    }
    public static class Discovery
    {
        static readonly Regex Shard = new Regex(@"^(.*)-(\d{5})-of-(\d{5})\.gguf$", RegexOptions.IgnoreCase);
        public static Inventory Scan(string packageRoot, Settings settings)
        { return Scan(packageRoot, settings, null); }
        public static Inventory Scan(string packageRoot, Settings settings, Func<string, bool> ready)
        {
            settings.Validate(); var result = new Inventory(); Action<string> warn = result.Warnings.Add;
            string assets = Paths.Full(packageRoot, settings.AssetRoot);
            foreach (var spec in new[] {
                new[] { "cpu", "CPU", settings.CpuPath },
                new[] { "cuda12", "CUDA 12.4", settings.Cuda12Path },
                new[] { "cuda13", "CUDA 13.1 (기존 설정)", settings.Cuda13Path },
                new[] { "cuda134", "CUDA 13.4", settings.Cuda134Path } })
            {
                var b = new BackendInfo { Id = spec[0], Label = spec[1], Folder = Paths.Full(assets, spec[2]), Exe = "", Error = "" };
                var files = Paths.Files(b.Folder, "llama-server.exe", 3, warn).ToList();
                if (files.Count == 1) b.Exe = files[0];
                else if (files.Count == 0) b.Error = "llama-server.exe 없음";
                else b.Error = "실행 파일 " + files.Count + "개 발견. 자산 설정에서 한 버전의 폴더를 지정하세요.";
                result.Backends.Add(b);
            }
            string modelRoot = Paths.Full(assets, settings.ModelsPath);
            foreach (string file in Paths.Files(modelRoot, "*.gguf", 6, warn).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string name = Path.GetFileName(file);
                    if (Regex.IsMatch(name, @"^(mmproj|projector|mtp)([-_.]|$)", RegexOptions.IgnoreCase)) continue;
                    var shard = Shard.Match(name);
                    if (shard.Success && Int32.Parse(shard.Groups[2].Value) != 1) continue;
                    if (ready != null && !ready(file)) { result.PendingFiles.Add(name); continue; }
                    long bytes = new FileInfo(file).Length;
                    if (shard.Success)
                    {
                        int index = Int32.Parse(shard.Groups[2].Value), count = Int32.Parse(shard.Groups[3].Value);
                        if (index != 1) continue;
                        if (count < 1 || count > 1000) throw new InvalidDataException("분할 파일 개수 범위 오류");
                        bytes = 0; bool partsReady = true;
                        for (int i = 1; i <= count; i++)
                        {
                            string part = Path.Combine(Path.GetDirectoryName(file), shard.Groups[1].Value + "-" + i.ToString("D5") + "-of-" + count.ToString("D5") + ".gguf");
                            if (!File.Exists(part)) throw new InvalidDataException("분할 모델 누락: " + Path.GetFileName(part));
                            if (ready != null && !ready(part)) { partsReady = false; continue; }
                            Gguf.Header(part); bytes += new FileInfo(part).Length;
                        }
                        if (!partsReady) { result.PendingFiles.Add(name + " (분할 파일 확인 중)"); continue; }
                    }
                    var metadata = Gguf.ReadHeader(file); string arch = metadata.Architecture;
                    if (new[] { "clip", "bert", "nomic-bert", "t5", "wavtokenizer-dec", "whisper" }.Contains(arch.ToLowerInvariant()))
                    { warn("텍스트 생성용이 아닌 모델 제외: " + name + " [" + arch + "]"); continue; }
                    string rel = Paths.Relative(modelRoot, file);
                    string stem = shard.Success ? shard.Groups[1].Value : Path.GetFileNameWithoutExtension(file);
                    string slug = Regex.Replace(stem, @"[^a-zA-Z0-9._-]+", "-").Trim('-', '.');
                    if (slug.Length == 0) slug = "local-model";
                    if (slug.Length > 80) slug = slug.Substring(0, 80);
                    // Always include relative-path hash: identity is stable after moving the whole package.
                    string id = slug + "-" + Paths.Hash(rel.ToLowerInvariant()).Substring(0, 8);
                    result.Models.Add(new ModelInfo { Id = id, Name = stem, FullPath = file, RelativePath = rel, Architecture = arch, ContextLength = metadata.ContextLength, Bytes = bytes });
                }
                catch (Exception e) { warn("모델 제외: " + Path.GetFileName(file) + " / " + e.Message); }
            }
            return result;
        }
        public static ModelInfo Choose(List<ModelInfo> models, string saved)
        {
            return models.FirstOrDefault(x => x.RelativePath == saved)
                ?? models.FirstOrDefault(x => Regex.IsMatch(x.Name, @"Qwen3\.5-4B.*Q4_K_M", RegexOptions.IgnoreCase))
                ?? models.OrderBy(x => x.Bytes).ThenBy(x => x.RelativePath).FirstOrDefault();
        }
        public static List<BackendInfo> Order(Inventory inv, Settings s)
        {
            string[] ids = s.Backend == "auto" ? (s.CpuFallback ? new[] { "cuda12", "cuda134", "cuda13", "cpu" } : new[] { "cuda12", "cuda134", "cuda13" }) : new[] { s.Backend };
            return ids.Select(id => inv.Backends.FirstOrDefault(x => x.Id == id)).Where(x => x != null && !String.IsNullOrEmpty(x.Exe)).ToList();
        }
    }
    public sealed class GgufMetadata
    {
        public string Architecture;
        public int? ContextLength;
    }

    public static class Gguf
    {
        // Bounded metadata/tensor-directory validation. Tensor bytes are not loaded or hashed here.
        static void Need(BinaryReader r, long count)
        { if (count < 0 || count > r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException("GGUF 메타데이터가 잘렸습니다."); }
        static string Str(BinaryReader r)
        {
            ulong n = r.ReadUInt64(); if (n > 4 * 1024 * 1024) throw new InvalidDataException("GGUF 문자열 길이 제한 초과");
            Need(r, (long)n); return Encoding.UTF8.GetString(r.ReadBytes((int)n));
        }
        static void Skip(BinaryReader r, uint type, int depth)
        {
            if (depth > 4) throw new InvalidDataException("GGUF 배열 중첩 제한 초과");
            int size;
            switch (type)
            {
                case 0: case 1: case 7: size = 1; break;
                case 2: case 3: size = 2; break;
                case 4: case 5: case 6: size = 4; break;
                case 10: case 11: case 12: size = 8; break;
                case 8:
                    ulong len = r.ReadUInt64(); if (len > 64 * 1024 * 1024) throw new InvalidDataException("GGUF 길이 제한 초과");
                    Need(r, (long)len); r.BaseStream.Seek((long)len, SeekOrigin.Current); return;
                case 9:
                    uint t = r.ReadUInt32(); ulong n = r.ReadUInt64();
                    if (n > 2000000) throw new InvalidDataException("GGUF 배열 크기 제한 초과");
                    for (ulong i = 0; i < n; i++) Skip(r, t, depth + 1); return;
                default: throw new InvalidDataException("알 수 없는 GGUF 메타데이터 형식: " + type);
            }
            Need(r, size); r.BaseStream.Seek(size, SeekOrigin.Current);
        }
        public static string Header(string file) { return ReadHeader(file).Architecture; }
        public static GgufMetadata ReadHeader(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var r = new BinaryReader(stream))
            {
                if (stream.Length < 24 || r.ReadUInt32() != 0x46554747) throw new InvalidDataException("GGUF 파일이 아닙니다. 다운로드 오류/HTML 파일인지 확인하세요.");
                uint version = r.ReadUInt32(); if (version != 2 && version != 3) throw new InvalidDataException("지원하지 않는 GGUF 버전: " + version);
                ulong tensors = r.ReadUInt64();
                if (tensors == 0 || tensors > 1000000) throw new InvalidDataException("GGUF 텐서 개수 범위 오류");
                ulong count = r.ReadUInt64(); if (count > 100000) throw new InvalidDataException("GGUF 메타데이터 개수 제한 초과");
                string architecture = "unknown"; uint alignment = 32;
                var contexts = new Dictionary<string, int>(StringComparer.Ordinal);
                for (ulong i = 0; i < count; i++)
                {
                    if (stream.Position > 64 * 1024 * 1024) throw new InvalidDataException("GGUF 메타데이터 길이 제한 초과");
                    string key = Str(r); uint type = r.ReadUInt32();
                    if (key == "general.architecture" && type == 8) architecture = Str(r);
                    else if (key == "general.alignment" && type == 4) alignment = r.ReadUInt32();
                    else if (key.EndsWith(".context_length", StringComparison.Ordinal) && (type == 4 || type == 10))
                    {
                        ulong value = type == 4 ? r.ReadUInt32() : r.ReadUInt64();
                        if (value > 0 && value <= Int32.MaxValue) contexts[key] = (int)value;
                    }
                    else Skip(r, type, 0);
                }
                if (alignment == 0 || alignment > 1048576 || (alignment & (alignment - 1)) != 0) throw new InvalidDataException("GGUF 정렬 값 오류");
                ulong minimumPayload = 0;
                for (ulong i = 0; i < tensors; i++)
                {
                    Str(r); uint dimensions = r.ReadUInt32();
                    if (dimensions < 1 || dimensions > 4) throw new InvalidDataException("GGUF 텐서 차원 오류");
                    ulong elements = 1, row = 0;
                    for (uint d = 0; d < dimensions; d++)
                    {
                        ulong n = r.ReadUInt64(); if (n == 0) throw new InvalidDataException("GGUF 텐서 크기 오류");
                        if (d == 0) row = n; elements = checked(elements * n);
                    }
                    uint tensorType = r.ReadUInt32(); ulong offset = r.ReadUInt64();
                    if (offset % alignment != 0) throw new InvalidDataException("GGUF 텐서 위치 정렬 오류");
                    // Common formats, including all types used by the bundled Q4_K_M model.
                    // Unknown future formats get a bounds check; llama.cpp verifies their full payload.
                    ulong block = 1, bytes = 0;
                    switch (tensorType)
                    {
                        case 0: bytes = 4; break; case 1: case 30: bytes = 2; break;
                        case 2: block = 32; bytes = 18; break; case 3: block = 32; bytes = 20; break;
                        case 6: block = 32; bytes = 22; break; case 7: block = 32; bytes = 24; break;
                        case 8: block = 32; bytes = 34; break; case 9: block = 32; bytes = 36; break;
                        case 10: block = 256; bytes = 84; break; case 11: block = 256; bytes = 110; break;
                        case 12: block = 256; bytes = 144; break; case 13: block = 256; bytes = 176; break;
                        case 14: block = 256; bytes = 210; break; case 15: block = 256; bytes = 292; break;
                        case 24: bytes = 1; break; case 25: bytes = 2; break; case 26: bytes = 4; break;
                        case 27: case 28: bytes = 8; break;
                    }
                    if (bytes != 0 && row % block != 0) throw new InvalidDataException("GGUF 양자화 블록 크기 오류");
                    ulong length = bytes == 0 ? 1 : checked(elements / block * bytes);
                    minimumPayload = Math.Max(minimumPayload, checked(offset + length));
                }
                ulong dataStart = checked(((ulong)stream.Position + alignment - 1) / alignment * alignment);
                if (dataStart > (ulong)stream.Length || minimumPayload > (ulong)stream.Length - dataStart)
                    throw new InvalidDataException("GGUF 텐서 데이터가 잘렸습니다. 파일 복사/다운로드를 완료하세요.");
                int context;
                return new GgufMetadata { Architecture = architecture, ContextLength = contexts.TryGetValue(architecture + ".context_length", out context) ? (int?)context : null };
            }
        }
    }

    public sealed class Capabilities
    {
        public string Help;
        public Capabilities(string help) { Help = help ?? ""; }
        public bool Has(string option)
        { return Regex.IsMatch(Help, @"(?<![\w-])" + Regex.Escape(option) + @"(?=[\s,=\[<]|$)"); }
        public bool Router { get { return Has("--models-preset") && Has("--models-max") && Has("--offline"); } }
        public bool AutoGpu
        {
            get
            {
                var m = Regex.Match(Help, @"(?im)^.*--(?:n-gpu-layers|gpu-layers).*(?:\r?\n.{0,200}){0,2}");
                return m.Success && Regex.IsMatch(m.Value, @"\bauto\b");
            }
        }
        public bool NvidiaListed(string text)
        {
            return Regex.IsMatch(text ?? "", @"(?im)^\s*(?:\d+\s*:\s*)?CUDA\d+\s*:")
                || Regex.IsMatch(text ?? "", @"(?im)^\s*Device\s+\d+:.*compute capability");
        }
    }

    public static class Plans
    {
        public static string Preset(string packageRoot, IEnumerable<ModelInfo> models)
        {
            var b = new StringBuilder("version = 1\r\n\r\n");
            foreach (var m in models)
            {
                string path = Paths.Relative(packageRoot, m.FullPath).Replace('\\', '/');
                if (path.IndexOfAny(new[] { '\r', '\n', ';', '#' }) >= 0) throw new InvalidDataException("모델 경로의 ; 또는 # 문자는 목록 모드에서 지원하지 않습니다. 폴더 이름을 바꾸거나 단일 모델 모드를 선택하세요.");
                b.Append('[').Append(m.Id).Append("]\r\nmodel = ").Append(path).Append("\r\n\r\n");
            }
            return b.ToString();
        }
        public static List<string> Arguments(Settings s, Capabilities c, BackendInfo backend, bool router, string modelPath, string id, string presetPath, int? reducedGpu)
        {
            var a = new List<string>();
            if (router) { a.AddRange(new[] { "--models-preset", presetPath, "--models-max", "1" }); if (c.Has("--no-models-autoload")) a.Add("--no-models-autoload"); }
            else { a.AddRange(new[] { "-m", modelPath, "--alias", id }); }
            a.AddRange(new[] { "--host", "127.0.0.1", "--port", s.Port.ToString(), "-c", s.Context.ToString() });
            // Do not silently reduce the requested context during automatic memory fitting.
            if (c.Has("--fit-ctx")) a.AddRange(new[] { "--fit-ctx", s.Context.ToString() });
            if (c.Has("--parallel")) a.AddRange(new[] { "--parallel", "1" });
            int threads = s.Threads == 0 ? Math.Max(1, Math.Min(12, Environment.ProcessorCount / 2)) : s.Threads;
            a.AddRange(new[] { "-t", threads.ToString() });
            string gpu = "0";
            if (backend.IsGpu) gpu = reducedGpu.HasValue ? reducedGpu.Value.ToString() : s.GpuLayers >= 0 ? s.GpuLayers.ToString() : c.AutoGpu ? "auto" : "999";
            a.AddRange(new[] { "-ngl", gpu });
            if (c.Has("--offline")) a.Add("--offline");
            if (c.Has("--cache-ram")) a.AddRange(new[] { "--cache-ram", "256" });
            if (c.Has("--no-mmproj")) a.Add("--no-mmproj"); // text-only; never load OCR/projector assets.
            if (c.Has("--jinja")) a.Add("--jinja");
            // --reasoning-format controls parsing, not whether the model thinks.
            if (c.Has("--reasoning")) a.AddRange(new[] { "--reasoning", s.ThinkingOff ? "off" : "on" });
            else if (c.Has("--chat-template-kwargs")) a.AddRange(new[] { "--chat-template-kwargs", s.ThinkingOff ? "{\"enable_thinking\":false}" : "{\"enable_thinking\":true}" });
            // Keep llama.cpp's default origin policy so browser tools on Confluence can connect.
            // Authorization must be explicit: the CORS header wildcard does not cover it.
            if (c.Has("--cors-headers")) a.AddRange(new[] { "--cors-headers", "*, Content-Type, Authorization" });
            return a;
        }
    }
}
