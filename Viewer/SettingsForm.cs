namespace Viewer;

public sealed class SettingsForm : Form
{
    private readonly AppDatabase database;
    private readonly Action refreshMainWindow;
    private readonly ListBox rootListBox = new();
    private readonly ListBox tagListBox = new();
    private readonly Button addRootButton = new();
    private readonly Button deleteRootButton = new();
    private readonly Button openDatabaseFolderButton = new();
    private readonly Button openLogFolderButton = new();
    private readonly Button renameTagButton = new();
    private readonly Button deleteTagButton = new();

    public SettingsForm(AppDatabase database, Action refreshMainWindow)
    {
        this.database = database;
        this.refreshMainWindow = refreshMainWindow;

        Text = "설정";
        Width = 520;
        Height = 520;
        MinimumSize = new Size(460, 420);
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LoadRoots();
        LoadTags();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 6,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var topButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };

        addRootButton.Text = "루트 추가";
        addRootButton.Width = 110;
        addRootButton.Height = 30;
        addRootButton.Click += (_, _) => AddRoot();

        deleteRootButton.Text = "루트 삭제";
        deleteRootButton.Width = 110;
        deleteRootButton.Height = 30;
        deleteRootButton.Click += (_, _) => DeleteSelectedRoots();

        openDatabaseFolderButton.Text = "DB 위치";
        openDatabaseFolderButton.Width = 110;
        openDatabaseFolderButton.Height = 30;
        openDatabaseFolderButton.Click += (_, _) => OpenDatabaseFolder();

        openLogFolderButton.Text = "로그 위치";
        openLogFolderButton.Width = 110;
        openLogFolderButton.Height = 30;
        openLogFolderButton.Click += (_, _) => OpenLogFolder();

        topButtons.Controls.AddRange([addRootButton, deleteRootButton, openDatabaseFolderButton, openLogFolderButton]);

        var rootLabel = new Label
        {
            Text = "루트 경로",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        rootListBox.Dock = DockStyle.Fill;
        rootListBox.IntegralHeight = false;
        rootListBox.SelectionMode = SelectionMode.MultiExtended;
        rootListBox.HorizontalScrollbar = true;

        var tagLabel = new Label
        {
            Text = "태그 관리",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        tagListBox.Dock = DockStyle.Fill;
        tagListBox.IntegralHeight = false;
        tagListBox.SelectionMode = SelectionMode.MultiExtended;
        rootListBox.SelectedIndexChanged += (_, _) =>
        {
            if (rootListBox.Focused)
            {
                tagListBox.ClearSelected();
            }
        };
        tagListBox.SelectedIndexChanged += (_, _) =>
        {
            if (tagListBox.Focused)
            {
                rootListBox.ClearSelected();
            }
        };

        var bottomButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        deleteTagButton.Text = "삭제";
        deleteTagButton.Width = 90;
        deleteTagButton.Height = 30;
        deleteTagButton.Click += (_, _) => DeleteTag();

        renameTagButton.Text = "이름 변경";
        renameTagButton.Width = 100;
        renameTagButton.Height = 30;
        renameTagButton.Click += (_, _) => RenameTag();

        bottomButtons.Controls.AddRange([deleteTagButton, renameTagButton]);

        root.Controls.Add(topButtons, 0, 0);
        root.Controls.Add(rootLabel, 0, 1);
        root.Controls.Add(rootListBox, 0, 2);
        root.Controls.Add(tagLabel, 0, 3);
        root.Controls.Add(tagListBox, 0, 4);
        root.Controls.Add(bottomButtons, 0, 5);
        root.MouseDown += (_, _) => ClearListSelections();
        topButtons.MouseDown += (_, _) => ClearListSelections();
        rootLabel.MouseDown += (_, _) => ClearListSelections();
        tagLabel.MouseDown += (_, _) => ClearListSelections();
        bottomButtons.MouseDown += (_, _) => ClearListSelections();
        Controls.Add(root);
    }

    private void LoadRoots()
    {
        rootListBox.Items.Clear();
        foreach (var rootPath in database.GetRoots())
        {
            rootListBox.Items.Add(rootPath);
        }
    }

    private void LoadTags()
    {
        tagListBox.Items.Clear();
        foreach (var tag in database.GetTags())
        {
            tagListBox.Items.Add(tag);
        }
    }

    private void AddRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "이미지 라이브러리 루트 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        database.AddRoot(dialog.SelectedPath);
        LoadRoots();
        refreshMainWindow();
    }

    private void DeleteSelectedRoots()
    {
        var selectedRoots = rootListBox.SelectedItems.Cast<string>().ToList();
        if (selectedRoots.Count == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"선택한 루트 {selectedRoots.Count}개를 등록 목록에서 제거합니다. 실제 폴더와 이미지는 삭제하지 않습니다.",
            "루트 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        database.DeleteRoots(selectedRoots);
        LoadRoots();
        refreshMainWindow();
    }

    private void RenameTag()
    {
        if (tagListBox.SelectedItems.Count != 1 || tagListBox.SelectedItem is not string oldName)
        {
            MessageBox.Show(this, "이름을 변경할 태그를 하나만 선택하세요.", "태그 이름 변경", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newName = PromptText("태그 이름 변경", "새 태그 이름", oldName);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        database.RenameTag(oldName, newName);
        LoadTags();
        refreshMainWindow();
    }

    private void DeleteTag()
    {
        var selectedTags = tagListBox.SelectedItems.Cast<string>().ToList();
        if (selectedTags.Count == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"선택한 태그 {selectedTags.Count}개를 삭제합니다. 폴더와 이미지는 삭제하지 않습니다.",
            "태그 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        database.DeleteTags(selectedTags);
        LoadTags();
        refreshMainWindow();
    }

    private void ClearListSelections()
    {
        rootListBox.ClearSelected();
        tagListBox.ClearSelected();
        ActiveControl = null;
    }

    private void OpenDatabaseFolder()
    {
        var databasePath = database.DatabasePath;
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            return;
        }

        var arguments = File.Exists(databasePath)
            ? $"/select,\"{databasePath}\""
            : $"\"{databaseDirectory}\"";

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true
        });
    }

    private void OpenLogFolder()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{logDirectory}\"",
            UseShellExecute = true
        });
    }

    private static string? PromptText(string title, string label, string initialValue)
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 380,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var labelControl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = initialValue
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
        table.Controls.Add(labelControl, 0, 0);
        table.Controls.Add(textBox, 0, 1);
        table.Controls.Add(buttons, 0, 2);
        dialog.Controls.Add(table);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        return dialog.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
    }
}
