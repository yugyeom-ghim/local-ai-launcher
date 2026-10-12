using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using SILocalAI.Distribution;

public sealed class ModelDownloadEntry
{
    public string id, name;
    public DownloadItem model, license;
    public DownloadItem[] parts;
}
public sealed class ModelDownloadCatalog
{ public ModelDownloadEntry[] models; }
public static class ModelDownload
{
    static void CheckOrigin(DownloadItem item)
    {
        FullDownload.CheckItem(item);
        Uri url;
        if (!Uri.TryCreate(item.url, UriKind.Absolute, out url) || url.Scheme != "https" || url.Host != "github.com"
            || !url.AbsolutePath.StartsWith("/yugyeom-ghim/local-ai-launcher/releases/download/", StringComparison.Ordinal)
            || !url.AbsolutePath.EndsWith("/" + item.name, StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected download origin.");
    }
    public static void Validate(ModelDownloadEntry entry)
    {
        if (entry == null || entry.id == null || !System.Text.RegularExpressions.Regex.IsMatch(entry.id, "^[a-z0-9-]+$"))
            throw new InvalidDataException("Invalid model ID.");
        FullDownload.CheckItem(entry.model);
        if (!entry.model.name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) || entry.model.size > 512L * 1024 * 1024 * 1024)
            throw new InvalidDataException("Invalid GGUF.");
        CheckOrigin(entry.license);
        if (entry.license.name != entry.id + "-LICENSE.txt") throw new InvalidDataException("Invalid license name.");
        if (entry.parts == null || entry.parts.Length == 0 || entry.parts.Length > 512) throw new InvalidDataException("Missing model parts.");
        for (int i = 0; i < entry.parts.Length; i++)
        {
            var item = entry.parts[i]; CheckOrigin(item);
            if (item.name != entry.model.name + ".part" + (i + 1).ToString("00") || item.size >= 2147483648L)
                throw new InvalidDataException("Invalid model part order/size.");
        }
        if (entry.parts.Sum(x => x.size) != entry.model.size) throw new InvalidDataException("Invalid model part sizes.");
    }
    // A process-owned mutex prevents concurrent writes without creating download.lock.
    public static string GateName(string output)
    {
        using (var sha = SHA256.Create())
            return "Local\\LocalAI.ModelDownload." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(output).ToUpperInvariant()))).Replace("-", "");
    }
    public static string Prepare(ModelDownloadEntry entry, string root, string partsFolder, bool assembleOnly, bool verifyOnly)
    {
        Validate(entry);
        root = Path.GetFullPath(root); partsFolder = Path.GetFullPath(partsFolder);
        string models = Path.Combine(root, "models"), licenses = Path.Combine(root, "licenses");
        string output = Path.Combine(models, entry.model.name), license = Path.Combine(licenses, entry.license.name);
        string cache = Path.Combine(root, ".download-cache-" + entry.model.sha256.Substring(0, 16));
        bool held = false, complete = false, assembling = false;
        using (var gate = new Mutex(false, GateName(output)))
        {
            try
            {
                try { held = gate.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                if (!held) throw new IOException("이 모델을 이미 다운로드하고 있습니다.");
                bool exists = FullDownload.Verified(output, entry.model);
                if (File.Exists(output) && !exists) throw new InvalidDataException("같은 이름의 다른 모델이 있습니다. 다른 폴더를 사용하세요.");
                if (verifyOnly)
                {
                    if (!exists || !FullDownload.Verified(license, entry.license)) throw new FileNotFoundException("완성된 모델 또는 라이선스 파일이 없거나 손상됐습니다.");
                    complete = true; return output;
                }
                Directory.CreateDirectory(models); Directory.CreateDirectory(licenses);
                if (!FullDownload.Verified(license, entry.license))
                {
                    if (File.Exists(license)) throw new InvalidDataException("기존 라이선스 파일의 SHA256이 다릅니다.");
                    string supplied = Path.Combine(partsFolder, entry.license.name);
                    if (FullDownload.Verified(supplied, entry.license)) File.Copy(supplied, license);
                    else if (assembleOnly) throw new FileNotFoundException("라이선스 파일이 없거나 손상됐습니다.");
                    else FullDownload.Fetch(entry.license, license, delegate(long count) { });
                }
                if (!exists)
                {
                    Directory.CreateDirectory(cache);
                    var paths = new string[entry.parts.Length]; long done = 0; DateTime last = DateTime.MinValue;
                    for (int i = 0; i < entry.parts.Length; i++)
                    {
                        var item = entry.parts[i]; string supplied = Path.Combine(partsFolder, item.name);
                        paths[i] = FullDownload.Verified(supplied, item) ? supplied : Path.Combine(cache, item.name);
                        if (!FullDownload.Verified(paths[i], item))
                        {
                            if (assembleOnly) throw new FileNotFoundException("받아 둔 파일이 없거나 손상됐습니다: " + item.name);
                            // Only our own corrupt cache is discarded; supplied files are preserved.
                            if (File.Exists(paths[i])) File.Delete(paths[i]);
                        }
                        FullDownload.Fetch(item, paths[i], count => {
                            if ((DateTime.UtcNow - last).TotalSeconds < 2) return; last = DateTime.UtcNow;
                            Console.Write("\r{0} {1:0.0}%   ", entry.name, (done + count) * 100.0 / entry.model.size);
                        });
                        done += item.size;
                    }
                    Console.WriteLine("\nGGUF 생성 및 SHA256 검사 중...");
                    assembling = true;
                    FullDownload.Assemble(new DownloadManifest { archive = entry.model, parts = entry.parts }, paths, output);
                }
                complete = true; return output;
            }
            finally
            {
                if (held)
                {
                    try
                    {
                        if (assembling && File.Exists(output + ".partial")) File.Delete(output + ".partial");
                        if (Directory.Exists(cache))
                        {
                            if (complete) foreach (var part in entry.parts)
                            {
                                string owned = Path.Combine(cache, part.name);
                                if (File.Exists(owned)) File.Delete(owned);
                                if (File.Exists(owned + ".partial")) File.Delete(owned + ".partial");
                            }
                            if (!Directory.EnumerateFileSystemEntries(cache).Any()) Directory.Delete(cache);
                        }
                    }
                    finally { gate.ReleaseMutex(); }
                }
            }
        }
    }
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "인터넷 PC용 · 모델 다운로드";
        int result = 0;
        try
        {
            ModelDownloadCatalog catalog;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ModelDownloads"))
            using (var reader = new StreamReader(stream)) catalog = new JavaScriptSerializer().Deserialize<ModelDownloadCatalog>(reader.ReadToEnd());
            foreach (var model in catalog.models) Validate(model);
            if (args.Contains("--list"))
            {
                foreach (var entry in catalog.models) Console.WriteLine(entry.id + " · " + entry.name + " · " + entry.model.size);
                return 0;
            }
            string requested = Option(args, "--model");
            if (requested == null)
            {
                Console.WriteLine("추가할 모델을 선택하세요.\n");
                for (int i = 0; i < catalog.models.Length; i++) Console.WriteLine("{0}. {1} · {2:0.00} GB", i + 1, catalog.models[i].name, catalog.models[i].model.size / 1e9);
                Console.WriteLine("0. 모두 받기"); Console.Write("번호: ");
                int choice;
                if (!Int32.TryParse(Console.ReadLine(), out choice) || choice < 0 || choice > catalog.models.Length) throw new ArgumentException("번호를 확인하세요.");
                requested = choice == 0 ? "all" : catalog.models[choice - 1].id;
            }
            var selected = catalog.models.Where(x => requested == "all" || x.id == requested).ToArray();
            if (selected.Length == 0) throw new ArgumentException("모델 ID를 확인하세요.");
            string root = Path.GetFullPath(Option(args, "--directory") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Local_AI_Models"));
            string parts = Option(args, "--parts-directory") ?? AppDomain.CurrentDomain.BaseDirectory;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            foreach (var model in selected)
                Console.WriteLine("\n완료: " + Prepare(model, root, parts, args.Contains("--assemble-only"), args.Contains("--verify-only")));
            Console.WriteLine("GGUF를 앱의 모델 폴더에 복사하세요. 라이선스는 licenses 폴더에 있습니다.");
        }
        catch (Exception e) { Console.WriteLine("완료하지 못했습니다: " + e.Message); result = 1; }
        if (!args.Contains("--no-pause")) { Console.WriteLine("Enter를 누르면 닫습니다."); Console.ReadLine(); }
        return result;
    }
    static string Option(string[] args, string key)
    {
        int i = Array.IndexOf(args, key); if (i < 0) return null;
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException(key + " 값이 필요합니다.");
        return args[i + 1];
    }
}
