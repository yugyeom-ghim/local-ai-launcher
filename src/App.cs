using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SILocalAI
{
    static class Program
    {
        [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            SetErrorMode(0x8001); // no missing-DLL crash dialog blocking an unattended backend probe.
            string root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            Mutex mutex = null; bool owned = false;
            try
            {
                mutex = new Mutex(false, "Local\\SI.LocalAI." + Paths.Hash(root.ToLowerInvariant()).Substring(0, 24));
                try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                if (!owned) { MessageBox.Show("이미 실행 중입니다. 작업 표시줄 또는 알림 영역에서 Local AI를 확인하세요.", "Local AI"); return 0; }
                string state;
                try
                {
                    state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
                    string test = Path.Combine(state, ".write-test-" + Guid.NewGuid().ToString("N")); File.WriteAllText(test, ""); File.Delete(test);
                }
                catch
                {
                    state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SI.LocalAI", Paths.Hash(root.ToLowerInvariant()).Substring(0, 16));
                    Directory.CreateDirectory(state);
                }
                Directory.CreateDirectory(Path.Combine(state, "cache"));
                var logger = new AppLog(Path.Combine(state, "launcher.log"));
                logger.Write("Local AI Launcher / 시작 위치 " + root);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { logger.Write(e.Exception.ToString()); MessageBox.Show(e.Exception.Message, "실행 오류", MessageBoxButtons.OK, MessageBoxIcon.Error); };
                Application.Run(new MainForm(root, state, logger, args.Contains("--start-minimized")));
                return 0;
            }
            catch (Exception e) { MessageBox.Show(e.ToString(), "Local AI 초기화 실패", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
            finally { if (owned && mutex != null) mutex.ReleaseMutex(); if (mutex != null) mutex.Dispose(); }
        }
    }

    public sealed partial class MainForm : Form
    {
        readonly string root, state, configFile;
        readonly AppLog log;
        readonly ServerController server;
        Settings config;
        Inventory inventory = new Inventory();
        bool uiBusy, closing, monitoring, initialMinimized, cancelRequested;
        readonly ConcurrentQueue<string> pendingLog = new ConcurrentQueue<string>();
        readonly System.Windows.Forms.Timer logTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer monitorTimer = new System.Windows.Forms.Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolTip tips = new ToolTip();
        Label statusLabel, hardwareLabel, modelCount;
        TextBox baseUrl, chatUrl, modelId, logBox;
        ComboBox backendBox, modeBox, modelBox, thinkingModeBox;
        NumericUpDown portBox, contextBox, threadsBox, timeoutBox, gpuBox;
        CheckBox autoBox, fallbackBox, browserBox, apiKeyBox;
        Button startButton, stopButton, scanButton, loadButton, testButton, webButton, applyButton;
        DataGridView assetGrid;
        readonly Dictionary<string, TextBox> pathBoxes = new Dictionary<string, TextBox>();
        readonly List<Control> editable = new List<Control>();
        readonly Color ink = Theme.Ink;
        readonly Color paper = Theme.Background;
        readonly Color border = Theme.Line;
        static readonly string[] BackendIds = { "auto", "cpu", "cuda12", "cuda13", "cuda134" };
        static readonly string[] ModeIds = { "auto", "router", "single" };
        public MainForm(string packageRoot, string statePath, AppLog logger, bool minimized)
        {
            root = packageRoot; state = statePath; log = logger; initialMinimized = minimized;
            configFile = Path.Combine(state, "settings.json"); config = LoadSettings();
            server = new ServerController(root, state, log);
            Text = "Local AI Launcher";
            Font = new Font("Malgun Gothic", 9F);
            ForeColor = ink; BackColor = paper;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1180, 860); MinimumSize = new Size(720, 540);
            Icon = SystemIcons.Application;
            BuildUi(); ApplyConfig(); SetBusy(false);
            log.Line += OnLog;
            server.Changed += delegate(ServerStatus s) { if (!closing && !IsDisposed) UpdateStatus(s); };
            logTimer.Interval = 200; logTimer.Tick += delegate { DrainLog(); }; logTimer.Start();
            monitorTimer.Interval = 3000;
            monitorTimer.Tick += async delegate
            {
                if (monitoring || closing || uiBusy) return;
                monitoring = true;
                try { await server.MonitorAsync(); } catch (Exception e) { log.Write(e.Message); }
                finally { monitoring = false; }
            };
            monitorTimer.Start();
            libraryTimer.Interval = 3000;
            libraryTimer.Tick += async delegate
            {
                if (uiBusy || catalogRefreshing || closing) return;
                catalogRefreshing = true;
                try { await ScanAsync(false); }
                catch (Exception e) { log.Write("폴더 자동 검색: " + e.Message); }
                finally { catalogRefreshing = false; }
            };
            libraryTimer.Start();
            tray.Icon = SystemIcons.Application; tray.Text = "Local AI Launcher";
            var menu = new ContextMenuStrip();
            menu.Items.Add("열기", null, delegate { ShowWindow(); });
            menu.Items.Add("서버 종료 후 닫기", null, delegate { ShowWindow(); Close(); });
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { ShowWindow(); };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) { tray.Visible = true; Hide(); } };
            Shown += async delegate
            {
                FitToWorkingArea();
                var hardwareTask = DetectHardwareAsync();
                await Work(async delegate
                {
                    await ScanAsync();
                    if (inventory.PendingFiles.Count > 0) { await Task.Delay(2800); if (!closing && !cancelRequested) await ScanAsync(); }
                    if (!cancelRequested && !closing && config.AutoStart && inventory.Models.Count > 0 && Discovery.Order(inventory, config).Count > 0)
                    {
                        await server.StartAsync(config, inventory, SelectedModel());
                        if (server.Status.Ready) loadedCatalogSignature = LibraryMonitor.ModelSignature(inventory.Models);
                    }
                    if (config.OpenBrowser && server.Status.Ready) OpenUi();
                });
                await hardwareTask;
                if (closing) return;
                if (initialMinimized && server.Alive) { WindowState = FormWindowState.Minimized; }
            };
            FormClosing += OnClosing;
            FormClosed += delegate { server.Dispose(); log.Line -= OnLog; logTimer.Dispose(); monitorTimer.Dispose(); libraryTimer.Dispose(); tray.Dispose(); tips.Dispose(); modelBox.Dispose(); if (brandImage != null) brandImage.Dispose(); };
        }
        Settings LoadSettings()
        {
            string source = configFile;
            string portableSettings = Path.Combine(root, "state", "settings.json");
            if (!File.Exists(source) && File.Exists(portableSettings)) source = portableSettings;
            if (File.Exists(source))
            {
                try { var s = Json.Read<Settings>(File.ReadAllText(source, Encoding.UTF8)); s.Validate(); return s; }
                catch (Exception e)
                {
                    log.Write("설정 읽기 실패. 원본을 보존하고 자동 시작을 껐습니다: " + e.Message);
                    try { File.Copy(source, configFile + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), false); } catch { }
                    MessageBox.Show("저장된 설정을 읽지 못했습니다. 원본은 보존합니다. 자산 경로와 설정을 확인한 후 서버 시작을 누르세요.\r\n" + e.Message, "설정 확인");
                    return new Settings { AutoStart = false };
                }
            }
            var defaults = new Settings();
            if (!Directory.Exists(Paths.Full(root, defaults.AssetRoot)))
            {
                if (Directory.Exists(Path.Combine(root, "bin")) && Directory.Exists(Path.Combine(root, "models"))) defaults.AssetRoot = ".";
                else if (Directory.Exists(Path.Combine(root, "ai", "bin"))) defaults.AssetRoot = "ai";
            }
            return defaults;
        }
        Label LabelText(string text) { return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 8, 4) }; }
        Button ButtonText(string text, EventHandler click)
        {
            var b = new ActionButton { Text = text, AutoSize = true, Height = 32, MinimumSize = new Size(90, 32), BackColor = Color.White, Margin = new Padding(4) };
            b.FlatAppearance.BorderColor = border; b.Click += click; return b;
        }
        TextBox ReadonlyText() { return new TextBox { ReadOnly = true, Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(3, 5, 3, 5) }; }
        ComboBox Combo(string[] values)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(3, 5, 3, 5) };
            c.Items.AddRange(values); editable.Add(c); return c;
        }
        NumericUpDown Number(int min, int max, int step)
        { var n = new NumericUpDown { Minimum = min, Maximum = max, Increment = step, Width = 106, ThousandsSeparator = true, Margin = new Padding(3, 5, 12, 5) }; editable.Add(n); return n; }
        void BuildAssets(Control page)
        {
            var viewport = new ContentViewport(); page.Controls.Add(viewport);
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, RowCount = 9, Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            viewport.SetContent(t);
            string[] keys = { "AssetRoot", "ModelsPath", "CpuPath", "Cuda12Path", "Cuda13Path", "Cuda134Path" };
            string[] names = { "AI 자산 루트", "모델 폴더", "CPU 실행기 폴더", "CUDA 12.4 폴더", "CUDA 13.1 (기존)", "CUDA 13.4 폴더" };
            for (int i = 0; i < keys.Length; i++)
            {
                t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                string key = keys[i]; var box = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 7, 3, 3) };
                pathBoxes[key] = box; editable.Add(box);
                t.Controls.Add(LabelText(names[i]), 0, i); t.Controls.Add(box, 1, i);
                var browse = ButtonText("찾기", delegate { BrowseAsset(key); }); browse.MinimumSize = new Size(75, 28); editable.Add(browse); t.Controls.Add(browse, 2, i);
            }
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            applyButton = ButtonText("자산 경로 적용 / 다시 검색", async delegate { await Work(RescanFromUi); });
            t.Controls.Add(applyButton, 0, keys.Length); t.SetColumnSpan(applyButton, 3); editable.Add(applyButton);
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var note = L("루트는 이 실행 도구 위치 기준, 나머지는 AI 자산 루트 기준 상대경로입니다. DLL은 각 실행기 폴더에 함께 두세요.\nocr / runtime 폴더와 mmproj는 텍스트 모델 목록에서 제외합니다.", 9F, false);
            note.ForeColor = Theme.Muted; note.Margin = new Padding(3, 12, 3, 12);
            t.Controls.Add(note, 0, keys.Length + 1); t.SetColumnSpan(note, 3);
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
            assetGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            assetGrid.Columns.Add("type", "자산"); assetGrid.Columns.Add("path", "발견 경로"); assetGrid.Columns.Add("state", "상태");
            assetGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; assetGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            assetGrid.Columns[0].FillWeight = 20; assetGrid.Columns[1].FillWeight = 60; assetGrid.Columns[2].FillWeight = 30;
            t.Controls.Add(assetGrid, 0, keys.Length + 2); t.SetColumnSpan(assetGrid, 3);
        }
        void ApplyConfig()
        {
            backendBox.SelectedIndex = Array.IndexOf(BackendIds, config.Backend); modeBox.SelectedIndex = Array.IndexOf(ModeIds, config.Mode);
            portBox.Value = config.Port; contextBox.Value = config.Context; threadsBox.Value = config.Threads;
            timeoutBox.Value = config.LoadTimeout; gpuBox.Value = config.GpuLayers;
            autoBox.Checked = config.AutoStart; thinkingModeBox.SelectedIndex = config.ThinkingOff ? 0 : 1; apiKeyBox.Checked = config.ApiKeyRequired; fallbackBox.Checked = config.CpuFallback; browserBox.Checked = config.OpenBrowser;
            pathBoxes["AssetRoot"].Text = config.AssetRoot; pathBoxes["ModelsPath"].Text = config.ModelsPath;
            pathBoxes["CpuPath"].Text = config.CpuPath; pathBoxes["Cuda12Path"].Text = config.Cuda12Path; pathBoxes["Cuda13Path"].Text = config.Cuda13Path; pathBoxes["Cuda134Path"].Text = config.Cuda134Path;
            baseUrl.Text = ""; chatUrl.Text = ""; modelId.Text = "";
        }
        void ReadConfig()
        {
            var s = config.Clone();
            s.Backend = BackendIds[Math.Max(0, backendBox.SelectedIndex)]; s.Mode = ModeIds[Math.Max(0, modeBox.SelectedIndex)];
            s.Port = (int)portBox.Value; s.Context = (int)contextBox.Value; s.Threads = (int)threadsBox.Value;
            s.LoadTimeout = (int)timeoutBox.Value; s.GpuLayers = (int)gpuBox.Value;
            s.AutoStart = autoBox.Checked; s.ThinkingOff = thinkingModeBox.SelectedIndex != 1; s.ApiKeyRequired = apiKeyBox.Checked; s.CpuFallback = fallbackBox.Checked; s.OpenBrowser = browserBox.Checked;
            s.AssetRoot = pathBoxes["AssetRoot"].Text.Trim(); s.ModelsPath = pathBoxes["ModelsPath"].Text.Trim();
            s.CpuPath = pathBoxes["CpuPath"].Text.Trim(); s.Cuda12Path = pathBoxes["Cuda12Path"].Text.Trim(); s.Cuda13Path = pathBoxes["Cuda13Path"].Text.Trim(); s.Cuda134Path = pathBoxes["Cuda134Path"].Text.Trim();
            var m = SelectedModel(); if (m != null) s.Model = m.RelativePath;
            s.Validate(); config = s;
        }
        void SaveConfig() { Paths.AtomicWrite(configFile, Json.Write(config)); }
        ModelInfo SelectedModel() { return modelBox.SelectedItem as ModelInfo; }
        async Task ScanAsync() { await ScanAsync(true); }
        async Task ScanAsync(bool force)
        {
            var snapshot = config.Clone();
            var found = await Task.Run(delegate { return catalog.Poll(root, snapshot, force); });
            if (closing || found == null) return;
            inventory = found;
            bindingModels = true;
            try {
                modelBox.Items.Clear(); modelBox.Items.AddRange(inventory.Models.Cast<object>().ToArray());
                modelBox.SelectedItem = Discovery.Choose(inventory.Models, config.Model);
            } finally { bindingModels = false; }
            var selected = SelectedModel();
            if (selected != null) config.Model = selected.RelativePath;
            assetGrid.Rows.Clear();
            foreach (var b in inventory.Backends) assetGrid.Rows.Add(b.Label, Paths.Relative(root,b.Folder), String.IsNullOrEmpty(b.Error) ? "파일 감지 · 실행 시 호환 검사" : b.Error);
            foreach (var m in inventory.Models) assetGrid.Rows.Add("GGUF", Paths.Relative(root,m.FullPath), m.Architecture + " / " + (m.Bytes/1073741824.0).ToString("0.00") + " GiB");
            foreach (var name in inventory.PendingFiles) assetGrid.Rows.Add("확인 중",name,"파일 안정화/복사 상태 확인 중");
            string warnings = String.Join("\n",inventory.Warnings);
            if (warnings != warningSignature) { foreach (var warning in inventory.Warnings) log.Write(warning); warningSignature = warnings; }
            int pending = inventory.PendingFiles.Count;
            bool catalogChanged = server.Alive && loadedCatalogSignature != LibraryMonitor.ModelSignature(inventory.Models);
            catalogNote.Text = pending > 0 ? "파일 확인 중 " + pending + "개 · 복사가 끝나면 자동 반영합니다."
                : catalogChanged ? "목록 변경 감지 · 새 모델 실행 시 서버에 반영됩니다."
                : inventory.Warnings.Count > 0 ? "제외/확인 항목 " + inventory.Warnings.Count + "개 · 진단 로그를 확인하세요."
                : "폴더 자동 확인 켜짐 · " + DateTime.Now.ToString("HH:mm:ss") + " 갱신";
            if (!server.Alive && server.Status.State != "error") statusLabel.Text = pending>0 ? "GGUF 파일 확인 중..." : selected==null ? "models 폴더에 GGUF 파일을 넣어주세요." : "파일 발견 · 모델을 선택하고 서버를 시작하세요.";
            log.Write("라이브러리 갱신 · 모델 " + inventory.Models.Count + "개 / 파일 확인 중 " + pending + "개");
            UpdateRuntimeCards(); BindModelGrid(); SaveConfig(); SetBusy(uiBusy);
        }
        async Task StartFromUi()
        {
            ReadConfig();
            var requested = SelectedModel();
            if (requested == null) throw new InvalidOperationException("준비된 모델을 먼저 선택하세요.");
            if (server.Alive && MessageBox.Show(this, "선택 모델로 서버를 시작하면 진행 중인 생성 요청이 중단됩니다. 계속할까요?", "서버 전환 / 재시작", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            // Refresh before stopping a usable server. Never silently substitute a different file.
            await ScanAsync();
            var chosen = inventory.Models.FirstOrDefault(m => m.Id == requested.Id && m.FullPath == requested.FullPath);
            if (chosen == null) throw new InvalidOperationException("선택 파일이 삭제되었거나 복사/변경 중입니다. 파일 확인이 끝난 후 다시 실행하세요.");
            if (closing || cancelRequested) return;
            modelBox.SelectedItem = chosen; config.Model = chosen.RelativePath; SaveConfig();
            server.Stop();
            await server.StartAsync(config, inventory, chosen);
            if (server.Status.Ready) { loadedCatalogSignature = LibraryMonitor.ModelSignature(inventory.Models); catalogNote.Text = "선택 모델 사용 가능 · 새 파일은 자동으로 검색합니다."; }
            if (config.OpenBrowser && server.Status.Ready) OpenUi();
        }
        async Task RescanFromUi()
        {
            ReadConfig(); await ScanAsync(); SaveConfig();
            if (server.Alive) statusLabel.Text = "목록을 갱신했습니다. 실행 중인 서버는 유지합니다. 새 모델은 선택 후 실행하세요.";
        }
        async Task Work(Func<Task> work)
        {
            if (uiBusy || closing) return;
            uiBusy = true; cancelRequested = false; SetBusy(true);
            try { while(catalogRefreshing && !closing) await Task.Delay(40); if (!closing) await work(); }
            catch (OperationCanceledException) { log.Write("작업 취소"); }
            catch (Exception e) { log.Write(e.ToString()); if (!closing) MessageBox.Show(this, e.Message, "확인 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { uiBusy = false; if (!closing) SetBusy(false); }
        }
        void SetBusy(bool value)
        {
            foreach (var c in editable) c.Enabled = !value;
            scanButton.Enabled = !value; modelGrid.Enabled = !value; searchBox.Enabled = !value;
            stopButton.Enabled = value || server.Alive;
            testButton.Enabled = !value && server.Status.Ready;
            webButton.Enabled = server.Status.Listening;
            progress.Style = value ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (!value) progress.Value = server.Status.Ready ? 100 : 0;
            RefreshSelection(); UpdateConnectionPanel();
        }
        void UpdateStatus(ServerStatus s)
        {
            statusLabel.Text = s.Message.Replace("\r", " ").Replace("\n", " ");
            tips.SetToolTip(statusLabel, s.Message.Length > 1500 ? s.Message.Substring(0, 1500) : s.Message);
            statusLabel.ForeColor = s.State == "error" ? Color.Firebrick : s.Ready ? Color.FromArgb(27, 102, 69) : ink;
            hardwareLabel.Text = (s.Backend == "" ? "미선택" : s.Backend) + " | " + (s.Mode == "router" ? "목록 모드" : "단일 모델") + " | " + s.Gpu + " | PID " + s.Pid;
            if (!String.IsNullOrEmpty(s.BaseUrl)) baseUrl.Text = s.BaseUrl;
            if (!String.IsNullOrEmpty(s.ChatUrl)) chatUrl.Text = s.ChatUrl;
            if (!String.IsNullOrEmpty(s.ModelId)) modelId.Text = s.ModelId;
            tray.Text = s.Ready ? "Local AI · 사용 가능" : "Local AI · " + (s.State == "error" ? "오류" : "대기/로딩");
            SetBusy(uiBusy); UpdateDashboard(s); UpdateConnectionPanel();
        }
        void BrowseAsset(string key)
        {
            TryAction(delegate
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = key == "AssetRoot" ? "bin과 models가 들어 있는 ai 폴더를 선택하세요." : "해당 자산 폴더를 선택하세요.";
                    string from = key == "AssetRoot" ? root : Paths.Full(root, pathBoxes["AssetRoot"].Text.Trim());
                    string current = Paths.Full(from, pathBoxes[key].Text.Trim());
                    if (Directory.Exists(current)) dlg.SelectedPath = current;
                    if (dlg.ShowDialog(this) == DialogResult.OK) pathBoxes[key].Text = Paths.Relative(from, dlg.SelectedPath);
                }
            });
        }
        void TryAction(Action action)
        { try { action(); } catch (Exception e) { log.Write(e.Message); if (!closing) MessageBox.Show(this, e.Message, "확인 필요"); } }
        void OpenPath(string path)
        { TryAction(delegate { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }); }
        void OpenUi()
        {
            if (!server.Status.Listening) { MessageBox.Show(this, "서버 시작 후 열 수 있습니다.", "llama-ui"); return; }
            string url = server.Status.BaseUrl;
            OpenPath(url.EndsWith("/v1", StringComparison.Ordinal) ? url.Substring(0, url.Length - 3) : url);
        }
        void OnLog(string line)
        {
            pendingLog.Enqueue(line); string ignored;
            while (pendingLog.Count > 2000) pendingLog.TryDequeue(out ignored);
        }
        void DrainLog()
        {
            if (closing || logBox == null) return;
            var b = new StringBuilder(); string line; int count = 0;
            while (count++ < 200 && pendingLog.TryDequeue(out line)) b.AppendLine(line);
            if (b.Length == 0) return;
            if (logBox.TextLength > 100000) logBox.Text = logBox.Text.Substring(logBox.TextLength - 50000);
            logBox.AppendText(b.ToString());
        }
        void ShowWindow() { Show(); WindowState = FormWindowState.Normal; tray.Visible = false; Activate(); }
        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (closing) return;
            if ((server.Alive || uiBusy) && e.CloseReason == CloseReason.UserClosing)
            {
                var answer = MessageBox.Show(this, "종료하면 이 도구가 시작한 AI 서버와 모델도 종료됩니다.\r\n\r\n예: 서버 종료 후 닫기\r\n아니요: 알림 영역으로 최소화\r\n취소: 계속 사용", "Local AI 종료", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
                if (answer == DialogResult.No) { e.Cancel = true; WindowState = FormWindowState.Minimized; return; }
            }
            closing = true; monitorTimer.Stop(); libraryTimer.Stop(); server.Stop();
            try { ReadConfig(); SaveConfig(); } catch (Exception ex) { log.Write("종료 시 설정 저장 실패: " + ex.Message); }
        }
    }
}
