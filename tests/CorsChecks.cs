using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SILocalAI;

// Uses the packaged Windows server and the launcher's real argument/environment builders.
// Empty router mode exercises HTTP middleware without downloading or loading a model.
class CorsChecks
{
    static int checks;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++; Console.WriteLine("PASS " + name);
    }
    static HttpWebResponse Request(int port, string method, string path, string origin, string key)
    {
        var r = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + path);
        r.Proxy = null; r.Method = method; r.Timeout = 2000;
        r.Headers["Origin"] = origin;
        if (method == "OPTIONS") {
            r.Headers["Access-Control-Request-Method"] = "POST";
            r.Headers["Access-Control-Request-Headers"] = "content-type,authorization";
        }
        if (!String.IsNullOrEmpty(key)) r.Headers["Authorization"] = "Bearer " + key;
        if (method == "POST") {
            r.ContentType = "application/json";
            byte[] body = Encoding.UTF8.GetBytes("{\"model\":\"cors-test-missing-model\",\"messages\":[{\"role\":\"user\",\"content\":\"test\"}],\"stream\":false}");
            r.ContentLength = body.Length;
            using (var stream = r.GetRequestStream()) stream.Write(body, 0, body.Length);
        }
        try { return (HttpWebResponse)r.GetResponse(); }
        catch (WebException e) { if (e.Response != null) return (HttpWebResponse)e.Response; throw; }
    }
    static void Run(string exe, string state, Capabilities caps, bool auth)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var settings = new Settings { Port = port, ApiKeyRequired = auth };
        string preset = Path.Combine(state, "empty.ini"); File.WriteAllText(preset, "version = 1\n");
        string key = LocalApiKeys.LoadOrCreate(state);
        var args = Plans.Arguments(settings, caps, new BackendInfo { Id = "cpu" }, true, "", "", preset, null);
        LocalApiKeys.AddArguments(args, settings, caps, LocalApiKeys.FilePath(state));
        using (var server = new OwnedProcess(OwnedProcess.Info(exe, args, state, Path.Combine(state, "cache"), state), null)) {
            bool ready = false;
            for (int i = 0; i < 100 && server.Alive; i++) {
                try { using (var r = Request(port, "GET", "/health", "https://confluence.example", null)) ready = r.StatusCode == HttpStatusCode.OK; }
                catch (WebException) { }
                if (ready) break;
                Thread.Sleep(100);
            }
            if (!ready) throw new Exception("Server did not start: " + server.Tail);
            foreach (string origin in new[] { "https://confluence.example:8443", "null" }) {
                string label = "auth=" + auth + " origin=" + origin;
                using (var r = Request(port, "OPTIONS", "/v1/chat/completions", origin, null)) {
                    Check(r.StatusCode == HttpStatusCode.OK, "preflight without key " + label);
                    Check(r.Headers["Access-Control-Allow-Origin"] == origin, "preflight origin " + label);
                    string[] headers = (r.Headers["Access-Control-Allow-Headers"] ?? "").ToLowerInvariant().Split(',').Select(x => x.Trim()).ToArray();
                    Check(headers.Contains("authorization") && headers.Contains("content-type"), "explicit browser headers " + label);
                    Check((r.Headers["Access-Control-Allow-Methods"] ?? "").Contains("POST"), "POST permitted " + label);
                }
                using (var r = Request(port, "GET", "/v1/models", origin, auth ? key : null)) {
                    Check(r.StatusCode == HttpStatusCode.OK && r.Headers["Access-Control-Allow-Origin"] == origin, "model listing with CORS " + label);
                }
                using (var r = Request(port, "POST", "/v1/chat/completions", origin, auth ? key : null)) {
                    Check((r.StatusCode == HttpStatusCode.BadRequest || r.StatusCode == HttpStatusCode.NotFound) && r.Headers["Access-Control-Allow-Origin"] == origin, "POST reaches missing-model handler with CORS " + label);
                }
                if (auth) foreach (string invalid in new[] { "", "wrong-test-key" }) {
                    using (var r = Request(port, "POST", "/v1/chat/completions", origin, invalid))
                        Check(r.StatusCode == HttpStatusCode.Unauthorized && r.Headers["Access-Control-Allow-Origin"] == origin, "unauthorized POST retains CORS " + label);
                }
            }
        }
    }
    public static int Main(string[] args)
    {
        string state = Path.Combine(Path.GetTempPath(), "si-cors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(state);
        try {
            string exe = Path.GetFullPath(args[0]);
            var help = Probe.Run(exe, new[] { "--help" }, state, state, 20, CancellationToken.None).GetAwaiter().GetResult();
            Check(help.Exit == 0, "pinned server help");
            var caps = new Capabilities(help.Text);
            Check(caps.Has("--cors-origins") && caps.Has("--cors-headers"), "pinned CORS capabilities");
            Run(exe, state, caps, false); Run(exe, state, caps, true);
            Console.WriteLine("CORS checks passed: " + checks + "; no model inference or real Confluence browser tested.");
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { try { Directory.Delete(state, true); } catch { } }
    }
}
