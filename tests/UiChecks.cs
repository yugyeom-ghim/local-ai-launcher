// Native Windows idle/UI regression probe. Never starts llama-server or modifies a user's settings.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Windows.Forms;
using SILocalAI;
class UiChecks
{
    static MainForm form;
    static readonly Dictionary<string, object> result = new Dictionary<string, object>();
    static object Field(string name) { var f = typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); return f == null ? null : f.GetValue(form); }
    static void Fail(string text) { throw new Exception(text); }
    static void Model(string path)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write((uint)0x46554747); w.Write((uint)3); w.Write((ulong)1); w.Write((ulong)1);
            byte[] key = Encoding.UTF8.GetBytes("general.architecture"), value = Encoding.UTF8.GetBytes("qwen35");
            w.Write((ulong)key.Length); w.Write(key); w.Write((uint)8); w.Write((ulong)value.Length); w.Write(value);
            byte[] tensor = Encoding.UTF8.GetBytes("test.weight"); w.Write((ulong)tensor.Length); w.Write(tensor);
            w.Write((uint)1); w.Write((ulong)32); w.Write((uint)0); w.Write((ulong)0);
            while (w.BaseStream.Position % 32 != 0) w.Write((byte)0); w.Write(new byte[128]);
        }
    }
    static IEnumerable<Control> All(Control parent)
    { foreach (Control c in parent.Controls) { yield return c; foreach (Control child in All(c)) yield return child; } }
    static void Capture(string path)
    {
        form.Refresh();
        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path, ImageFormat.Png); }
    }
    [STAThread] static int Main(string[] args)
    {
        string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        string root = Path.Combine(Path.GetTempPath(), "local-ai-ui-" + Guid.NewGuid().ToString("N"));
        string state = Path.Combine(root, "state"), models = Path.Combine(root, "ai", "models");
        Directory.CreateDirectory(state); Directory.CreateDirectory(models);
        foreach (string n in new[] { "Qwen3.5-2B-Q4_K_M.gguf", "Qwen3.5-4B-Q4_K_M.gguf", "Qwen3.5-9B-Q4_K_M.gguf" }) Model(Path.Combine(models, n));
        bool baseline = args.Length > 1 && args[1] == "baseline";
        // For release validation a real payload can be inspected, always with isolated settings and AutoStart=false.
        string package = args.Length > 2 ? Path.GetFullPath(args[2]) : root;
        File.WriteAllText(Path.Combine(state, "settings.json"), args.Length > 2 ? "{\"AutoStart\":false}" : "{\"AutoStart\":false,\"AssetRoot\":\"ai\"}");
        var sw = Stopwatch.StartNew(); long shown = 0, last = 0, gap = 0; int beats = 0, layouts = 0, startLayouts = 0, scans = -1;
        double cpu = 0; bool measuring = false; int exit = 0;
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        var timer = new System.Windows.Forms.Timer { Interval = 100 };
        try
        {
            form = new MainForm(package, state, new AppLog(Path.Combine(state, "log.txt")), false);
            result["construct_ms"] = sw.ElapsedMilliseconds;
            foreach (var c in All(form)) c.Layout += delegate { layouts++; };
            form.Shown += delegate { shown = sw.ElapsedMilliseconds; result["shown_ms"] = shown; timer.Start(); };
            timer.Tick += delegate
            {
                try
                {
                    long n = sw.ElapsedMilliseconds;
                    if (!measuring && n - shown >= 6000)
                    { measuring = true; cpu = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds; startLayouts = layouts; last = n;
                      var cat = Field("catalog"); var count = cat.GetType().GetProperty("ScanCount"); if (count != null) scans = (int)count.GetValue(cat, null); }
                    if (measuring) { beats++; gap = Math.Max(gap, n - last); last = n; }
                    if (n - shown < 16000) return;
                    timer.Stop();
                    result["idle_cpu_ms_10s"] = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds - cpu;
                    result["idle_layout_events"] = layouts - startLayouts; result["max_heartbeat_gap_ms"] = gap; result["heartbeats"] = beats;
                    var catalog = Field("catalog"); var property = catalog.GetType().GetProperty("ScanCount");
                    if (property != null) result["idle_directory_scans"] = (int)property.GetValue(catalog, null) - scans;
                    result["model_count"] = ((DataGridView)Field("modelGrid")).Rows.Count;
                    result["server_alive"] = ((ServerController)Field("server")).Alive;
                    result["platform"] = Environment.OSVersion.ToString();
                    Console.WriteLine(Json.Write(result)); Console.Out.Flush();
                    if (!baseline)
                    {
                        if ((bool)result["server_alive"]) Fail("Unexpected server process");
                        if ((double)result["idle_cpu_ms_10s"] > 1200) Fail("Idle CPU regression");
                        if (gap > 1000 || beats < 60) Fail("UI heartbeat stalled");
                        if ((int)result["idle_directory_scans"] != 0) Fail("Idle directory enumeration");
                        if (sw.ElapsedMilliseconds - n > 5000 || (long)result["construct_ms"] > 6000) Fail("Slow UI construction");
                        if ((int)result["idle_layout_events"] > 40) Fail("Repeated idle layout");
                        if (Field("searchBox") != null) Fail("Model search still present");
                        if (((Label)Field("cpuValue")).Text != "확인 전") Fail("Automatic hardware probe");
                        if (((DataGridView)Field("modelGrid")).Rows.Count == 0) Fail("Model discovery failed");
                        // Exercise long names and all navigation pages. No timers are disabled during performance sampling.
                        ((Label)Field("cpuValue")).Text = "Intel(R) Core(TM) i9-14900K 24-Core Processor / AMD Ryzen Threadripper PRO 7995WX";
                        ((Label)Field("gpuValue")).Text = "NVIDIA GeForce RTX 4060 Ti";
                        var inventory = (Inventory)Field("inventory");
                        foreach (var model in inventory.Models) { var entry = ModelCatalog.Current.models.FirstOrDefault(m => m.file == Path.GetFileName(model.FullPath)); if (entry != null) model.Bytes = entry.size; }
                        typeof(MainForm).GetMethod("BindModelGrid", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                        var nav = (Control)Field("pages"); var selected = nav.GetType().GetProperty("SelectedIndex");
                        foreach (Size size in new[] { new Size(1120, 740), new Size(800, 600) })
                        {
                            form.ClientSize = size; form.PerformLayout(); Application.DoEvents();
                            Capture(Path.Combine(output, "models-" + size.Width + ".png"));
                            var start = (Control)Field("startButton"); var stop = (Control)Field("stopButton");
                            if (start.Width < 65 || stop.Width < 50 || start.Bounds.IntersectsWith(stop.Bounds)) Fail("Run controls clipped/overlap");
                        }
                        form.ClientSize = new Size(1120, 740);
                        for (int i = 1; i < 4; i++) { selected.SetValue(nav, i, null); Application.DoEvents(); Capture(Path.Combine(output, "page-" + i + ".png")); }
                        selected.SetValue(nav, 2, null);
                        var nested = All(nav).First(c => c != nav && c.GetType().Name == "WorkspaceTabs");
                        nested.GetType().GetProperty("SelectedIndex").SetValue(nested, 2, null); Application.DoEvents(); Capture(Path.Combine(output, "pc-info.png"));
                        selected.SetValue(nav, 0, null);
                        form.Scale(new SizeF(1.5F, 1.5F)); form.ClientSize = new Size(1500, 1050); Application.DoEvents(); Capture(Path.Combine(output, "models-scale150.png"));
                        result["render_checks"] = "1120x740, 800x600, API, settings, logs, long CPU, simulated 150% scale";
                    }
                }
                catch (Exception e) { result["error"] = e.ToString(); exit = 1; }
                finally
                {
                    if (!timer.Enabled) { typeof(MainForm).GetField("closing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true); form.Close(); }
                }
            };
            Application.Run(form);
        }
        catch (Exception e) { result["error"] = e.ToString(); exit = 1; }
        finally
        {
            timer.Dispose(); File.WriteAllText(Path.Combine(output, "UI_CHECKS.json"), Json.Write(result), Encoding.UTF8);
            Console.WriteLine(Json.Write(result)); try { Directory.Delete(root, true); } catch { }
        }
        return exit;
    }
}
