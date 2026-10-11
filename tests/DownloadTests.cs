using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SILocalAI.Distribution;

public static class DownloadTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
    static DownloadItem Item(string path) { return new DownloadItem { name = Path.GetFileName(path), size = new FileInfo(path).Length, sha256 = FullDownload.Hash(path) }; }
    static bool Rejected(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
    static void RangeTest(string root, bool ranges)
    {
        byte[] body = Encoding.UTF8.GetBytes("full verified offline download");
        string reference = Path.Combine(root, "reference"); File.WriteAllBytes(reference, body);
        var item = Item(reference);
        string output = Path.Combine(root, ranges ? "resumed" : "restarted");
        File.WriteAllBytes(output + ".partial", new byte[] { body[0], body[1], body[2] });
        var server = new TcpListener(IPAddress.Loopback, 0); server.Start();
        string headers = ""; Exception serverError = null;
        var worker = new Thread(() => {
            try {
                using (var client = server.AcceptTcpClient())
                using (var stream = client.GetStream()) {
                    var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    string line;
                    while (!String.IsNullOrEmpty(line = reader.ReadLine())) headers += line + "\n";
                    int offset = ranges ? 3 : 0;
                    string response = ranges ? "HTTP/1.1 206 Partial Content\r\nContent-Range: bytes 3-" + (body.Length - 1) + "/" + body.Length + "\r\n" : "HTTP/1.1 200 OK\r\n";
                    byte[] head = Encoding.ASCII.GetBytes(response + "Content-Length: " + (body.Length - offset) + "\r\nConnection: close\r\n\r\n");
                    stream.Write(head, 0, head.Length); stream.Write(body, offset, body.Length - offset);
                }
            } catch(Exception ex) { serverError = ex; }
        });
        worker.IsBackground = true; worker.Start();
        item.url = "http://127.0.0.1:" + ((IPEndPoint)server.LocalEndpoint).Port + "/part";
        try { FullDownload.Fetch(item, output, ignored => {}); }
        finally { server.Stop(); worker.Join(3000); }
        if (serverError != null) throw serverError;
        Check(headers.ToLowerInvariant().Contains("range: bytes=3-") && FullDownload.Verified(output, item), ranges ? "HTTP Range resumes partial bytes" : "HTTP 200 restarts instead of appending duplicate bytes");
    }
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "si-download-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string first = Path.Combine(root, "part1"), second = Path.Combine(root, "part2"), expected = Path.Combine(root, "expected");
            File.WriteAllText(first, "alpha"); File.WriteAllText(second, "beta"); File.WriteAllText(expected, "alphabeta");
            var manifest = new DownloadManifest { archive = Item(expected), parts = new[] { Item(first), Item(second) } };
            Check(FullDownload.Verified(first, manifest.parts[0]), "valid SHA256 accepted");
            var unsafeItem = Item(first); unsafeItem.name = "../escape";
            Check(Rejected(() => FullDownload.CheckItem(unsafeItem)), "unsafe output name rejected");
            string assembled = Path.Combine(root, "archive.zip");
            FullDownload.Assemble(manifest, new[] { first, second }, assembled);
            Check(FullDownload.Verified(assembled, manifest.archive), "all parts assembled and whole hash checked");
            FullDownload.Assemble(manifest, new[] { first, second }, assembled);
            Check(FullDownload.Verified(assembled, manifest.archive), "verified archive reused");
            File.WriteAllText(assembled, "do not overwrite");
            Check(Rejected(() => FullDownload.Assemble(manifest, new[] { first, second }, assembled)) && File.ReadAllText(assembled) == "do not overwrite", "existing different ZIP preserved");
            File.WriteAllText(second, "xxxx");
            string rejected = Path.Combine(root, "rejected.zip");
            Check(Rejected(() => FullDownload.Assemble(manifest, new[] { first, second }, rejected)) && !File.Exists(rejected), "corrupt part cannot publish a ZIP");
            FullDownload.Fetch(manifest.parts[0], first, ignored => {});
            Check(FullDownload.Verified(first, manifest.parts[0]), "cached part needs no network");
            RangeTest(root, true); RangeTest(root, false);
            Console.WriteLine("Download checks: " + passed + " passed."); return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
