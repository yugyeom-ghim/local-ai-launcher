using System;
using System.IO;
using System.Linq;
using SILocalAI.Distribution;

public static class InstallerDownloadTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
    static bool Fails(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } catch (IOException) { return true; } }
    static DownloadItem Item(string path) { return new DownloadItem { name = Path.GetFileName(path), size = new FileInfo(path).Length, sha256 = FullDownload.Hash(path), url = "https://github.com/yugyeom-ghim/local-ai-launcher/releases/download/test/" + Path.GetFileName(path) }; }
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "installer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string expected = Path.Combine(root, "Local_AI_Offline_Setup.exe");
            File.WriteAllText(expected, "MZ-test-fixture-not-executable");
            string first = expected + ".part01", second = expected + ".part02";
            byte[] data = File.ReadAllBytes(expected);
            File.WriteAllBytes(first, data.Take(7).ToArray()); File.WriteAllBytes(second, data.Skip(7).ToArray());
            var manifest = new InstallerManifest { installer = Item(expected), parts = new[] { Item(first), Item(second) } };
            string outputRoot = Path.Combine(root, "result");
            string output = InstallerDownload.Prepare(manifest, outputRoot, root, true, false);
            Check(FullDownload.Verified(output, manifest.installer) && Directory.GetFileSystemEntries(outputRoot).Length == 1, "only one verified offline EXE remains");
            Check(File.Exists(first) && File.Exists(second), "manual parts preserved");
            File.Delete(first); File.Delete(second);
            InstallerDownload.Prepare(manifest, outputRoot, root, false, true);
            Check(FullDownload.Verified(output, manifest.installer), "completed EXE verifies without parts or network");
            File.WriteAllText(output, "preserve this file");
            Check(Fails(() => InstallerDownload.Prepare(manifest, outputRoot, root, true, false)) && File.ReadAllText(output) == "preserve this file", "different existing EXE never overwritten");
            File.Delete(output);
            Check(Fails(() => InstallerDownload.Prepare(manifest, outputRoot, root, true, false)) && !File.Exists(output), "missing parts fail without network or incomplete EXE");
            string cache = Path.Combine(outputRoot, ".download-cache-" + manifest.installer.sha256.Substring(0,16));
            Directory.CreateDirectory(cache);
            File.WriteAllBytes(Path.Combine(cache, manifest.parts[0].name), data.Take(7).ToArray());
            File.WriteAllBytes(Path.Combine(cache, manifest.parts[1].name), data.Skip(7).ToArray());
            InstallerDownload.Prepare(manifest, outputRoot, root, true, false);
            Check(FullDownload.Verified(output, manifest.installer) && !Directory.Exists(cache), "resume from owned cache then remove download parts");
            manifest.parts[0].url = "https://example.invalid/file";
            Check(Fails(() => InstallerDownload.Validate(manifest)), "unexpected origin rejected");
            manifest.parts[0].url = "https://github.com/yugyeom-ghim/local-ai-launcher/releases/download/test/" + manifest.parts[0].name;
            manifest.parts[1].name = manifest.parts[0].name;
            Check(Fails(() => InstallerDownload.Validate(manifest)), "duplicate or reordered parts rejected");
            manifest.parts[1].name = Path.GetFileName(second);
            manifest.installer.size++;
            Check(Fails(() => InstallerDownload.Validate(manifest)), "incorrect total size rejected");
            Console.WriteLine("Installer download checks: " + passed + " passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
