using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using SILocalAI.Distribution;

public sealed class ModelDownloadEntry
{ public string id, name, tier; public DownloadItem[] files; }
public sealed class ModelDownloadCatalog
{ public ModelDownloadEntry[] models; }
public static class ModelDownload
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "인터넷 PC 전용 · 모델 다운로드";
        int result = 0;
        try
        {
            ModelDownloadCatalog catalog;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ModelDownloads"))
            using (var reader = new StreamReader(stream)) catalog = new JavaScriptSerializer().Deserialize<ModelDownloadCatalog>(reader.ReadToEnd());
            string requested = Option(args, "--model");
            if (requested == null)
            {
                Console.WriteLine("추가할 모델을 선택하세요. GitHub에서 받습니다.\n");
                for (int i = 0; i < catalog.models.Length; i++) Console.WriteLine("{0}. {1} · {2} · {3:0.00} GB", i + 1, catalog.models[i].name, catalog.models[i].tier, catalog.models[i].files.Sum(x => x.size) / 1e9);
                Console.WriteLine("0. 모두 받기"); Console.Write("번호: ");
                int choice;
                if (!Int32.TryParse(Console.ReadLine(), out choice) || choice < 0 || choice > catalog.models.Length) throw new ArgumentException("번호를 확인하세요.");
                requested = choice == 0 ? "all" : catalog.models[choice - 1].id;
            }
            var selected = catalog.models.Where(x => requested == "all" || x.id == requested).ToArray();
            if (selected.Length == 0) throw new ArgumentException("모델 ID를 확인하세요.");
            string root = Path.GetFullPath(Option(args, "--directory") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Local_AI_Model_Packs"));
            bool verify = args.Contains("--verify-only"); ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            foreach (var model in selected)
            {
                if (model.id == null || !System.Text.RegularExpressions.Regex.IsMatch(model.id, "^[a-z0-9-]+$")) throw new InvalidDataException("Invalid model ID");
                foreach (var file in model.files)
                {
                    FullDownload.CheckItem(file);
                    if (!file.url.StartsWith("https://github.com/yugyeom-ghim/local-ai-launcher/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected origin");
                }
                if (model.files.Select(x => x.name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != model.files.Length) throw new InvalidDataException("Duplicate names");
                string folder = Path.Combine(root, model.id); Directory.CreateDirectory(folder);
                using (var gate = new FileStream(Path.Combine(folder, "download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    long done = 0, total = model.files.Sum(x => x.size); DateTime last = DateTime.MinValue;
                    foreach (var file in model.files)
                    {
                        string path = Path.Combine(folder, file.name);
                        if (verify) { if (!FullDownload.Verified(path, file)) throw new InvalidDataException("Missing or damaged: " + file.name); }
                        else FullDownload.Fetch(file, path, count => {
                            if ((DateTime.UtcNow - last).TotalSeconds < 2) return; last = DateTime.UtcNow;
                            Console.Write("\r{0} {1:0.0}%   ", model.name, (done + count) * 100.0 / total);
                        });
                        done += file.size; Console.WriteLine("\nSHA256 OK: " + file.name);
                    }
                }
                Console.WriteLine("완료: " + folder);
            }
            Console.WriteLine("업무망에서 Launcher → 모델 팩 설치 → .modelpack.json 선택");
        }
        catch (Exception e) { Console.WriteLine("완료하지 못했습니다: " + e.Message); result = 1; }
        if (!args.Contains("--no-pause")) { Console.WriteLine("Enter를 누르면 닫습니다."); Console.ReadLine(); }
        return result;
    }
    static string Option(string[] args, string key)
    { int i = Array.IndexOf(args, key); if (i < 0) return null; if (i + 1 >= args.Length) throw new ArgumentException(key + " 값이 필요합니다."); return args[i + 1]; }
}
