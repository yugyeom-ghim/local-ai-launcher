using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace SILocalAI.Distribution
{
    public sealed class DownloadItem
    {
        public string name { get; set; }
        public long size { get; set; }
        public string sha256 { get; set; }
        public string url { get; set; }
    }
    public sealed class DownloadManifest
    {
        public DownloadItem archive { get; set; }
        public DownloadItem[] parts { get; set; }
    }

    public static class FullDownload
    {
        public static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static bool Verified(string path, DownloadItem item)
        {
            return File.Exists(path) && new FileInfo(path).Length == item.size && Hash(path) == item.sha256;
        }
        public static void CheckItem(DownloadItem item)
        {
            if (item == null || String.IsNullOrEmpty(item.name) || item.name != Path.GetFileName(item.name)
                || item.name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || item.name.Contains("..")
                || item.size <= 0 || item.sha256 == null || item.sha256.Length != 64
                || item.sha256.Any(c => !"0123456789abcdef".Contains(c)))
                throw new InvalidDataException("Invalid download manifest.");
        }
        public static void Fetch(DownloadItem item, string path, Action<long> progress)
        {
            if (Verified(path, item)) { progress(item.size); return; }
            if (File.Exists(path)) throw new InvalidDataException("기존 파일의 SHA256이 다릅니다: " + path);
            string partial = path + ".partial";
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    long offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                    if (offset >= item.size)
                    {
                        if (Verified(partial, item)) { File.Move(partial, path); progress(item.size); return; }
                        File.Delete(partial); offset = 0;
                    }
                    var request = (HttpWebRequest)WebRequest.Create(item.url);
                    request.UserAgent = "SI-Local-AI-Downloader";
                    request.Timeout = 60000;
                    request.ReadWriteTimeout = 90000;
                    if (offset > 0) request.AddRange(offset);
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK) offset = 0;
                        else if (response.StatusCode != HttpStatusCode.PartialContent
                            || response.Headers["Content-Range"] == null
                            || !response.Headers["Content-Range"].StartsWith("bytes " + offset + "-", StringComparison.Ordinal))
                            throw new InvalidDataException("Invalid HTTP range response.");
                        using (var input = response.GetResponseStream())
                        using (var output = new FileStream(partial, offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[1024 * 1024];
                            int count;
                            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                offset += count;
                                if (offset > item.size) throw new InvalidDataException("Download exceeds expected size.");
                                output.Write(buffer, 0, count);
                                progress(offset);
                            }
                        }
                    }
                    if (new FileInfo(partial).Length < item.size)
                        throw new EndOfStreamException("다운로드가 중단되었습니다. 이어받기를 시도합니다.");
                    if (!Verified(partial, item))
                    {
                        File.Delete(partial);
                        throw new InvalidDataException("다운로드 SHA256 불일치: " + item.name);
                    }
                    File.Move(partial, path);
                    return;
                }
                catch (Exception ex)
                {
                    if (!(ex is IOException) && !(ex is WebException)) throw;
                    if (attempt == 3) throw;
                    Console.WriteLine("\n연결을 다시 시도합니다 ({0}/3). 받은 파일은 보관합니다.", attempt + 1);
                    Thread.Sleep(attempt * 2000);
                }
            }
        }
        public static void Assemble(DownloadManifest manifest, string[] paths, string output)
        {
            if (paths.Length != manifest.parts.Length) throw new InvalidDataException("Missing parts.");
            if (File.Exists(output))
            {
                if (Verified(output, manifest.archive)) return;
                throw new InvalidDataException("기존 ZIP을 덮어쓰지 않습니다. 다른 폴더를 사용하세요: " + output);
            }
            for (int i = 0; i < paths.Length; i++)
                if (!Verified(paths[i], manifest.parts[i])) throw new InvalidDataException("Part verification failed: " + paths[i]);
            string temporary = output + ".partial";
            using (var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                foreach (string part in paths)
                    using (var input = File.OpenRead(part)) input.CopyTo(destination, 1024 * 1024);
            if (!Verified(temporary, manifest.archive)) throw new InvalidDataException("전체 ZIP의 SHA256이 일치하지 않습니다.");
            File.Move(temporary, output);
        }
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Local AI 전체 다운로드";
            int result = 0;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                DownloadManifest manifest;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FullDownloadManifest"))
                using (var reader = new StreamReader(stream))
                    manifest = new JavaScriptSerializer().Deserialize<DownloadManifest>(reader.ReadToEnd());
                CheckItem(manifest.archive);
                if (manifest.parts == null || manifest.parts.Length == 0) throw new InvalidDataException("Missing parts.");
                foreach (var item in manifest.parts)
                {
                    CheckItem(item);
                    if (!item.url.StartsWith("https://github.com/yugyeom-ghim/si_local_model/releases/download/", StringComparison.Ordinal) && !item.url.StartsWith("https://github.com/yugyeom-ghim/local-ai-launcher/releases/download/", StringComparison.Ordinal))
                        throw new InvalidDataException("Unexpected download origin.");
                }
                if (manifest.parts.Select(p => p.name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.parts.Length
                    || manifest.parts.Sum(p => p.size) != manifest.archive.size)
                    throw new InvalidDataException("Invalid part sizes/names.");
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string cache = Path.Combine(root, "Local_AI_Download_Cache");
                Directory.CreateDirectory(cache);
                using (var downloadLock = new FileStream(Path.Combine(cache, "download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    string output = Path.Combine(root, manifest.archive.name);
                    Console.WriteLine("Local AI 전체 다운로드\n실행기·DLL·Qwen 모델을 GitHub에서 받아 ZIP 하나로 저장합니다.");
                    Console.WriteLine("위치: " + root);
                    Console.WriteLine("완료 후 ZIP을 압축 해제하세요. 모델이나 설치 프로그램은 자동 실행하지 않습니다.");
                    if (Verified(output, manifest.archive)) Console.WriteLine("이미 전체 다운로드와 SHA256 검사가 완료된 ZIP입니다.");
                    else
                    {
                        if (File.Exists(output)) throw new InvalidDataException("같은 이름의 다른 ZIP이 있습니다. 빈 폴더에서 실행하세요.");
                        long done = 0;
                        var paths = new string[manifest.parts.Length];
                        DateTime last = DateTime.MinValue;
                        for (int i = 0; i < manifest.parts.Length; i++)
                        {
                            var item = manifest.parts[i];
                            string manual = Path.Combine(root, item.name);
                            paths[i] = Verified(manual, item) ? manual : Path.Combine(cache, item.name);
                            if (args.Contains("--assemble-only") && !Verified(paths[i], item))
                                throw new FileNotFoundException("받아 둔 조각이 없거나 손상됐습니다: " + item.name);
                            Fetch(item, paths[i], count => {
                                if ((DateTime.UtcNow - last).TotalSeconds < 2) return;
                                last = DateTime.UtcNow;
                                Console.Write("\r다운로드 {0:F1}% ({1:F2} / {2:F2} GB)   ", 100.0 * (done + count) / manifest.archive.size,
                                    (done + count) / 1e9, manifest.archive.size / 1e9);
                            });
                            done += item.size;
                        }
                        Console.WriteLine("\n파일을 합치고 전체 SHA256을 검사합니다. 잠시 기다려 주세요.");
                        Assemble(manifest, paths, output);
                        foreach (string path in paths)
                            if (Path.GetDirectoryName(path) == cache) File.Delete(path);
                    }
                    Console.WriteLine("\n완료: " + output);
                    Console.WriteLine("이 ZIP을 승인된 절차로 사내 PC에 옮겨 압축을 푼 뒤 Launcher를 실행하세요.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n다운로드를 완료하지 못했습니다: " + ex.Message);
                Console.WriteLine("같은 위치에서 다시 실행하면 받은 파일부터 이어서 진행합니다.");
                Console.WriteLine("회사 정책으로 차단된 경우 승인된 다운로드 경로를 사용하세요.");
                result = 1;
            }
            if (!args.Contains("--no-pause")) { Console.WriteLine("\nEnter를 누르면 닫습니다."); Console.ReadLine(); }
            return result;
        }
    }
}
