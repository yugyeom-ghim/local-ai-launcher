using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SILocalAI
{
    public sealed class AppLog
    {
        readonly object gate = new object();
        readonly string path;
        readonly List<string> secrets = new List<string>();
        public void HideSecret(string value) { if (!String.IsNullOrEmpty(value)) lock (gate) { if (!secrets.Contains(value)) secrets.Add(value); } }
        public string Clean(string text) { lock (gate) { foreach (string value in secrets) text = text.Replace(value, "[API key hidden]"); return text; } }
        public event Action<string> Line;
        public AppLog(string file) { path = file; }
        public void Write(string text)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + Clean(text);
            lock (gate)
            {
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 4 * 1024 * 1024)
                    { if (File.Exists(path + ".1")) File.Delete(path + ".1"); File.Move(path, path + ".1"); }
                    File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                }
                catch { /* A disk logging failure must not kill an owned server. UI still receives the message. */ }
            }
            var handler = Line; if (handler != null) { try { handler(line); } catch { } }
        }
    }

    public sealed class Job : IDisposable
    {
        IntPtr handle;
        const uint KillOnClose = 0x00002000;
        [StructLayout(LayoutKind.Sequential)] struct BasicLimits
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
        [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits
        {
            public BasicLimits BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateJobObject(IntPtr attr, string name);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr value);
        public Job()
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "프로세스 관리 Job 생성 실패");
            var limits = new ExtendedLimits(); limits.BasicLimitInformation.LimitFlags = KillOnClose;
            int size = Marshal.SizeOf(typeof(ExtendedLimits)); IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(limits, p, false);
                if (!SetInformationJobObject(handle, 9, p, (uint)size)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "프로세스 자동 정리 설정 실패");
            }
            catch { Dispose(); throw; }
            finally { Marshal.FreeHGlobal(p); }
        }
        public void Attach(Process p)
        {
            if (p.HasExited) return;
            if (!AssignProcessToJobObject(handle, p.Handle))
            { int error = Marshal.GetLastWin32Error(); if (!p.HasExited) throw new System.ComponentModel.Win32Exception(error, "서버 프로세스를 관리 Job에 연결할 수 없습니다."); }
        }
        public void Dispose() { IntPtr h = Interlocked.Exchange(ref handle, IntPtr.Zero); if (h != IntPtr.Zero) CloseHandle(h); }
    }

    public sealed class OwnedProcess : IDisposable
    {
        readonly object gate = new object();
        readonly StringBuilder tail = new StringBuilder();
        readonly TaskCompletionSource<bool> outputEnded = new TaskCompletionSource<bool>();
        readonly TaskCompletionSource<bool> errorEnded = new TaskCompletionSource<bool>();
        Process process;
        Job job;
        public int Id { get { try { return process == null ? 0 : process.Id; } catch { return 0; } } }
        public bool Alive { get { try { return process != null && !process.HasExited; } catch { return false; } } }
        public int ExitCode { get { try { return process.ExitCode; } catch { return -1; } } }
        public string Tail { get { lock (gate) return tail.ToString(); } }
        public static ProcessStartInfo Info(string exe, IEnumerable<string> args, string cwd, string cache, string logRoot)
        {
            var info = new ProcessStartInfo(exe, Paths.Command(args));
            info.WorkingDirectory = cwd;
            info.UseShellExecute = false; info.CreateNoWindow = true;
            info.RedirectStandardOutput = true; info.RedirectStandardError = true;
            info.StandardOutputEncoding = Encoding.UTF8; info.StandardErrorEncoding = Encoding.UTF8;
            // No inherited llama flags may change the model, listening address, or resource limits.
            foreach (string key in info.EnvironmentVariables.Keys.Cast<string>().ToArray())
                if (key.StartsWith("LLAMA_ARG_", StringComparison.OrdinalIgnoreCase)) info.EnvironmentVariables.Remove(key);
            info.EnvironmentVariables.Remove("LLAMA_API_KEY");
            info.EnvironmentVariables.Remove("LLAMA_CHAT_TEMPLATE_KWARGS");
            info.EnvironmentVariables.Remove("HF_TOKEN"); info.EnvironmentVariables.Remove("HUGGING_FACE_HUB_TOKEN");
            info.EnvironmentVariables["LLAMA_ARG_OFFLINE"] = "1";
            info.EnvironmentVariables["HF_HUB_OFFLINE"] = "1";
            info.EnvironmentVariables["LLAMA_CACHE"] = cache;
            string dir = Path.GetDirectoryName(exe);
            var dllDirs = new List<string> { dir };
            foreach (string d in Paths.Files(dir, "*.dll", 2, null).Select(Path.GetDirectoryName).Distinct().Take(24)) if (!dllDirs.Contains(d)) dllDirs.Add(d);
            info.EnvironmentVariables["PATH"] = String.Join(";", dllDirs) + ";" + (info.EnvironmentVariables["PATH"] ?? "");
            return info;
        }
        public OwnedProcess(ProcessStartInfo info, Action<string> output)
        {
            job = new Job();
            process = new Process { StartInfo = info };
            Action<DataReceivedEventArgs, TaskCompletionSource<bool>> receive = delegate(DataReceivedEventArgs e, TaskCompletionSource<bool> ended)
            {
                if (e.Data == null) { ended.TrySetResult(true); return; }
                lock (gate)
                {
                    tail.AppendLine(e.Data);
                    if (tail.Length > 128 * 1024) tail.Remove(0, tail.Length - 96 * 1024);
                }
                if (output != null) { try { output(e.Data); } catch { } }
            };
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { receive(e, outputEnded); };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { receive(e, errorEnded); };
            try
            {
                if (!process.Start()) throw new InvalidOperationException("프로세스를 시작하지 못했습니다.");
                // Router children are not requested until this job association has succeeded.
                job.Attach(process);
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            }
            catch { Dispose(); throw; }
        }
        public async Task DrainAsync(CancellationToken ct)
        {
            await Task.WhenAny(Task.WhenAll(outputEnded.Task, errorEnded.Task), Task.Delay(1500, ct));
            ct.ThrowIfCancellationRequested();
        }
        public void Dispose()
        {
            var j = Interlocked.Exchange(ref job, null); if (j != null) j.Dispose();
            var p = Interlocked.Exchange(ref process, null);
            if (p != null)
            {
                try { if (!p.HasExited) { p.Kill(); p.WaitForExit(1500); } } catch { }
                p.Dispose();
            }
        }
    }
    public sealed class ProbeResult { public int Exit; public string Text; }
    public static class Probe
    {
        public static async Task<ProbeResult> Run(string exe, IEnumerable<string> args, string cwd, string state, int seconds, CancellationToken ct)
        {
            using (var p = new OwnedProcess(OwnedProcess.Info(exe, args, cwd, Path.Combine(state, "cache"), state), null))
            {
                var clock = Stopwatch.StartNew();
                while (p.Alive)
                {
                    ct.ThrowIfCancellationRequested();
                    if (clock.Elapsed.TotalSeconds > seconds) throw new TimeoutException("실행기 검사 시간 초과: " + Path.GetFileName(exe));
                    await Task.Delay(100, ct);
                }
                await p.DrainAsync(ct);
                return new ProbeResult { Exit = p.ExitCode, Text = p.Tail };
            }
        }
    }
    public sealed class HttpReply
    {
        public int Code; public string Body;
        public bool Ok { get { return Code >= 200 && Code < 300; } }
    }
    public sealed class LocalHttp : IDisposable
    {
        readonly HttpClient client;
        public string ApiKey = "";
        public LocalHttp()
        {
            var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
            client = new HttpClient(handler); client.Timeout = Timeout.InfiniteTimeSpan;
        }
        public static HttpRequestMessage Request(int port, string path, object payload, string key)
        {
            // The destination is never read from a file, response, or user supplied URL.
            if (String.IsNullOrEmpty(path) || !path.StartsWith("/") || path.StartsWith("//") || path.Contains("\\") || path.Any(Char.IsControl)) throw new ArgumentException("로컬 API 경로 오류");
            var req = new HttpRequestMessage(payload == null ? HttpMethod.Get : HttpMethod.Post, "http://127.0.0.1:" + port + path);
            if (!String.IsNullOrEmpty(key)) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            if (payload != null) req.Content = new StringContent(Json.Write(payload), Encoding.UTF8, "application/json");
            return req;
        }
        public async Task<HttpReply> Send(int port, string path, object payload, int seconds, CancellationToken token)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var req = Request(port, path, payload, ApiKey))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(seconds));
                using (var response = await client.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (body.Length > 8 * 1024 * 1024) throw new InvalidDataException("API 응답 크기 제한 초과");
                    return new HttpReply { Code = (int)response.StatusCode, Body = body };
                }
            }
        }
        public void Dispose() { client.Dispose(); }
    }
    public sealed class ServerStatus
    {
        public string State = "stopped", Message = "중지됨", Backend = "", Mode = "", ModelId = "", BaseUrl = "", ChatUrl = "", Gpu = "";
        public int Pid;
        public bool Listening, Ready, ApiKeyRequired;
        public string Thinking = "";
        public int Context;
        public string[] RegisteredModels = new string[0];
    }
    sealed class RouterUnavailableException : Exception { public RouterUnavailableException(string text) : base(text) { } }

    public sealed class ServerController : IDisposable
    {
        readonly string root, state;
        readonly AppLog log;
        readonly LocalHttp http = new LocalHttp();
        OwnedProcess process;
        CancellationTokenSource lifetime;
        Settings active;
        Inventory inventory;
        ModelInfo model;
        BackendInfo backend;
        Capabilities caps;
        bool router, busy, disposed;
        string activeKey = "";
        public ServerStatus Status = new ServerStatus();
        public event Action<ServerStatus> Changed;
        public bool Busy { get { return busy; } }
        public bool Alive { get { return process != null && process.Alive; } }
        public ServerController(string rootPath, string statePath, AppLog logger) { root = rootPath; state = statePath; log = logger; }
        void Publish(string kind, string message, bool listening, bool ready)
        {
            var s = new ServerStatus {
                State = kind, Message = log.Clean(message), Listening = listening, Ready = ready,
                ApiKeyRequired = active != null && active.ApiKeyRequired,
                Context = active == null ? 0 : active.Context,
                Thinking = active == null ? "" : caps == null ? "사고 모드 확인 중" : !caps.Has("--reasoning") && !caps.Has("--chat-template-kwargs") ? "사고 설정 미지원 · 모델 기본값" : "사고 기본값 " + (active.ThinkingOff ? "OFF" : "ON") + " (지원 템플릿)",
                Backend = backend == null ? "" : backend.Label, Mode = router ? "router" : "single",
                ModelId = model == null ? "" : model.Id, Pid = process == null ? 0 : process.Id,
                BaseUrl = active == null ? "" : "http://127.0.0.1:" + active.Port + "/v1",
                ChatUrl = active == null ? "" : "http://127.0.0.1:" + active.Port + "/v1/chat/completions",
                RegisteredModels = inventory == null ? new string[0] : (model == null ? new string[0] : new[] { model.Id }),
                Gpu = ""
            };
            if (process != null && backend != null)
            {
                var m = Regex.Matches(process.Tail, @"offloaded\s+(\d+)/(\d+)\s+layers", RegexOptions.IgnoreCase);
                s.Gpu = !backend.IsGpu ? "CPU 실행" : m.Count > 0 ? "GPU 레이어 " + m[m.Count - 1].Groups[1].Value + "/" + m[m.Count - 1].Groups[2].Value : "CUDA 실행기 / 실제 offload는 로그 확인";
            }
            Status = s;
            try { Paths.AtomicWrite(Path.Combine(state, "connection.json"), Json.Write(s)); } catch (Exception e) { log.Write("연결 상태 저장 실패: " + e.Message); }
            var h = Changed; if (h != null) h(s);
        }
        public ClientConnection Connection()
        {
            if (busy || !Alive || !Status.Ready || active == null || model == null)
                throw new InvalidOperationException("모델이 준비된 뒤 연결 정보를 복사하세요.");
            return new ClientConnection { BaseUrl = Status.BaseUrl, ModelId = model.Id, ModelName = model.Name,
                Context = active.Context, ApiKeyRequired = active.ApiKeyRequired, ApiKey = activeKey, Thinking = Status.Thinking };
        }
        static void PortAvailable(int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.ExclusiveAddressUse = true;
            try { listener.Start(); }
            catch (SocketException e) { throw new InvalidOperationException("포트 " + port + "를 사용할 수 없습니다. 기존 서버를 직접 종료하거나 포트를 변경하세요. 다른 프로세스는 자동 종료하지 않습니다. " + e.SocketErrorCode); }
            finally { listener.Stop(); }
        }
        void CloseProcess()
        {
            var p = process; process = null; if (p != null) p.Dispose();
        }
        public void Stop()
        {
            if (lifetime != null) lifetime.Cancel();
            CloseProcess();
            Publish("stopped", "중지됨 · 이 도구가 실행한 서버만 종료했습니다.", false, false);
        }
        public async Task StartAsync(Settings settings, Inventory found, ModelInfo chosen)
        {
            if (busy) return;
            Stop(); busy = true;
            if (lifetime != null) lifetime.Dispose(); lifetime = new CancellationTokenSource();
            var ct = lifetime.Token;
            active = settings.Clone(); inventory = found; model = chosen; backend = null; caps = null;
            try
            {
                active.Validate();
                activeKey = active.ApiKeyRequired ? LocalApiKeys.LoadOrCreate(state) : "";
                http.ApiKey = activeKey; log.HideSecret(activeKey);
                if (model == null || !File.Exists(model.FullPath)) throw new InvalidOperationException("사용할 GGUF 모델이 없습니다. models 폴더를 확인하고 다시 검색하세요.");
                PortAvailable(active.Port);
                var candidates = Discovery.Order(found, active);
                if (candidates.Count == 0) throw new InvalidOperationException("선택 가능한 llama-server.exe가 없습니다. 자산 설정과 bin 폴더를 확인하세요. 자동 GPU 모드에서 CPU 전환을 해제한 경우 CPU는 사용하지 않습니다.");
                var failures = new List<string>();
                foreach (var candidate in candidates)
                {
                    ct.ThrowIfCancellationRequested(); backend = candidate; bool retryReduced = false;
                    try
                    {
                        Publish("probing", candidate.Label + " 실행기 확인 중", false, false);
                        log.Write("검사: " + Paths.Relative(root, candidate.Exe));
                        var help = await Probe.Run(candidate.Exe, new[] { "--help" }, root, state, 20, ct);
                        if (help.Exit != 0) throw new InvalidOperationException("--help 실패 (종료 코드 " + help.Exit + ")\r\n" + help.Text);
                        caps = new Capabilities(help.Text);
                        foreach (string required in new[] { "--model", "--host", "--port", "--alias" })
                            if (!caps.Has(required)) throw new InvalidOperationException("필수 옵션 " + required + "가 없는 실행기입니다.");
                        if (candidate.IsGpu)
                        {
                            if (!caps.Has("--list-devices")) throw new InvalidOperationException("GPU 인식 검사를 지원하지 않는 실행기입니다. CPU를 선택하거나 실행기를 교체하세요.");
                            var devices = await Probe.Run(candidate.Exe, new[] { "--list-devices" }, root, state, 25, ct);
                            log.Write("GPU 검사: " + devices.Text.Trim());
                            if (devices.Exit != 0 || !caps.NvidiaListed(devices.Text)) throw new InvalidOperationException("사용 가능한 CUDA 장치를 확인하지 못했습니다. 드라이버·DLL·GPU 호환성을 확인하세요.");
                        }
                        if (!caps.Has("--offline")) log.Write("주의: --offline 미지원. 로컬 GGUF만 지정하고 router 모드는 사용하지 않습니다. 폐쇄망 정책이 외부 통신을 차단해야 합니다.");
                        if (!caps.Has("--reasoning") && !caps.Has("--chat-template-kwargs")) log.Write("주의: 이 실행기에서는 서버 기본 사고 ON/OFF를 설정할 수 없습니다. 모델의 템플릿 기본값이 적용됩니다.");
                        router = active.Mode == "router" && caps.Router;
                        if (active.Mode == "router" && !router) throw new InvalidOperationException("이 실행기는 안전한 목록 모드에 필요한 --models-preset / --models-max / --offline을 모두 지원하지 않습니다.");
                        if (active.Mode == "auto") log.Write("자동 모드 → 사용자가 선택한 모델 하나만 등록합니다.");
                        await LaunchAsync(null, ct);
                        Publish("ready", "사용 가능 · " + candidate.Label + " · " + model.Name, true, true);
                        log.Write("모델 로딩 확인 완료: " + model.Id);
                        return;
                    }
                    catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); failures.Add(candidate.Label + ": 검사/로딩 시간 초과"); log.Write(failures.Last()); }
                    catch (Exception e)
                    {
                        ct.ThrowIfCancellationRequested();
                        string tail = process == null ? "" : process.Tail;
                        log.Write(candidate.Label + " 실패: " + e.Message);
                        retryReduced = candidate.IsGpu && active.GpuLayers == -1 && caps != null && Regex.IsMatch(e.Message + tail, @"out of memory|cudaMalloc|failed to allocate|not enough memory", RegexOptions.IgnoreCase);
                        failures.Add(candidate.Label + ": " + e.Message);
                    }
                    if (retryReduced)
                    {
                        CloseProcess();
                        try
                        {
                            log.Write("메모리 부족 → 같은 CUDA 실행기로 GPU 16레이어만 한 번 재시도");
                            await LaunchAsync(16, ct);
                            Publish("ready", "사용 가능 · GPU 16레이어 제한 · " + model.Name, true, true); return;
                        }
                        catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); }
                        catch (Exception retryError) { log.Write("GPU 제한 재시도 실패: " + retryError.Message); }
                    }
                    CloseProcess();
                    if (active.Backend == "auto") log.Write("다음 등록 실행기를 확인합니다. CPU 전환은 허용된 경우에만 수행합니다.");
                }
                throw new InvalidOperationException("모든 실행 시도가 실패했습니다.\r\n" + String.Join("\r\n\r\n", failures.Select(x => x.Length > 1800 ? x.Substring(0, 1800) : x)));
            }
            catch (OperationCanceledException) { CloseProcess(); Publish("stopped", "사용자가 시작 작업을 취소했습니다.", false, false); }
            catch (Exception e) { CloseProcess(); log.Write(e.ToString()); Publish("error", e.Message, false, false); }
            finally { busy = false; }
        }
        async Task LaunchAsync(int? reducedGpu, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); CloseProcess();
            for (int attempt = 0; ; attempt++)
            {
                try { PortAvailable(active.Port); break; }
                catch (InvalidOperationException) { if (attempt >= 15) throw; }
                await Task.Delay(200, ct);
            }
            string preset = Path.Combine(state, "models.generated.ini");
            if (router) Paths.AtomicWrite(preset, Plans.Preset(root, new[] { model }));
            var args = Plans.Arguments(active, caps, backend, router, Paths.Relative(root, model.FullPath), model.Id, Paths.Runtime(root, preset), reducedGpu);
            LocalApiKeys.AddArguments(args, active, caps, Paths.Runtime(root, LocalApiKeys.FilePath(state)));
            log.Write("실행: " + Paths.Quote(Paths.Relative(root, backend.Exe)) + " " + Paths.Command(args));
            Publish("loading", (router ? "모델 목록 등록 및 로딩" : "모델 로딩") + " · " + backend.Label, false, false);
            process = new OwnedProcess(OwnedProcess.Info(backend.Exe, args, root, Path.Combine(state, "cache"), state), delegate(string line) { log.Write("llama | " + line); });
            var clock = Stopwatch.StartNew();
            while (true)
            {
                ct.ThrowIfCancellationRequested(); RequireAlive();
                if (clock.Elapsed.TotalSeconds > active.LoadTimeout) throw new TimeoutException("서버 준비 제한 시간 초과. 로딩 제한을 늘리거나 로그를 확인하세요.");
                try
                {
                    var h = await http.Send(active.Port, "/health", null, 2, ct);
                    if (h.Ok && Json.Text(Json.Object(h.Body), "status") == "ok") break;
                }
                catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); }
                catch (HttpRequestException) { }
                catch (InvalidOperationException) { }
                await Task.Delay(500, ct);
            }
            RequireAlive();
            if (router)
            {
                var list = await http.Send(active.Port, "/models", null, 5, ct);
                if (list.Code == 404 || list.Code == 405) throw new RouterUnavailableException("/models 목록 API가 없습니다.");
                if (!list.Ok || !Json.Rows(list.Body).Any(x => Json.Text(x, "id") == model.Id)) throw new InvalidDataException("자동 등록한 모델이 서버 목록에 없습니다. 생성된 preset과 실행기 버전을 확인하세요.\r\n" + list.Body);
                await LoadRouterModel(ct);
            }
            // Fail closed: verify the configured key is actually required, not merely present in argv.
            if (active.ApiKeyRequired)
            {
                using (var anonymous = new LocalHttp())
                {
                    var noKey = await anonymous.Send(active.Port, "/v1/models", null, 5, ct);
                    if (noKey.Code != 401) throw new InvalidOperationException("API 키 없이 모델 API가 거부되지 않았습니다. 인증 확인 실패로 서버를 중지합니다.");
                    anonymous.ApiKey = "si-local-invalid-test-key";
                    var wrongKey = await anonymous.Send(active.Port, "/v1/models", null, 5, ct);
                    if (wrongKey.Code != 401) throw new InvalidOperationException("잘못된 API 키가 거부되지 않았습니다. 인증 확인 실패로 서버를 중지합니다.");
                }
                log.Write("API 인증 검사: 키 없음 / 잘못된 키 → HTTP 401 확인");
            }
            var v1 = await http.Send(active.Port, "/v1/models", null, 5, ct);
            if (!v1.Ok || !Json.Rows(v1.Body).Any(x => Json.Text(x, "id") == model.Id)) throw new InvalidDataException("/v1/models에서 API 모델 이름을 확인하지 못했습니다.\r\n" + v1.Body);
            RequireAlive();
            Publish("testing", "모델 로딩 확인 · 실제 생성 API 검사 중", true, false);
            await GenerateAsync(ct);
            RequireAlive();
        }
        void RequireAlive()
        { if (!Alive) throw new InvalidOperationException("llama-server가 종료되었습니다. " + (process == null ? "" : "종료 코드 " + process.ExitCode + "\r\n" + process.Tail)); }
        async Task LoadRouterModel(CancellationToken ct)
        {
            Publish("loading", "모델 로딩 중 · " + model.Name + " · " + backend.Label, true, false);
            var load = await http.Send(active.Port, "/models/load", new { model = model.Id }, active.LoadTimeout, ct);
            if (load.Code == 404 || load.Code == 405) throw new RouterUnavailableException("/models/load API가 없습니다.");
            if (!load.Ok) throw new InvalidOperationException("모델 로딩 요청 실패: HTTP " + load.Code + "\r\n" + load.Body);
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < active.LoadTimeout)
            {
                ct.ThrowIfCancellationRequested(); RequireAlive();
                var reply = await http.Send(active.Port, "/models", null, 5, ct);
                if (!reply.Ok) throw new InvalidOperationException("모델 로딩 상태 확인 실패: " + reply.Body);
                var row = Json.Rows(reply.Body).FirstOrDefault(x => Json.Text(x, "id") == model.Id);
                object raw;
                var status = row != null && row.TryGetValue("status", out raw) ? raw as Dictionary<string, object> : null;
                if (Json.Text(status, "value") == "loaded") return;
                if (Json.Text(status, "failed").Equals("True", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("모델 자식 프로세스 로딩 실패: " + Json.Write(status) + "\r\n" + process.Tail);
                await Task.Delay(500, ct);
            }
            throw new TimeoutException("모델이 loaded 상태가 되지 않았습니다. /health 성공만으로 모델 로딩을 성공 처리하지 않습니다.");
        }
        public async Task SwitchAsync(ModelInfo next)
        {
            if (busy || !Alive || next == null) return;
            var config = active.Clone(); config.Model = next.RelativePath;
            await StartAsync(config, inventory, next);
        }
        public async Task MonitorAsync()
        {
            if (busy || process == null || disposed) return;
            if (!Alive) { log.Write("서버가 예기치 않게 종료됐습니다. " + process.Tail); CloseProcess(); Publish("error", "서버가 종료되었습니다. 로그를 확인한 뒤 다시 시작하세요.", false, false); return; }
            if (!router) return;
            var original = process;
            try
            {
                var r = await http.Send(active.Port, "/models", null, 2, lifetime.Token);
                if (busy || process != original || !r.Ok) return;
                var loaded = Json.Rows(r.Body).FirstOrDefault(x => { object raw; var v = x.TryGetValue("status", out raw) ? raw as Dictionary<string, object> : null; return Json.Text(v, "value") == "loaded"; });
                if (loaded != null)
                {
                    string id = Json.Text(loaded, "id"); var m = inventory.Models.FirstOrDefault(x => x.Id == id);
                    if (m != null && (!Status.Ready || model == null || model.Id != m.Id)) { model = m; Publish("ready", "사용 가능 · " + m.Name, true, true); }
                }
                else if (Status.Ready) Publish("listening", "서버 실행 중 · 모델 미로딩/변경 중. llama-ui에서 선택하거나 모델 불러오기를 누르세요.", true, false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { log.Write("상태 확인: " + e.Message); }
        }
        public async Task<string> TestAsync()
        {
            if (busy || !Alive || model == null) throw new InvalidOperationException("모델을 먼저 불러오세요.");
            busy = true;
            try { return await GenerateAsync(lifetime.Token); }
            finally { busy = false; }
        }
        async Task<string> GenerateAsync(CancellationToken ct)
        {
                var watch = Stopwatch.StartNew();
                var body = new Dictionary<string, object> {
                    { "model", model.Id }, { "messages", new[] { new { role = "user", content = "Reply with OK only." } } },
                    { "max_tokens", 24 }, { "temperature", 0.2 }, { "stream", false }
                };
                // A short diagnostic must not exhaust its token budget on hidden reasoning.
                // This affects this request only; normal client requests keep their own options.
                body["chat_template_kwargs"] = new { enable_thinking = false };
                body["reasoning_effort"] = "none";
                var r = await http.Send(active.Port, "/v1/chat/completions", body, active.LoadTimeout, ct);
                if (!r.Ok) throw new InvalidOperationException("생성 테스트 실패: HTTP " + r.Code + "\r\n" + r.Body);
                var o = Json.Object(r.Body); object choices;
                if (!o.TryGetValue("choices", out choices)) throw new InvalidDataException("응답에 choices가 없습니다.");
                var a = choices as System.Collections.IEnumerable; string content = "", finish = "";
                if (a != null) foreach (var entry in a)
                {
                    var d = entry as Dictionary<string, object>; object msg;
                    if (d != null && d.TryGetValue("message", out msg)) content = Json.Text(msg as Dictionary<string, object>, "content");
                    finish = Json.Text(d, "finish_reason"); break;
                }
                if (String.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("응답 본문이 비어 있습니다. Thinking/출력 한도/모델 템플릿을 확인하세요. finish_reason=" + finish);
                log.Write("API 생성 테스트 완료 · " + watch.Elapsed.TotalSeconds.ToString("0.00") + "초 · finish_reason=" + finish);
                return "API 응답을 받았습니다.\r\n경과: " + watch.Elapsed.TotalSeconds.ToString("0.00") + "초\r\n종료 이유: " + finish + "\r\n\r\n" + content;
        }
        public void Dispose()
        {
            disposed = true; Stop(); if (lifetime != null) lifetime.Dispose(); http.Dispose();
        }
    }
}
