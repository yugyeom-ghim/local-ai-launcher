// Automatic local catalog; no network, model download, or process launch.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

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
    public sealed class LibraryMonitor : IDisposable
    {
        readonly FileStability stability = new FileStability();
        string lastSignature = "";
        readonly object gate = new object();
        readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        string watchConfig = "";
        long changes;
        DateTime due = DateTime.MinValue;
        bool disposed, fallback;
        public int ScanCount { get; private set; }
        public bool RefreshDue { get { lock (gate) { return !disposed && DateTime.UtcNow >= due; } } }
        void Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath)) return;
            string ext = Path.GetExtension(e.FullPath);
            var renamed = e as RenamedEventArgs;
            string oldExt = renamed == null ? "" : Path.GetExtension(renamed.OldFullPath);
            // Ignore logs, settings and import progress. Renaming a directory may add many models.
            if (ext.Length > 0 && !Relevant(ext) && !Relevant(oldExt) && !Directory.Exists(e.FullPath)) return;
            lock (gate) { if (disposed) return; changes++; due = DateTime.UtcNow.AddMilliseconds(600); }
        }
        static bool Relevant(string ext)
        { return ext.Equals(".gguf", StringComparison.OrdinalIgnoreCase) || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase); }
        void WatchError(object sender, ErrorEventArgs e)
        { lock (gate) { if (disposed) return; fallback = true; changes++; due = DateTime.UtcNow; } }
        void Configure(string root, Settings s, string key)
        {
            foreach (var watcher in watchers) watcher.Dispose(); watchers.Clear();
            string assets = Paths.Full(root, s.AssetRoot);
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            fallback = false;
            foreach (string rel in new[] { s.ModelsPath, s.CpuPath, s.Cuda12Path, s.Cuda13Path, s.Cuda134Path })
            {
                string folder = Paths.Full(assets, rel);
                // Watch an existing parent when a configured folder has not been created yet.
                while (!Directory.Exists(folder)) { var parent = Directory.GetParent(folder); if (parent == null) break; folder = parent.FullName; }
                folders.Add(folder);
            }
            foreach (string folder in folders)
            {
                try
                {
                    var watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size, InternalBufferSize = 32768 };
                    watcher.Created += Changed; watcher.Changed += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed; watcher.Error += WatchError;
                    watchers.Add(watcher); watcher.EnableRaisingEvents = true;
                }
                catch (IOException) { fallback = true; }
                catch (UnauthorizedAccessException) { fallback = true; }
            }
            watchConfig = key;
        }
        public void Dispose()
        {
            lock (gate) { disposed = true; }
            foreach (var watcher in watchers) watcher.Dispose(); watchers.Clear();
        }
        public Inventory Poll(string root, Settings s, bool force)
        {
            // Called serially by the UI coordinator on a worker thread. Metadata only, not tensor hashing.
            s.Validate();
            string key = root + "|" + Json.Write(new[] { s.AssetRoot, s.ModelsPath, s.CpuPath, s.Cuda12Path, s.Cuda13Path, s.Cuda134Path });
            long generation;
            lock (gate)
            {
                if (disposed) return null;
                if (!force && key == watchConfig && DateTime.UtcNow < due) return null;
                if (key != watchConfig) Configure(root, s, key);
                generation = changes; ScanCount++;
            }
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
            lock (gate)
            {
                // A change delivered during enumeration must still be processed on the next tick.
                if (generation == changes) due = DateTime.UtcNow.AddSeconds(files.Length > ready.Count ? 3 : fallback ? 60 : 31536000);
            }
            if (!force && signature == lastSignature) return null;
            var inv = Discovery.Scan(root, s, delegate(string f) { return ready.Contains(f); });
            lastSignature = signature;
            return inv;
        }
        public static string ModelSignature(IEnumerable<ModelInfo> models)
        { return Paths.Hash(String.Join("\n", models.OrderBy(m => m.Id).Select(m => m.Id + "|" + m.Bytes + "|" + m.FullPath))); }
    }
}
