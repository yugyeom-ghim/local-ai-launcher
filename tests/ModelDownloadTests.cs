using System;
using System.IO;
using System.Linq;
using System.Threading;
using SILocalAI.Distribution;

public static class ModelDownloadTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
    static bool Fails(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } catch (IOException) { return true; } }
    static DownloadItem Item(string path) { return new DownloadItem { name = Path.GetFileName(path), size = new FileInfo(path).Length, sha256 = FullDownload.Hash(path), url = "https://github.com/yugyeom-ghim/local-ai-launcher/releases/download/test/" + Path.GetFileName(path) }; }
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "모델 다운로드 검사 " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string expected = Path.Combine(root, "fixture.gguf");
            File.WriteAllText(expected, "GGUF-fixture-for-byte-assembly");
            byte[] data = File.ReadAllBytes(expected);
            string first = expected + ".part01", second = expected + ".part02";
            File.WriteAllBytes(first, data.Take(7).ToArray()); File.WriteAllBytes(second, data.Skip(7).ToArray());
            string license = Path.Combine(root, "fixture-LICENSE.txt"); File.WriteAllText(license, "Test license");
            var entry = new ModelDownloadEntry { id = "fixture", name = "Fixture", model = Item(expected), license = Item(license), parts = new[] { Item(first), Item(second) } };
            string dest = Path.Combine(root, "완성"), output = Path.Combine(dest, "models", entry.model.name);
            ModelDownload.Prepare(entry, dest, root, true, false);
            Check(FullDownload.Verified(output, entry.model) && Directory.GetFiles(Path.GetDirectoryName(output)).Length == 1
                && FullDownload.Verified(Path.Combine(dest, "licenses", entry.license.name), entry.license)
                && Directory.GetFiles(dest, "*", SearchOption.AllDirectories).Length == 2, "only complete GGUF and separate license remain");
            Check(File.Exists(first) && File.Exists(second), "manual parts preserved");
            File.Delete(first); File.Delete(second);
            ModelDownload.Prepare(entry, dest, root, false, true);
            Check(FullDownload.Verified(output, entry.model), "completed GGUF verified without parts, JSON or network");
            File.WriteAllText(output, "preserve existing model");
            Check(Fails(() => ModelDownload.Prepare(entry, dest, root, true, false)) && File.ReadAllText(output) == "preserve existing model", "different existing GGUF preserved");
            File.Delete(output);
            Check(Fails(() => ModelDownload.Prepare(entry, dest, root, true, false)) && !File.Exists(output)
                && !Directory.GetFiles(dest, "*.lock", SearchOption.AllDirectories).Any(), "missing parts leave no model or lock");
            string cache = Path.Combine(dest, ".download-cache-" + entry.model.sha256.Substring(0, 16)); Directory.CreateDirectory(cache);
            File.WriteAllBytes(Path.Combine(cache, entry.parts[0].name), data.Take(7).ToArray());
            File.WriteAllBytes(Path.Combine(cache, entry.parts[1].name), data.Skip(7).ToArray());
            ModelDownload.Prepare(entry, dest, root, true, false);
            Check(FullDownload.Verified(output, entry.model) && !Directory.Exists(cache), "verified cache resumed and removed");
            File.Delete(output);
            File.WriteAllBytes(first, data.Take(7).ToArray()); File.WriteAllBytes(second, data.Skip(7).ToArray());
            byte[] damaged = File.ReadAllBytes(second); damaged[0] ^= 1; File.WriteAllBytes(second, damaged);
            Check(Fails(() => ModelDownload.Prepare(entry, dest, root, true, false)) && !File.Exists(output) && File.ReadAllBytes(second).SequenceEqual(damaged), "corrupt supplied part rejected and preserved");
            File.WriteAllBytes(second, data.Skip(7).ToArray());
            string sha = entry.model.sha256; entry.model.sha256 = new string('0', 64);
            Check(Fails(() => ModelDownload.Prepare(entry, dest, root, true, false)) && !File.Exists(output) && !File.Exists(output + ".partial"), "whole digest mismatch removes incomplete GGUF");
            entry.model.sha256 = sha;
            File.Delete(Path.Combine(dest, "licenses", entry.license.name)); File.Delete(license);
            Check(Fails(() => ModelDownload.Prepare(entry, dest, root, true, false)) && !File.Exists(output), "missing license prevents incomplete delivery");
            File.WriteAllText(license, "Test license");
            bool rejected = false;
            using (var gate = new Mutex(false, ModelDownload.GateName(output)))
            {
                gate.WaitOne();
                var other = new Thread(() => { rejected = Fails(() => ModelDownload.Prepare(entry, dest, root, true, false)); });
                other.Start(); other.Join(); gate.ReleaseMutex();
            }
            Check(rejected && !File.Exists(output) && !Directory.GetFiles(dest, "*.lock", SearchOption.AllDirectories).Any(), "concurrent writer rejected without lock file");
            string url = entry.parts[0].url; entry.parts[0].url = "https://example.invalid/file";
            Check(Fails(() => ModelDownload.Validate(entry)), "unexpected origin rejected"); entry.parts[0].url = url;
            entry.parts[1].name = entry.parts[0].name;
            Check(Fails(() => ModelDownload.Validate(entry)), "duplicate or reordered parts rejected"); entry.parts[1].name = Path.GetFileName(second);
            entry.model.size++;
            Check(Fails(() => ModelDownload.Validate(entry)), "incorrect total size rejected");
            Console.WriteLine("Model download checks: " + passed + " passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
