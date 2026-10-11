using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace SILocalAI
{
    public sealed partial class MainForm
    {
        TabPage apiPage;
        Label connectionSummary, apiStateLabel, apiPendingLabel;
        TextBox apiBaseField, apiModelField, apiKeyField;
        readonly List<Control> connectionActions = new List<Control>();

        void BuildApiPage(Control page)
        {
            var viewport = new ContentViewport(); page.Controls.Add(viewport);
            var body = AutoTable(1, 100); viewport.SetContent(body);
            var title = L("로컬 API 연결", 17F, true); title.Margin = new Padding(0, 0, 0, 8); AddRow(body, title);
            var intro = L("모델 시작 → 연결 정보 복사 → OpenCode 또는 OpenAI 호환 클라이언트에 입력", 10F, false);
            intro.ForeColor = Theme.Muted; intro.Margin = new Padding(0, 0, 0, 14); AddRow(body, intro);
            var columns = new ResponsiveColumns(840, 50, 50); AddRow(body, columns);
            var settings = AutoTable(1, 100); columns.Controls.Add(Card(settings, new Padding(18)));
            AddRow(settings, L("1. 연결 방식 설정", 12F, true));
            apiKeyBox = new CheckBox { Text = "로컬 API 키 사용", AutoSize = true, Margin = new Padding(0, 14, 0, 8), AccessibleName = "로컬 API 키 인증 사용" };
            editable.Add(apiKeyBox); AddRow(settings, apiKeyBox);
            var keyHelp = L("적용 시 로컬 키를 생성합니다. 기존 키는 유지됩니다.", 9F, false);
            keyHelp.ForeColor = Theme.Muted; AddRow(settings, keyHelp);
            apiPendingLabel = L("", 9F, true); apiPendingLabel.Margin = new Padding(0, 12, 0, 8); apiPendingLabel.ForeColor = Theme.Accent; AddRow(settings, apiPendingLabel);
            var modeHelp = L("사고 모드는 모델 라이브러리에서 선택합니다. 변경 후 서버를 재시작하세요.", 9F, false);
            modeHelp.ForeColor = Theme.Muted; AddRow(settings, modeHelp);
            var apply = Action("저장하고 서버에 적용", async delegate { await Work(StartFromUi); }, true); editable.Add(apply);
            var save = Action("설정만 저장", delegate { TryAction(delegate { ReadConfig(); SaveConfig(); UpdateConnectionPanel(); statusLabel.Text = "설정 저장 완료 · 서버를 시작 / 재시작하면 적용됩니다."; }); }, false); editable.Add(save);
            AddRow(settings, Actions(apply, save, Action("모델 / 사고 모드 선택", delegate { pages.SelectedIndex = 0; }, false)));
            var storage = L("키 저장 위치: state/local-api-key.txt", 8.5F, false);
            storage.ForeColor = Theme.Muted; AddRow(settings, storage);

            var live = AutoTable(1, 100); columns.Controls.Add(Card(live, new Padding(18)));
            AddRow(live, L("2. 실행 중인 서버 연결 정보", 12F, true));
            apiStateLabel = L("모델을 먼저 시작하세요.", 9F, false); apiStateLabel.Margin = new Padding(0, 12, 0, 10); apiStateLabel.ForeColor = Theme.Accent; AddRow(live, apiStateLabel);
            apiBaseField = ReadonlyText(); apiModelField = ReadonlyText(); apiKeyField = ReadonlyText(); apiKeyField.UseSystemPasswordChar = true;
            AddRow(live, ConnectionRow("기본 주소", apiBaseField)); AddRow(live, ConnectionRow("모델 ID", apiModelField)); AddRow(live, ConnectionRow("API 키", apiKeyField));
            var copyKey = Action("API 키 복사", delegate { TryAction(delegate { var c = server.Connection(); Clipboard.SetText(c.ApiKeyRequired ? c.ApiKey : "si-local-no-key"); statusLabel.Text = "현재 서버의 API 키 복사 완료"; }); }, false);
            connectionActions.Add(copyKey); AddRow(live, Actions(copyKey));
            var hint = L("주소는 /v1까지 입력합니다. 인증 없이 키 입력이 필요하면 si-local-no-key를 사용하세요.", 9F, false);
            hint.ForeColor = Theme.Muted; AddRow(live, hint);
            var browserNote = L("llama-ui에도 같은 API 키를 사용합니다.", 8.5F, false); browserNote.ForeColor = Theme.Muted; browserNote.Margin = new Padding(0, 10, 0, 0); AddRow(live, browserNote);

            var open = AutoTable(1, 100); AddRow(body, Card(open, new Padding(18)));
            AddRow(open, L("3. OpenCode에 연결", 12F, true));
            var steps = L("① 아래에서 설정 파일을 내보냅니다.\n② 새 프로젝트는 opencode.json으로 사용합니다. 기존 설정이 있으면 provider.si-local을 병합하세요.\n③ 키를 켰다면 OpenCode의 /connect → Other → si-local에 복사한 키를 입력합니다.\n④ OpenCode를 다시 열고 /models에서 Local AI를 선택합니다.", 9.5F, false);
            steps.Margin = new Padding(0, 12, 0, 8); AddRow(open, steps);
            var export = Action("OpenCode 설정 내보내기", delegate { TryAction(ExportOpenCode); }, true);
            var copy = Action("설정 JSON 복사", delegate { TryAction(delegate { Clipboard.SetText(ClientConfig.OpenCode(server.Connection())); statusLabel.Text = "OpenCode 설정 복사 완료 · 실제 키는 포함하지 않습니다."; }); }, false);
            var guide = Action("자세한 연결 안내", delegate { TryAction(ShowConnectionGuide); }, false);
            connectionActions.Add(export); connectionActions.Add(copy); connectionActions.Add(guide); AddRow(open, Actions(export, copy, guide));
            var contextHint = L("문맥 부족 오류가 나면 16K를 적용하세요. 메모리 사용량이 늘어납니다.", 9F, false);
            contextHint.ForeColor = Theme.Muted; AddRow(open, contextHint);
            var contextPreset = Action("문맥을 16K로 설정", delegate { contextBox.Value = 16384; statusLabel.Text = "문맥 16K 선택 · ‘저장하고 서버에 적용’을 누르면 반영됩니다."; }, false);
            editable.Add(contextPreset); AddRow(open, Actions(contextPreset));
            var limits = L("기존 MCP 설정은 유지하세요. 도구 호출 품질은 모델과 문맥 크기에 따라 달라집니다.", 9F, false);
            limits.ForeColor = Theme.Muted; AddRow(open, limits);
            apiKeyBox.CheckedChanged += delegate { UpdateConnectionPanel(); };
            thinkingModeBox.SelectedIndexChanged += delegate { UpdateConnectionPanel(); };
        }
        void UpdateConnectionPanel()
        {
            if (apiStateLabel == null || apiKeyField == null) return;
            bool ready = !uiBusy && !server.Busy && server.Alive && server.Status.Ready;
            foreach (Control c in connectionActions) c.Enabled = ready;
            apiPendingLabel.Text = "다음 적용: API 키 " + (apiKeyBox.Checked ? "사용" : "없음") + " · 사고 모드 " + (thinkingModeBox.SelectedIndex == 1 ? "ON" : "OFF");
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
                apiStateLabel.Text = uiBusy || server.Busy ? "서버 준비 중 · 완료 후 복사할 수 있습니다." : "연결 정보는 모델을 시작한 뒤 표시됩니다.";
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
