// Automatic local catalog; no network, model download, or process launch.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SILocalAI
{
    public sealed class FileStability
    {
        sealed class Stamp { public long Size, Modified; public DateTime Since; }
        readonly Dictionary<string, Stamp> stamps = new Dictionary<string, Stamp>(StringComparer.OrdinalIgnoreCase);
        public bool Observe(string file, long size, long modified, DateTime now)
        {
            Stamp last;
            if (!stamps.TryGetValue(file, out last) || last.Size != size || last.Modified != modified)
            { stamps[file] = new Stamp { Size = size, Modified = modified, Since = now }; return false; }
            return now - last.Since >= TimeSpan.FromSeconds(2.5);
        }
        public void Retain(IEnumerable<string> current)
        {
            var keep = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            foreach (string key in stamps.Keys.Where(k => !keep.Contains(k)).ToArray()) stamps.Remove(key);
        }
    }
    public sealed class LibraryMonitor
    {
        readonly FileStability stability = new FileStability();
        string lastSignature = "";
        public Inventory Poll(string root, Settings s, bool force)
        {
            // Called serially by the UI coordinator on a worker thread. Metadata only, not tensor hashing.
            s.Validate();
            string assets = Paths.Full(root, s.AssetRoot), models = Paths.Full(assets, s.ModelsPath);
            var ready = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = Paths.Files(models, "*.gguf", 6, null).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            var fingerprint = new StringBuilder(Json.Write(new[] { s.AssetRoot, s.ModelsPath, s.CpuPath, s.Cuda12Path, s.Cuda13Path, s.Cuda134Path }));
            DateTime now = DateTime.UtcNow;
            foreach (string file in files)
            {
                try
                {
                    var info = new FileInfo(file); long size = info.Length, modified = info.LastWriteTimeUtc.Ticks;
                    bool settled = stability.Observe(file, size, modified, now);
                    if (settled)
                    {
                        // Denies sharing with an active writer; a Created event alone is not copy completion.
                        try { using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)) { settled = stream.Length == size; } }
                        catch (IOException) { settled = false; }
                        catch (UnauthorizedAccessException) { settled = false; }
                    }
                    if (settled) ready.Add(file);
                    fingerprint.Append('\n').Append(file).Append('|').Append(size).Append('|').Append(modified).Append('|').Append(settled);
                }
                catch (IOException) { fingerprint.Append("\nmissing:").Append(file); }
                catch (UnauthorizedAccessException) { fingerprint.Append("\nunreadable:").Append(file); }
            }
            stability.Retain(files);
            foreach (string rel in new[] { s.CpuPath, s.Cuda12Path, s.Cuda13Path, s.Cuda134Path })
            {
                string folder = Paths.Full(assets, rel);
                fingerprint.Append('\n').Append(folder).Append('|').Append(Directory.Exists(folder));
                foreach (string f in Paths.Files(folder, "*", 3, null).Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f))
                { try { var info = new FileInfo(f); fingerprint.Append('\n').Append(f).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks); } catch (IOException) { } }
            }
            string signature = Paths.Hash(fingerprint.ToString());
            if (!force && signature == lastSignature) return null;
            var inv = Discovery.Scan(root, s, delegate(string f) { return ready.Contains(f); });
            lastSignature = signature;
            return inv;
        }
        public static string ModelSignature(IEnumerable<ModelInfo> models)
        { return Paths.Hash(String.Join("\n", models.OrderBy(m => m.Id).Select(m => m.Id + "|" + m.Bytes + "|" + m.FullPath))); }
    }
}
