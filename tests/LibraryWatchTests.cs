using System;
using System.IO;
using System.Threading;
using SILocalAI;
class LibraryWatchTests
{
    static void Require(bool condition, string text) { if (!condition) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static Inventory Await(LibraryMonitor m, string root, Settings s)
    { for (int i = 0; i < 50; i++) { Thread.Sleep(100); var found = m.Poll(root, s, false); if (found != null) return found; } throw new Exception("File change was not detected"); }
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "local-ai-watch-" + Guid.NewGuid().ToString("N"));
        string models = Path.Combine(root, "ai", "models"); Directory.CreateDirectory(models);
        try
        {
            var s = new Settings { AssetRoot = "ai" };
            using (var m = new LibraryMonitor())
            {
                m.Poll(root, s, true); int before = m.ScanCount;
                for (int i = 0; i < 30; i++) { if (m.Poll(root, s, false) != null) throw new Exception("Unexpected idle update"); }
                Require(m.ScanCount == before, "no idle directory scans");
                File.WriteAllText(Path.Combine(models, "copy.gguf"), "partial");
                Require(Await(m, root, s).PendingFiles.Count == 1, "new model waits for copy stability");
                Thread.Sleep(3100); Require(m.Poll(root, s, false).Models.Count == 0, "invalid model excluded after settling");
                File.Move(Path.Combine(models, "copy.gguf"), Path.Combine(models, "renamed.gguf"));
                Require(Await(m, root, s).PendingFiles.Count == 1, "renamed model discovered");
                File.Delete(Path.Combine(models, "renamed.gguf"));
                Require(Await(m, root, s).PendingFiles.Count == 0, "deleted model removed");
                before = m.ScanCount; File.WriteAllText(Path.Combine(models, "notes.txt"), "ignored"); Thread.Sleep(750); m.Poll(root, s, false);
                Require(m.ScanCount == before, "unrelated text does not rescan");
                s.ModelsPath = "new-models"; m.Poll(root, s, false); Directory.CreateDirectory(Path.Combine(root, "ai", s.ModelsPath));
                File.WriteAllText(Path.Combine(root, "ai", s.ModelsPath, "added.gguf"), "partial");
                Require(Await(m, root, s).PendingFiles.Count == 1, "new configured folder detected");
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
