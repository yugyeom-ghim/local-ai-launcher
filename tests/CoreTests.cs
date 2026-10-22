using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using SILocalAI;

namespace SILocalAI
{
    public static class TestProgram
    {
        static int passed, failed;
        static readonly List<string> report = new List<string>();
        static void Test(string title, Action check)
        {
            try { check(); passed++; report.Add("PASS " + title); }
            catch (Exception e) { failed++; report.Add("FAIL " + title + ": " + e.Message); }
        }
        static void Equal<T>(T want, T got) { if (!Object.Equals(want, got)) throw new Exception("expected=" + want + ", actual=" + got); }
        static void Yes(bool value) { if (!value) throw new Exception("assertion failed"); }
        static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("expected an exception"); }
        static void WriteModel(string path, string arch, Action<BinaryWriter> metadata = null, ulong extraCount = 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write((uint)0x46554747); w.Write((uint)3); w.Write((ulong)1); w.Write((ulong)1 + extraCount);
                if (metadata != null) metadata(w);
                byte[] key = Encoding.UTF8.GetBytes("general.architecture"); w.Write((ulong)key.Length); w.Write(key); w.Write((uint)8);
                byte[] value = Encoding.UTF8.GetBytes(arch); w.Write((ulong)value.Length); w.Write(value);
                byte[] tensor = Encoding.UTF8.GetBytes("test.weight"); w.Write((ulong)tensor.Length); w.Write(tensor);
                w.Write((uint)1); w.Write((ulong)32); w.Write((uint)0); w.Write((ulong)0);
                while (w.BaseStream.Position % 32 != 0) w.Write((byte)0);
                w.Write(new byte[128]);
            }
        }
        static void ContextMetadata(BinaryWriter w, string key, uint type, ulong value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(key); w.Write((ulong)bytes.Length); w.Write(bytes); w.Write(type);
            if (type == 10) w.Write(value); else w.Write((uint)value);
        }
        public static int Main(string[] args)
        {
            string temp = Path.Combine(Path.GetTempPath(), "SI Local AI 테스트 " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                Test("fresh install requires explicit start", delegate { Yes(!new Settings().AutoStart); });
                Test("v1.0 saved automatic start is preserved", delegate { Yes(Json.Read<Settings>("{\"AutoStart\":true,\"Schema\":1}").AutoStart); });
                Test("newly observed file is not stable", delegate { var g=new FileStability(); Yes(!g.Observe("a",100,1,DateTime.UtcNow)); });
                Test("file becomes stable after quiet interval", delegate { var g=new FileStability(); var n=DateTime.UtcNow; g.Observe("a",100,1,n); Yes(g.Observe("a",100,1,n.AddSeconds(3))); });
                Test("size change restarts stability interval", delegate { var g=new FileStability(); var n=DateTime.UtcNow; g.Observe("a",100,1,n); Yes(!g.Observe("a",200,2,n.AddSeconds(4))); });
                Test("timestamp-only change restarts interval", delegate { var g=new FileStability(); var n=DateTime.UtcNow; g.Observe("a",100,1,n); Yes(!g.Observe("a",100,2,n.AddSeconds(4))); });
                Test("removed and re-added file must settle again", delegate { var g=new FileStability(); var n=DateTime.UtcNow; g.Observe("a",100,1,n); g.Retain(new string[0]); Yes(!g.Observe("a",100,1,n.AddSeconds(4))); });
                Test("default configuration", delegate { new Settings().Validate(); });
                Test("configuration rejects bad port", delegate { Throws(delegate { new Settings { Port = 80 }.Validate(); }); });
                Test("configuration rejects bad context", delegate { Throws(delegate { new Settings { Context = 2 }.Validate(); }); });
                Test("relative path rejects drive", delegate { Throws(delegate { Paths.CheckRelative("C:\\AI"); }); });
                Test("relative path rejects network", delegate { Throws(delegate { Paths.CheckRelative("\\\\server\\share"); }); });
                Test("relative path rejects newline", delegate { Throws(delegate { Paths.CheckRelative("models\n-malicious"); }); });
                Test("quote empty argument", delegate { Equal("\"\"", Paths.Quote("")); });
                Test("quote spaces and Unicode", delegate { Equal("\"한글 model path\"", Paths.Quote("한글 model path")); });
                Test("quote embedded quote", delegate { Equal("\"a\\\"b\"", Paths.Quote("a\"b")); });
                Test("quote trailing backslash", delegate { Equal("\"C:\\AI\\\\\"", Paths.Quote("C:\\AI\\")); });
                Test("same root is dot", delegate { Equal(".", Paths.Relative(temp, temp)); });
                Test("path round trip", delegate { string f = Path.Combine(temp, "한글 폴더", "file.gguf"); Equal(Path.GetFullPath(f), Paths.Full(temp, Paths.Relative(temp, f))); });
                Test("JSON missing fields retain defaults", delegate { Equal(4096, Json.Read<Settings>("{\"Port\":1235}").Context); });
                Test("JSON round trip", delegate { var s = new Settings { Model = "한국어/model.gguf" }; Equal(s.Model, s.Clone().Model); });
                Test("exact option matching", delegate { var c = new Capabilities("--reasoning-format FORMAT\n--host HOST"); Yes(!c.Has("--reasoning")); Yes(c.Has("--host")); });
                Test("router requires offline support", delegate { Yes(!new Capabilities("--models-preset PATH\n--models-max N").Router); });
                Test("router capabilities", delegate { Yes(new Capabilities("--models-preset PATH\n--models-max N\n--offline").Router); });
                Test("CPU does not count as CUDA", delegate { Yes(!new Capabilities("").NvidiaListed("Available devices:\n CPU: x64\nfound 0 CUDA devices")); });
                Test("CUDA device recognized", delegate { Yes(new Capabilities("").NvidiaListed("Available devices:\n  CUDA0: NVIDIA GeForce RTX 3050 (6000 MiB, 5000 MiB free)")); });
                Test("GGUF header parser", delegate { string f = Path.Combine(temp, "valid.gguf"); WriteModel(f, "qwen35"); Equal("qwen35", Gguf.Header(f)); });
                Test("GGUF context before architecture ignores other architectures and rope keys", delegate {
                    string f = Path.Combine(temp, "context.gguf");
                    WriteModel(f, "qwen35", delegate(BinaryWriter w) {
                        ContextMetadata(w, "qwen35.context_length", 4, 262144);
                        ContextMetadata(w, "llama.context_length", 4, 8192);
                        ContextMetadata(w, "qwen35.rope.scaling.original_context_length", 4, 32768);
                    }, 3);
                    Equal((int?)262144, Gguf.ReadHeader(f).ContextLength);
                });
                Test("missing or unusable GGUF context remains unknown without rejecting model", delegate {
                    string f = Path.Combine(temp, "unknown-context.gguf"); WriteModel(f, "qwen35");
                    Equal((int?)null, Gguf.ReadHeader(f).ContextLength);
                    foreach (uint value in new uint[] { 0, UInt32.MaxValue }) {
                        WriteModel(f, "qwen35", delegate(BinaryWriter w) { ContextMetadata(w, "qwen35.context_length", 4, value); }, 1);
                        Equal((int?)null, Gguf.ReadHeader(f).ContextLength); Equal("qwen35", Gguf.Header(f));
                    }
                    WriteModel(f, "qwen35", delegate(BinaryWriter w) { ContextMetadata(w, "qwen35.context_length", 6, 262144); }, 1);
                    Equal((int?)null, Gguf.ReadHeader(f).ContextLength);
                });
                Test("uint64 GGUF context supported without integer overflow", delegate {
                    string f = Path.Combine(temp, "rwkv.gguf");
                    WriteModel(f, "rwkv", delegate(BinaryWriter w) { ContextMetadata(w, "rwkv.context_length", 10, 65536); }, 1);
                    Equal((int?)65536, Gguf.ReadHeader(f).ContextLength);
                    WriteModel(f, "rwkv", delegate(BinaryWriter w) { ContextMetadata(w, "rwkv.context_length", 10, UInt64.MaxValue); }, 1);
                    Equal((int?)null, Gguf.ReadHeader(f).ContextLength);
                });
                Test("large and custom saved context reaches server unchanged and disables fit reduction", delegate {
                    foreach (int requested in new[] { 65536, 131072, 262144, 70000, 1010000, Int32.MaxValue }) {
                        var saved = new Settings { Context = requested }.Clone(); saved.Validate();
                        foreach (bool router in new[] { false, true }) {
                            var a = Plans.Arguments(saved, new Capabilities("--fit-ctx N"), new BackendInfo { Id = "cpu" }, router, "m", "m", "p", null);
                            Equal(requested.ToString(), a[a.IndexOf("-c") + 1]);
                            Equal(requested.ToString(), a[a.IndexOf("--fit-ctx") + 1]);
                        }
                        var legacy = Plans.Arguments(saved, new Capabilities(""), new BackendInfo { Id = "cpu" }, false, "m", "m", "p", null);
                        Yes(!legacy.Contains("--fit-ctx"));
                    }
                });
                Test("truncated tensor payload rejected", delegate { string f = Path.Combine(temp, "truncated.gguf"); WriteModel(f, "qwen35"); using (var stream = File.OpenWrite(f)) stream.SetLength(stream.Length - 1); Throws(delegate { Gguf.Header(f); }); });
                Test("CUDA 13.4 config supported without replacing legacy path", delegate { var s = Json.Read<Settings>("{\"Cuda13Path\":\"bin/legacy-13.1\",\"Backend\":\"cuda134\"}"); s.Validate(); Equal("bin/legacy-13.1", s.Cuda13Path); Equal("bin/win-cuda-13.4-x64", s.Cuda134Path); });
                Test("HTML renamed as GGUF rejected", delegate { string f = Path.Combine(temp, "invalid.gguf"); File.WriteAllText(f, "<html>not a model</html>"); Throws(delegate { Gguf.Header(f); }); });
                var s0 = new Settings(); string assets = Paths.Full(temp, s0.AssetRoot);
                string models = Path.Combine(assets, "models"); Directory.CreateDirectory(models);
                WriteModel(Path.Combine(models, "Qwen3.5-4B-Q4_K_M.gguf"), "qwen35", delegate(BinaryWriter w) { ContextMetadata(w, "qwen35.context_length", 4, 262144); }, 1);
                Test("discovery exposes model context for UI without a second file read", delegate { Equal((int?)262144, Discovery.Scan(temp, s0).Models.Single().ContextLength); });
                WriteModel(Path.Combine(models, "mmproj-Qwen.gguf"), "clip");
                WriteModel(Path.Combine(models, "ocr", "ocr.gguf"), "qwen35");
                WriteModel(Path.Combine(models, "bert.gguf"), "bert");
                WriteModel(Path.Combine(models, "runtime", "hidden.gguf"), "qwen35");
                Test("discovery filters projector OCR runtime encoder", delegate { Equal(1, Discovery.Scan(temp, s0).Models.Count); });
                WriteModel(Path.Combine(models, "shards", "split-00001-of-00002.gguf"), "llama");
                Test("missing model shard excluded", delegate { var i = Discovery.Scan(temp, s0); Equal(1, i.Models.Count); Yes(i.Warnings.Any(w => w.Contains("분할 모델 누락"))); });
                WriteModel(Path.Combine(models, "shards", "split-00002-of-00002.gguf"), "llama");
                Test("complete shards form one model", delegate { Equal(2, Discovery.Scan(temp, s0).Models.Count); });
                string cpu = Path.Combine(assets, "bin", "win-cpu-x64"); Directory.CreateDirectory(cpu); File.WriteAllText(Path.Combine(cpu, "llama-server.exe"), "test placeholder");
                Test("runtime discovery", delegate { Equal(1, Discovery.Scan(temp, s0).Backends.Count(b => b.Exe != "")); });
                Test("unstable GGUF excluded from launch catalog", delegate { var i=Discovery.Scan(temp,s0,delegate(string f) {return false;}); Equal(0,i.Models.Count); Yes(i.PendingFiles.Count>0); });
                Test("one unstable shard excludes entire model", delegate { var i=Discovery.Scan(temp,s0,delegate(string f) {return !f.EndsWith("split-00002-of-00002.gguf");}); Yes(!i.Models.Any(m=>m.Name=="split")); Yes(i.PendingFiles.Any(n=>n.Contains("split"))); });
                Test("catalog signature changes on model addition", delegate { var a=new ModelInfo {Id="a",Bytes=100,FullPath="a"}; var b=new ModelInfo {Id="b",Bytes=100,FullPath="b"}; Yes(LibraryMonitor.ModelSignature(new[]{a})!=LibraryMonitor.ModelSignature(new[]{a,b})); });
                Test("catalog signature is order independent", delegate { var a=new ModelInfo {Id="a",Bytes=100,FullPath="a"}; var b=new ModelInfo {Id="b",Bytes=100,FullPath="b"}; Equal(LibraryMonitor.ModelSignature(new[]{a,b}),LibraryMonitor.ModelSignature(new[]{b,a})); });
                Test("preferred Qwen selected", delegate { Yes(Discovery.Choose(Discovery.Scan(temp, s0).Models, "").Name.StartsWith("Qwen3.5-4B")); });
                Test("saved model respected", delegate { var i = Discovery.Scan(temp, s0); var m = i.Models.Last(); Equal(m.Id, Discovery.Choose(i.Models, m.RelativePath).Id); });
                Test("model IDs distinct", delegate { var i = Discovery.Scan(temp, s0); Equal(i.Models.Count, i.Models.Select(m => m.Id).Distinct().Count()); });
                Test("generated preset has relative paths only", delegate { string p = Plans.Preset(temp, Discovery.Scan(temp, s0).Models); Yes(p.StartsWith("version = 1")); Yes(!p.Contains(temp)); Yes(p.Contains("model = offline-assets/ai/models/")); });
                Test("model IDs survive package move", delegate {
                    string expected = String.Join(",", Discovery.Scan(temp, s0).Models.Select(m => m.Id));
                    string target = temp + " moved"; Directory.Move(temp, target);
                    try { Equal(expected, String.Join(",", Discovery.Scan(target, s0).Models.Select(m => m.Id))); }
                    finally { Directory.Move(target, temp); }
                });
                Test("backend priority and explicit CPU", delegate {
                    var i = new Inventory(); foreach (string id in new[] { "cpu", "cuda12", "cuda13" }) i.Backends.Add(new BackendInfo { Id = id, Exe = "test" });
                    Equal("cuda12,cuda13,cpu", String.Join(",", Discovery.Order(i, s0).Select(x => x.Id)));
                    Equal("cpu", Discovery.Order(i, new Settings { Backend = "cpu" }).Single().Id);
                });
                Test("explicit CUDA never falls back to CPU", delegate {
                    var i = new Inventory(); i.Backends.Add(new BackendInfo { Id = "cpu", Exe = "cpu" });
                    Equal(0, Discovery.Order(i, new Settings { Backend = "cuda134" }).Count);
                });
                Test("CUDA 12 then 13.4 then CPU priority", delegate {
                    var i = new Inventory(); foreach (string id in new[] { "cpu", "cuda134", "cuda12" }) i.Backends.Add(new BackendInfo { Id = id, Exe = "test" });
                    Equal("cuda12,cuda134,cpu", String.Join(",", Discovery.Order(i, s0).Select(x => x.Id)));
                });
                Test("fallback opt out", delegate {
                    var i = new Inventory(); i.Backends.Add(new BackendInfo { Id = "cpu", Exe = "test" });
                    Equal(0, Discovery.Order(i, new Settings { CpuFallback = false }).Count);
                });
                Test("CPU arguments, no unsupported reasoning", delegate {
                    var a = Plans.Arguments(s0, new Capabilities("--parallel N\n--offline\n--reasoning-format FORMAT"), new BackendInfo { Id = "cpu" }, false, "models/q.gguf", "q", "preset.ini", null);
                    Equal("0", a[a.IndexOf("-ngl") + 1]); Yes(a.Contains("--offline")); Yes(!a.Contains("--reasoning")); Yes(a.Contains("--alias"));
                });
                Test("browser origins unrestricted with explicit JSON and bearer headers", delegate {
                    foreach (bool router in new[] { false, true }) {
                        var a = Plans.Arguments(s0, new Capabilities("--cors-origins ORIGINS\n--cors-headers HEADERS"), new BackendInfo { Id = "cpu" }, router, "m", "m", "p", null);
                        Yes(!a.Contains("--cors-origins")); Equal("127.0.0.1", a[a.IndexOf("--host") + 1]);
                        Equal("*, Content-Type, Authorization", a[a.IndexOf("--cors-headers") + 1]);
                    }
                });
                Test("older runtime does not receive unsupported CORS flags", delegate {
                    var a = Plans.Arguments(s0, new Capabilities(""), new BackendInfo { Id = "cpu" }, false, "m", "m", "p", null);
                    Yes(!a.Any(x => x.StartsWith("--cors-")));
                });
                Test("router args never fix model alias", delegate {
                    var a = Plans.Arguments(s0, new Capabilities("--offline\n--reasoning MODE"), new BackendInfo { Id = "cuda12" }, true, "m.gguf", "m", "p.ini", null);
                    Yes(a.Contains("--models-preset")); Yes(!a.Contains("-m")); Yes(!a.Contains("--alias")); Yes(a.Contains("--reasoning"));
                });
                Test("router disables implicit model loading when supported", delegate {
                    var a = Plans.Arguments(s0, new Capabilities("--no-models-autoload\n--offline"), new BackendInfo { Id = "cpu" }, true, "a.gguf", "a", "p.ini", null);
                    Yes(a.Contains("--no-models-autoload"));
                });
                Test("legacy ThinkingOff false and auth default survive load", delegate {
                    var old = Json.Read<Settings>("{\"ThinkingOff\":false,\"Port\":2345,\"ModelsPath\":\"custom/models\"}");
                    Yes(!old.ThinkingOff); Yes(!old.ApiKeyRequired); Equal(2345, old.Port); Equal("custom/models", old.ModelsPath); old.Validate();
                });
                Test("auth preference survives settings round trip", delegate { Yes(new Settings { ApiKeyRequired = true }.Clone().ApiKeyRequired); });
                Test("thinking ON explicitly passed rather than template default", delegate {
                    var a = Plans.Arguments(new Settings { ThinkingOff = false }, new Capabilities("--reasoning MODE\n--jinja"), new BackendInfo { Id = "cpu" }, false, "m", "m", "p", null);
                    Equal("on", a[a.IndexOf("--reasoning") + 1]); Yes(a.Contains("--jinja"));
                });
                Test("thinking OFF explicit and format cannot masquerade as switch", delegate {
                    var a = Plans.Arguments(new Settings(), new Capabilities("--reasoning MODE\n--reasoning-format FORMAT"), new BackendInfo { Id = "cpu" }, false, "m", "m", "p", null);
                    Equal("off", a[a.IndexOf("--reasoning") + 1]); Yes(!a.Contains("--reasoning-format"));
                });
                Test("older runtime template switch explicitly supports ON and OFF", delegate {
                    foreach(bool off in new[]{true,false}) {
                        var a = Plans.Arguments(new Settings { ThinkingOff = off }, new Capabilities("--chat-template-kwargs JSON"), new BackendInfo {Id="cpu"}, false,"m","m","p",null);
                        var kwargs=Json.Object(a[a.IndexOf("--chat-template-kwargs")+1]); Equal(!off,(bool)kwargs["enable_thinking"]);
                    }
                });
                Test("local key persists without rotation and differs across installs", delegate {
                    string a=LocalApiKeys.LoadOrCreate(Path.Combine(temp,"keys-a"));
                    Equal(a, LocalApiKeys.LoadOrCreate(Path.Combine(temp,"keys-a")));
                    Yes(a!=LocalApiKeys.LoadOrCreate(Path.Combine(temp,"keys-b"))); Yes(a.Length>=64);
                });
                Test("malformed key fails instead of silently disabling authentication", delegate {
                    string path=Path.Combine(temp,"bad-key");Directory.CreateDirectory(path);
                    File.WriteAllText(LocalApiKeys.FilePath(path),"# comment only"); Throws(delegate {LocalApiKeys.LoadOrCreate(path);});
                    Equal("# comment only",File.ReadAllText(LocalApiKeys.FilePath(path)));
                });
                Test("authentication unsupported fails closed", delegate {
                    Throws(delegate {LocalApiKeys.AddArguments(new List<string>(), new Settings {ApiKeyRequired=true},new Capabilities("--api-key KEY"),"key.txt");});
                });
                Test("key path only in command, optional auth retains compatibility", delegate {
                    var a=new List<string>(); LocalApiKeys.AddArguments(a,new Settings(),new Capabilities(""),"key.txt"); Equal(0,a.Count);
                    LocalApiKeys.AddArguments(a,new Settings {ApiKeyRequired=true},new Capabilities("--api-key-file PATH"),"state/key.txt");
                    Equal("--api-key-file",a[0]); Equal("state/key.txt",a[1]);
                });
                Test("OpenCode export roundtrip uses active model port context without key", delegate {
                    var c=new ClientConnection {BaseUrl="http://127.0.0.1:2345/v1",ModelId="model-hash",ModelName="Qwen \"quoted\" \\ path",Context=16384,ApiKeyRequired=true,ApiKey="SECRET-MUST-NOT-EXPORT"};
                    string text=ClientConfig.OpenCode(c);Yes(!text.Contains(c.ApiKey));Yes(!text.Contains("apiKey"));Yes(text.Contains("\r\n"));
                    var j=Json.Object(text); Equal("si-local/model-hash",(string)j["model"]);
                    var provider=(Dictionary<string,object>)((Dictionary<string,object>)j["provider"])["si-local"];
                    Equal(c.BaseUrl,((Dictionary<string,object>)provider["options"])["baseURL"]);
                    var model=(Dictionary<string,object>)((Dictionary<string,object>)provider["models"])[c.ModelId];
                    Equal(c.ModelName+" (local)",model["name"]); Equal(c.Context,((Dictionary<string,object>)model["limit"])["context"]);
                    Yes(!ClientConfig.Guide(c).Contains(c.ApiKey));
                    File.WriteAllText(Path.Combine(temp,"opencode-example.json"),text);
                });
                Test("keyless export works even for clients requiring a key field", delegate {
                    var c=new ClientConnection {BaseUrl="http://127.0.0.1:1234/v1",ModelId="m",ModelName="m",Context=4096}; Yes(ClientConfig.OpenCode(c).Contains("si-local-no-key"));
                });
                Test("atomic state replacement", delegate { string f = Path.Combine(temp, "state", "test.json"); Paths.AtomicWrite(f, "one"); Paths.AtomicWrite(f, "two"); Equal("two", File.ReadAllText(f)); });
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
            if (args.Length > 1) Test("real packaged GGUF tensor bounds and architecture", delegate { Equal("qwen35", Gguf.Header(args[1])); });
            string summary = "Core tests: " + passed + " passed, " + failed + " failed";
            foreach (var line in report) Console.WriteLine(line); Console.WriteLine(summary);
            string outFile = args.Length > 0 ? args[0] : "core-test-results.txt";
            File.WriteAllLines(outFile, report.Concat(new[] { summary }), Encoding.UTF8);
            return failed == 0 ? 0 : 1;
        }
    }
}
