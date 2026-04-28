namespace Viewer;

public sealed class Form1 : Form
{
    private readonly AppDatabase database = new();
    private readonly FolderScanner scanner = new();

    private readonly Button scanButton = new();
    private readonly ComboBox searchFieldComboBox = new();
    private readonly TextBox searchBox = new();
    private readonly Button tagFilterButton = new();
    private readonly ToolStripDropDown tagFilterDropDown = new();
    private readonly CheckedListBox tagFilterListBox = new();
    private readonly ComboBox tagFilterModeComboBox = new();
    private readonly Button clearTagFilterButton = new();
    private readonly ComboBox sortComboBox = new();
    private readonly TabControl tabs = new();
    private readonly ListView folderList = new();
    private readonly PictureBox thumbnailBox = new();
    private readonly TextBox displayNameBox = new();
    private readonly TextBox authorBox = new();
    private readonly TextBox numberBox = new();
    private readonly NumericUpDown scoreBox = new();
    private readonly TextBox tagsBox = new();
    private readonly TextBox memoBox = new();
    private readonly TextBox pathBox = new();
    private readonly Label statsLabel = new();
    private readonly CheckBox favoriteCheckBox = new();
    private readonly Button saveButton = new();
    private readonly Button viewButton = new();
    private readonly Button thumbnailButton = new();
    private readonly Button deleteFolderButton = new();
    private readonly Label statusLabel = new();
    private readonly Label tagFilterStatusLabel = new();
    private readonly ToolTip toolTip = new();

    private List<FolderItem> folders = [];
    private readonly List<string> activeTagFilters = [];
    private FolderItem? selectedFolder;
    private bool loadingDetails;
    private bool updatingTagFilterListBox;
    private CancellationTokenSource? scanCancellationTokenSource;
    private CancellationTokenSource? thumbnailLoadCancellationTokenSource;

    public Form1()
    {
        BuildUi();
        Shown += async (_, _) => await OnShownAsync();
    }

    private void BuildUi()
    {
        Text = "로컬 이미지 뷰어";
        Width = 1280;
        Height = 820;
        MinimumSize = new Size(1024, 680);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var menuStrip = new MenuStrip
        {
            Dock = DockStyle.Top
        };
        var settingsMenuItem = new ToolStripMenuItem("설정");
        settingsMenuItem.Click += (_, _) => OpenSettings();
        menuStrip.Items.Add(settingsMenuItem);
        MainMenuStrip = menuStrip;

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            Padding = new Padding(8)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));

        scanButton.Text = "스캔/동기화";
        scanButton.Dock = DockStyle.Fill;
        scanButton.Click += async (_, _) => await ScanAsync();

        searchFieldComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        searchFieldComboBox.Items.AddRange(["이름", "작가", "메모", "경로"]);
        searchFieldComboBox.SelectedIndex = 0;
        searchFieldComboBox.Dock = DockStyle.Fill;
        searchFieldComboBox.SelectedIndexChanged += (_, _) => LoadFolders();
        toolTip.SetToolTip(searchFieldComboBox, "검색할 항목을 선택합니다. 기본값은 이름입니다.");

        searchBox.PlaceholderText = "검색어";
        searchBox.Dock = DockStyle.Fill;
        searchBox.TextChanged += (_, _) => LoadFolders();

        tagFilterButton.Text = "태그";
        tagFilterButton.Dock = DockStyle.Fill;
        tagFilterButton.TextAlign = ContentAlignment.MiddleLeft;
        tagFilterButton.Click += (_, _) => ShowTagFilterMenu();
        tagFilterButton.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode == Keys.Escape)
            {
                MoveFocusAwayFromTagFilter();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
        };
        toolTip.SetToolTip(tagFilterButton, "태그 메뉴를 열어 필터를 선택합니다. 선택된 태그를 다시 누르면 해제됩니다.");
        BuildTagFilterDropDown();

        tagFilterModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        tagFilterModeComboBox.Items.AddRange(["AND", "OR"]);
        tagFilterModeComboBox.SelectedIndex = 0;
        tagFilterModeComboBox.Dock = DockStyle.Fill;
        tagFilterModeComboBox.SelectedIndexChanged += (_, _) =>
        {
            UpdateTagFilterStatus();
            LoadFolders();
        };
        toolTip.SetToolTip(tagFilterModeComboBox, "AND: 선택한 태그를 모두 가진 폴더, OR: 선택한 태그 중 하나라도 가진 폴더");

        clearTagFilterButton.Text = "전체";
        clearTagFilterButton.Dock = DockStyle.Fill;
        clearTagFilterButton.Click += (_, _) => ClearTagFilter();
        toolTip.SetToolTip(clearTagFilterButton, "태그 필터를 모두 비우고 전체 목록을 표시합니다.");

        sortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        sortComboBox.Items.AddRange(["날짜 순", "이름 순", "작가 순", "점수 순", "최근 본 순"]);
        sortComboBox.SelectedIndex = 0;
        sortComboBox.Dock = DockStyle.Fill;
        sortComboBox.SelectedIndexChanged += (_, _) => LoadFolders();

        toolbar.Controls.Add(scanButton, 0, 0);
        toolbar.Controls.Add(searchFieldComboBox, 1, 0);
        toolbar.Controls.Add(searchBox, 2, 0);
        toolbar.Controls.Add(tagFilterButton, 3, 0);
        toolbar.Controls.Add(tagFilterModeComboBox, 4, 0);
        toolbar.Controls.Add(clearTagFilterButton, 5, 0);
        toolbar.Controls.Add(sortComboBox, 6, 0);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));

        tabs.Dock = DockStyle.Top;
        tabs.Height = 32;
        tabs.TabPages.Add("전체 목록");
        tabs.TabPages.Add("즐겨찾기");
        tabs.TabPages.Add("최근 본 것");
        tabs.SelectedIndexChanged += (_, _) => LoadFolders();

        folderList.Dock = DockStyle.Fill;
        folderList.View = View.Details;
        folderList.FullRowSelect = true;
        folderList.HideSelection = false;
        folderList.MultiSelect = false;
        folderList.Columns.Add("이름", 190);
        folderList.Columns.Add("작가", 120);
        folderList.Columns.Add("태그", 180);
        folderList.Columns.Add("점수", 54);
        folderList.Columns.Add("메모", 260);
        folderList.SelectedIndexChanged += (_, _) => SelectFolderFromList();
        folderList.DoubleClick += (_, _) => OpenViewer();

        var leftPanel = new Panel { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(folderList);
        leftPanel.Controls.Add(tabs);

        var detail = BuildDetailPanel();
        contentLayout.Controls.Add(leftPanel, 0, 0);
        contentLayout.Controls.Add(detail, 1, 0);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(8, 5, 8, 0)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Text = "준비됨";

        tagFilterStatusLabel.Dock = DockStyle.Fill;
        tagFilterStatusLabel.Font = new Font(Font, FontStyle.Bold);
        tagFilterStatusLabel.ForeColor = Color.FromArgb(25, 90, 170);
        tagFilterStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        tagFilterStatusLabel.Text = "";

        statusPanel.Controls.Add(statusLabel, 0, 0);
        statusPanel.Controls.Add(tagFilterStatusLabel, 1, 0);

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(contentLayout, 0, 1);
        root.Controls.Add(statusPanel, 0, 2);
        root.MouseDown += (_, _) => MoveFocusAwayFromTagFilter();
        toolbar.MouseDown += (_, _) => MoveFocusAwayFromTagFilter();
        contentLayout.MouseDown += (_, _) => MoveFocusAwayFromTagFilter();
        statusPanel.MouseDown += (_, _) => MoveFocusAwayFromTagFilter();
        Controls.Add(root);
        Controls.Add(menuStrip);
    }

    private Control BuildDetailPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 11,
            AutoScroll = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

        thumbnailBox.Dock = DockStyle.Fill;
        thumbnailBox.Height = 220;
        thumbnailBox.BackColor = Color.FromArgb(235, 235, 235);
        thumbnailBox.SizeMode = PictureBoxSizeMode.Zoom;
        panel.Controls.Add(thumbnailBox, 0, 0);
        panel.SetColumnSpan(thumbnailBox, 2);

        displayNameBox.Multiline = true;
        displayNameBox.ScrollBars = ScrollBars.Vertical;
        AddLabeledControl(panel, "이름", displayNameBox, 1);
        AddLabeledControl(panel, "작가", authorBox, 2);
        AddLabeledControl(panel, "번호", numberBox, 3);

        scoreBox.Minimum = 0;
        scoreBox.Maximum = 100;
        AddLabeledControl(panel, "점수", scoreBox, 4);

        tagsBox.PlaceholderText = "쉼표로 구분";
        AddLabeledControl(panel, "태그", tagsBox, 5);

        memoBox.Multiline = true;
        memoBox.ScrollBars = ScrollBars.Vertical;
        AddLabeledControl(panel, "메모", memoBox, 6);

        pathBox.ReadOnly = true;
        AddLabeledControl(panel, "경로", pathBox, 7);

        statsLabel.AutoSize = true;
        statsLabel.Padding = new Padding(0, 8, 0, 0);
        panel.Controls.Add(new Label { Text = "정보", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, 8);
        panel.Controls.Add(statsLabel, 1, 8);

        favoriteCheckBox.Text = "즐겨찾기";
        favoriteCheckBox.AutoSize = true;
        panel.Controls.Add(favoriteCheckBox, 1, 9);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        saveButton.Text = "저장";
        saveButton.Click += (_, _) => SaveSelectedFolder();

        viewButton.Text = "보기";
        viewButton.Click += (_, _) => OpenViewer();

        thumbnailButton.Text = "썸네일 선택";
        thumbnailButton.Click += (_, _) => ChooseThumbnail();

        deleteFolderButton.Text = "DB에서 제거";
        deleteFolderButton.Click += (_, _) => DeleteSelectedFolder();

        StyleDetailButton(saveButton, "현재 상세 패널의 이름, 작가, 점수, 태그, 메모를 DB에 저장합니다.");
        StyleDetailButton(viewButton, "선택한 폴더의 이미지를 뷰어 창으로 엽니다.");
        StyleDetailButton(thumbnailButton, "선택한 폴더 안의 이미지 중 하나를 목록 썸네일로 지정합니다.");
        StyleDetailButton(deleteFolderButton, "실제 파일은 유지하고 이 폴더를 DB 목록에서만 제거합니다.");

        buttons.Controls.AddRange([saveButton, viewButton, thumbnailButton, deleteFolderButton]);
        panel.Controls.Add(buttons, 1, 10);

        foreach (Control control in panel.Controls)
        {
            control.Margin = new Padding(4);
        }

        SetDetailsEnabled(false);
        return panel;
    }

    private static void AddLabeledControl(TableLayoutPanel panel, string label, Control control, int row)
    {
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        }, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private async Task OnShownAsync()
    {
        statusLabel.Text = "초기 데이터를 불러오는 중...";
        await Task.Yield();

        database.Initialize();
        LoadTagFilters();
        LoadFolders(autoSelectFirst: false);
        var rootCount = database.GetRoots().Count;
        statusLabel.Text = rootCount == 0
            ? "루트 폴더를 추가한 뒤 스캔/동기화를 실행하세요."
            : $"루트 {rootCount}개 등록됨";
    }

    private async Task ScanAsync()
    {
        var roots = database.GetRoots();
        if (roots.Count == 0)
        {
            MessageBox.Show(this, "먼저 루트 폴더를 추가하세요.", "스캔", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        scanCancellationTokenSource?.Dispose();
        scanCancellationTokenSource = new CancellationTokenSource();
        var scanLog = new ScanLog();
        using var progressForm = new ScanProgressForm(CancelScan);
        var progress = new Progress<ScanProgress>(scanProgress =>
        {
            var statusText = $"스캔 {scanProgress.FoldersVisited}개 / 이미지 폴더 {scanProgress.ImageFoldersFound}개 / 저장 {scanProgress.SavedFolders}개 / 변경 없음 {scanProgress.SkippedFolders}개";
            statusLabel.Text = statusText;
            progressForm.UpdateStatus(statusText);
        });

        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("기존 DB 상태 확인 중...");
            var existingSignatureMap = await Task.Run(database.GetFolderScanSignatureMap, scanCancellationTokenSource.Token);

            progressForm.UpdateStatus("스캔 및 변경분 저장 중...");
            using var scanWriteSession = database.BeginScanWriteSession();
            var summary = await scanner.ScanStreamingAsync(
                roots,
                result =>
                {
                    return !existingSignatureMap.TryGetValue(result.FolderPath, out var existingSignature)
                        || result.FolderModifiedAt > existingSignature.FolderModifiedAt
                        || result.ImageCount != existingSignature.ImageCount
                        || result.TotalImageBytes != existingSignature.TotalImageBytes;
                },
                result => scanWriteSession.Save(result),
                progress,
                scanLog,
                scanCancellationTokenSource.Token);
            scanWriteSession.Commit();

            progressForm.UpdateStatus("삭제/누락 항목 동기화 중...");
            var cleanupSummary = await Task.Run(() =>
            {
                scanCancellationTokenSource.Token.ThrowIfCancellationRequested();
                return database.RemoveMissingFoldersAndImages();
            }, scanCancellationTokenSource.Token);
            summary.RemovedFolders = cleanupSummary.RemovedFolders;
            summary.RemovedImages = cleanupSummary.RemovedImages;
            var summaryText = $"스캔 완료: 이미지 폴더 {summary.ImageFoldersFound}개 / 저장 {summary.SavedFolders}개 / 변경 없음 {summary.SkippedFolders}개 / 삭제 폴더 {summary.RemovedFolders}개 / 삭제 이미지 {summary.RemovedImages}개";
            scanLog.Add(summaryText);
            LoadTagFilters();
            await Task.Yield();
            LoadFolders(autoSelectFirst: false);
            statusLabel.Text = summaryText;
        }
        catch (OperationCanceledException)
        {
            scanLog.Add("스캔 취소됨");
            statusLabel.Text = "스캔 취소됨";
        }
        catch (Exception exception)
        {
            scanLog.Add($"스캔 실패: {exception.Message}");
            MessageBox.Show(this, exception.Message, "스캔 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "스캔 실패";
        }
        finally
        {
            SaveScanLog(scanLog);
            progressForm.Close();
            SetBusy(false);
            scanCancellationTokenSource?.Dispose();
            scanCancellationTokenSource = null;
        }
    }

    private void CancelScan()
    {
        scanCancellationTokenSource?.Cancel();
        statusLabel.Text = "스캔 취소 요청 중...";
    }

    private void LoadFolders(long? folderIdToSelect = null, bool autoSelectFirst = true)
    {
        var selectedId = folderIdToSelect ?? selectedFolder?.Id;
        var searchField = searchFieldComboBox.SelectedIndex switch
        {
            1 => FolderSearchField.Author,
            2 => FolderSearchField.Memo,
            3 => FolderSearchField.Path,
            _ => FolderSearchField.Name
        };
        var mode = tabs.SelectedIndex switch
        {
            1 => FolderListMode.Favorites,
            2 => FolderListMode.Recent,
            _ => FolderListMode.All
        };
        var sortMode = sortComboBox.SelectedIndex switch
        {
            1 => FolderSortMode.Name,
            2 => FolderSortMode.Author,
            3 => FolderSortMode.Score,
            4 => FolderSortMode.Recent,
            _ => FolderSortMode.Date
        };
        var tagFilterMode = tagFilterModeComboBox.SelectedIndex == 1 ? TagFilterMode.Or : TagFilterMode.And;

        folders = database.GetFolders(mode, sortMode, searchField, searchBox.Text, activeTagFilters, tagFilterMode);
        folderList.BeginUpdate();
        folderList.Items.Clear();
        foreach (var folder in folders)
        {
            var item = new ListViewItem(folder.DisplayName);
            item.SubItems.Add(folder.Author ?? "");
            item.SubItems.Add(folder.TagSummary);
            item.SubItems.Add(folder.Score.ToString());
            item.SubItems.Add(Shorten(folder.Memo, 80));
            item.Tag = folder;
            folderList.Items.Add(item);
        }

        folderList.EndUpdate();
        if (folders.Count == 0)
        {
            ClearDetails();
            return;
        }

        var selectedItem = folderList.Items
            .Cast<ListViewItem>()
            .FirstOrDefault(item => item.Tag is FolderItem folder && folder.Id == selectedId);

        if (selectedItem is not null)
        {
            selectedItem.Selected = true;
            selectedItem.Focused = true;
            selectedItem.EnsureVisible();
            if (selectedItem.Tag is FolderItem selectedFolderItem)
            {
                ShowFolder(selectedFolderItem);
            }
        }
        else if (autoSelectFirst && selectedFolder is null && folderList.Items.Count > 0)
        {
            var firstItem = folderList.Items[0];
            firstItem.Selected = true;
            firstItem.Focused = true;
            firstItem.EnsureVisible();
            if (firstItem.Tag is FolderItem firstFolder)
            {
                ShowFolder(firstFolder);
            }
        }
        else if (selectedFolder is not null)
        {
            ClearDetails();
        }
    }

    private void SelectFolderFromList()
    {
        if (folderList.SelectedItems.Count == 0)
        {
            return;
        }

        if (folderList.SelectedItems[0].Tag is FolderItem folder)
        {
            ShowFolder(folder);
        }
    }

    private void ShowFolder(FolderItem folder)
    {
        loadingDetails = true;
        selectedFolder = folder;
        SetDetailsEnabled(true);
        displayNameBox.Text = folder.DisplayName;
        authorBox.Text = folder.Author ?? "";
        numberBox.Text = folder.Number ?? "";
        scoreBox.Value = Math.Clamp(folder.Score, (int)scoreBox.Minimum, (int)scoreBox.Maximum);
        tagsBox.Text = string.Join(", ", folder.Tags);
        memoBox.Text = folder.Memo ?? "";
        pathBox.Text = folder.Path;
        favoriteCheckBox.Checked = folder.IsFavorite;
        var lastImageName = string.IsNullOrWhiteSpace(folder.LastImagePath) ? "-" : Path.GetFileName(folder.LastImagePath);
        statsLabel.Text = $"열람 {folder.ViewCount}회 / 마지막 열람: {(folder.LastViewedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-")} / 마지막 이미지: {lastImageName}";
        LoadThumbnailAsync(folder.ThumbnailPath);
        loadingDetails = false;
    }

    private void ClearDetails()
    {
        selectedFolder = null;
        displayNameBox.Clear();
        authorBox.Clear();
        numberBox.Clear();
        scoreBox.Value = 0;
        tagsBox.Clear();
        memoBox.Clear();
        pathBox.Clear();
        statsLabel.Text = "";
        favoriteCheckBox.Checked = false;
        thumbnailBox.Image?.Dispose();
        thumbnailBox.Image = null;
        SetDetailsEnabled(false);
    }

    private void SaveSelectedFolder()
    {
        if (selectedFolder is null || loadingDetails)
        {
            return;
        }

        selectedFolder.DisplayName = string.IsNullOrWhiteSpace(displayNameBox.Text) ? new DirectoryInfo(selectedFolder.Path).Name : displayNameBox.Text.Trim();
        selectedFolder.Author = string.IsNullOrWhiteSpace(authorBox.Text) ? null : authorBox.Text.Trim();
        selectedFolder.Number = string.IsNullOrWhiteSpace(numberBox.Text) ? null : numberBox.Text.Trim();
        selectedFolder.Score = (int)scoreBox.Value;
        selectedFolder.Tags = tagsBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        selectedFolder.Memo = string.IsNullOrWhiteSpace(memoBox.Text) ? null : memoBox.Text.Trim();
        selectedFolder.IsFavorite = favoriteCheckBox.Checked;

        database.SaveFolder(selectedFolder);
        statusLabel.Text = "저장됨";
        LoadTagFilters(selectedFolder.Tags);
        LoadFolders(selectedFolder.Id);
    }

    private void OpenViewer()
    {
        if (selectedFolder is null)
        {
            return;
        }

        var images = database.GetImages(selectedFolder.Id).Where(image => File.Exists(image.Path)).ToList();
        if (images.Count == 0)
        {
            MessageBox.Show(this, "열 수 있는 이미지가 없습니다. 스캔/동기화를 다시 실행해 보세요.", "보기", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        database.MarkFolderViewed(selectedFolder.Id, images[0].Path);
        using var viewer = new ImageViewerForm(images);
        viewer.ShowDialog(this);
        database.UpdateLastImagePath(selectedFolder.Id, viewer.CurrentImagePath);
        LoadFolders(selectedFolder.Id);
    }

    private void DeleteSelectedFolder()
    {
        if (selectedFolder is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"DB 목록에서만 제거합니다. 실제 폴더와 이미지는 삭제하지 않습니다.\n\n{selectedFolder.DisplayName}",
            "폴더 제거",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        database.DeleteFolder(selectedFolder.Id);
        statusLabel.Text = $"DB에서 제거됨: {selectedFolder.DisplayName}";
        ClearDetails();
        LoadTagFilters();
        LoadFolders(null);
    }

    private void ChooseThumbnail()
    {
        if (selectedFolder is null)
        {
            return;
        }

        var images = database.GetImages(selectedFolder.Id).Where(image => File.Exists(image.Path)).ToList();
        if (images.Count == 0)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "썸네일로 사용할 이미지를 선택하세요",
            InitialDirectory = selectedFolder.Path,
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!images.Any(image => string.Equals(image.Path, dialog.FileName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "해당 폴더 안의 이미지만 썸네일로 선택할 수 있습니다.", "썸네일", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        selectedFolder.ThumbnailPath = dialog.FileName;
        LoadThumbnailAsync(dialog.FileName);
        SaveSelectedFolder();
    }

    private async void LoadThumbnailAsync(string? path)
    {
        thumbnailLoadCancellationTokenSource?.Cancel();
        thumbnailLoadCancellationTokenSource?.Dispose();
        thumbnailLoadCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = thumbnailLoadCancellationTokenSource.Token;

        thumbnailBox.Image?.Dispose();
        thumbnailBox.Image = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var image = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sourceImage = Image.FromStream(stream);
                return new Bitmap(sourceImage);
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                image.Dispose();
                return;
            }

            thumbnailBox.Image?.Dispose();
            thumbnailBox.Image = image;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            thumbnailBox.Image = null;
        }
    }

    private void SetDetailsEnabled(bool enabled)
    {
        foreach (var control in new Control[] { displayNameBox, authorBox, numberBox, scoreBox, tagsBox, memoBox, favoriteCheckBox, saveButton, viewButton, thumbnailButton, deleteFolderButton })
        {
            control.Enabled = enabled;
        }
    }

    private void SetBusy(bool busy)
    {
        scanButton.Enabled = !busy;
        searchBox.Enabled = !busy;
        searchFieldComboBox.Enabled = !busy;
        sortComboBox.Enabled = !busy;
        tagFilterButton.Enabled = !busy;
        tagFilterModeComboBox.Enabled = !busy;
        clearTagFilterButton.Enabled = !busy;
        folderList.Enabled = !busy;
    }

    private void SaveScanLog(ScanLog scanLog)
    {
        if (scanLog.Entries.Count == 0)
        {
            return;
        }

        var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, $"scan_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        File.WriteAllLines(logPath, scanLog.Entries);
    }

    private void LoadTagFilters(IEnumerable<string>? preferredTags = null)
    {
        var tags = database.GetTags();

        activeTagFilters.RemoveAll(activeTag => !tags.Contains(activeTag, StringComparer.OrdinalIgnoreCase));

        updatingTagFilterListBox = true;
        tagFilterListBox.Items.Clear();
        foreach (var tag in tags)
        {
            tagFilterListBox.Items.Add(tag, activeTagFilters.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }
        updatingTagFilterListBox = false;
        ResizeTagFilterDropDown();

        UpdateTagFilterStatus();
    }

    private void ShowTagFilterMenu()
    {
        if (tagFilterListBox.Items.Count == 0)
        {
            return;
        }

        if (!tagFilterDropDown.Visible)
        {
            ResizeTagFilterDropDown();
            tagFilterDropDown.Show(tagFilterButton, new Point(0, tagFilterButton.Height));
            tagFilterListBox.Focus();
        }
    }

    private void SyncActiveTagFiltersFromListBox()
    {
        if (updatingTagFilterListBox)
        {
            return;
        }

        activeTagFilters.Clear();
        foreach (var checkedItem in tagFilterListBox.CheckedItems)
        {
            if (checkedItem is string tag)
            {
                activeTagFilters.Add(tag);
            }
        }

        UpdateTagFilterStatus();
        LoadFolders();
    }

    private void ClearTagFilter()
    {
        activeTagFilters.Clear();
        updatingTagFilterListBox = true;
        for (var itemIndex = 0; itemIndex < tagFilterListBox.Items.Count; itemIndex++)
        {
            tagFilterListBox.SetItemChecked(itemIndex, false);
        }
        updatingTagFilterListBox = false;
        UpdateTagFilterStatus();
        LoadFolders();
        MoveFocusAwayFromTagFilter();
    }

    private void UpdateTagFilterStatus()
    {
        if (activeTagFilters.Count == 0)
        {
            tagFilterStatusLabel.Text = "";
            tagFilterButton.Text = "태그";
            tagFilterButton.Font = Font;
            tagFilterButton.ForeColor = SystemColors.ControlText;
            toolTip.SetToolTip(clearTagFilterButton, "태그 필터를 모두 비우고 전체 목록을 표시합니다.");
            return;
        }

        var tagSummary = string.Join(", ", activeTagFilters);
        var modeText = tagFilterModeComboBox.SelectedIndex == 1 ? "OR" : "AND";
        tagFilterButton.Text = ShortenTagSummary(tagSummary);
        tagFilterButton.Font = new Font(Font, FontStyle.Bold);
        tagFilterButton.ForeColor = Color.FromArgb(25, 90, 170);
        tagFilterStatusLabel.Text = $"태그 필터({modeText}): {tagSummary}";
        toolTip.SetToolTip(clearTagFilterButton, $"현재 태그 필터({modeText}): {tagSummary}");
    }

    private void BuildTagFilterDropDown()
    {
        tagFilterListBox.CheckOnClick = true;
        tagFilterListBox.BorderStyle = BorderStyle.None;
        tagFilterListBox.IntegralHeight = false;
        tagFilterListBox.HorizontalScrollbar = true;
        tagFilterListBox.ItemCheck += (_, _) => BeginInvoke(SyncActiveTagFiltersFromListBox);
        tagFilterListBox.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode == Keys.Escape)
            {
                MoveFocusAwayFromTagFilter();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
        };

        var host = new ToolStripControlHost(tagFilterListBox)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };
        tagFilterDropDown.Padding = Padding.Empty;
        tagFilterDropDown.Items.Add(host);
        tagFilterDropDown.AutoClose = true;
    }

    private void ResizeTagFilterDropDown()
    {
        var visibleItemCount = Math.Clamp(tagFilterListBox.Items.Count, 1, 10);
        var itemHeight = Math.Max(tagFilterListBox.ItemHeight, 18);
        var width = Math.Max(tagFilterButton.Width, 160);
        var height = visibleItemCount * itemHeight + 6;

        tagFilterListBox.Size = new Size(width, height);
        if (tagFilterDropDown.Items[0] is ToolStripControlHost host)
        {
            host.Size = tagFilterListBox.Size;
        }
    }

    private void MoveFocusAwayFromTagFilter()
    {
        tagFilterDropDown.Close();

        if (folderList.Items.Count > 0)
        {
            folderList.Focus();
            return;
        }

        searchBox.Focus();
    }
    private static string ShortenTagSummary(string tagSummary)
    {
        const int maxLength = 14;
        if (tagSummary.Length <= maxLength)
        {
            return tagSummary;
        }

        return tagSummary[..Math.Max(0, maxLength - 3)] + "...";
    }

    private void OpenSettings()
    {
        using var settingsForm = new SettingsForm(database, RefreshAfterSettingsChanged);
        settingsForm.ShowDialog(this);
    }

    private void RefreshAfterSettingsChanged()
    {
        LoadTagFilters();
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = "설정 변경사항을 반영했습니다.";
    }

    private void StyleDetailButton(Button button, string description)
    {
        button.Width = 110;
        button.Height = 28;
        button.Margin = new Padding(4);
        button.UseVisualStyleBackColor = true;
        toolTip.SetToolTip(button, description);
    }

    private static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 3)] + "...";
    }

}
