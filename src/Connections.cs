using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace SILocalAI
{
    public sealed partial class MainForm
    {
        Control apiPage;
        Label connectionSummary, apiStateLabel, apiPendingLabel, apiContextSummary;
        ComboBox apiContextBox;
        bool syncingContext;
        TextBox apiBaseField, apiModelField, apiKeyField;
        readonly List<Control> connectionActions = new List<Control>();

        void BuildApiPage(Control page)
        {
            var surface = new Surface { Dock = DockStyle.Fill, Padding = new Padding(22) }; page.Controls.Add(surface);
            var viewport = new ContentViewport(); var body = AutoTable(1, 100);
            var live = body;
            AddRow(live, L("로컬 API 연결 정보", 17F, true));
            apiStateLabel = L("모델을 시작하세요", 10F, false); apiStateLabel.ForeColor = Theme.Muted; apiStateLabel.Margin = new Padding(0, 12, 0, 18); AddRow(live, apiStateLabel);
            apiBaseField = ReadonlyText(); apiModelField = ReadonlyText(); apiKeyField = ReadonlyText(); apiKeyField.UseSystemPasswordChar = true;
            AddRow(live, ConnectionRow("주소", apiBaseField)); AddRow(live, ConnectionRow("모델 ID", apiModelField)); AddRow(live, ConnectionRow("API 키", apiKeyField));
            testButton = Action("연결 테스트", async delegate { await Work(async delegate { string text = await server.TestAsync(); if (!closing) MessageBox.Show(this, text, "연결 테스트"); }); }, false);
            var export = Action("OpenCode 설정 내보내기", delegate { TryAction(ExportOpenCode); }, true);
            var copy = Action("설정 복사", delegate { TryAction(delegate { Clipboard.SetText(ClientConfig.OpenCode(server.Connection())); statusLabel.Text = "설정 복사됨"; }); }, false);
            var guide = Action("연결 방법", delegate { TryAction(ShowConnectionGuide); }, false);
            connectionActions.Add(export); connectionActions.Add(copy); connectionActions.Add(guide);
            AddRow(live, Actions(export, copy, guide, testButton));
            AddRow(body, L("문맥 크기", 10F, true));
            apiContextBox = Combo(new string[0]); apiContextBox.AccessibleName = "API 문맥 크기";
            apiContextBox.Items.AddRange(new object[] { 4096, 8192, 16384, 32768 });
            apiContextBox.FormattingEnabled = true;
            apiContextBox.Format += delegate(object sender, ListControlConvertEventArgs e) { e.Value = ContextText(Convert.ToInt32(e.ListItem)); };
            apiContextBox.SelectedIndexChanged += delegate {
                if (!syncingContext && apiContextBox.SelectedItem != null) contextBox.Value = (int)apiContextBox.SelectedItem;
                UpdateContextPanel();
            };
            contextBox.ValueChanged += delegate { UpdateContextPanel(); };
            AddRow(body, apiContextBox);
            apiContextSummary = L("", 9.5F, false); apiContextSummary.ForeColor = Theme.Muted; AddRow(body, apiContextSummary);
            var contextHint = L("OpenCode·MCP는 16K부터 확인하세요. 문맥을 늘리면 메모리 사용량도 늘어납니다.", 9F, false);
            contextHint.ForeColor = Theme.Muted; contextHint.Margin = new Padding(0, 7, 0, 0); AddRow(body, contextHint);
            var settings = body;
            apiKeyBox = new CheckBox { Text = "API 키 사용", AutoSize = true, Margin = new Padding(0, 20, 0, 12), AccessibleName = "로컬 API 키 인증 사용" };
            editable.Add(apiKeyBox); AddRow(settings, apiKeyBox);
            apiPendingLabel = L("", 9F, false); apiPendingLabel.ForeColor = Theme.Muted; AddRow(settings, apiPendingLabel);
            var save = Action("저장", delegate { TryAction(delegate { ReadConfig(); SaveConfig(); UpdateConnectionPanel(); statusLabel.Text = "저장됨 · 다음 시작 시 적용"; }); }, false); editable.Add(save);
            var apply = Action("저장 후 시작 / 재시작", async delegate { await Work(StartFromUi); }, false); editable.Add(apply);
            AddRow(settings, Actions(save, apply)); viewport.SetContent(body); surface.Controls.Add(viewport);
            apiKeyBox.CheckedChanged += delegate { UpdateConnectionPanel(); };
            thinkingModeBox.SelectedIndexChanged += delegate { UpdateConnectionPanel(); };
        }
        static string ContextText(int value) { return value % 1024 == 0 ? (value / 1024) + "K · " + value.ToString("N0") + " 토큰" : value.ToString("N0") + " 토큰"; }
        void UpdateContextPanel()
        {
            if (apiContextBox == null || apiContextSummary == null || syncingContext) return;
            int selected = (int)contextBox.Value;
            syncingContext = true;
            try
            {
                if (!apiContextBox.Items.Contains(selected)) apiContextBox.Items.Add(selected);
                apiContextBox.SelectedItem = selected;
            }
            finally { syncingContext = false; }
            if (server.Status.Ready)
                apiContextSummary.Text = "실행 중 " + ContextText(server.Status.Context) + (server.Status.Context != selected ? " · 변경하려면 재시작" : "");
            else apiContextSummary.Text = "다음 시작: " + ContextText(selected);
        }
        void UpdateConnectionPanel()
        {
            if (apiStateLabel == null || apiKeyField == null) return;
            UpdateContextPanel();
            bool ready = !uiBusy && !server.Busy && server.Alive && server.Status.Ready;
            foreach (Control c in connectionActions) c.Enabled = ready;
            apiPendingLabel.Text = "문맥 변경 후 재시작하고 OpenCode 설정을 다시 내보내세요.";
            if (ready)
            {
                var c = server.Connection();
                apiBaseField.Text = c.BaseUrl; apiModelField.Text = c.ModelId;
                apiKeyField.Text = c.ApiKeyRequired ? c.ApiKey : "si-local-no-key";
                apiStateLabel.Text = "준비 완료 · API 키 " + (c.ApiKeyRequired ? "인증 확인됨" : "없음") + " · " + c.Thinking;
                connectionSummary.Text = apiStateLabel.Text;
                tips.SetToolTip(apiModelField, c.ModelId);
            }
            else
            {
                apiBaseField.Clear(); apiModelField.Clear(); apiKeyField.Clear();
                apiStateLabel.Text = uiBusy || server.Busy ? "모델 준비 중" : "모델을 시작하세요";
                connectionSummary.Text = apiStateLabel.Text;
            }
        }
        void ExportOpenCode()
        {
            var connection = server.Connection();
            using (var dialog = new SaveFileDialog { Title = "OpenCode 연결 설정 내보내기", FileName = "opencode.si-local.json", Filter = "JSON 설정 (*.json)|*.json", AddExtension = true, OverwritePrompt = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                // No automatic edit of company/global/project configs, nor silent companion overwrite.
                Paths.AtomicWrite(dialog.FileName, ClientConfig.OpenCode(connection));
                statusLabel.Text = connection.ApiKeyRequired ? "설정 내보내기 완료 · API 키는 /connect에서 입력하세요." : "설정 내보내기 완료 · 키 인증 없이 연결할 수 있습니다.";
            }
        }
        void ShowConnectionGuide()
        {
            string text = ClientConfig.Guide(server.Connection());
            using (var dialog = new Form { Text = "API / OpenCode 연결 안내", Font = Font, BackColor = paper, StartPosition = FormStartPosition.CenterParent,
                AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi, ClientSize = new Size(650, 540), MinimumSize = new Size(380, 300), ShowInTaskbar = false, ShowIcon = false })
            {
                var viewport = new ContentViewport { Padding = new Padding(22) }; dialog.Controls.Add(viewport);
                var body = AutoTable(1, 100); viewport.SetContent(body); AddRow(body, L(text, 10F, false));
                var close = Action("닫기", delegate { dialog.Close(); }, true); dialog.CancelButton = close;
                AddRow(body, Actions(Action("안내 복사", delegate { Clipboard.SetText(text); }, false), close));
                dialog.Shown += delegate { var area = Screen.FromControl(dialog).WorkingArea; dialog.MinimumSize = new Size(Math.Min(dialog.MinimumSize.Width, area.Width), Math.Min(dialog.MinimumSize.Height, area.Height)); dialog.Size = new Size(Math.Min(dialog.Width, area.Width), Math.Min(dialog.Height, area.Height)); };
                dialog.ShowDialog(this);
            }
        }
    }
}
