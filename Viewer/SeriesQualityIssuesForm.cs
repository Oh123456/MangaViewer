namespace Viewer;

public sealed class SeriesQualityIssuesForm : Form
{
    private readonly ListView issueList = new();

    public SeriesQualityIssuesForm(IReadOnlyList<SeriesQualityIssue> issues)
    {
        Text = "묶음 품질 검사";
        AppIcons.ApplyTo(this);
        Width = 900;
        Height = 560;
        MinimumSize = new Size(720, 420);
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LoadIssues(issues);
        Localization.ApplyTo(this);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 2,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        issueList.Dock = DockStyle.Fill;
        issueList.View = View.Details;
        issueList.FullRowSelect = true;
        issueList.HideSelection = false;
        issueList.Columns.Add("묶음", 180);
        issueList.Columns.Add("문제", 110);
        issueList.Columns.Add("내용", 260);
        issueList.Columns.Add("폴더", 320);
        root.Controls.Add(issueList, 0, 0);

        var closeButton = new Button
        {
            Text = "닫기",
            DialogResult = DialogResult.OK,
            Width = 86,
            Height = 30
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(closeButton);
        root.Controls.Add(buttons, 0, 1);

        Controls.Add(root);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    private void LoadIssues(IReadOnlyList<SeriesQualityIssue> issues)
    {
        issueList.Items.Clear();
        foreach (var issue in issues)
        {
            var item = new ListViewItem(issue.SeriesName);
            item.SubItems.Add(issue.IssueType);
            item.SubItems.Add(issue.Detail);
            item.SubItems.Add(issue.FolderNames);
            issueList.Items.Add(item);
        }
    }
}
