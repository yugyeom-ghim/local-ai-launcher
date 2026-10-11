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
    public DownloadItem[] files { get; set; }
}

public static class InstallerDownload
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "인터넷 PC 전용 · Local AI 설치 파일 다운로드";
        int result = 0;
        try
        {
            bool verifyOnly = args.Contains("--verify-only");
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Local_AI_Offline_Installer");
            int index = Array.IndexOf(args, "--directory");
            if (index >= 0) folder = Path.GetFullPath(args[index + 1]);
            InstallerManifest manifest;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InstallerManifest"))
            using (var reader = new StreamReader(stream))
                manifest = new JavaScriptSerializer().Deserialize<InstallerManifest>(reader.ReadToEnd());
            if (manifest.files == null || manifest.files.Length < 2) throw new InvalidDataException("Missing installer files.");
            foreach (var item in manifest.files)
            {
                FullDownload.CheckItem(item);
                if (!item.url.StartsWith("https://github.com/yugyeom-ghim/si_local_model/releases/download/", StringComparison.Ordinal) && !item.url.StartsWith("https://github.com/yugyeom-ghim/local-ai-launcher/releases/download/", StringComparison.Ordinal))
                    throw new InvalidDataException("Unexpected download origin.");
            }
            if (manifest.files.Select(x => x.name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.files.Length)
                throw new InvalidDataException("Duplicate installer file names.");
            Console.WriteLine("Local AI Launcher 설치 파일 다운로드");
            Console.WriteLine("GitHub에서 설치 파일을 받습니다.");
            if (verifyOnly) Console.WriteLine("검증 전용: 네트워크 다운로드를 수행하지 않습니다.");
            Directory.CreateDirectory(folder);
            using (var fileLock = new FileStream(Path.Combine(folder, "download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                long total = manifest.files.Sum(x => x.size), done = 0;
                DateTime last = DateTime.MinValue;
                foreach (var item in manifest.files)
                {
                    string path = Path.Combine(folder, item.name);
                    if (verifyOnly)
                    {
                        if (!FullDownload.Verified(path, item)) throw new InvalidDataException("Missing or damaged: " + item.name);
                    }
                    else FullDownload.Fetch(item, path, count => {
                        if ((DateTime.UtcNow - last).TotalSeconds < 2) return;
                        last = DateTime.UtcNow;
                        Console.Write("\r다운로드 {0:F1}% ({1:F2} / {2:F2} GB)   ", 100.0 * (done + count) / total, (done + count) / 1e9, total / 1e9);
                    });
                    done += item.size;
                    Console.WriteLine("\nSHA256 OK: " + item.name);
                }
                Console.WriteLine("완료: " + folder);
                Console.WriteLine("설치 폴더를 업무망으로 반입하세요.");
                Console.WriteLine("설치: Local_AI_Offline_Setup.exe");
            }
        }
        catch (Exception ex) { Console.WriteLine("완료하지 못했습니다: " + ex.Message); result = 1; }
        if (!args.Contains("--no-pause")) { Console.WriteLine("Enter를 누르면 닫습니다."); Console.ReadLine(); }
        return result;
    }
}
