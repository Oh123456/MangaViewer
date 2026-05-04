namespace Viewer;

public sealed class ScanProgressForm : Form
{
    private readonly Label statusLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly Button cancelButton = new();
    private readonly Action cancelScan;
    private readonly System.Diagnostics.Stopwatch elapsedStopwatch = new();
    private readonly System.Windows.Forms.Timer elapsedTimer = new();
    private string currentStatus = Localization.T("스캔 준비 중...");

    public ScanProgressForm(Action cancelScan)
    {
        this.cancelScan = cancelScan;

        Text = "스캔 중";
        AppIcons.ApplyTo(this);
        Width = 460;
        Height = 175;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildUi();
        Localization.ApplyTo(this);
        elapsedTimer.Interval = 1000;
        elapsedTimer.Tick += (_, _) => RefreshStatusText();
        elapsedStopwatch.Start();
        elapsedTimer.Start();
        FormClosed += (_, _) =>
        {
            elapsedTimer.Stop();
            elapsedTimer.Dispose();
        };
    }

    public void UpdateStatus(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        currentStatus = Localization.T(text);
        RefreshStatusText();
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
        statusLabel.Text = currentStatus;
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
            UpdateStatus(Localization.T("취소 요청 중..."));
            cancelScan();
        };
        buttonPanel.Controls.Add(cancelButton);

        root.Controls.Add(statusLabel, 0, 0);
        root.Controls.Add(progressBar, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        Controls.Add(root);
    }

    private void RefreshStatusText()
    {
        var elapsedText = elapsedStopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        statusLabel.Text = $"{currentStatus} / {Localization.T("경과")} {elapsedText}";
    }

    protected override void OnShown(EventArgs eventArgs)
    {
        base.OnShown(eventArgs);
        Text = Localization.T(Text);
    }
}
