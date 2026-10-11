using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace SILocalAI
{
    public sealed class ModelProfile
    {
        public string id, name, file, repository, revision, url, sha256, license, license_url, tier;
        public long size;
        public int vram_gib, ram_gib, context;
        public bool bundled;
    }
    public sealed class ModelCatalog
    {
        public ModelProfile[] models;
        static ModelCatalog cached;
        public static ModelCatalog Current
        {
            get
            {
                if (cached == null)
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SILocalAI.ModelCatalog"))
                    using (var reader = new StreamReader(stream)) cached = Json.Read<ModelCatalog>(reader.ReadToEnd());
                return cached;
            }
        }
        public ModelProfile Find(ModelInfo model)
        { return model == null ? null : models.FirstOrDefault(x => x.file == Path.GetFileName(model.FullPath) && x.size == model.Bytes); }
        public ModelProfile Recommend(double vramGiB, double ramGiB)
        {
            // Planning estimates for one GPU; never sum VRAM across adapters.
            return models.Where(x => x.vram_gib <= vramGiB + 0.25 && x.ram_gib <= ramGiB + 0.25)
                .OrderByDescending(x => x.vram_gib).ThenByDescending(x => x.size).FirstOrDefault();
        }
    }
    public sealed class ModelPart
    { public string name, sha256, url; public long size; }
    public sealed class ModelPack
    { public int schema; public ModelProfile model; public ModelPart[] parts; }

    public static class ModelPacks
    {
        static string Hex(byte[] hash) { return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); }
        static bool Digest(string hash) { return hash != null && Regex.IsMatch(hash, "^[0-9a-f]{64}$"); }
        static bool Leaf(string name)
        {
            return name != null && Regex.IsMatch(name, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,180}$") && !name.Contains("..")
                && !name.EndsWith(".") && !Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)", RegexOptions.IgnoreCase);
        }
        public static ModelPack Read(string path)
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("모델 팩 정보가 너무 큽니다.");
            var p = Json.Read<ModelPack>(File.ReadAllText(path));
            if (p == null || p.schema != 1 || p.model == null || !Leaf(p.model.file) || !p.model.file.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
                || !Digest(p.model.sha256) || p.model.size <= 0 || p.model.size > 512L * 1024 * 1024 * 1024
                || p.parts == null || p.parts.Length == 0 || p.parts.Length > 512) throw new InvalidDataException("모델 팩 형식을 확인하세요.");
            long total = 0;
            foreach (var part in p.parts)
            {
                if (part == null || !Leaf(part.name) || !Digest(part.sha256) || part.size <= 0 || part.size >= 2L * 1024 * 1024 * 1024)
                    throw new InvalidDataException("모델 팩 조각 정보가 올바르지 않습니다.");
                total = checked(total + part.size);
            }
            if (total != p.model.size || p.parts.Select(x => x.name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != p.parts.Length)
                throw new InvalidDataException("모델 팩 크기 또는 파일 목록이 올바르지 않습니다.");
            var known = ModelCatalog.Current.models.FirstOrDefault(x => x.file == p.model.file);
            if (known != null && (known.size != p.model.size || known.sha256 != p.model.sha256))
                throw new InvalidDataException("등록된 모델의 SHA256과 다릅니다.");
            return p;
        }
        public static string Import(string manifestPath, string modelFolder, Action<long, long> progress, CancellationToken cancel)
        {
            var pack = Read(manifestPath);
            string source = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            Directory.CreateDirectory(modelFolder);
            string target = Path.Combine(modelFolder, pack.model.file);
            // No replacement: user files and an actively loaded model remain untouched.
            if (File.Exists(target)) throw new IOException("같은 이름의 모델이 이미 있습니다: " + pack.model.file);
            foreach (var part in pack.parts)
            {
                var info = new FileInfo(Path.Combine(source, part.name));
                if (!info.Exists || info.Length != part.size || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("조각 누락 또는 크기 오류: " + part.name);
            }
            string temp = target + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024))
                using (var whole = SHA256.Create())
                {
                    var buffer = new byte[4 * 1024 * 1024]; long done = 0;
                    foreach (var part in pack.parts)
                    using (var input = new FileStream(Path.Combine(source, part.name), FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var digest = SHA256.Create())
                    {
                        int count; long read = 0;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancel.ThrowIfCancellationRequested(); read += count; done += count;
                            if (read > part.size) throw new InvalidDataException("조각 크기 변경: " + part.name);
                            digest.TransformBlock(buffer, 0, count, buffer, 0); whole.TransformBlock(buffer, 0, count, buffer, 0);
                            output.Write(buffer, 0, count); if (progress != null) progress(done, pack.model.size);
                        }
                        digest.TransformFinalBlock(new byte[0], 0, 0);
                        if (read != part.size || Hex(digest.Hash) != part.sha256) throw new InvalidDataException("SHA256 불일치: " + part.name);
                    }
                    whole.TransformFinalBlock(new byte[0], 0, 0);
                    if (done != pack.model.size || Hex(whole.Hash) != pack.model.sha256) throw new InvalidDataException("모델 SHA256 불일치");
                    output.Flush(true);
                }
                cancel.ThrowIfCancellationRequested();
                Gguf.Header(temp);
                File.Move(temp, target); // GGUF becomes discoverable only after full verification.
                return target;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
