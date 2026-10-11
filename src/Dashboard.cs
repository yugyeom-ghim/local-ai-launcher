using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Management;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;
using System.Globalization;

namespace SILocalAI
{
    public sealed partial class MainForm
    {
        readonly LibraryMonitor catalog = new LibraryMonitor();
        readonly System.Windows.Forms.Timer libraryTimer = new System.Windows.Forms.Timer();
        bool catalogRefreshing, bindingModels;
        string loadedCatalogSignature = "", warningSignature = "";
        Label cpuValue, cpuNote, gpuValue, gpuNote, runtimeValue, runtimeNote;
        Label selectedName, selectedMeta, selectedPath, selectedState, catalogNote, runningNote;
        Badge stateBadge;
        ProgressBar progress;
        DataGridView modelGrid;
        WorkspaceTabs pages;
        Image brandImage;
        float uiScale = 1F;
        bool buildingUi, detectingHardware;
        readonly List<Control> layoutBatch = new List<Control>();
        double detectedVram, detectedRam;
        Label recommendation;
        CancellationTokenSource modelImport;

        Label L(string text, float size, bool bold) { return Theme.Text(text, size, bold); }
        TableLayoutPanel Table(int columns, params float[] widths)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, Margin = new Padding(0), BackColor = Color.Transparent };
            if (buildingUi) { t.SuspendLayout(); layoutBatch.Add(t); }
            foreach (float w in widths) t.ColumnStyles.Add(new ColumnStyle(w < 0 ? SizeType.Absolute : SizeType.Percent, Math.Abs(w)));
            return t;
        }
        TableLayoutPanel AutoTable(int columns, params float[] widths)
        {
            var t = Table(columns, widths); t.Dock = DockStyle.Top; t.AutoSize = true; t.AutoSizeMode = AutoSizeMode.GrowAndShrink; return t;
        }
        void AddRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowStyles.Count; table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(control, 0, row);
            if (table.ColumnCount > 1) table.SetColumnSpan(control, table.ColumnCount);
        }
        Surface Card(Control content, Padding padding)
        {
            var card = new Surface { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 12, 12), Padding = padding };
            card.Controls.Add(content); return card;
        }
        FlowLayoutPanel Actions(params Control[] controls)
        {
            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, Margin = new Padding(0, 6, 0, 6), BackColor = Color.Transparent };
            foreach (var c in controls) { c.Dock = DockStyle.None; c.AutoSize = true; c.Margin = new Padding(0, 0, 8, 6); flow.Controls.Add(c); }
            return flow;
        }
        ActionButton Action(string text, EventHandler click, bool primary)
        {
            var b = new ActionButton { Text = text, Primary = primary, Dock = DockStyle.Fill, AutoSize = true };
            if (click != null) b.Click += click; return b;
        }
        Surface Stat(string title, out Label value, out Label note)
        {
            var t = AutoTable(1, 100);
            var heading = L(title, 8.5F, true); heading.ForeColor = Theme.Muted; heading.Margin = new Padding(0, 0, 0, 8);
            value = L("확인 전", 11F, true); value.Margin = new Padding(0, 0, 0, 8);
            note = L("", 9F, false); note.ForeColor = Theme.Muted;
            AddRow(t, heading); AddRow(t, value); AddRow(t, note);
            var card = Card(t, new Padding(16)); card.Dock = DockStyle.Fill; return card;
        }
        void BuildUi()
        {
            SuspendLayout(); buildingUi = true;
            var shell = Table(1, 100); shell.BackColor = Color.White; shell.RowCount = 3;
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            var header = Table(2, -92, 100); header.Padding = new Padding(24, 12, 24, 10);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SILocalAI.BrandLogo"))
            { if (stream != null) using (var original = Image.FromStream(stream)) brandImage = new Bitmap(original); }
            var logo = new PictureBox { Image = brandImage, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 20, 0), BackColor = Color.White, AccessibleName = "회사 로고" };
            header.Controls.Add(logo, 0, 0);
            header.Controls.Add(new Label { Text = "Local AI Launcher", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 21F, FontStyle.Bold), ForeColor = Theme.Ink, Margin = new Padding(0) }, 1, 0);
            shell.Controls.Add(header, 0, 0);
            pages = new WorkspaceTabs();
            var main = new Panel { BackColor = paper, Padding = new Padding(24, 20, 24, 16) };
            var settings = new Panel { BackColor = paper, Padding = new Padding(24, 20, 24, 16) };
            var diagnostic = new Panel { BackColor = paper, Padding = new Padding(24, 20, 24, 16) };
            apiPage = new Panel { BackColor = paper, Padding = new Padding(24, 20, 24, 16) };
            pages.AddPage("모델", main); pages.AddPage("API 연결", apiPage); pages.AddPage("설정", settings); pages.AddPage("로그", diagnostic);
            BuildLibrary(main); BuildSettings(settings); BuildApiPage(apiPage); BuildLogs(diagnostic);
            shell.Controls.Add(pages, 0, 1);
            var foot = Table(3, -92, 100, -116); foot.Padding = new Padding(24, 6, 24, 6);
            stateBadge = new Badge { Text = "대기", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0) };
            statusLabel = new Label { Text = "모델 확인 중", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true, ForeColor = Theme.Muted, Margin = new Padding(0) };
            progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous, Maximum = 100, Margin = new Padding(12, 12, 0, 12) };
            foot.Controls.Add(stateBadge, 0, 0); foot.Controls.Add(statusLabel, 1, 0); foot.Controls.Add(progress, 2, 0); shell.Controls.Add(foot, 0, 2);
            modelBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            modelBox.SelectedIndexChanged += delegate { if (!bindingModels) { RememberSelection(); RefreshSelection(); } };
            Controls.Add(shell);
            for (int i = layoutBatch.Count - 1; i >= 0; i--) layoutBatch[i].ResumeLayout(false);
            layoutBatch.Clear(); buildingUi = false; ResumeLayout(true);
        }
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            uiScale *= factor.Width; base.ScaleControl(factor, specified);
        }
        void FitToWorkingArea()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min((int)(760 * uiScale), area.Width), Math.Min((int)(560 * uiScale), area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 2));
        }
        void BuildLibrary(Control page)
        {
            // Fixed outer bounds; only the details scroll. No preferred-height feedback into the window.
            var columns = Table(2, 58, 42); columns.RowCount = 1; columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.Controls.Add(columns);
            var list = Table(1, 100); list.RowCount = 3; list.Padding = new Padding(18);
            list.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); list.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); list.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var library = new Surface { Dock = DockStyle.Fill, Padding = new Padding(0), Margin = new Padding(0, 0, 16, 0) }; library.Controls.Add(list); columns.Controls.Add(library, 0, 0);
            var bar = Table(2, 100, -124);
            modelCount = L("모델", 15F, true); modelCount.Dock = DockStyle.Fill; bar.Controls.Add(modelCount, 0, 0);
            var add = Action("+ 모델 추가", delegate { ShowModelGuide(); }, false); add.Margin = new Padding(0, 0, 0, 14); bar.Controls.Add(add, 1, 0); list.Controls.Add(bar, 0, 0);
            modelGrid = MakeGrid(); modelGrid.Columns.Add("name", "모델"); modelGrid.Columns.Add("size", "용량");
            modelGrid.Columns[0].FillWeight = 76; modelGrid.Columns[1].FillWeight = 24;
            modelGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; modelGrid.RowTemplate.MinimumHeight = 68;
            modelGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            modelGrid.SelectionChanged += delegate { if (bindingModels) return; var m = modelGrid.CurrentRow == null ? null : modelGrid.CurrentRow.Tag as ModelInfo; if (m != null) modelBox.SelectedItem = m; };
            list.Controls.Add(modelGrid, 0, 1);
            catalogNote = new Label { Text = "", Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Theme.Muted, TextAlign = ContentAlignment.BottomLeft, Font = new Font("Malgun Gothic", 9F) }; list.Controls.Add(catalogNote, 0, 2);
            var details = new Surface { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(20) }; columns.Controls.Add(details, 1, 0);
            var detailShell = Table(1, 100); detailShell.RowCount = 2;
            detailShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); detailShell.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            details.Controls.Add(detailShell); var viewport = new ContentViewport(); detailShell.Controls.Add(viewport, 0, 0);
            var detail = AutoTable(1, 100);
            var tag = L("선택한 모델", 9F, false); tag.ForeColor = Theme.Muted; tag.Margin = new Padding(0, 0, 0, 12); AddRow(detail, tag);
            selectedName = L("모델을 추가하세요", 18F, true); selectedName.Margin = new Padding(0, 0, 0, 10); AddRow(detail, selectedName);
            selectedMeta = L("", 10F, false); selectedMeta.ForeColor = Theme.Muted; selectedMeta.Margin = new Padding(0, 0, 0, 18); AddRow(detail, selectedMeta);
            selectedPath = L("", 9F, false); // Full filename is available as a tooltip, not a repeated paragraph.
            AddRow(detail, L("실행 장치", 10F, true));
            backendBox = Combo(new[] { "자동", "CPU", "CUDA 12.4", "CUDA 13.1 (기존)", "CUDA 13.4" }); backendBox.Margin = new Padding(0, 8, 0, 16); AddRow(detail, backendBox);
            AddRow(detail, L("사고 모드", 10F, true));
            thinkingModeBox = Combo(new[] { "OFF — 빠른 답변", "ON — 깊이 생각" }); thinkingModeBox.Margin = new Padding(0, 8, 0, 12); AddRow(detail, thinkingModeBox);
            selectedState = L("", 9F, false); selectedState.ForeColor = Theme.Accent; selectedState.Margin = new Padding(0, 0, 0, 12); AddRow(detail, selectedState);
            startButton = Action("시작", async delegate { await Work(StartFromUi); }, true);
            stopButton = Action("중지", delegate { cancelRequested = true; if (modelImport != null) modelImport.Cancel(); else server.Stop(); }, false);
            var run = Table(2, 70, 30); run.RowCount = 1; run.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); run.Height = 48; run.Dock = DockStyle.Top; startButton.AutoSize = false; stopButton.AutoSize = false;
            startButton.Margin = new Padding(0, 0, 8, 0); stopButton.Margin = new Padding(0); run.Controls.Add(startButton, 0, 0); run.Controls.Add(stopButton, 1, 0); loadButton = startButton;
            var runArea = Table(1, 100); runArea.RowCount = 2; runArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); runArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); runArea.Controls.Add(run, 0, 0); detailShell.Controls.Add(runArea, 0, 1);
            runningNote = L("", 9F, false); runningNote.Visible = false; runningNote.ForeColor = Theme.Muted; runningNote.Margin = new Padding(0, 16, 0, 0); AddRow(detail, runningNote);
            webButton = Action("대화 열기", delegate { OpenUi(); }, false); runArea.Controls.Add(Actions(webButton), 0, 1);
            viewport.SetContent(detail);
            // Compatibility fields remain internal; connection details have a single home in the API tab.
            baseUrl = ReadonlyText(); chatUrl = ReadonlyText(); modelId = ReadonlyText(); hardwareLabel = L("", 9F, false);
            scanButton = Action("새로고침", async delegate { await Work(RescanFromUi); }, false);
            connectionSummary = L("", 9F, false);
        }
        static string DisplayModel(ModelInfo model)
        {
            var profile = ModelCatalog.Current.Find(model);
            return profile == null ? Path.GetFileNameWithoutExtension(model.Name) : profile.name;
        }
        Control ConnectionRow(string text, TextBox field)
        {
            var row = AutoTable(3, -65, 100, -64); row.RowStyles.Add(new RowStyle(SizeType.AutoSize)); row.Margin = new Padding(0, 0, 0, 5);
            var label = L(text, 8.5F, false); label.ForeColor = Theme.Muted; label.Padding = new Padding(0, 9, 0, 0); row.Controls.Add(label, 0, 0);
            field.Font = new Font("Segoe UI", 9F); field.Margin = new Padding(0, 8, 8, 0); field.BorderStyle = BorderStyle.FixedSingle; field.AccessibleName = text;
            row.Controls.Add(field, 1, 0);
            var b = Action("복사", delegate { TryAction(delegate { if (!String.IsNullOrWhiteSpace(field.Text)) { Clipboard.SetText(field.Text); statusLabel.Text = text + " 복사 완료"; } }); }, false);
            b.Margin = new Padding(0); row.Controls.Add(b, 2, 0); return row;
        }
        void ShowModelGuide()
        {
            using (var dialog = new Form { Text = "모델 추가", Font = Font, BackColor = Color.White, StartPosition = FormStartPosition.CenterParent,
                AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi, ClientSize = new Size(590, 440), MinimumSize = new Size(420, 340), MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false })
            {
                var viewport = new ContentViewport { Padding = new Padding(24) }; var body = AutoTable(1, 100);
                var heading = L("모델 추가", 18F, true); heading.Margin = new Padding(0, 0, 0, 18); AddRow(body, heading);
                AddRow(body, L("GGUF 파일을 모델 폴더에 복사하면 목록에 나타납니다.", 10F, false));
                var folder = new TextBox { ReadOnly = true, Dock = DockStyle.Top, Margin = new Padding(0, 14, 0, 12), Text = Paths.Full(Paths.Full(root, config.AssetRoot), config.ModelsPath), AccessibleName = "모델 폴더" }; AddRow(body, folder);
                AddRow(body, Actions(Action("폴더 열기", delegate { OpenModels(); }, false), Action("모델 팩 설치", async delegate { dialog.Close(); await Work(ImportModelPack); }, true)));
                foreach (var model in ModelCatalog.Current.models)
                {
                    var item = L(model.name + "  ·  " + (model.size / 1e9).ToString("0.00") + " GB\n" + model.tier, 9.5F, false);
                    item.ForeColor = Theme.Muted; item.Margin = new Padding(0, 12, 0, 0); AddRow(body, item);
                }
                var close = Action("닫기", delegate { dialog.Close(); }, false); dialog.CancelButton = close; AddRow(body, Actions(Action("목록 새로고침", async delegate { dialog.Close(); await Work(RescanFromUi); }, false), close));
                viewport.SetContent(body); dialog.Controls.Add(viewport); dialog.ShowDialog(this);
            }
        }
        async Task ImportModelPack()
        {
            using (var picker = new OpenFileDialog { Title = "오프라인 모델 팩 선택", Filter = "모델 팩|*.modelpack.json", CheckFileExists = true })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                string path = picker.FileName;
                string folder = Paths.Full(Paths.Full(root, config.AssetRoot), config.ModelsPath);
                var reporter = new Progress<int>(delegate(int percent) { if (!closing) statusLabel.Text = "모델 팩 설치 · SHA256 검사 " + percent + "%"; });
                int last = -1;
                using (var cancellation = new CancellationTokenSource())
                {
                    modelImport = cancellation; stopButton.Text = "설치 취소";
                    try { await Task.Run(delegate { ModelPacks.Import(path, folder, delegate(long done, long total) {
                        int percent = (int)(done * 100 / total); if (percent != last) { last = percent; ((IProgress<int>)reporter).Report(percent); }
                    }, cancellation.Token); }); }
                    finally { modelImport = null; if (!closing) stopButton.Text = "중지"; }
                }
                if (!closing) { await ScanAsync(); statusLabel.Text = "모델 추가 완료 · 목록에서 선택 후 시작하세요."; }
            }
        }
        DataGridView MakeGrid()
        {
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                MultiSelect = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, GridColor = Theme.Line,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Font = new Font("Segoe UI", 11F), Margin = new Padding(0), AccessibleName = "사용 가능한 로컬 모델" };
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Background, ForeColor = Theme.Muted, Font = new Font("Malgun Gothic", 8.5F, FontStyle.Bold), Padding = new Padding(8, 6, 0, 6), SelectionBackColor = Theme.Background, SelectionForeColor = Theme.Muted };
            g.DefaultCellStyle = new DataGridViewCellStyle { Font = g.Font, BackColor = Color.White, ForeColor = Theme.Ink, SelectionBackColor = Theme.Pale, SelectionForeColor = Theme.Ink, Padding = new Padding(8, 8, 6, 8) };
            return g;
        }
        void BuildSettings(Control page)
        {
            var tabs = new WorkspaceTabs();
            var paths = new Panel { BackColor = Color.White, Padding = new Padding(18) };
            var settings = new Panel { BackColor = Color.White, Padding = new Padding(18) };
            var pc = new Panel { BackColor = Color.White, Padding = new Padding(18) };
            tabs.AddPage("서버", settings); tabs.AddPage("폴더", paths); tabs.AddPage("PC 정보", pc); page.Controls.Add(tabs); BuildAssets(paths);
            var pcViewport = new ContentViewport(); var pcBody = AutoTable(1, 100);
            AddRow(pcBody, Actions(Action("PC 정보 확인", async delegate { await DetectHardwareAsync(); }, false)));
            AddRow(pcBody, Stat("CPU · 메모리", out cpuValue, out cpuNote)); AddRow(pcBody, Stat("그래픽 장치", out gpuValue, out gpuNote));
            AddRow(pcBody, Stat("실행기", out runtimeValue, out runtimeNote));
            recommendation = L("", 10F, false); recommendation.ForeColor = Theme.Accent; AddRow(pcBody, recommendation); pcViewport.SetContent(pcBody); pc.Controls.Add(pcViewport);
            assetGrid.BackgroundColor = Color.White; assetGrid.GridColor = Theme.Line; assetGrid.EnableHeadersVisualStyles = false; assetGrid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Background; assetGrid.BorderStyle = BorderStyle.None;
            var viewport = new ContentViewport(); settings.Controls.Add(viewport); var t = AutoTable(1, 100); viewport.SetContent(t);
            modeBox = Combo(new[] { "자동 (선택 모델만 실행)", "목록 (선택 모델만 등록)", "단일 모델 (호환 모드)" }); AddRow(t, L("서버 모드", 9F, true)); AddRow(t, modeBox);
            portBox = Number(1024, 65535, 1); contextBox = Number(512, 32768, 512); threadsBox = Number(0, 512, 1); timeoutBox = Number(30, 900, 30); gpuBox = Number(-1, 999, 1);
            var options = new ResponsiveColumns(620, 50, 50);
            string[] names = { "서버 포트", "문맥 토큰", "CPU 스레드 (0=자동)", "GPU 레이어 (-1=자동)", "로딩 제한 (초)" };
            Control[] fields = { portBox, contextBox, threadsBox, gpuBox, timeoutBox };
            for (int i = 0; i < fields.Length; i++) { var field = AutoTable(1, 100); field.Margin = new Padding(0, 10, 16, 4); AddRow(field, L(names[i], 9F, false)); fields[i].Dock = DockStyle.Top; AddRow(field, fields[i]); options.Controls.Add(field); } AddRow(t, options);
            autoBox = new CheckBox { Text = "앱을 열 때 마지막 모델 자동 실행", AutoSize = true, Margin = new Padding(0, 10, 0, 8) };
            fallbackBox = new CheckBox { Text = "자동 모드에서 CPU 전환 허용", AutoSize = true, Margin = new Padding(0, 4, 0, 8) };
            browserBox = new CheckBox { Text = "서버 시작 후 llama-ui 열기", AutoSize = true, Margin = new Padding(0, 4, 0, 8) };
            foreach (Control c in new Control[] { autoBox, fallbackBox, browserBox }) { editable.Add(c); AddRow(t, c); }
            var save = Action("설정 저장", delegate { TryAction(delegate { ReadConfig(); SaveConfig(); statusLabel.Text = "저장됨 · 다음 시작 시 적용"; }); }, true); editable.Add(save); AddRow(t, Actions(save));
            var note = L("다음 서버 시작 시 적용됩니다.", 9F, false); note.ForeColor = Theme.Muted; AddRow(t, note);
        }
        void BuildLogs(Control page)
        {
            var t = Table(1, 100); t.RowCount = 2; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.Controls.Add(t);
            var bar = AutoTable(1, 100); var note = L("실행 기록", 9F, false); note.ForeColor = Theme.Muted; AddRow(bar, note);
            AddRow(bar, Actions(Action("로그 폴더 열기", delegate { OpenPath(state); }, false))); t.Controls.Add(bar, 0, 0);
            logBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, BackColor = Color.FromArgb(248, 250, 252), ForeColor = Theme.Ink, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9F) }; t.Controls.Add(logBox, 0, 1);
        }
        void RememberSelection()
        {
            var m=SelectedModel(); if(m==null) return;
            config.Model=m.RelativePath;
            try {SaveConfig();} catch(Exception e) {log.Write("모델 선택 저장 실패: "+e.Message);}
        }
        void BindModelGrid()
        {
            if(modelGrid==null || modelBox==null) return;
            bindingModels=true;
            try
            {
                string id=SelectedModel()==null?"":SelectedModel().Id; modelGrid.Rows.Clear();
                foreach(var m in inventory.Models)
                {
                    bool running=server.Status.Ready && server.Status.ModelId==m.Id;
                    int r=modelGrid.Rows.Add(DisplayModel(m) + (running ? " · 실행 중" : ""), (m.Bytes/1e9).ToString("0.0")+" GB"); modelGrid.Rows[r].Tag=m;
                    modelGrid.Rows[r].Cells[0].ToolTipText = m.RelativePath;
                }
                modelGrid.ClearSelection();
                foreach(DataGridViewRow row in modelGrid.Rows) if(((ModelInfo)row.Tag).Id==id) { modelGrid.CurrentCell=row.Cells[0]; row.Selected=true; break; }
                modelCount.Text="모델  "+inventory.Models.Count;
            }
            finally {bindingModels=false;}
            RefreshSelection();
        }
        void RefreshSelection()
        {
            if(selectedName==null || modelBox==null) return; var m=SelectedModel();
            selectedName.Text=m==null?"모델을 추가하세요":DisplayModel(m);
            var profile = ModelCatalog.Current.Find(m);
            selectedMeta.Text=m==null?"GGUF 파일 또는 모델 팩":(m.Bytes/1073741824.0).ToString("0.00")+" GiB" + (profile == null ? " · " + m.Architecture : " · 문맥 " + (profile.context / 1024) + "K 권장");
            if(m!=null) tips.SetToolTip(selectedName,m.RelativePath);
            bool same=m!=null && server.Status.Ready && server.Status.ModelId==m.Id;
            selectedState.Text=server.Alive ? (same ? "실행 중 · 변경 사항은 재시작 후 적용" : "시작하면 현재 모델을 교체합니다") : "";
            selectedState.Visible = server.Alive;
            startButton.Text=uiBusy?"준비 중…":server.Alive?(same?"재시작":"모델 전환") : "시작";
            startButton.Enabled=!uiBusy && m!=null && inventory.Backends.Any(b=>!String.IsNullOrEmpty(b.Exe));
        }
        void UpdateDashboard(ServerStatus s)
        {
            if(stateBadge==null) return;
            stateBadge.Text=s.Ready?"준비 완료":s.State=="error"?"오류":s.State=="loading"?"로딩 중":s.State=="probing" || s.State=="testing"?"검사 중":s.Listening?"모델 대기":"대기";
            stateBadge.ForeColor=s.State=="error"?Theme.Danger:s.Ready?Theme.Accent:Theme.Muted;
            stateBadge.BackColor=s.State=="error"?Color.FromArgb(254,237,234):s.Ready?Theme.Pale:Color.FromArgb(233,238,245);
            runningNote.Visible = s.Listening;
            runningNote.Text=s.Ready?s.Backend + " · 실행 중":s.Listening?"모델 준비 중":"";
            if(s.Ready) modelId.Text=s.ModelId; else if(!s.Listening) modelId.Text="";
            if(!s.Listening && !uiBusy) { baseUrl.Text=""; chatUrl.Text=""; }
            BindModelGrid();
        }
        void UpdateRuntimeCards()
        {
            int count=inventory.Backends.Count(b=>!String.IsNullOrEmpty(b.Exe)); runtimeValue.Text=count+"개 실행기 감지";
            runtimeNote.Text=String.Join("  /  ",inventory.Backends.Select(b=>b.Label+(!String.IsNullOrEmpty(b.Exe)?" ✓":" -")));
            tips.SetToolTip(runtimeNote,"파일 존재를 확인한 상태입니다. DLL·GPU 실행 호환성은 서버를 시작할 때 실제 검사합니다.");
        }
        async Task DetectHardwareAsync()
        {
            if (detectingHardware) return; detectingHardware = true; cpuValue.Text = "확인 중…";
            try
            {
                var result=await Task.Run(delegate {
                    string cpu="CPU 정보 확인 불가",memory="",gpu="GPU 정보 확인 불가",driver="";
                    var errors=new List<string>();
                    Action<string,Action<ManagementObject>> query=delegate(string text,Action<ManagementObject> each) {
                        try {
                            var options=new EnumerationOptions {Timeout=TimeSpan.FromSeconds(3),ReturnImmediately=true};
                            using(var searcher=new ManagementObjectSearcher(new ManagementScope("root\\CIMV2"),new ObjectQuery(text),options))
                            using(var rows=searcher.Get()) foreach(ManagementObject row in rows) { using(row) each(row); }
                        } catch(Exception e) {errors.Add(e.Message);}
                    };
                    query("SELECT Name FROM Win32_Processor",delegate(ManagementObject r) { cpu=Convert.ToString(r["Name"]).Trim(); });
                    query("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem",delegate(ManagementObject r) {memory=(Convert.ToDouble(r["TotalPhysicalMemory"])/1073741824.0).ToString("0")+" GiB RAM";});
                    var gpus=new List<string>();
                    query("SELECT Name,DriverVersion FROM Win32_VideoController",delegate(ManagementObject r) {string name=Convert.ToString(r["Name"]); gpus.Add(name); if(name.IndexOf("NVIDIA",StringComparison.OrdinalIgnoreCase)>=0){gpu=name;driver="Driver "+Convert.ToString(r["DriverVersion"]);}});
                    if(!gpus.Any(n=>n.IndexOf("NVIDIA",StringComparison.OrdinalIgnoreCase)>=0)) {gpu=gpus.Count>0?String.Join(" / ",gpus):"GPU 정보 확인 불가";driver=gpus.Count>0?"NVIDIA 미감지 · CPU 실행기 사용 가능":"CUDA 장치는 서버 시작 시 다시 검사합니다.";}
                    return new[]{cpu,memory,gpu,driver,String.Join("; ",errors)};
                });
                if(closing || IsDisposed)return;
                cpuValue.Text=result[0]; cpuNote.Text=(result[1].Length>0?result[1]+"  ·  ":"")+Environment.ProcessorCount+" 논리 프로세서";
                double.TryParse(result[1].Split(' ')[0], NumberStyles.Float, CultureInfo.CurrentCulture, out detectedRam);
                detectedVram = 0;
                string smi = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe") }.FirstOrDefault(File.Exists);
                if (smi != null)
                {
                    try
                    {
                        var probe = await Probe.Run(smi, new[] { "--query-gpu=memory.total", "--format=csv,noheader,nounits" }, root, state, 6, CancellationToken.None);
                        if (probe.Exit == 0) foreach (string line in probe.Text.Split('\n'))
                        { double mib; if (Double.TryParse(line.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out mib)) detectedVram = Math.Max(detectedVram, mib / 1024); }
                    }
                    catch (Exception e) { log.Write("VRAM 확인 불가: " + e.Message); }
                }
                if (closing || IsDisposed) return;
                gpuValue.Text=result[2]; gpuNote.Text=(detectedVram > 0 ? detectedVram.ToString("0.#") + " GiB VRAM · " : "") + result[3];
                tips.SetToolTip(cpuValue,result[0]); tips.SetToolTip(gpuValue,result[2]);
                var recommended = ModelCatalog.Current.Recommend(detectedVram, detectedRam);
                recommendation.Text = recommended == null ? "사양별 모델 / 추가 방법에서 모델을 선택하세요." : "사양 기준 추천 · " + recommended.name + "  (문맥 " + (recommended.context / 1024) + "K)";
                if (detectedVram == 0 && result[2].IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) recommendation.Text = "VRAM 확인 불가 · 4060 Ti 8GB → 4B / 16GB → 9B";
                if(result[4].Length>0) log.Write("PC 환경 일부 확인 불가 (서버 실행과 별개): "+result[4]);
            } catch(Exception e) { if(!closing) log.Write("PC 감지 실패: "+e.Message); }
            finally { detectingHardware = false; }
        }
        void OpenModels()
        {
            TryAction(delegate {
                string path=Paths.Full(Paths.Full(root,config.AssetRoot),config.ModelsPath);
                Directory.CreateDirectory(path); OpenPath(path);
            });
        }
    }
}
