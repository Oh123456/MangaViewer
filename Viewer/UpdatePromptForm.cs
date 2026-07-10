namespace Viewer;

public sealed class UpdatePromptForm : Form
{
    private readonly Button primaryButton = new();
    private readonly Button cancelButton = new();

    public UpdatePromptForm(UpdateCheckResult update)
    {
        Text = Localization.T("업데이트 발견");
        AppIcons.ApplyTo(this);
        Width = 620;
        Height = 520;
        MinimumSize = new Size(520, 380);
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var summaryLabel = new Label
        {
            Text = CreateSummaryText(update),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };

        var notesBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Text = string.IsNullOrWhiteSpace(update.Body)
                ? Localization.T("릴리즈 노트가 없습니다.")
                : update.Body.Replace("\n", Environment.NewLine)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        primaryButton.Text = string.IsNullOrWhiteSpace(update.AssetDownloadUrl)
            ? Localization.T("릴리즈 페이지 열기")
            : Localization.T("다운로드");
        primaryButton.Width = 110;
        primaryButton.Height = 30;
        primaryButton.DialogResult = DialogResult.Yes;

        cancelButton.Text = Localization.T("취소");
        cancelButton.Width = 90;
        cancelButton.Height = 30;
        cancelButton.DialogResult = DialogResult.Cancel;

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(primaryButton);

        root.Controls.Add(summaryLabel, 0, 0);
        root.Controls.Add(notesBox, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        Controls.Add(root);

        AcceptButton = primaryButton;
        CancelButton = cancelButton;
    }

    private static string CreateSummaryText(UpdateCheckResult update)
    {
        var assetText = string.IsNullOrWhiteSpace(update.AssetName)
            ? Localization.T("다운로드 파일 없음")
            : update.AssetName;
        return string.Format(
            Localization.T("새 버전이 있습니다.\n\n현재 버전: {0}\n최신 버전: {1}\n파일: {2}"),
            update.CurrentVersion,
            update.LatestVersion,
            assetText);
    }
}
