using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Management;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;

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
        TextBox searchBox;
        DataGridView modelGrid;
        TabControl pages;
        Control dashboardHead, dashboardStats, dashboardFoot;
        TableLayoutPanel dashboardLayout;
        Image brandImage;
        float uiScale = 1F;
        bool sizingDashboard, dashboardSizeQueued;

        Label L(string text, float size, bool bold) { return Theme.Text(text, size, bold); }
        TableLayoutPanel Table(int columns, params float[] widths)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, Margin = new Padding(0), BackColor = Color.Transparent };
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
            value = L("감지 중...", 11F, true); value.Margin = new Padding(0, 0, 0, 8);
            note = L("PC 환경을 확인하고 있습니다.", 8.5F, false); note.ForeColor = Theme.Muted;
            AddRow(t, heading); AddRow(t, value); AddRow(t, note);
            var card = Card(t, new Padding(16)); card.Dock = DockStyle.Fill; return card;
        }
        void BuildUi()
        {
            SuspendLayout();
            var viewport = new ContentViewport { Padding = new Padding(20, 16, 8, 12) }; Controls.Add(viewport);
            dashboardLayout = AutoTable(1, 100);
            var heading = AutoTable(3, -90, 100, -176); heading.Margin = new Padding(0, 0, 12, 16); heading.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SILocalAI.BrandLogo"))
            {
                if (stream != null) using (var original = Image.FromStream(stream)) brandImage = new Bitmap(original);
            }
            var logo = new PictureBox { Image = brandImage, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(78, 60),
                Margin = new Padding(0, 4, 12, 4), BackColor = Color.White, AccessibleName = "SI 로고" };
            heading.Controls.Add(logo, 0, 0);
            var titles = AutoTable(1, 100);
            AddRow(titles, L("Local AI Launcher", 20F, true));
            var sub = L("내 PC에서 실행하는 오프라인 AI", 9.5F, false); sub.ForeColor = Theme.Muted; sub.Margin = new Padding(0, 4, 0, 0); AddRow(titles, sub);
            heading.Controls.Add(titles, 1, 0);
            heading.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
            var local = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = new Padding(12, 7, 0, 0) };
            var localTitle = new Label { Text = "LOCAL ONLY", AutoSize = true, Font = new Font("Malgun Gothic", 9F, FontStyle.Bold),
                ForeColor = Theme.Accent, Margin = new Padding(0), Anchor = AnchorStyles.Right };
            var address = new Label { Text = "127.0.0.1 전용", AutoSize = true, Font = new Font("Malgun Gothic", 9F),
                ForeColor = Theme.Muted, Margin = new Padding(0, 6, 0, 0), Anchor = AnchorStyles.Right };
            local.Controls.Add(localTitle); local.Controls.Add(address); heading.Controls.Add(local, 2, 0); dashboardHead = heading; AddRow(dashboardLayout, heading);

            var stats = new ResponsiveColumns(610, 36, 34, 30);
            stats.Controls.Add(Stat("CPU · 메모리", out cpuValue, out cpuNote));
            stats.Controls.Add(Stat("그래픽 장치", out gpuValue, out gpuNote));
            stats.Controls.Add(Stat("로컬 실행기", out runtimeValue, out runtimeNote)); dashboardStats = stats; AddRow(dashboardLayout, stats);

            pages = new TabControl { Height = 530, Dock = DockStyle.Top, Padding = new Point(18, 8),
                Font = new Font("Malgun Gothic", 9F, FontStyle.Bold), Margin = new Padding(0, 2, 12, 0) };
            var main = new TabPage("모델 라이브러리") { BackColor = paper, Padding = new Padding(12) };
            var assets = new TabPage("경로 · 고급 설정") { BackColor = paper, Padding = new Padding(12) };
            var diagnostic = new TabPage("진단 로그") { BackColor = paper, Padding = new Padding(12) };
            apiPage = new TabPage("API 연결") { BackColor = paper, Padding = new Padding(12) };
            pages.Multiline = true;
            pages.TabPages.AddRange(new[] { main, apiPage, assets, diagnostic });
            BuildLibrary(main); BuildSettings(assets); BuildApiPage(apiPage); BuildLogs(diagnostic); AddRow(dashboardLayout, pages);

            var foot = AutoTable(3, -112, 100, -132); foot.Margin = new Padding(0, 12, 12, 0); foot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stateBadge = new Badge { Text = "대기", Dock = DockStyle.Fill, MinimumSize = new Size(98, 34), Margin = new Padding(0, 0, 12, 0) }; foot.Controls.Add(stateBadge, 0, 0);
            statusLabel = L("모델과 실행기를 검색합니다.", 9F, false); statusLabel.Padding = new Padding(0, 7, 0, 7); foot.Controls.Add(statusLabel, 1, 0);
            progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous, Maximum = 100, MinimumSize = new Size(0, 6), Margin = new Padding(12, 12, 0, 12) }; foot.Controls.Add(progress, 2, 0);
            dashboardFoot = foot; AddRow(dashboardLayout, foot); viewport.SetContent(dashboardLayout);
            modelBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            modelBox.SelectedIndexChanged += delegate { if (!bindingModels) { RememberSelection(); RefreshSelection(); } };
            Resize += delegate { QueueDashboardSize(); };
            dashboardLayout.Layout += delegate { QueueDashboardSize(); };
            dashboardHead.SizeChanged += delegate { SizeDashboard(); };
            dashboardStats.SizeChanged += delegate { SizeDashboard(); };
            dashboardFoot.SizeChanged += delegate { SizeDashboard(); };
            ResumeLayout(true);
        }
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            uiScale *= factor.Width; base.ScaleControl(factor, specified);
        }
        void QueueDashboardSize()
        {
            if (!IsHandleCreated || IsDisposed || closing || dashboardSizeQueued) return;
            dashboardSizeQueued = true;
            BeginInvoke((MethodInvoker)delegate {
                dashboardSizeQueued = false;
                if (!IsDisposed && !closing) SizeDashboard();
            });
        }
        void SizeDashboard()
        {
            if (pages == null || sizingDashboard) return;
            sizingDashboard = true;
            try
            {
                int available = ClientSize.Height - dashboardHead.Height - dashboardStats.Height - dashboardFoot.Height - (int)(76 * uiScale);
                int height = Math.Max((int)(410 * uiScale), available);
                if (pages.Height != height) pages.Height = height;
            }
            finally { sizingDashboard = false; }
        }
        void FitToWorkingArea()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min((int)(720 * uiScale), area.Width), Math.Min((int)(540 * uiScale), area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 2));
            SizeDashboard();
        }
        void BuildLibrary(Control page)
        {
            var viewport = new ContentViewport(); page.Controls.Add(viewport);
            var columns = new ResponsiveColumns(940, 57, 43); viewport.SetContent(columns);
            var list = AutoTable(1, 100);
            modelCount = L("로컬 모델", 12F, true); AddRow(list, modelCount);
            scanButton = Action("새로고침", async delegate { await Work(RescanFromUi); }, false);
            AddRow(list, Actions(Action("models 폴더 열기", delegate { OpenModels(); }, false), scanButton,
                Action("모델 추가 방법", delegate { ShowModelGuide(); }, false)));
            var search = AutoTable(2, -46, 100); search.RowStyles.Add(new RowStyle(SizeType.AutoSize)); search.Margin = new Padding(0, 0, 0, 12);
            var searchLabel = L("검색", 9F, false); searchLabel.Padding = new Padding(0, 4, 0, 0); search.Controls.Add(searchLabel, 0, 0);
            searchBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Malgun Gothic", 10F), AccessibleName = "모델 검색" };
            searchBox.TextChanged += delegate { BindModelGrid(); }; search.Controls.Add(searchBox, 1, 0); AddRow(list, search);
            modelGrid = MakeGrid(); modelGrid.Columns.Add("name", "모델"); modelGrid.Columns.Add("size", "크기"); modelGrid.Columns.Add("state", "상태");
            modelGrid.Columns[0].FillWeight = 64; modelGrid.Columns[1].FillWeight = 18; modelGrid.Columns[2].FillWeight = 18;
            modelGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; modelGrid.RowTemplate.MinimumHeight = 52;
            modelGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True; modelGrid.Height = 230; modelGrid.Dock = DockStyle.Fill;
            modelGrid.SelectionChanged += delegate { if (bindingModels) return; var m = modelGrid.CurrentRow == null ? null : modelGrid.CurrentRow.Tag as ModelInfo; if (m != null) modelBox.SelectedItem = m; };
            modelGrid.CellDoubleClick += async delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && modelGrid.Rows[e.RowIndex].Tag is ModelInfo) await Work(StartFromUi); };
            int gridRow = list.RowStyles.Count; list.RowCount = gridRow + 1; list.RowStyles.Add(new RowStyle(SizeType.Absolute, 230)); list.Controls.Add(modelGrid, 0, gridRow);
            catalogNote = L("models 폴더에 GGUF 파일을 넣으면 자동으로 검색합니다.", 8.5F, false); catalogNote.ForeColor = Theme.Muted; catalogNote.Margin = new Padding(0, 10, 0, 10); AddRow(list, catalogNote);
            var guide = L("모델 추가  ·  models 폴더 열기 → .gguf 파일 복사 → 목록에서 선택 후 시작", 9F, true);
            guide.ForeColor = Theme.Accent; guide.BackColor = Theme.Pale; guide.Padding = new Padding(12); AddRow(list, guide);
            var library = Card(list, new Padding(16)); columns.Controls.Add(library);

            var side = AutoTable(1, 100); columns.Controls.Add(side);
            var detail = AutoTable(1, 100);
            var tag = L("선택한 모델", 9F, true); tag.ForeColor = Theme.Muted; tag.Margin = new Padding(0, 0, 0, 8); AddRow(detail, tag);
            selectedName = L("사용할 모델을 선택하세요", 13F, true); selectedName.Margin = new Padding(0, 0, 0, 8); AddRow(detail, selectedName);
            selectedMeta = L("GGUF 모델이 아직 없습니다.", 9F, false); selectedMeta.ForeColor = Theme.Muted; AddRow(detail, selectedMeta);
            selectedPath = L("", 8.5F, false); selectedPath.ForeColor = Theme.Muted; selectedPath.Margin = new Padding(0, 5, 0, 12); AddRow(detail, selectedPath);
            AddRow(detail, L("실행기", 9F, true));
            backendBox = Combo(new[] { "자동 검사 (CUDA → CPU)", "CPU", "CUDA 12.4", "CUDA 13.1 (기존)", "CUDA 13.4" }); backendBox.Margin = new Padding(0, 5, 0, 10); AddRow(detail, backendBox);
            AddRow(detail, L("사고 모드", 9F, true));
            thinkingModeBox = Combo(new[] { "OFF · 빠르게 답변", "ON · 생각한 뒤 답변" });
            thinkingModeBox.Margin = new Padding(0, 5, 0, 6); AddRow(detail, thinkingModeBox);
            var thinkingHint = L("시작 / 재시작 시 적용 · 지원 모델에서 사용", 8.5F, false);
            thinkingHint.ForeColor = Theme.Muted; thinkingHint.Margin = new Padding(0, 0, 0, 10); AddRow(detail, thinkingHint);
            selectedState = L("선택 후 실행 버튼을 누르세요.", 9F, false); selectedState.ForeColor = Theme.Accent; selectedState.Margin = new Padding(0, 0, 0, 6); AddRow(detail, selectedState);
            startButton = Action("선택한 모델 시작", async delegate { await Work(StartFromUi); }, true);
            stopButton = Action("중지", delegate { cancelRequested = true; server.Stop(); }, false);
            AddRow(detail, Actions(startButton, stopButton)); loadButton = startButton; AddRow(side, Card(detail, new Padding(16)));

            var ct = AutoTable(1, 100); var ctTitle = L("로컬 API 연결 정보", 12F, true); ctTitle.Margin = new Padding(0, 0, 0, 8); AddRow(ct, ctTitle);
            runningNote = L("서버를 시작하면 연결 정보가 표시됩니다.", 8.5F, false); runningNote.ForeColor = Theme.Muted; runningNote.Margin = new Padding(0, 0, 0, 10); AddRow(ct, runningNote);
            baseUrl = ReadonlyText(); chatUrl = ReadonlyText(); modelId = ReadonlyText();
            AddRow(ct, ConnectionRow("기본 주소", baseUrl)); AddRow(ct, ConnectionRow("요청 주소", chatUrl)); AddRow(ct, ConnectionRow("모델 ID", modelId));
            hardwareLabel = L("", 8.5F, false); hardwareLabel.ForeColor = Theme.Muted; hardwareLabel.Margin = new Padding(0, 6, 0, 0); AddRow(ct, hardwareLabel);
            webButton = Action("llama-ui 열기", delegate { OpenUi(); }, false);
            testButton = Action("연결 테스트", async delegate { await Work(async delegate { string text = await server.TestAsync(); if (!closing) MessageBox.Show(this, text, "API 생성 테스트"); }); }, false);
            connectionSummary = L("API 키 없이 연결 · 사고 모드 OFF", 9F, true); connectionSummary.ForeColor = Theme.Accent; AddRow(ct, connectionSummary);
            AddRow(ct, Actions(Action("API 연결 설정 / OpenCode", delegate { pages.SelectedTab = apiPage; }, true), webButton, testButton)); AddRow(side, Card(ct, new Padding(16)));
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
            using (var dialog = new Form { Text = "모델 추가 방법", Font = Font, BackColor = paper, StartPosition = FormStartPosition.CenterParent,
                AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi, ClientSize = new Size(600, 480), MinimumSize = new Size(400, 320), MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false })
            {
                var viewport = new ContentViewport { Padding = new Padding(22) }; dialog.Controls.Add(viewport);
                var body = AutoTable(1, 100); viewport.SetContent(body);
                var heading = L("GGUF 파일을 넣으면 자동으로 표시됩니다", 15F, true); heading.Margin = new Padding(0, 0, 0, 16); AddRow(body, heading);
                foreach (string step in new[] {
                    "1. ‘models 폴더 열기’를 눌러 현재 사용 중인 모델 폴더를 엽니다.",
                    "2. 승인된 GGUF 모델 파일(.gguf)을 그 폴더에 복사합니다. 기본 위치는 offline-assets/ai/models 입니다.",
                    "3. 복사가 끝나면 잠시 후 목록에 나타납니다. 원하는 모델을 선택하고 ‘선택한 모델 시작’을 누르세요." })
                { var text = L(step, 10F, false); text.Margin = new Padding(0, 0, 0, 14); AddRow(body, text); }
                var note = L("· 파일을 추가하거나 선택하는 것만으로 서버가 바뀌지 않습니다.\n· 분할 GGUF는 모든 조각을 같은 폴더에 복사하세요.\n· mmproj 등 이미지용 파일은 텍스트 모델 목록에서 제외됩니다.\n· 목록에 없으면 ‘새로고침’과 ‘진단 로그’를 확인하세요.\n· 실제 실행 가능 여부는 llama.cpp 버전과 PC 메모리에 따라 달라집니다.", 9F, false);
                note.ForeColor = Theme.Muted; AddRow(body, note);
                var folder = new TextBox { ReadOnly = true, Dock = DockStyle.Top, Margin = new Padding(0, 14, 0, 8), Text = Paths.Full(Paths.Full(root, config.AssetRoot), config.ModelsPath), AccessibleName = "현재 모델 폴더" }; AddRow(body, folder);
                var close = Action("닫기", delegate { dialog.Close(); }, true); dialog.AcceptButton = close; dialog.CancelButton = close;
                AddRow(body, Actions(Action("models 폴더 열기", delegate { OpenModels(); }, false), close));
                dialog.Shown += delegate { var area = Screen.FromControl(dialog).WorkingArea; dialog.MinimumSize = new Size(Math.Min(dialog.MinimumSize.Width, area.Width), Math.Min(dialog.MinimumSize.Height, area.Height)); dialog.Size = new Size(Math.Min(dialog.Width, area.Width), Math.Min(dialog.Height, area.Height)); };
                dialog.ShowDialog(this);
            }
        }
        DataGridView MakeGrid()
        {
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                MultiSelect = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, GridColor = Theme.Line,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Font = new Font("Malgun Gothic", 9F), Margin = new Padding(0), AccessibleName = "사용 가능한 로컬 모델" };
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Background, ForeColor = Theme.Muted, Font = new Font("Malgun Gothic", 8.5F, FontStyle.Bold), Padding = new Padding(8, 6, 0, 6), SelectionBackColor = Theme.Background, SelectionForeColor = Theme.Muted };
            g.DefaultCellStyle = new DataGridViewCellStyle { Font = g.Font, BackColor = Color.White, ForeColor = Theme.Ink, SelectionBackColor = Theme.Pale, SelectionForeColor = Theme.Ink, Padding = new Padding(8, 8, 6, 8) };
            return g;
        }
        void BuildSettings(Control page)
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var paths = new TabPage("자산 경로") { BackColor = Color.White, Padding = new Padding(12) };
            var settings = new TabPage("서버 옵션") { BackColor = Color.White, Padding = new Padding(12) };
            tabs.TabPages.AddRange(new[] { paths, settings }); page.Controls.Add(tabs); BuildAssets(paths);
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
            var save = Action("설정 저장", delegate { TryAction(delegate { ReadConfig(); SaveConfig(); statusLabel.Text = "저장했습니다. 실행 중 서버에는 다음 시작 시 반영됩니다."; }); }, true); editable.Add(save); AddRow(t, Actions(save));
            var note = L("새 설정은 다음 서버 시작 시 반영합니다.\n자동 실행 옵션은 Windows 시작프로그램 등록이 아닙니다.", 9F, false); note.ForeColor = Theme.Muted; AddRow(t, note);
        }
        void BuildLogs(Control page)
        {
            var t = Table(1, 100); t.RowCount = 2; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.Controls.Add(t);
            var bar = AutoTable(1, 100); var note = L("오류와 llama-server 출력을 확인할 수 있습니다.", 9F, false); note.ForeColor = Theme.Muted; AddRow(bar, note);
            AddRow(bar, Actions(Action("PC 다시 감지", async delegate { await DetectHardwareAsync(); }, false), Action("로그 폴더 열기", delegate { OpenPath(state); }, false))); t.Controls.Add(bar, 0, 0);
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
                string id=SelectedModel()==null?"":SelectedModel().Id, term=searchBox.Text.Trim(); modelGrid.Rows.Clear();
                foreach(var m in inventory.Models.Where(m=>term.Length==0 || m.Name.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0 || m.Architecture.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0))
                {
                    bool running=server.Status.Ready && server.Status.ModelId==m.Id;
                    int r=modelGrid.Rows.Add(m.Name+"\n"+m.Architecture, (m.Bytes/1073741824.0).ToString("0.00")+" GiB",running?"준비 완료":server.Status.ModelId==m.Id && server.Status.State=="loading"?"로딩 중":"발견"); modelGrid.Rows[r].Tag=m;
                    modelGrid.Rows[r].Cells[2].Style.ForeColor=running?Theme.Accent:Theme.Muted;
                }
                modelGrid.ClearSelection();
                foreach(DataGridViewRow row in modelGrid.Rows) if(((ModelInfo)row.Tag).Id==id) { modelGrid.CurrentCell=row.Cells[0]; row.Selected=true; break; }
                modelCount.Text="로컬 모델  "+inventory.Models.Count;
            }
            finally {bindingModels=false;}
            RefreshSelection();
        }
        void RefreshSelection()
        {
            if(selectedName==null || modelBox==null) return; var m=SelectedModel();
            selectedName.Text=m==null?"사용할 모델이 없습니다":m.Name;
            selectedMeta.Text=m==null?"models 폴더에 GGUF 파일을 넣으세요.":m.Architecture+"  ·  "+(m.Bytes/1073741824.0).ToString("0.00")+" GiB";
            selectedPath.Text=m==null?"":m.RelativePath;
            if(m!=null) { tips.SetToolTip(selectedPath,m.FullPath); tips.SetToolTip(selectedName,m.Name); }
            bool same=m!=null && server.Status.Ready && server.Status.ModelId==m.Id;
            selectedState.Text=m==null?"복사 중인 파일은 확인 후 목록에 표시됩니다.":same?"이 모델이 실행 중입니다.":server.Alive?"선택만으로 실행 모델이 바뀌지는 않습니다.":"선택한 모델만 메모리에 불러옵니다.";
            startButton.Text=uiBusy?"준비 중...":server.Alive?(same?"서버 재시작":"이 모델로 전환") : "선택한 모델 시작";
            startButton.Enabled=!uiBusy && m!=null && inventory.Backends.Any(b=>!String.IsNullOrEmpty(b.Exe));
        }
        void UpdateDashboard(ServerStatus s)
        {
            if(stateBadge==null) return;
            stateBadge.Text=s.Ready?"준비 완료":s.State=="error"?"오류":s.State=="loading"?"로딩 중":s.State=="probing" || s.State=="testing"?"검사 중":s.Listening?"모델 대기":"대기";
            stateBadge.ForeColor=s.State=="error"?Theme.Danger:s.Ready?Theme.Accent:Theme.Muted;
            stateBadge.BackColor=s.State=="error"?Color.FromArgb(254,237,234):s.Ready?Theme.Pale:Color.FromArgb(233,238,245);
            runningNote.Text=s.Ready?"생성 확인 완료 · "+s.Backend:s.Listening?"서버 실행 중 · 모델 준비 대기":"서버 중지 · 연결 정보는 실행 후 사용하세요.";
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
                gpuValue.Text=result[2]; gpuNote.Text=result[3]; tips.SetToolTip(cpuValue,result[0]); tips.SetToolTip(gpuValue,result[2]);
                if(result[4].Length>0) log.Write("PC 환경 일부 확인 불가 (서버 실행과 별개): "+result[4]);
            } catch(Exception e) { if(!closing) log.Write("PC 감지 실패: "+e.Message); }
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
