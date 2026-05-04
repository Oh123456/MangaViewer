namespace Viewer;

public sealed class DuplicateNameGroupsForm : Form
{
    private readonly List<DuplicateNameGroup> groups;
    private readonly Action refreshMainWindow;
    private readonly ListView folderList = new();
    private readonly Button openFolderButton = new();
    private readonly Button deleteFolderButton = new();
    private readonly Button closeButton = new();

    public DuplicateNameGroupsForm(List<DuplicateNameGroup> groups, Action refreshMainWindow)
    {
        this.groups = groups;
        this.refreshMainWindow = refreshMainWindow;

        Text = "이름 중복 폴더";
        AppIcons.ApplyTo(this);
        Width = 1100;
        Height = 640;
        MinimumSize = new Size(860, 480);
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LoadGroups();
        Localization.ApplyTo(this);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(new Label
        {
            Text = "DB에 같은 이름으로 등록된 폴더입니다. 실제 파일 삭제는 휴지통으로 이동합니다.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        folderList.Dock = DockStyle.Fill;
        folderList.View = View.Details;
        folderList.FullRowSelect = true;
        folderList.HideSelection = false;
        folderList.MultiSelect = true;
        folderList.Columns.Add("이름", 260);
        folderList.Columns.Add("이미지", 70);
        folderList.Columns.Add("수정일", 130);
        folderList.Columns.Add("상태", 80);
        folderList.Columns.Add("경로", 560);
        folderList.DoubleClick += (_, _) => OpenSelectedFolder();
        folderList.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode != Keys.Enter)
            {
                return;
            }

            OpenSelectedFolder();
            keyEventArgs.Handled = true;
            keyEventArgs.SuppressKeyPress = true;
        };
        root.Controls.Add(folderList, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        closeButton.Text = "닫기";
        closeButton.Width = 90;
        closeButton.DialogResult = DialogResult.OK;
        openFolderButton.Text = "폴더 열기";
        openFolderButton.Width = 100;
        openFolderButton.DialogResult = DialogResult.None;
        openFolderButton.Click += (_, _) => OpenSelectedFolder();
        deleteFolderButton.Text = "폴더 삭제";
        deleteFolderButton.Width = 100;
        deleteFolderButton.DialogResult = DialogResult.None;
        deleteFolderButton.Click += (_, _) => DeleteSelectedFolders();
        buttons.Controls.AddRange([closeButton, deleteFolderButton, openFolderButton]);
        root.Controls.Add(buttons, 0, 2);

        Controls.Add(root);
        AcceptButton = openFolderButton;
        CancelButton = closeButton;
    }

    private void LoadGroups()
    {
        folderList.BeginUpdate();
        folderList.Items.Clear();
        foreach (var group in groups)
        {
            foreach (var folder in group.Folders)
            {
                var item = new ListViewItem(folder.DisplayName);
                item.SubItems.Add(folder.ImageCount.ToString());
                item.SubItems.Add(folder.FolderModifiedAt?.ToString("yyyy-MM-dd HH:mm") ?? "");
                item.SubItems.Add(folder.PathExists ? Localization.T("존재") : Localization.T("깨짐"));
                item.SubItems.Add(folder.Path);
                item.Tag = folder;
                folderList.Items.Add(item);
            }
        }

        folderList.EndUpdate();
    }

    private List<FolderItem> GetSelectedFolders()
    {
        return folderList.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag)
            .OfType<FolderItem>()
            .ToList();
    }

    private void OpenSelectedFolder()
    {
        var folder = GetSelectedFolders().FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        if (!TryOpenPathInExplorer(folder.Path, out var errorMessage))
        {
            MessageBox.Show(this, $"{folder.Path}\n\n{errorMessage}", Localization.T("폴더 열기 실패"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeleteSelectedFolders()
    {
        var selectedFolders = GetSelectedFolders();
        if (selectedFolders.Count == 0)
        {
            return;
        }

        var message = string.Format(Localization.T("선택한 실제 폴더 {0}개를 휴지통으로 이동합니다.\n\n이 작업은 DB에서 제거하는 것이 아니라 실제 폴더를 삭제하는 작업입니다.\n계속할까요?"), selectedFolders.Count);
        var result = MessageBox.Show(this, message, Localization.T("실제 폴더 삭제"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        var deletedCount = 0;
        foreach (var folder in selectedFolders)
        {
            if (!Directory.Exists(folder.Path))
            {
                continue;
            }

            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    folder.Path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                folder.PathExists = false;
                deletedCount++;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, $"{folder.Path}\n\n{exception.Message}", Localization.T("폴더 삭제 실패"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        refreshMainWindow();
        LoadGroups();
        MessageBox.Show(this, string.Format(Localization.T("폴더 {0}개를 휴지통으로 이동했습니다. `경로 확인` 또는 스캔/동기화 후 깨진 경로 필터에 반영됩니다."), deletedCount), Localization.T("폴더 삭제"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static bool TryOpenPathInExplorer(string path, out string errorMessage)
    {
        errorMessage = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = Localization.T("경로가 비어 있습니다.");
            return false;
        }

        if (!Directory.Exists(path))
        {
            errorMessage = Localization.T("폴더가 존재하지 않습니다. 설정의 `경로 확인` 또는 스캔/동기화로 상태를 갱신하세요.");
            return false;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }
}
