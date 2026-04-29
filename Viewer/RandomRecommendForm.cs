namespace Viewer;

public sealed class RandomRecommendForm : Form
{
    private readonly NumericUpDown countBox = new();

    public int RecommendCount => (int)countBox.Value;

    public RandomRecommendForm(int maxCount)
    {
        Text = "랜덤 추천";
        Width = 320;
        Height = 160;
        MinimumSize = new Size(300, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var countLabel = new Label
        {
            Text = "추천 개수",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        countBox.Minimum = 1;
        countBox.Maximum = Math.Max(1, maxCount);
        countBox.Value = Math.Min(10, Math.Max(1, maxCount));
        countBox.Dock = DockStyle.Fill;

        var hintLabel = new Label
        {
            Text = $"현재 목록 후보: {maxCount}개",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        var okButton = new Button
        {
            Text = "확인",
            DialogResult = DialogResult.OK,
            Width = 80
        };
        var cancelButton = new Button
        {
            Text = "취소",
            DialogResult = DialogResult.Cancel,
            Width = 80
        };

        buttons.Controls.AddRange([okButton, cancelButton]);
        root.Controls.Add(countLabel, 0, 0);
        root.Controls.Add(countBox, 1, 0);
        root.Controls.Add(hintLabel, 1, 1);
        root.Controls.Add(buttons, 0, 2);
        root.SetColumnSpan(buttons, 2);
        Controls.Add(root);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
