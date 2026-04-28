namespace Viewer;

public sealed class ScanProgressForm : Form
{
    private readonly Label statusLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly Button cancelButton = new();
    private readonly Action cancelScan;

    public ScanProgressForm(Action cancelScan)
    {
        this.cancelScan = cancelScan;

        Text = "스캔 중";
        Width = 460;
        Height = 175;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildUi();
    }

    public void UpdateStatus(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        statusLabel.Text = text;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Text = "스캔 준비 중...";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        progressBar.Dock = DockStyle.Fill;
        progressBar.Style = ProgressBarStyle.Marquee;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 12)
        };

        cancelButton.Text = "취소";
        cancelButton.Width = 90;
        cancelButton.Height = 30;
        cancelButton.Click += (_, _) =>
        {
            cancelButton.Enabled = false;
            statusLabel.Text = "취소 요청 중...";
            cancelScan();
        };
        buttonPanel.Controls.Add(cancelButton);

        root.Controls.Add(statusLabel, 0, 0);
        root.Controls.Add(progressBar, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        Controls.Add(root);
    }
}
