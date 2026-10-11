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
        Label connectionSummary, apiStateLabel, apiPendingLabel;
        TextBox apiBaseField, apiModelField, apiKeyField;
        readonly List<Control> connectionActions = new List<Control>();

        void BuildApiPage(Control page)
        {
            var viewport = new ContentViewport(); var body = AutoTable(1, 100);
            var live = AutoTable(1, 100); AddRow(body, Card(live, new Padding(22)));
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
            var settings = AutoTable(1, 100); AddRow(body, Card(settings, new Padding(22)));
            apiKeyBox = new CheckBox { Text = "API 키 사용", AutoSize = true, Margin = new Padding(0, 0, 0, 12), AccessibleName = "로컬 API 키 인증 사용" };
            editable.Add(apiKeyBox); AddRow(settings, apiKeyBox);
            apiPendingLabel = L("", 9F, false); apiPendingLabel.ForeColor = Theme.Muted; AddRow(settings, apiPendingLabel);
            var save = Action("저장", delegate { TryAction(delegate { ReadConfig(); SaveConfig(); UpdateConnectionPanel(); statusLabel.Text = "저장됨 · 다음 시작 시 적용"; }); }, false); editable.Add(save);
            var apply = Action("저장 후 시작 / 재시작", async delegate { await Work(StartFromUi); }, false); editable.Add(apply);
            AddRow(settings, Actions(save, apply)); viewport.SetContent(body); page.Controls.Add(viewport);
            apiKeyBox.CheckedChanged += delegate { UpdateConnectionPanel(); };
            thinkingModeBox.SelectedIndexChanged += delegate { UpdateConnectionPanel(); };
        }
        void UpdateConnectionPanel()
        {
            if (apiStateLabel == null || apiKeyField == null) return;
            bool ready = !uiBusy && !server.Busy && server.Alive && server.Status.Ready;
            foreach (Control c in connectionActions) c.Enabled = ready;
            apiPendingLabel.Text = "다음 시작 시 적용 · API 키 " + (apiKeyBox.Checked ? "사용" : "없음") + " · 사고 모드 " + (thinkingModeBox.SelectedIndex == 1 ? "ON" : "OFF");
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
                AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi, ClientSize = new Size(650, 540), MinimumSize = new Size(380, 300), ShowInTaskbar = false })
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
