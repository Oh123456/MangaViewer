namespace Viewer;

public sealed class Form1 : Form
{
    private readonly AppDatabase database = new();
    private readonly FolderScanner scanner = new();

    private readonly Button scanButton = new();
    private readonly Button randomButton = new();
    private readonly ComboBox searchFieldComboBox = new();
    private readonly TextBox searchBox = new();
    private readonly Button tagFilterButton = new();
    private readonly ToolStripDropDown tagFilterDropDown = new();
    private readonly CheckedListBox tagFilterListBox = new();
    private readonly ComboBox tagFilterModeComboBox = new();
    private readonly Button clearTagFilterButton = new();
    private readonly ComboBox sortComboBox = new();
    private readonly ComboBox quickFilterComboBox = new();
    private readonly TabControl tabs = new();
    private readonly TabPage randomTabPage = new("랜덤 추천");
    private readonly ListView folderList = new();
    private readonly PictureBox thumbnailBox = new();
    private readonly TextBox displayNameBox = new();
    private readonly TextBox authorBox = new();
    private readonly TextBox numberBox = new();
    private readonly TextBox seriesNameBox = new();
    private readonly NumericUpDown seriesOrderBox = new();
    private readonly NumericUpDown scoreBox = new();
    private readonly TextBox tagsBox = new();
    private readonly TextBox memoBox = new();
    private readonly TextBox pathBox = new();
    private readonly Label statsLabel = new();
    private readonly CheckBox favoriteCheckBox = new();
    private readonly CheckBox reservedCheckBox = new();
    private readonly Button saveButton = new();
    private readonly Button viewButton = new();
    private readonly Button thumbnailButton = new();
    private readonly Button openFolderButton = new();
    private readonly Button copyPathButton = new();
    private readonly Button deleteFolderButton = new();
    private readonly Label statusLabel = new();
    private readonly Label tagFilterStatusLabel = new();
    private readonly ToolTip toolTip = new();
    private readonly ContextMenuStrip folderListMenu = new();

    private List<FolderItem> folders = [];
    private List<FolderItem> randomFolders = [];
    private readonly List<string> activeTagFilters = [];
    private FolderItem? selectedFolder;
    private bool loadingDetails;
    private bool updatingTagFilterListBox;
    private bool suppressTabChanged;
    private int sortedColumnIndex = -1;
    private bool sortDescending;
    private CancellationTokenSource? scanCancellationTokenSource;
    private CancellationTokenSource? thumbnailLoadCancellationTokenSource;

    public Form1()
    {
        BuildUi();
        ApplySavedWindowPlacement();
        Shown += async (_, _) => await OnShownAsync();
        FormClosing += (_, _) => SaveWindowPlacement();
    }

    private void BuildUi()
    {
        Text = "로컬 이미지 뷰어";
        Width = 1440;
        Height = 1040;
        MinimumSize = new Size(1120, 900);
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
            ColumnCount = 10,
            Padding = new Padding(8)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));

        scanButton.Text = "스캔/동기화";
        scanButton.Dock = DockStyle.Fill;
        scanButton.Click += async (_, _) => await ScanAsync();

        randomButton.Text = "랜덤";
        randomButton.Dock = DockStyle.Fill;
        randomButton.Click += (_, _) => ShowRandomFolders();
        toolTip.SetToolTip(randomButton, "현재 검색/탭/태그 조건의 목록에서 무작위로 골라 보여줍니다.");

        searchFieldComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        searchFieldComboBox.Items.AddRange(["이름", "작가", "메모", "경로", "묶음"]);
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
        sortComboBox.Items.AddRange(["날짜 순", "이름 순", "작가 순", "점수 순", "최근 본 순", "묶음 순", "이미지 수 순"]);
        sortComboBox.SelectedIndex = 0;
        sortComboBox.Dock = DockStyle.Fill;
        sortComboBox.SelectedIndexChanged += (_, _) => LoadFolders();

        quickFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        quickFilterComboBox.Items.AddRange(["전체", "미열람", "점수 없음", "태그 없음", "묶음 없음", "썸네일 없음", "깨진 경로"]);
        quickFilterComboBox.SelectedIndex = 0;
        quickFilterComboBox.Dock = DockStyle.Fill;
        quickFilterComboBox.SelectedIndexChanged += (_, _) => LoadFolders();
        toolTip.SetToolTip(quickFilterComboBox, "정리가 필요한 항목만 빠르게 필터링합니다.");

        toolbar.Controls.Add(scanButton, 0, 0);
        toolbar.Controls.Add(randomButton, 1, 0);
        toolbar.Controls.Add(searchFieldComboBox, 2, 0);
        toolbar.Controls.Add(searchBox, 3, 0);
        toolbar.Controls.Add(tagFilterButton, 4, 0);
        toolbar.Controls.Add(tagFilterModeComboBox, 5, 0);
        toolbar.Controls.Add(clearTagFilterButton, 6, 0);
        toolbar.Controls.Add(sortComboBox, 7, 0);
        toolbar.Controls.Add(quickFilterComboBox, 8, 0);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));

        tabs.Dock = DockStyle.Top;
        tabs.Height = 32;
        tabs.TabPages.Add("전체 목록");
        tabs.TabPages.Add("즐겨찾기");
        tabs.TabPages.Add("최근 본 것");
        tabs.TabPages.Add("보류함");
        tabs.TabPages.Add("묶음 목록");
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (suppressTabChanged)
            {
                return;
            }

            if (tabs.SelectedTab == randomTabPage)
            {
                folders = randomFolders;
                PopulateFolderList(null, autoSelectFirst: true);
                statusLabel.Text = $"랜덤 추천 {folders.Count}개";
                return;
            }

            LoadFolders();
        };

        folderList.Dock = DockStyle.Fill;
        folderList.View = View.Details;
        folderList.FullRowSelect = true;
        folderList.HideSelection = false;
        folderList.MultiSelect = true;
        folderList.Columns.Add("이름", 190);
        folderList.Columns.Add("작가", 110);
        folderList.Columns.Add("태그", 150);
        folderList.Columns.Add("점수", 54);
        folderList.Columns.Add("묶음", 130);
        folderList.Columns.Add("묶음 편수", 74);
        folderList.Columns.Add("메모", 320);
        folderList.Columns.Add("이미지", 62);
        folderList.Columns.Add("수정일", 112);
        ApplySavedColumnWidths();
        folderList.SelectedIndexChanged += (_, _) => SelectFolderFromList();
        folderList.ColumnClick += (_, columnClickEventArgs) => SortByColumn(columnClickEventArgs.Column);
        folderList.ColumnWidthChanged += (_, _) => SaveColumnWidths();
        folderList.DoubleClick += (_, _) => OpenViewer();
        folderList.MouseDown += (_, mouseEventArgs) =>
        {
            if (mouseEventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            var item = folderList.GetItemAt(mouseEventArgs.X, mouseEventArgs.Y);
            if (item is not null && !item.Selected)
            {
                folderList.SelectedItems.Clear();
                item.Selected = true;
            }
        };
        BuildFolderListMenu();
        folderList.ContextMenuStrip = folderListMenu;

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

    private void ApplySavedWindowPlacement()
    {
        var placement = AppSettings.Current.MainWindow;
        if (placement.HasBounds && placement.Width > 0 && placement.Height > 0)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = placement.Bounds;
        }

        if (placement.WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private void SaveWindowPlacement()
    {
        var placement = AppSettings.Current.MainWindow;
        placement.WindowState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
        placement.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        AppSettings.Save();
    }

    private Control BuildDetailPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 13,
            AutoScroll = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));

        thumbnailBox.Dock = DockStyle.Fill;
        thumbnailBox.Height = 300;
        thumbnailBox.BackColor = Color.FromArgb(235, 235, 235);
        thumbnailBox.SizeMode = PictureBoxSizeMode.Zoom;
        panel.Controls.Add(thumbnailBox, 0, 0);
        panel.SetColumnSpan(thumbnailBox, 2);

        displayNameBox.Multiline = true;
        displayNameBox.ScrollBars = ScrollBars.Vertical;
        AddLabeledControl(panel, "이름", displayNameBox, 1);
        AddLabeledControl(panel, "작가", authorBox, 2);
        AddLabeledControl(panel, "번호", numberBox, 3);

        seriesNameBox.PlaceholderText = "같은 묶음 이름";
        seriesNameBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        seriesNameBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
        AddLabeledControl(panel, "묶음", seriesNameBox, 4);

        seriesOrderBox.Minimum = 0;
        seriesOrderBox.Maximum = 999;
        AddLabeledControl(panel, "편수", seriesOrderBox, 5);

        scoreBox.Minimum = 0;
        scoreBox.Maximum = 100;
        AddLabeledControl(panel, "점수", scoreBox, 6);

        tagsBox.PlaceholderText = "쉼표로 구분";
        AddLabeledControl(panel, "태그", tagsBox, 7);

        memoBox.Multiline = true;
        memoBox.ScrollBars = ScrollBars.Vertical;
        AddLabeledControl(panel, "메모", memoBox, 8);

        pathBox.ReadOnly = true;
        AddLabeledControl(panel, "경로", pathBox, 9);

        statsLabel.AutoSize = false;
        statsLabel.Dock = DockStyle.Fill;
        statsLabel.TextAlign = ContentAlignment.MiddleLeft;
        statsLabel.Padding = new Padding(0, 4, 0, 0);
        panel.Controls.Add(new Label { Text = "정보", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, 10);
        panel.Controls.Add(statsLabel, 1, 10);

        favoriteCheckBox.Text = "즐겨찾기";
        favoriteCheckBox.AutoSize = true;
        reservedCheckBox.Text = "보류함";
        reservedCheckBox.AutoSize = true;

        var checkPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        checkPanel.Controls.AddRange([favoriteCheckBox, reservedCheckBox]);
        panel.Controls.Add(checkPanel, 1, 11);

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

        openFolderButton.Text = "폴더 열기";
        openFolderButton.Click += (_, _) => OpenSelectedFolderInExplorer();

        copyPathButton.Text = "경로 복사";
        copyPathButton.Click += (_, _) => CopySelectedFolderPath();

        deleteFolderButton.Text = "DB에서 제거";
        deleteFolderButton.Click += (_, _) => DeleteSelectedFolder();

        StyleDetailButton(saveButton, "현재 상세 패널의 이름, 작가, 점수, 태그, 메모, 즐겨찾기, 보류함 상태를 DB에 저장합니다.");
        StyleDetailButton(viewButton, "선택한 폴더의 이미지를 뷰어 창으로 엽니다.");
        StyleDetailButton(thumbnailButton, "선택한 폴더 안의 이미지 중 하나를 목록 썸네일로 지정합니다.");
        StyleDetailButton(openFolderButton, "선택한 폴더를 파일 탐색기로 엽니다.");
        StyleDetailButton(copyPathButton, "선택한 폴더 경로를 클립보드에 복사합니다.");
        StyleDetailButton(deleteFolderButton, "실제 파일은 유지하고 이 폴더를 DB 목록에서만 제거합니다.");

        buttons.Controls.AddRange([saveButton, viewButton, thumbnailButton, openFolderButton, copyPathButton, deleteFolderButton]);
        panel.Controls.Add(buttons, 1, 12);

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

    private void BuildFolderListMenu()
    {
        folderListMenu.Items.Add("묶음으로 만들기", null, (_, _) => AssignSeriesToSelectedFolders());
        folderListMenu.Items.Add("묶음 자동 추정", null, (_, _) => GuessSeriesFromSelectedFolders());
        folderListMenu.Items.Add("묶음 편집/추가", null, (_, _) => EditSeriesFromSelectedFolders());
        folderListMenu.Items.Add("묶음 해제", null, (_, _) => ClearSeriesFromSelectedFolders());
        folderListMenu.Items.Add(new ToolStripSeparator());
        folderListMenu.Items.Add("선택 항목에 태그 추가", null, (_, _) => AddTagsToSelectedFolders());
        folderListMenu.Items.Add(new ToolStripSeparator());
        folderListMenu.Items.Add("즐겨찾기 설정", null, (_, _) => SetSelectedFoldersFavorite(true));
        folderListMenu.Items.Add("즐겨찾기 해제", null, (_, _) => SetSelectedFoldersFavorite(false));
        folderListMenu.Items.Add("보류함 설정", null, (_, _) => SetSelectedFoldersReserved(true));
        folderListMenu.Items.Add("보류함 해제", null, (_, _) => SetSelectedFoldersReserved(false));
        folderListMenu.Items.Add(new ToolStripSeparator());
        folderListMenu.Items.Add("선택 항목 DB에서 제거", null, (_, _) => DeleteSelectedFoldersFromList());
        folderListMenu.Opening += (_, cancelEventArgs) =>
        {
            cancelEventArgs.Cancel = folderList.SelectedItems.Count == 0;
        };
    }

    private void ApplySavedColumnWidths()
    {
        var widths = AppSettings.Current.FolderListColumnWidths;
        for (var columnIndex = 0; columnIndex < Math.Min(widths.Count, folderList.Columns.Count); columnIndex++)
        {
            if (widths[columnIndex] > 24)
            {
                folderList.Columns[columnIndex].Width = widths[columnIndex];
            }
        }
    }

    private void SaveColumnWidths()
    {
        AppSettings.Current.FolderListColumnWidths = folderList.Columns
            .Cast<ColumnHeader>()
            .Select(column => column.Width)
            .ToList();
        AppSettings.Save();
    }

    private void SortByColumn(int columnIndex)
    {
        if (sortedColumnIndex == columnIndex)
        {
            sortDescending = !sortDescending;
        }
        else
        {
            sortedColumnIndex = columnIndex;
            sortDescending = false;
        }

        var sortIndex = columnIndex switch
        {
            0 => 1,
            1 => 2,
            3 => 3,
            5 or 6 => 5,
            7 => 6,
            8 => 0,
            _ => sortComboBox.SelectedIndex
        };

        if (sortIndex >= 0 && sortIndex < sortComboBox.Items.Count)
        {
            sortComboBox.SelectedIndex = sortIndex;
            if (sortComboBox.SelectedIndex == sortIndex)
            {
                LoadFolders();
            }
        }
    }

    private void UpdateListStatus()
    {
        var sortText = sortComboBox.SelectedItem?.ToString() ?? "정렬";
        var directionText = sortDescending ? "역순" : "기본순";
        var selectedText = folderList.SelectedItems.Count == 0 ? "" : $" / 선택 {folderList.SelectedItems.Count}개";
        statusLabel.Text = $"{sortText} {directionText} / 목록 {folderList.Items.Count}개{selectedText}";
    }

    private async Task OnShownAsync()
    {
        statusLabel.Text = "초기 데이터를 불러오는 중...";
        await Task.Yield();

        database.Initialize();
        LoadTagFilters();
        LoadSeriesNames();
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
        if (tabs.SelectedTab == randomTabPage)
        {
            RefreshRandomFolders(selectedId, autoSelectFirst);
            return;
        }

        folders = QueryCurrentFolders();
        ApplyListSortDirection();
        PopulateFolderList(selectedId, autoSelectFirst);
    }

    private void RefreshRandomFolders(long? selectedId, bool autoSelectFirst)
    {
        if (randomFolders.Count == 0)
        {
            folders = [];
            PopulateFolderList(selectedId, autoSelectFirst);
            return;
        }

        var randomOrder = randomFolders
            .Select((folder, order) => new { folder.Id, Order = order })
            .ToDictionary(item => item.Id, item => item.Order);
        var refreshedFolders = database
            .GetFolders(FolderListMode.All, FolderSortMode.Name, FolderSearchField.Name, "", [], TagFilterMode.And)
            .Where(folder => randomOrder.ContainsKey(folder.Id))
            .OrderBy(folder => randomOrder[folder.Id])
            .ToList();

        randomFolders = refreshedFolders;
        folders = randomFolders;
        ApplyListSortDirection();
        PopulateFolderList(selectedId, autoSelectFirst);
    }

    private void ApplyListSortDirection()
    {
        if (sortDescending)
        {
            folders.Reverse();
        }
    }

    private List<FolderItem> QueryCurrentFolders()
    {
        var searchField = searchFieldComboBox.SelectedIndex switch
        {
            1 => FolderSearchField.Author,
            2 => FolderSearchField.Memo,
            3 => FolderSearchField.Path,
            4 => FolderSearchField.Series,
            _ => FolderSearchField.Name
        };
        var mode = tabs.SelectedIndex switch
        {
            1 when tabs.SelectedTab != randomTabPage => FolderListMode.Favorites,
            2 when tabs.SelectedTab != randomTabPage => FolderListMode.Recent,
            3 when tabs.SelectedTab != randomTabPage => FolderListMode.Reserved,
            4 when tabs.SelectedTab != randomTabPage => FolderListMode.Series,
            _ => FolderListMode.All
        };
        var sortMode = sortComboBox.SelectedIndex switch
        {
            1 => FolderSortMode.Name,
            2 => FolderSortMode.Author,
            3 => FolderSortMode.Score,
            4 => FolderSortMode.Recent,
            5 => FolderSortMode.Series,
            6 => FolderSortMode.ImageCount,
            _ => FolderSortMode.Date
        };
        var tagFilterMode = tagFilterModeComboBox.SelectedIndex == 1 ? TagFilterMode.Or : TagFilterMode.And;
        var quickFilterMode = quickFilterComboBox.SelectedIndex switch
        {
            1 => QuickFilterMode.Unviewed,
            2 => QuickFilterMode.NoScore,
            3 => QuickFilterMode.NoTags,
            4 => QuickFilterMode.NoSeries,
            5 => QuickFilterMode.NoThumbnail,
            6 => QuickFilterMode.BrokenPath,
            _ => QuickFilterMode.All
        };

        var result = database.GetFolders(mode, sortMode, searchField, searchBox.Text, activeTagFilters, tagFilterMode, quickFilterMode);
        if (quickFilterMode == QuickFilterMode.BrokenPath)
        {
            result = result.Where(folder => !Directory.Exists(folder.Path)).ToList();
        }

        return result;
    }

    private void PopulateFolderList(long? selectedId, bool autoSelectFirst)
    {
        folderList.BeginUpdate();
        folderList.Items.Clear();
        foreach (var folder in folders)
        {
            var item = new ListViewItem(folder.DisplayName);
            item.SubItems.Add(folder.Author ?? "");
            item.SubItems.Add(folder.TagSummary);
            item.SubItems.Add(folder.Score.ToString());
            item.SubItems.Add(folder.SeriesName ?? "");
            item.SubItems.Add(folder.SeriesOrder?.ToString() ?? "");
            item.SubItems.Add(Shorten(folder.Memo, 80));
            item.SubItems.Add(folder.ImageCount.ToString());
            item.SubItems.Add(folder.FolderModifiedAt?.ToString("yyyy-MM-dd") ?? "");
            item.Tag = folder;
            if (!string.IsNullOrWhiteSpace(folder.SeriesName))
            {
                item.BackColor = Color.FromArgb(245, 250, 255);
            }

            folderList.Items.Add(item);
        }

        folderList.EndUpdate();
        UpdateListStatus();
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

    private void ShowRandomFolders()
    {
        var candidates = QueryCurrentFolders();
        if (candidates.Count == 0)
        {
            MessageBox.Show(this, "랜덤으로 고를 이미지 폴더가 없습니다.", "랜덤", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new RandomRecommendForm(candidates.Count);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var targetCount = Math.Min(dialog.RecommendCount, candidates.Count);
        var recommendedFolders = new List<FolderItem>();
        var recommendedFolderIds = new HashSet<long>();
        foreach (var candidate in candidates.OrderBy(_ => Random.Shared.Next()))
        {
            var displayFolder = GetRandomDisplayFolder(candidate);
            if (!recommendedFolderIds.Add(displayFolder.Id))
            {
                continue;
            }

            recommendedFolders.Add(displayFolder);
            if (recommendedFolders.Count >= targetCount)
            {
                break;
            }
        }

        folders = recommendedFolders;
        randomFolders = folders;

        suppressTabChanged = true;
        if (!tabs.TabPages.Contains(randomTabPage))
        {
            tabs.TabPages.Add(randomTabPage);
        }

        tabs.SelectedTab = randomTabPage;
        suppressTabChanged = false;

        selectedFolder = null;
        PopulateFolderList(null, autoSelectFirst: true);
        statusLabel.Text = $"랜덤 추천 {folders.Count}개 / 후보 {candidates.Count}개";
    }

    private FolderItem GetRandomDisplayFolder(FolderItem folder)
    {
        if (string.IsNullOrWhiteSpace(folder.SeriesName))
        {
            return folder;
        }

        return database.GetFirstFolderInSeries(folder.SeriesName) ?? folder;
    }

    private void SelectFolderFromList()
    {
        UpdateListStatus();
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
        seriesNameBox.Text = folder.SeriesName ?? "";
        seriesOrderBox.Value = Math.Clamp(folder.SeriesOrder ?? 0, (int)seriesOrderBox.Minimum, (int)seriesOrderBox.Maximum);
        scoreBox.Value = Math.Clamp(folder.Score, (int)scoreBox.Minimum, (int)scoreBox.Maximum);
        tagsBox.Text = string.Join(", ", folder.Tags);
        memoBox.Text = folder.Memo ?? "";
        pathBox.Text = folder.Path;
        favoriteCheckBox.Checked = folder.IsFavorite;
        reservedCheckBox.Checked = folder.IsReserved;
        var lastImageName = string.IsNullOrWhiteSpace(folder.LastImagePath) ? "-" : Path.GetFileName(folder.LastImagePath);
        var seriesText = string.IsNullOrWhiteSpace(folder.SeriesName) ? "" : $" / 묶음: {folder.SeriesName} #{folder.SeriesOrder}";
        statsLabel.Text = $"이미지 {folder.ImageCount}장 / 열람 {folder.ViewCount}회{seriesText}{Environment.NewLine}마지막 열람: {(folder.LastViewedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-")} / 마지막 이미지: {lastImageName}";
        LoadThumbnailAsync(folder.ThumbnailPath);
        loadingDetails = false;
    }

    private void ClearDetails()
    {
        selectedFolder = null;
        displayNameBox.Clear();
        authorBox.Clear();
        numberBox.Clear();
        seriesNameBox.Clear();
        seriesOrderBox.Value = 0;
        scoreBox.Value = 0;
        tagsBox.Clear();
        memoBox.Clear();
        pathBox.Clear();
        statsLabel.Text = "";
        favoriteCheckBox.Checked = false;
        reservedCheckBox.Checked = false;
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
        selectedFolder.SeriesName = string.IsNullOrWhiteSpace(seriesNameBox.Text) ? null : seriesNameBox.Text.Trim();
        selectedFolder.SeriesOrder = selectedFolder.SeriesName is null || seriesOrderBox.Value <= 0 ? null : (int)seriesOrderBox.Value;
        selectedFolder.Score = (int)scoreBox.Value;
        selectedFolder.Tags = tagsBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        selectedFolder.Memo = string.IsNullOrWhiteSpace(memoBox.Text) ? null : memoBox.Text.Trim();
        selectedFolder.IsFavorite = favoriteCheckBox.Checked;
        selectedFolder.IsReserved = reservedCheckBox.Checked;

        database.SaveFolder(selectedFolder);
        statusLabel.Text = "저장됨";
        LoadTagFilters(selectedFolder.Tags);
        LoadSeriesNames();
        LoadFolders(selectedFolder.Id);
    }

    private void OpenSelectedFolderInExplorer()
    {
        if (selectedFolder is null || !Directory.Exists(selectedFolder.Path))
        {
            MessageBox.Show(this, "열 수 있는 폴더가 없습니다.", "폴더 열기", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{selectedFolder.Path}\"",
            UseShellExecute = true
        });
    }

    private void CopySelectedFolderPath()
    {
        if (selectedFolder is null)
        {
            return;
        }

        Clipboard.SetText(selectedFolder.Path);
        statusLabel.Text = "경로 복사됨";
    }

    private void OpenViewer()
    {
        if (selectedFolder is null)
        {
            return;
        }

        if (SaveSeriesMetadataBeforeViewing())
        {
            return;
        }

        var isSeriesViewer = ShouldOpenAsSeries(selectedFolder);
        var images = isSeriesViewer
            ? database.GetSeriesImages(selectedFolder.SeriesName!).Where(image => File.Exists(image.Path)).ToList()
            : database.GetImages(selectedFolder.Id).Where(image => File.Exists(image.Path)).ToList();
        if (images.Count == 0)
        {
            MessageBox.Show(this, "열 수 있는 이미지가 없습니다. 스캔/동기화를 다시 실행해 보세요.", "보기", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        database.MarkFolderViewed(images[0].FolderId, images[0].Path);
        using var viewer = new ImageViewerForm(images, 0, isSeriesViewer);
        viewer.ShowDialog(this);
        database.UpdateLastImagePath(selectedFolder.Id, viewer.CurrentImagePath);
        LoadFolders(selectedFolder.Id);
    }

    private bool ShouldOpenAsSeries(FolderItem folder)
    {
        return !string.IsNullOrWhiteSpace(folder.SeriesName);
    }

    private bool SaveSeriesMetadataBeforeViewing()
    {
        if (selectedFolder is null)
        {
            return true;
        }

        var inputSeriesName = string.IsNullOrWhiteSpace(seriesNameBox.Text) ? null : seriesNameBox.Text.Trim();
        var inputSeriesOrder = inputSeriesName is null || seriesOrderBox.Value <= 0 ? null : (int?)seriesOrderBox.Value;
        if (string.Equals(selectedFolder.SeriesName, inputSeriesName, StringComparison.Ordinal)
            && selectedFolder.SeriesOrder == inputSeriesOrder)
        {
            return false;
        }

        selectedFolder.SeriesName = inputSeriesName;
        selectedFolder.SeriesOrder = inputSeriesOrder;
        database.SaveFolder(selectedFolder);
        statusLabel.Text = "묶음 설정 저장됨";
        LoadFolders(selectedFolder.Id);
        return selectedFolder is null;
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
        foreach (var control in new Control[] { displayNameBox, authorBox, numberBox, seriesNameBox, seriesOrderBox, scoreBox, tagsBox, memoBox, favoriteCheckBox, reservedCheckBox, saveButton, viewButton, thumbnailButton, openFolderButton, copyPathButton, deleteFolderButton })
        {
            control.Enabled = enabled;
        }
    }

    private void SetBusy(bool busy)
    {
        scanButton.Enabled = !busy;
        randomButton.Enabled = !busy;
        searchBox.Enabled = !busy;
        searchFieldComboBox.Enabled = !busy;
        sortComboBox.Enabled = !busy;
        quickFilterComboBox.Enabled = !busy;
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

    private void LoadSeriesNames()
    {
        var source = new AutoCompleteStringCollection();
        source.AddRange(database.GetSeriesNames().ToArray());
        seriesNameBox.AutoCompleteCustomSource = source;
    }

    private List<FolderItem> GetSelectedFolderItems()
    {
        return folderList.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag)
            .OfType<FolderItem>()
            .ToList();
    }

    private void AddTagsToSelectedFolders()
    {
        var selectedFolders = GetSelectedFolderItems();
        if (selectedFolders.Count == 0)
        {
            return;
        }

        var text = PromptText("태그 추가", "추가할 태그(쉼표로 구분)", "");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var tags = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        database.AddTagsToFolders(selectedFolders.Select(folder => folder.Id), tags);
        LoadTagFilters();
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = $"선택 {selectedFolders.Count}개에 태그 추가됨";
    }

    private void AssignSeriesToSelectedFolders()
    {
        var selectedFolders = GetSelectedFolderItems();
        if (selectedFolders.Count == 0)
        {
            return;
        }

        using var dialog = new SeriesAssignForm(selectedFolders);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        database.AssignSeries(dialog.SeriesName, dialog.Assignments);
        LoadSeriesNames();
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = $"묶음 저장됨: {dialog.SeriesName}";
    }

    private void GuessSeriesFromSelectedFolders()
    {
        var selectedFolders = GetSelectedFolderItems();
        if (selectedFolders.Count == 0)
        {
            return;
        }

        var guesses = selectedFolders
            .Select(folder => new SeriesGuessCandidate(folder, GuessSeries(folder)))
            .ToList();
        var inferredOrders = FillMissingSeriesOrders(guesses);
        var candidateFolders = selectedFolders
            .OrderBy(folder => inferredOrders[folder.Id])
            .ToList();

        var guessedSeriesName = GetCommonSeriesName(guesses
            .Where(item => item.Guess is not null)
            .Select(item => item.Guess!.Value.SeriesName));
        using var dialog = new SeriesAssignForm(candidateFolders, "묶음 자동 추정", guessedSeriesName);
        foreach (var item in guesses)
        {
            dialog.SetOrder(item.Folder.Id, inferredOrders[item.Folder.Id]);
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        database.AssignSeries(dialog.SeriesName, dialog.Assignments);
        LoadSeriesNames();
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = $"묶음 추정 저장됨: {dialog.SeriesName}";
    }

    private static Dictionary<long, int> FillMissingSeriesOrders(IReadOnlyList<SeriesGuessCandidate> guesses)
    {
        var result = new Dictionary<long, int>();
        var usedOrders = new HashSet<int>();
        foreach (var item in guesses.Where(item => item.Guess is not null).OrderBy(item => item.Guess!.Value.SeriesOrder))
        {
            var order = item.Guess!.Value.SeriesOrder;
            while (usedOrders.Contains(order))
            {
                order++;
            }

            result[item.Folder.Id] = order;
            usedOrders.Add(order);
        }

        var nextOrder = 1;
        foreach (var item in guesses.Where(item => item.Guess is null).OrderBy(item => item.Folder.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            while (usedOrders.Contains(nextOrder))
            {
                nextOrder++;
            }

            result[item.Folder.Id] = nextOrder;
            usedOrders.Add(nextOrder);
        }

        return result;
    }

    private static string? GetCommonSeriesName(IEnumerable<string> seriesNames)
    {
        var names = seriesNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
        {
            return null;
        }

        if (names.Count == 1)
        {
            return names[0];
        }

        var shortestName = names.OrderBy(name => name.Length).First();
        var commonLength = shortestName.Length;
        foreach (var name in names)
        {
            while (commonLength > 0 && !name.StartsWith(shortestName[..commonLength], StringComparison.OrdinalIgnoreCase))
            {
                commonLength--;
            }
        }

        var commonName = shortestName[..commonLength].Trim(' ', '-', '_', '.', '[', ']', '(', ')');
        return commonName.Length == 0 ? names[0] : commonName;
    }

    private static (string SeriesName, int SeriesOrder)? GuessSeries(FolderItem folder)
    {
        var candidates = new[]
        {
            folder.DisplayName,
            new DirectoryInfo(folder.Path).Name
        };

        foreach (var candidate in candidates)
        {
            var guess = GuessSeries(candidate);
            if (guess is not null)
            {
                return guess;
            }
        }

        return null;
    }

    private static (string SeriesName, int SeriesOrder)? GuessSeries(string text)
    {
        var trimmed = text.Trim();
        var patterns = new[]
        {
            @"^(?<name>.+?)[\s_\-]*(?:第\s*)?(?<order>\d{1,3})\s*편\s*$",
            @"^(?<name>.+?)[\s_\-]*(?:vol\.?|volume)\s*(?<order>\d{1,3})\s*$",
            @"^(?<name>.+?)[\s_\-]*\((?<order>\d{1,3})\)\s*$",
            @"^(?<name>.+?)[\s_\-]*\[(?<order>\d{1,3})\]\s*$",
            @"^(?<name>.+?)[\s_\-]+(?<order>\d{1,3})\s*$"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(trimmed, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups["order"].Value, out var order) || order <= 0)
            {
                continue;
            }

            var seriesName = match.Groups["name"].Value.Trim();
            seriesName = seriesName.Trim('-', '_', ' ', '.', '[', ']', '(', ')');
            if (seriesName.Length == 0)
            {
                continue;
            }

            return (seriesName, order);
        }

        return null;
    }

    private void EditSeriesFromSelectedFolders()
    {
        var selectedFolders = GetSelectedFolderItems();
        var existingSeriesName = selectedFolders
            .Select(folder => folder.SeriesName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        if (string.IsNullOrWhiteSpace(existingSeriesName))
        {
            AssignSeriesToSelectedFolders();
            return;
        }

        var seriesFolders = database.GetFoldersBySeries(existingSeriesName);
        var mergedFolders = seriesFolders
            .Concat(selectedFolders)
            .GroupBy(folder => folder.Id)
            .Select(group => group.First())
            .ToList();

        using var dialog = new SeriesAssignForm(mergedFolders, "묶음 편집/추가");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        database.AssignSeries(dialog.SeriesName, dialog.Assignments, existingSeriesName, clearExistingSeries: true);
        LoadSeriesNames();
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = $"묶음 수정됨: {dialog.SeriesName}";
    }

    private void ClearSeriesFromSelectedFolders()
    {
        var selectedFolders = GetSelectedFolderItems();
        if (selectedFolders.Count == 0)
        {
            return;
        }

        var foldersToClear = selectedFolders;
        var seriesNames = selectedFolders
            .Select(folder => folder.SeriesName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (tabs.SelectedTab is not null && tabs.SelectedTab.Text == "묶음 목록" && seriesNames.Count > 0)
        {
            foldersToClear = seriesNames
                .SelectMany(seriesName => database.GetFoldersBySeries(seriesName!))
                .GroupBy(folder => folder.Id)
                .Select(group => group.First())
                .ToList();
        }

        var result = MessageBox.Show(
            this,
            $"선택한 묶음 정보 {foldersToClear.Count}개를 해제합니다. 실제 폴더와 이미지는 유지됩니다.",
            "묶음 해제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        database.ClearSeries(foldersToClear.Select(folder => folder.Id));
        LoadSeriesNames();
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = $"묶음 해제됨: {foldersToClear.Count}개";
    }

    private void SetSelectedFoldersFavorite(bool value)
    {
        var selectedFolders = GetSelectedFolderItems();
        database.UpdateFoldersFlags(selectedFolders.Select(folder => folder.Id), value, null);
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = $"선택 {selectedFolders.Count}개 즐겨찾기 {(value ? "설정" : "해제")}됨";
    }

    private void SetSelectedFoldersReserved(bool value)
    {
        var selectedFolders = GetSelectedFolderItems();
        database.UpdateFoldersFlags(selectedFolders.Select(folder => folder.Id), null, value);
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = $"선택 {selectedFolders.Count}개 보류함 {(value ? "설정" : "해제")}됨";
    }

    private void DeleteSelectedFoldersFromList()
    {
        var selectedFolders = GetSelectedFolderItems();
        if (selectedFolders.Count == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"선택한 {selectedFolders.Count}개 폴더를 DB 목록에서만 제거합니다. 실제 파일은 삭제하지 않습니다.",
            "DB에서 제거",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        database.DeleteFolders(selectedFolders.Select(folder => folder.Id));
        ClearDetails();
        LoadTagFilters();
        LoadFolders(null);
        statusLabel.Text = $"선택 {selectedFolders.Count}개 DB에서 제거됨";
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

public sealed record SeriesGuessCandidate(FolderItem Folder, (string SeriesName, int SeriesOrder)? Guess);
