using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using SILocalAI.Distribution;

public sealed class InstallerManifest
{
    public DownloadItem installer { get; set; }
    public DownloadItem[] parts { get; set; }
}

public static class InstallerDownload
{
    public static void Validate(InstallerManifest manifest)
    {
        if (manifest == null) throw new InvalidDataException("Missing installer manifest.");
        FullDownload.CheckItem(manifest.installer);
        if (manifest.installer.name != "Local_AI_Offline_Setup.exe" || manifest.installer.size >= 4294967295L)
            throw new InvalidDataException("Invalid standalone installer.");
        if (manifest.parts == null || manifest.parts.Length == 0) throw new InvalidDataException("Missing installer parts.");
        for (int i = 0; i < manifest.parts.Length; i++)
        {
            var item = manifest.parts[i];
            FullDownload.CheckItem(item);
            if (item.name != manifest.installer.name + ".part" + (i + 1).ToString("00") || item.size >= 2147483648L)
                throw new InvalidDataException("Invalid installer part order/size.");
            Uri url;
            if (!Uri.TryCreate(item.url, UriKind.Absolute, out url) || url.Scheme != "https" || url.Host != "github.com"
                || !url.AbsolutePath.StartsWith("/yugyeom-ghim/local-ai-launcher/releases/download/", StringComparison.Ordinal)
                || !url.AbsolutePath.EndsWith("/" + item.name, StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected download origin.");
        }
        if (manifest.parts.Sum(x => x.size) != manifest.installer.size)
            throw new InvalidDataException("Invalid installer part sizes.");
    }

    public static string Prepare(InstallerManifest manifest, string folder, string partsFolder, bool assembleOnly, bool verifyOnly)
    {
        Validate(manifest);
        folder = Path.GetFullPath(folder);
        partsFolder = Path.GetFullPath(partsFolder);
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, manifest.installer.name);
        string cache = Path.Combine(folder, ".download-cache-" + manifest.installer.sha256.Substring(0, 16));
        Directory.CreateDirectory(cache);
        string lockPath = Path.Combine(cache, "download.lock");
        bool complete = false;
        using (var fileLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            if (FullDownload.Verified(output, manifest.installer)) complete = true;
            else
            {
                if (File.Exists(output)) throw new InvalidDataException("같은 이름의 다른 설치 파일이 있습니다. 다른 폴더를 사용하세요.");
                if (verifyOnly) throw new FileNotFoundException("완성된 설치 파일이 없습니다.");
                long done = 0;
                DateTime last = DateTime.MinValue;
                var paths = new string[manifest.parts.Length];
                for (int i = 0; i < manifest.parts.Length; i++)
                {
                    var item = manifest.parts[i];
                    string manual = Path.Combine(partsFolder, item.name);
                    paths[i] = FullDownload.Verified(manual, item) ? manual : Path.Combine(cache, item.name);
                    if (assembleOnly && !FullDownload.Verified(paths[i], item))
                        throw new FileNotFoundException("받아 둔 파일이 없거나 손상됐습니다: " + item.name);
                    // Verified local bytes are reused without a network request.
                    FullDownload.Fetch(item, paths[i], count => {
                        if ((DateTime.UtcNow - last).TotalSeconds < 2) return;
                        last = DateTime.UtcNow;
                        Console.Write("\r다운로드 {0:F1}% ({1:F2} / {2:F2} GB)   ", 100.0 * (done + count) / manifest.installer.size,
                            (done + count) / 1e9, manifest.installer.size / 1e9);
                    });
                    done += item.size;
                }
                Console.WriteLine("\nSetup.exe 생성 및 SHA256 검사 중...");
                FullDownload.Assemble(new DownloadManifest { archive = manifest.installer, parts = manifest.parts }, paths, output);
                complete = true;
            }
            // Never delete manually supplied parts or unrelated files.
            if (complete)
                foreach (var item in manifest.parts)
                {
                    string owned = Path.Combine(cache, item.name);
                    if (File.Exists(owned)) File.Delete(owned);
                    if (File.Exists(owned + ".partial")) File.Delete(owned + ".partial");
                }
        }
        if (complete)
        {
            File.Delete(lockPath);
            if (!Directory.EnumerateFileSystemEntries(cache).Any()) Directory.Delete(cache);
        }
        return output;
    }

    static string Argument(string[] args, string name, string fallback)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0) return fallback;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException(name + " 경로가 필요합니다.");
        return Path.GetFullPath(args[index + 1]);
    }

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "인터넷 PC용 · Local AI 설치 파일 다운로드";
        int result = 0;
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string folder = Argument(args, "--directory", Path.Combine(root, "Local_AI_Offline_Installer"));
            string partsFolder = Argument(args, "--parts-directory", root);
            InstallerManifest manifest;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerManifest"))
            using (var reader = new StreamReader(stream))
                manifest = new JavaScriptSerializer().Deserialize<InstallerManifest>(reader.ReadToEnd());
            bool verifyOnly = args.Contains("--verify-only"), assembleOnly = args.Contains("--assemble-only");
            Console.WriteLine("Local AI Launcher 설치 파일 다운로드");
            if (!verifyOnly && !assembleOnly) Console.WriteLine("GitHub에서 받아 Setup.exe 하나로 저장합니다. 여유 공간 9GB가 필요합니다.");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string output = Prepare(manifest, folder, partsFolder, assembleOnly, verifyOnly);
            Console.WriteLine("\n완료: " + output);
            Console.WriteLine("이 EXE 하나를 업무망으로 옮겨 설치하세요.");
        }
        catch (Exception ex) { Console.WriteLine("완료하지 못했습니다: " + ex.Message); result = 1; }
        if (!args.Contains("--no-pause")) { Console.WriteLine("Enter를 누르면 닫습니다."); Console.ReadLine(); }
        return result;
    }
}
