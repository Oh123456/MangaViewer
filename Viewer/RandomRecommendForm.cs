namespace Viewer;

public sealed class RandomRecommendForm : Form
{
    private readonly NumericUpDown countBox = new();
    private readonly NumericUpDown minImageCountBox = new();
    private readonly NumericUpDown maxImageCountBox = new();
    private readonly CheckBox cycleRandomCheckBox = new();

    public int RecommendCount => (int)countBox.Value;
    public int MinImageCount => (int)minImageCountBox.Value;
    public int? MaxImageCount => maxImageCountBox.Value <= 0 ? null : (int)maxImageCountBox.Value;
    public bool CycleRandomEnabled => cycleRandomCheckBox.Checked;
    public bool CycleResetRequested { get; private set; }

    public RandomRecommendForm(int maxCount, int initialCount, int initialMinImageCount, int initialMaxImageCount, bool initialCycleRandomEnabled)
    {
        Text = "랜덤 추천";
        AppIcons.ApplyTo(this);
        Width = 360;
        Height = 264;
        MinimumSize = new Size(340, 250);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 6,
            ColumnCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var countLabel = new Label
        {
            Text = "추천 개수",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        countBox.Minimum = 1;
        countBox.Maximum = Math.Max(1, maxCount);
        countBox.Value = Math.Clamp(initialCount <= 0 ? 10 : initialCount, 1, Math.Max(1, maxCount));
        countBox.Dock = DockStyle.Fill;

        var minImageCountLabel = new Label
        {
            Text = "최소 이미지",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        minImageCountBox.Minimum = 0;
        minImageCountBox.Maximum = 1_000_000;
        minImageCountBox.Value = Math.Clamp(initialMinImageCount, 0, 1_000_000);
        minImageCountBox.Dock = DockStyle.Fill;

        var maxImageCountLabel = new Label
        {
            Text = "최대 이미지",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        maxImageCountBox.Minimum = 0;
        maxImageCountBox.Maximum = 1_000_000;
        maxImageCountBox.Value = Math.Clamp(initialMaxImageCount, 0, 1_000_000);
        maxImageCountBox.Dock = DockStyle.Fill;

        cycleRandomCheckBox.Text = "순회 랜덤";
        cycleRandomCheckBox.Checked = initialCycleRandomEnabled;
        cycleRandomCheckBox.Dock = DockStyle.Fill;
        cycleRandomCheckBox.TextAlign = ContentAlignment.MiddleLeft;

        var hintLabel = new Label
        {
            Text = string.Format(Localization.T("현재 목록 후보: {0}개 / 최대 0은 제한 없음"), maxCount),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };

        var okButton = new Button
        {
            Text = "확인",
            DialogResult = DialogResult.OK,
            Width = 80
        };
        var resetCycleButton = new Button
        {
            Text = "순회 초기화",
            Width = 100
        };
        var cancelButton = new Button
        {
            Text = "취소",
            DialogResult = DialogResult.Cancel,
            Width = 80
        };
        resetCycleButton.Click += (_, _) =>
        {
            CycleResetRequested = true;
            resetCycleButton.Text = Localization.T("초기화됨");
        };

        buttons.Controls.AddRange([okButton, resetCycleButton, cancelButton]);
        root.Controls.Add(countLabel, 0, 0);
        root.Controls.Add(countBox, 1, 0);
        root.Controls.Add(minImageCountLabel, 0, 1);
        root.Controls.Add(minImageCountBox, 1, 1);
        root.Controls.Add(maxImageCountLabel, 0, 2);
        root.Controls.Add(maxImageCountBox, 1, 2);
        root.Controls.Add(cycleRandomCheckBox, 1, 3);
        root.Controls.Add(hintLabel, 1, 4);
        root.Controls.Add(buttons, 0, 5);
        root.SetColumnSpan(buttons, 2);
        Controls.Add(root);
        Localization.ApplyTo(this);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
