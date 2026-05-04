namespace Viewer;

public sealed class SeriesAssignForm : Form
{
    private readonly TextBox seriesNameBox = new();
    private readonly DataGridView folderGrid = new();
    private readonly Button removeButton = new();
    private readonly Button renumberButton = new();

    public string SeriesName => seriesNameBox.Text.Trim();

    public void SetOrder(long folderId, int order)
    {
        foreach (DataGridViewRow row in folderGrid.Rows)
        {
            if (row.Tag is FolderItem folder && folder.Id == folderId)
            {
                row.Cells[0].Value = Math.Max(1, order);
                return;
            }
        }
    }

    public List<SeriesAssignment> Assignments
    {
        get
        {
            var assignments = new List<SeriesAssignment>();
            foreach (DataGridViewRow row in folderGrid.Rows)
            {
                if (row.IsNewRow || row.Tag is not FolderItem folder)
                {
                    continue;
                }

                var order = 0;
                if (row.Cells[0].Value is not null)
                {
                    int.TryParse(row.Cells[0].Value.ToString(), out order);
                }

                assignments.Add(new SeriesAssignment(folder.Id, Math.Max(1, order)));
            }

            return assignments
                .OrderBy(assignment => assignment.SeriesOrder)
                .ToList();
        }
    }

    public SeriesAssignForm(IReadOnlyList<FolderItem> folders, string title = "묶음으로 만들기", string? initialSeriesName = null)
    {
        Text = title;
        AppIcons.ApplyTo(this);
        Width = 720;
        Height = 520;
        MinimumSize = new Size(620, 420);
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LoadFolders(folders, initialSeriesName);
        Localization.ApplyTo(this);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 4,
            ColumnCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            Text = "묶음 이름",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        seriesNameBox.Dock = DockStyle.Fill;
        root.Controls.Add(seriesNameBox, 1, 0);

        folderGrid.Dock = DockStyle.Fill;
        folderGrid.AllowUserToAddRows = false;
        folderGrid.AllowUserToDeleteRows = false;
        folderGrid.MultiSelect = true;
        folderGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        folderGrid.RowHeadersVisible = false;
        folderGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        folderGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "편수",
            FillWeight = 18,
            MinimumWidth = 60
        });
        folderGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "이름",
            ReadOnly = true,
            FillWeight = 46
        });
        folderGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "경로",
            ReadOnly = true,
            FillWeight = 80
        });
        folderGrid.CellValidating += (_, eventArgs) =>
        {
            if (eventArgs.ColumnIndex != 0)
            {
                return;
            }

            if (!int.TryParse(eventArgs.FormattedValue?.ToString(), out var order) || order <= 0)
            {
                eventArgs.Cancel = true;
                MessageBox.Show(this, Localization.T("편수는 1 이상의 숫자로 입력하세요."), Localization.T("묶음"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        root.Controls.Add(folderGrid, 0, 1);
        root.SetColumnSpan(folderGrid, 2);

        var editButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        removeButton.Text = "선택 제거";
        removeButton.Width = 100;
        removeButton.Click += (_, _) => RemoveSelectedRows();
        renumberButton.Text = "번호 다시 매기기";
        renumberButton.Width = 120;
        renumberButton.Click += (_, _) => RenumberRows();
        editButtons.Controls.AddRange([removeButton, renumberButton]);
        root.Controls.Add(editButtons, 0, 2);
        root.SetColumnSpan(editButtons, 2);

        var bottomButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        var okButton = new Button
        {
            Text = "저장",
            DialogResult = DialogResult.OK,
            Width = 86
        };
        var cancelButton = new Button
        {
            Text = "취소",
            DialogResult = DialogResult.Cancel,
            Width = 86
        };
        bottomButtons.Controls.AddRange([okButton, cancelButton]);
        root.Controls.Add(bottomButtons, 0, 3);
        root.SetColumnSpan(bottomButtons, 2);

        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void LoadFolders(IReadOnlyList<FolderItem> folders, string? initialSeriesName)
    {
        folderGrid.Rows.Clear();
        var orderedFolders = folders
            .OrderBy(folder => folder.SeriesOrder ?? int.MaxValue)
            .ThenBy(folder => folder.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < orderedFolders.Count; index++)
        {
            var folder = orderedFolders[index];
            var rowIndex = folderGrid.Rows.Add(folder.SeriesOrder ?? index + 1, folder.DisplayName, folder.Path);
            folderGrid.Rows[rowIndex].Tag = folder;
        }

        var firstSeriesName = initialSeriesName ?? orderedFolders
            .Select(folder => folder.SeriesName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        seriesNameBox.Text = firstSeriesName ?? "";
    }

    private void RemoveSelectedRows()
    {
        var selectedRows = folderGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .OrderByDescending(row => row.Index)
            .ToList();
        foreach (var row in selectedRows)
        {
            folderGrid.Rows.Remove(row);
        }

        RenumberRows();
    }

    private void RenumberRows()
    {
        for (var rowIndex = 0; rowIndex < folderGrid.Rows.Count; rowIndex++)
        {
            folderGrid.Rows[rowIndex].Cells[0].Value = rowIndex + 1;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        if (DialogResult == DialogResult.OK)
        {
            folderGrid.EndEdit();
            if (string.IsNullOrWhiteSpace(SeriesName))
            {
                MessageBox.Show(this, Localization.T("묶음 이름을 입력하세요."), Localization.T("묶음"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                eventArgs.Cancel = true;
                return;
            }

            if (Assignments.Count == 0)
            {
                MessageBox.Show(this, Localization.T("묶음에 포함할 폴더가 없습니다."), Localization.T("묶음"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                eventArgs.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(eventArgs);
    }
}

public sealed record SeriesAssignment(long FolderId, int SeriesOrder);
