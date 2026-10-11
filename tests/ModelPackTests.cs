using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using SILocalAI;

public static class ModelPackTests
{
    static string Hash(byte[] data) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
    static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    static void Reject(Action act) { try { act(); } catch { return; } throw new Exception("Expected rejection"); }
    static int passed;
    static void Check(string label, Action act) { act(); passed++; Console.WriteLine("PASS " + label); }
    static byte[] Fixture()
    {
        using (var s = new MemoryStream())
        using (var w = new BinaryWriter(s))
        {
            w.Write((uint)0x46554747); w.Write((uint)3); w.Write((ulong)1); w.Write((ulong)1);
            foreach (string text in new[] { "general.architecture", "qwen35" })
            { byte[] b = Encoding.UTF8.GetBytes(text); w.Write((ulong)b.Length); w.Write(b); if (text == "general.architecture") w.Write((uint)8); }
            byte[] name = Encoding.UTF8.GetBytes("test.weight"); w.Write((ulong)name.Length); w.Write(name);
            w.Write((uint)1); w.Write((ulong)32); w.Write((uint)0); w.Write((ulong)0);
            while (s.Position % 32 != 0) w.Write((byte)0); w.Write(new byte[128]); return s.ToArray();
        }
    }
    static ModelPack Pack(string root, byte[] bytes)
    {
        int mid = bytes.Length / 2; var first = bytes.Take(mid).ToArray(); var second = bytes.Skip(mid).ToArray();
        File.WriteAllBytes(Path.Combine(root,"fixture.part01"), first); File.WriteAllBytes(Path.Combine(root,"fixture.part02"), second);
        return new ModelPack { schema=1, model=new ModelProfile { file="fixture.gguf", size=bytes.Length, sha256=Hash(bytes) }, parts=new[] {
            new ModelPart {name="fixture.part01",size=first.Length,sha256=Hash(first)},new ModelPart {name="fixture.part02",size=second.Length,sha256=Hash(second)} } };
    }
    public static int Main(string[] args)
    {
        string root = Path.Combine(Path.GetTempPath(), "모델 팩 검사 " + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            if (args.Length == 2)
            {
                // Real released parts; used on Windows before publication.
                var pack=ModelPacks.Read(args[0]); string dest=Path.GetFullPath(args[1]);
                string target=ModelPacks.Import(args[0],dest,null,CancellationToken.None);
                Check("real model import, part/full SHA256 and GGUF bounds", delegate { Assert(new FileInfo(target).Length==pack.model.size); });
                var settings=new Settings { AssetRoot=".", ModelsPath=".", Model=pack.model.file };
                var inv=Discovery.Scan(dest,settings); var chosen=Discovery.Choose(inv.Models,settings.Model);
                Check("discovery and saved model selection",delegate {Assert(chosen!=null && chosen.FullPath==target);});
                Check("selected GGUF passed to server",delegate {
                    var command=Plans.Arguments(settings,new Capabilities("--offline\n--reasoning MODE\n--jinja"),new BackendInfo {Id="cpu"},false,chosen.FullPath,chosen.Id,"unused.ini",null);
                    Assert(command[command.IndexOf("-m")+1]==target);
                });
                Console.WriteLine("MODEL "+pack.model.file+" SHA256 "+pack.model.sha256+" SIZE "+pack.model.size);
                return 0;
            }
            string manifest=Path.Combine(root,"fixture.modelpack.json"), models=Path.Combine(root,"설치 모델");
            byte[] data=Fixture(); var packFixture=Pack(root,data);
            Action save=delegate {File.WriteAllText(manifest,Json.Write(packFixture));}; save();
            Check("offline import is atomic and discoverable",delegate {
                ModelPacks.Import(manifest,models,delegate(long done,long total){Assert(!File.Exists(Path.Combine(models,"fixture.gguf")));},CancellationToken.None);
                Assert(File.ReadAllBytes(Path.Combine(models,"fixture.gguf")).SequenceEqual(data));
                Assert(Discovery.Scan(models,new Settings {AssetRoot=".",ModelsPath="."}).Models.Count==1);
            });
            Check("existing file preserved",delegate {Reject(delegate{ModelPacks.Import(manifest,models,null,CancellationToken.None);});Assert(File.ReadAllBytes(Path.Combine(models,"fixture.gguf")).SequenceEqual(data));});
            string failures=Path.Combine(root,"실패 모델");
            Check("missing part leaves no model",delegate {File.Move(Path.Combine(root,"fixture.part02"),Path.Combine(root,"saved"));Reject(delegate{ModelPacks.Import(manifest,failures,null,CancellationToken.None);});File.Move(Path.Combine(root,"saved"),Path.Combine(root,"fixture.part02"));Assert(!Directory.GetFiles(failures).Any());});
            Check("same-size corruption removed before discovery",delegate {
                var damaged=File.ReadAllBytes(Path.Combine(root,"fixture.part02"));damaged[0]^=1;File.WriteAllBytes(Path.Combine(root,"fixture.part02"),damaged);
                Reject(delegate{ModelPacks.Import(manifest,failures,null,CancellationToken.None);});Assert(!Directory.GetFiles(failures).Any());Pack(root,data);
            });
            Check("whole digest checked independently",delegate {packFixture.model.sha256=new string('0',64);save();Reject(delegate{ModelPacks.Import(manifest,failures,null,CancellationToken.None);});Assert(!Directory.GetFiles(failures).Any());packFixture.model.sha256=Hash(data);save();});
            Check("traversal and Windows device names rejected",delegate {foreach(string n in new[]{"../escape","C:escape","NUL.part","folder\\part","file."}){packFixture.parts[0].name=n;save();Reject(delegate{ModelPacks.Read(manifest);});}packFixture.parts[0].name="fixture.part01";save();});
            Check("duplicate parts rejected",delegate {packFixture.parts[1].name="fixture.part01";save();Reject(delegate{ModelPacks.Read(manifest);});packFixture.parts[1].name="fixture.part02";save();});
            Check("cancel removes partial file",delegate {var cancel=new CancellationTokenSource();cancel.Cancel();Reject(delegate{ModelPacks.Import(manifest,failures,null,cancel.Token);});Assert(!Directory.GetFiles(failures).Any());});
            Check("non-GGUF with valid hash rejected",delegate {packFixture=Pack(root,new byte[256]);save();Reject(delegate{ModelPacks.Import(manifest,failures,null,CancellationToken.None);});Assert(!Directory.GetFiles(failures).Any());});
            Check("4060 Ti 8GB and 16GB differ",delegate {Assert(ModelCatalog.Current.Recommend(8,16).id=="qwen35-4b");Assert(ModelCatalog.Current.Recommend(16,32).id=="qwen35-9b");});
            Check("Distributed catalog excludes large manual models",delegate {Assert(ModelCatalog.Current.models.Length==3);Assert(ModelCatalog.Current.Recommend(24,32).id=="qwen35-9b");Assert(ModelCatalog.Current.Recommend(24,8).id=="qwen35-2b");Assert(ModelCatalog.Current.Recommend(0,8).id=="qwen35-2b");});
            Check("saved model remains selected after additions",delegate {
                var original=new ModelInfo{Name="Qwen3.5-4B-Q4_K_M",RelativePath="old.gguf",Bytes=4};var extra=new ModelInfo{Name="extra",RelativePath="new.gguf",Bytes=1};
                Assert(Discovery.Choose(new System.Collections.Generic.List<ModelInfo>{extra,original},"old.gguf")==original);
            });
            Console.WriteLine(passed+" model pack checks passed");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{Directory.Delete(root,true);}
    }
}
