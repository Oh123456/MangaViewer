namespace Viewer;

public sealed class Form1 : Form
{
    private readonly AppDatabase database = new();
    private readonly FolderScanner scanner = new();

    private readonly Button scanButton = new();
    private readonly Button incomingScanButton = new();
    private readonly Button fullScanButton = new();
    private readonly Button randomButton = new();
    private readonly ComboBox searchFieldComboBox = new();
    private readonly TextBox searchBox = new();
    private readonly Button tagFilterButton = new();
    private readonly ToolStripDropDown tagFilterDropDown = new();
    private readonly CheckedListBox tagFilterListBox = new();
    private readonly Button toggleAllTagFilterButton = new();
    private readonly Button excludedTagFilterButton = new();
    private readonly ToolStripDropDown excludedTagFilterDropDown = new();
    private readonly CheckedListBox excludedTagFilterListBox = new();
    private readonly Button toggleAllExcludedTagFilterButton = new();
    private readonly ComboBox tagFilterModeComboBox = new();
    private readonly Button clearTagFilterButton = new();
    private readonly ComboBox sortComboBox = new();
    private readonly ComboBox quickFilterComboBox = new();
    private readonly TabControl mediaModeTabs = new();
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
    private readonly ToolStripDropDown tagInputSuggestionDropDown = new();
    private readonly ListBox tagInputSuggestionListBox = new();
    private readonly TextBox memoBox = new();
    private readonly TextBox pathBox = new();
    private readonly Label statsLabel = new();
    private readonly CheckBox favoriteCheckBox = new();
    private readonly CheckBox reservedCheckBox = new();
    private readonly Button saveButton = new();
    private readonly Button viewButton = new();
    private readonly Button videoViewButton = new();
    private readonly Button thumbnailButton = new();
    private readonly Button openFolderButton = new();
    private readonly Button copyPathButton = new();
    private readonly Button moveToMainRootButton = new();
    private readonly Button deleteFolderButton = new();
    private readonly Label statusLabel = new();
    private readonly Label tagFilterStatusLabel = new();
    private readonly Button firstPageButton = new();
    private readonly Button previousPageButton = new();
    private readonly Button nextPageButton = new();
    private readonly Button lastPageButton = new();
    private readonly Label pageStatusLabel = new();
    private readonly ComboBox pageSizeComboBox = new();
    private readonly ToolTip toolTip = new();
    private readonly ContextMenuStrip folderListMenu = new();
    private readonly ToolStripMenuItem moveSelectedToMainRootMenuItem = new();
    private readonly ToolStripMenuItem deleteSelectedFoldersMenuItem = new();

    private List<FolderItem> folders = [];
    private List<FolderItem> randomFolders = [];
    private readonly HashSet<string> cycleRandomUsedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> activeTagFilters = [];
    private readonly List<string> excludedTagFilters = [];
    private readonly List<string> allTagNames = [];
    private FolderItem? selectedFolder;
    private bool loadingDetails;
    private bool updatingTagFilterListBox;
    private bool suppressTabChanged;
    private bool applyingLocalization;
    private int sortedColumnIndex = -1;
    private bool sortDescending;
    private int currentPageIndex;
    private int pageSize = 500;
    private int totalFolderCount;
    private string? cycleRandomSignature;
    private CancellationTokenSource? scanCancellationTokenSource;
    private CancellationTokenSource? thumbnailLoadCancellationTokenSource;

    private readonly record struct FolderListViewport(long? TopFolderId, int TopIndex);
    private readonly record struct FolderMoveResult(int MovedCount, bool IsCanceled, Exception? Exception);

    public Form1()
    {
        BuildUi();
        ApplyLocalization();
        ApplySavedWindowPlacement();
        Shown += async (_, _) => await OnShownAsync();
        FormClosing += (_, _) => SaveWindowPlacement();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            SaveSelectedFolder();
            return true;
        }

        if (keyData == Keys.F5)
        {
            if (tabs.SelectedTab == randomTabPage)
            {
                ShowRandomFolders();
            }
            else
            {
                LoadFolders(selectedFolder?.Id, autoSelectFirst: false);
                statusLabel.Text = Localization.T("새로고침됨");
            }

            return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    private void BuildUi()
    {
        Text = Localization.T("app.title");
        AppIcons.ApplyTo(this);
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
        var settingsMenuItem = new ToolStripMenuItem(Localization.T("menu.settings"));
        settingsMenuItem.Click += (_, _) => OpenSettings();
        var randomMenuItem = new ToolStripMenuItem(Localization.T("menu.random"));
        randomMenuItem.Click += (_, _) => ShowRandomFolders();
        var updateMenuItem = new ToolStripMenuItem(Localization.T("menu.checkUpdates"));
        updateMenuItem.Click += async (_, _) => await CheckForUpdatesAsync(showNoUpdateMessage: true);
        menuStrip.Items.Add(settingsMenuItem);
        menuStrip.Items.Add(updateMenuItem);
        menuStrip.Items.Add(randomMenuItem);
        MainMenuStrip = menuStrip;

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 13,
            Padding = new Padding(8)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));

        scanButton.Text = Localization.T("toolbar.quickSync");
        scanButton.Dock = DockStyle.Fill;
        scanButton.Click += async (_, _) => await ScanAsync(ScanMode.QuickSync);
        toolTip.SetToolTip(scanButton, "기존 DB 폴더의 변경 시각을 먼저 확인해서 변경 없는 폴더는 빠르게 건너뜁니다.");

        incomingScanButton.Text = Localization.T("toolbar.incomingScan");
        incomingScanButton.Dock = DockStyle.Fill;
        incomingScanButton.Click += async (_, _) => await ScanAsync(ScanMode.QuickSync, RootKind.Incoming);
        toolTip.SetToolTip(incomingScanButton, "신규등록 루트만 스캔합니다. 전체 라이브러리 정리를 건너뛰어 첫 등록을 빠르게 처리합니다.");

        fullScanButton.Text = Localization.T("toolbar.fullScan");
        fullScanButton.Dock = DockStyle.Fill;
        fullScanButton.Click += async (_, _) => await ScanAsync(ScanMode.FullRescan);
        toolTip.SetToolTip(fullScanButton, "모든 폴더의 이미지 목록을 다시 확인합니다. 오래 걸릴 수 있습니다.");

        randomButton.Text = Localization.T("toolbar.random");
        randomButton.Dock = DockStyle.Fill;
        randomButton.Click += (_, _) => ShowRandomFolders();
        toolTip.SetToolTip(randomButton, "현재 검색/탭/태그 조건의 목록에서 무작위로 골라 보여줍니다.");

        searchFieldComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        searchFieldComboBox.Items.AddRange(["이름", "작가", "메모", "경로", "묶음"]);
        searchFieldComboBox.SelectedIndex = 0;
        searchFieldComboBox.Dock = DockStyle.Fill;
        searchFieldComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!applyingLocalization)
            {
                LoadFolders();
            }
        };
        toolTip.SetToolTip(searchFieldComboBox, "검색할 항목을 선택합니다. 기본값은 이름입니다.");

        searchBox.PlaceholderText = Localization.T("toolbar.search");
        searchBox.Dock = DockStyle.Fill;
        searchBox.TextChanged += (_, _) => LoadFolders();

        tagFilterButton.Text = Localization.T("toolbar.tag");
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

        excludedTagFilterButton.Text = Localization.T("toolbar.excludedTag");
        excludedTagFilterButton.Dock = DockStyle.Fill;
        excludedTagFilterButton.TextAlign = ContentAlignment.MiddleLeft;
        excludedTagFilterButton.Click += (_, _) => ShowExcludedTagFilterMenu();
        excludedTagFilterButton.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode == Keys.Escape)
            {
                MoveFocusAwayFromTagFilter();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
        };
        toolTip.SetToolTip(excludedTagFilterButton, "선택한 태그가 하나라도 포함된 폴더를 목록에서 제외합니다.");
        BuildExcludedTagFilterDropDown();

        tagFilterModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        tagFilterModeComboBox.Items.AddRange([Localization.T("tagMode.contains"), Localization.T("tagMode.and"), Localization.T("tagMode.or")]);
        tagFilterModeComboBox.SelectedIndex = 0;
        tagFilterModeComboBox.Dock = DockStyle.Fill;
        tagFilterModeComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (applyingLocalization)
            {
                return;
            }

            UpdateTagFilterStatus();
            LoadFolders();
        };
        toolTip.SetToolTip(tagFilterModeComboBox, "포함/AND: 선택한 태그를 모두 가진 폴더, OR: 선택한 태그 중 하나라도 가진 폴더");

        clearTagFilterButton.Text = Localization.T("toolbar.clear");
        clearTagFilterButton.Dock = DockStyle.Fill;
        clearTagFilterButton.Click += (_, _) => ClearTagFilter();
        toolTip.SetToolTip(clearTagFilterButton, "태그 필터를 모두 비우고 전체 목록을 표시합니다.");

        sortComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        sortComboBox.Items.AddRange(["날짜 순", "이름 순", "작가 순", "점수 순", "최근 본 순", "묶음 순", "이미지 수 순"]);
        sortComboBox.SelectedIndex = 0;
        sortComboBox.Dock = DockStyle.Fill;
        sortComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!applyingLocalization)
            {
                LoadFolders();
            }
        };

        quickFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        quickFilterComboBox.Items.AddRange(["전체", "미열람", "점수 없음", "태그 없음", "묶음 없음", "썸네일 없음", "깨진 경로"]);
        quickFilterComboBox.SelectedIndex = 0;
        quickFilterComboBox.Dock = DockStyle.Fill;
        quickFilterComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!applyingLocalization)
            {
                LoadFolders();
            }
        };
        toolTip.SetToolTip(quickFilterComboBox, "정리가 필요한 항목만 빠르게 필터링합니다.");

        toolbar.Controls.Add(scanButton, 0, 0);
        toolbar.Controls.Add(incomingScanButton, 1, 0);
        toolbar.Controls.Add(fullScanButton, 2, 0);
        toolbar.Controls.Add(randomButton, 3, 0);
        toolbar.Controls.Add(searchFieldComboBox, 4, 0);
        toolbar.Controls.Add(searchBox, 5, 0);
        toolbar.Controls.Add(tagFilterButton, 6, 0);
        toolbar.Controls.Add(excludedTagFilterButton, 7, 0);
        toolbar.Controls.Add(tagFilterModeComboBox, 8, 0);
        toolbar.Controls.Add(clearTagFilterButton, 9, 0);
        toolbar.Controls.Add(sortComboBox, 10, 0);
        toolbar.Controls.Add(quickFilterComboBox, 11, 0);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));

        mediaModeTabs.Dock = DockStyle.Fill;
        mediaModeTabs.Height = 32;
        mediaModeTabs.TabPages.Add(Localization.T("tabs.imageMode"));
        mediaModeTabs.TabPages.Add(Localization.T("tabs.videoMode"));
        mediaModeTabs.SelectedIndexChanged += (_, _) =>
        {
            if (suppressTabChanged)
            {
                return;
            }

            cycleRandomSignature = null;
            cycleRandomUsedKeys.Clear();
            selectedFolder = null;
            currentPageIndex = 0;
            LoadFolders(autoSelectFirst: true);
        };

        tabs.Dock = DockStyle.Fill;
        tabs.Height = 32;
        tabs.TabPages.Add(Localization.T("tabs.all"));
        tabs.TabPages.Add(Localization.T("tabs.favorites"));
        tabs.TabPages.Add(Localization.T("tabs.recent"));
        tabs.TabPages.Add(Localization.T("tabs.reserved"));
        tabs.TabPages.Add(Localization.T("tabs.series"));
        tabs.TabPages.Add(Localization.T("tabs.newRegistration"));
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (suppressTabChanged)
            {
                return;
            }

            if (tabs.SelectedTab == randomTabPage)
            {
                folders = randomFolders;
                currentPageIndex = 0;
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
        folderList.DoubleClick += (_, _) => OpenSelectedFolderItem();
        folderList.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode != Keys.Enter || !folderList.Focused)
            {
                return;
            }

            OpenSelectedFolderItem();
            keyEventArgs.Handled = true;
            keyEventArgs.SuppressKeyPress = true;
        };
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

        var pagingPanel = BuildPagingPanel();
        pagingPanel.Dock = DockStyle.Fill;
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        leftPanel.Controls.Add(mediaModeTabs, 0, 0);
        leftPanel.Controls.Add(tabs, 0, 1);
        leftPanel.Controls.Add(folderList, 0, 2);
        leftPanel.Controls.Add(pagingPanel, 0, 3);

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

    private Control BuildPagingPanel()
    {
        var pagingPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 4, 6, 4),
            WrapContents = false
        };

        firstPageButton.Text = "<<";
        previousPageButton.Text = "<";
        nextPageButton.Text = ">";
        lastPageButton.Text = ">>";
        foreach (var button in new[] { firstPageButton, previousPageButton, nextPageButton, lastPageButton })
        {
            button.Width = 42;
            button.Height = 26;
        }

        firstPageButton.Click += (_, _) => MovePage(0);
        previousPageButton.Click += (_, _) => MovePage(currentPageIndex - 1);
        nextPageButton.Click += (_, _) => MovePage(currentPageIndex + 1);
        lastPageButton.Click += (_, _) => MovePage(GetLastPageIndex());
        toolTip.SetToolTip(firstPageButton, "첫 페이지로 이동합니다.");
        toolTip.SetToolTip(previousPageButton, "이전 페이지로 이동합니다.");
        toolTip.SetToolTip(nextPageButton, "다음 페이지로 이동합니다.");
        toolTip.SetToolTip(lastPageButton, "마지막 페이지로 이동합니다.");

        pageSizeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        pageSizeComboBox.Width = 82;
        pageSizeComboBox.Items.AddRange(["200", "500", "1000", "2000"]);
        pageSizeComboBox.SelectedItem = pageSize.ToString();
        pageSizeComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (applyingLocalization)
            {
                return;
            }

            if (int.TryParse(pageSizeComboBox.SelectedItem?.ToString(), out var selectedPageSize))
            {
                pageSize = selectedPageSize;
                currentPageIndex = 0;
                selectedFolder = null;
                if (tabs.SelectedTab == randomTabPage)
                {
                    PopulateFolderList(null, autoSelectFirst: true);
                }
                else
                {
                    LoadCurrentFolderPage();
                    PopulateFolderList(null, autoSelectFirst: true);
                }
            }
        };
        toolTip.SetToolTip(pageSizeComboBox, "한 페이지에 표시할 목록 개수입니다.");

        pageStatusLabel.AutoSize = true;
        pageStatusLabel.Padding = new Padding(8, 5, 0, 0);
        pageStatusLabel.Text = "0 / 0";

        pagingPanel.Controls.AddRange([firstPageButton, previousPageButton, nextPageButton, lastPageButton, pageSizeComboBox, pageStatusLabel]);
        return pagingPanel;
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
        tagsBox.TextChanged += (_, _) => UpdateTagInputSuggestions();
        tagsBox.KeyDown += (_, keyEventArgs) => HandleTagInputSuggestionKeyDown(keyEventArgs);
        tagsBox.Leave += (_, _) => BeginInvoke(() =>
        {
            if (!tagInputSuggestionListBox.Focused)
            {
                tagInputSuggestionDropDown.Close();
            }
        });
        AddLabeledControl(panel, "태그", tagsBox, 7);
        BuildTagInputSuggestionDropDown();

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

        viewButton.Text = "이미지 보기";
        viewButton.Click += (_, _) => OpenViewer();

        videoViewButton.Text = "영상 보기";
        videoViewButton.Click += (_, _) => OpenVideoViewer();

        thumbnailButton.Text = "썸네일 선택";
        thumbnailButton.Click += (_, _) => ChooseThumbnail();

        openFolderButton.Text = "폴더 열기";
        openFolderButton.Click += (_, _) => OpenSelectedFolderInExplorer();

        copyPathButton.Text = "경로 복사";
        copyPathButton.Click += (_, _) => CopySelectedFolderPath();

        moveToMainRootButton.Text = "메인으로 이동";
        moveToMainRootButton.Click += async (_, _) => await MoveSelectedFolderToMainRootAsync();

        deleteFolderButton.Text = "DB에서 제거";
        deleteFolderButton.Click += (_, _) => DeleteSelectedFolder();

        StyleDetailButton(saveButton, "현재 상세 패널의 이름, 작가, 점수, 태그, 메모, 즐겨찾기, 보류함 상태를 DB에 저장합니다.");
        StyleDetailButton(viewButton, "선택한 폴더의 이미지를 뷰어 창으로 엽니다.");
        StyleDetailButton(videoViewButton, "선택한 폴더의 영상을 외부 플레이어로 재생하는 전용 창을 엽니다.");
        StyleDetailButton(thumbnailButton, "선택한 폴더 안의 이미지 중 하나를 목록 썸네일로 지정합니다.");
        StyleDetailButton(openFolderButton, "선택한 폴더를 파일 탐색기로 엽니다.");
        StyleDetailButton(copyPathButton, "선택한 폴더 경로를 클립보드에 복사합니다.");
        StyleDetailButton(moveToMainRootButton, "신규등록 폴더를 선택한 메인 루트 아래로 이동하고 DB 경로를 즉시 갱신합니다.");
        StyleDetailButton(deleteFolderButton, "실제 파일은 유지하고 이 폴더를 DB 목록에서만 제거합니다.");

        buttons.Controls.AddRange([saveButton, viewButton, videoViewButton, thumbnailButton, openFolderButton, copyPathButton, moveToMainRootButton, deleteFolderButton]);
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
        moveSelectedToMainRootMenuItem.Text = "선택 항목 메인으로 이동";
        moveSelectedToMainRootMenuItem.Click += async (_, _) => await MoveSelectedFoldersToMainRootAsync();
        folderListMenu.Items.Add(moveSelectedToMainRootMenuItem);
        folderListMenu.Items.Add(new ToolStripSeparator());
        deleteSelectedFoldersMenuItem.Text = "선택 항목 DB에서 제거";
        deleteSelectedFoldersMenuItem.Click += (_, _) => DeleteSelectedFoldersFromList();
        folderListMenu.Items.Add(deleteSelectedFoldersMenuItem);
        folderListMenu.Opening += (_, cancelEventArgs) =>
        {
            cancelEventArgs.Cancel = folderList.SelectedItems.Count == 0;
            var isNewRegistration = GetCurrentFolderListMode() == FolderListMode.NewRegistration;
            moveSelectedToMainRootMenuItem.Visible = isNewRegistration;
            deleteSelectedFoldersMenuItem.Text = isNewRegistration ? "선택 항목 폴더 삭제" : "선택 항목 DB에서 제거";
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
        var pageStart = totalFolderCount == 0 ? 0 : currentPageIndex * pageSize + 1;
        var pageEnd = totalFolderCount == 0 ? 0 : Math.Min(totalFolderCount, currentPageIndex * pageSize + folders.Count);
        statusLabel.Text = $"{sortText} {directionText} / 표시 {pageStart:N0}-{pageEnd:N0} / 전체 {totalFolderCount:N0}개{selectedText}";
    }

    private async Task OnShownAsync()
    {
        statusLabel.Text = "초기 데이터를 불러오는 중...";
        await Task.Yield();

        database.Initialize();
        LoadTagFilters();
        LoadSeriesNames();
        LoadFolders(autoSelectFirst: false);
        var rootCount = database.GetRoots(mediaKind: GetCurrentMediaKind()).Count;
        statusLabel.Text = rootCount == 0
            ? "루트 폴더를 추가한 뒤 스캔/동기화를 실행하세요."
            : $"루트 {rootCount}개 등록됨";
        if (AppSettings.Current.AutoCheckForUpdates)
        {
            _ = CheckForUpdatesAsync(showNoUpdateMessage: false);
        }
    }

    private async Task CheckForUpdatesAsync(bool showNoUpdateMessage)
    {
        var result = await UpdateService.CheckLatestAsync();
        if (!result.IsConfigured)
        {
            if (showNoUpdateMessage)
            {
                MessageBox.Show(this, result.ErrorMessage, Localization.T("업데이트 확인"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            if (showNoUpdateMessage)
            {
                MessageBox.Show(this, result.ErrorMessage, Localization.T("업데이트 확인 실패"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (!result.HasUpdate)
        {
            if (showNoUpdateMessage)
            {
                MessageBox.Show(this, string.Format(Localization.T("현재 최신 버전입니다.\n\n현재 버전: {0}"), result.CurrentVersion), Localization.T("업데이트 확인"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return;
        }

        using var updatePrompt = new UpdatePromptForm(result);
        var dialogResult = updatePrompt.ShowDialog(this);
        if (dialogResult == DialogResult.Yes)
        {
            await DownloadOrOpenUpdateAsync(result);
        }
    }

    private async Task DownloadOrOpenUpdateAsync(UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(result.AssetDownloadUrl))
        {
            if (!string.IsNullOrWhiteSpace(result.ReleasePageUrl))
            {
                UpdateService.OpenReleasePage(result.ReleasePageUrl);
            }

            return;
        }

        using var progressForm = new ScanProgressForm(() => { }) { Text = Localization.T("업데이트 다운로드") };
        try
        {
            progressForm.Show(this);
            var progress = new Progress<string>(message => progressForm.UpdateStatus(message));
            var downloadPath = await UpdateService.DownloadUpdateAsync(result, progress);
            progressForm.Close();

            var openResult = MessageBox.Show(
                this,
                string.Format(Localization.T("업데이트 파일을 다운로드했습니다.\n\n{0}\n\n지금 업데이트를 적용할까요?\n앱이 종료된 뒤 업데이트 후 다시 실행됩니다."), downloadPath),
                Localization.T("업데이트 다운로드"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (openResult == DialogResult.Yes)
            {
                UpdateService.LaunchUpdater(downloadPath);
                Application.Exit();
                return;
            }

            UpdateService.OpenUpdatesFolder();
        }
        catch (Exception exception)
        {
            progressForm.Close();
            MessageBox.Show(this, exception.Message, Localization.T("업데이트 다운로드 실패"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ScanAsync(ScanMode scanMode, RootKind? rootKind = null)
    {
        var mediaKind = GetCurrentMediaKind();
        var roots = database.GetRoots(rootKind, mediaKind);
        if (roots.Count == 0)
        {
            var mediaText = mediaKind == MediaKind.Video ? "영상" : "이미지";
            var rootMessage = rootKind == RootKind.Incoming
                ? $"먼저 {mediaText} 신규등록 루트 폴더를 추가하세요."
                : $"먼저 {mediaText} 루트 폴더를 추가하세요.";
            MessageBox.Show(this, rootMessage, "스캔", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        scanCancellationTokenSource?.Dispose();
        scanCancellationTokenSource = new CancellationTokenSource();
        var scanLog = new ScanLog();
        using var progressForm = new ScanProgressForm(CancelScan);
        var modeText = rootKind == RootKind.Incoming
            ? "신규등록 스캔"
            : scanMode == ScanMode.QuickSync ? "빠른 동기화" : "전체 스캔";
        var progress = new Progress<ScanProgress>(scanProgress =>
        {
            var stageText = string.IsNullOrWhiteSpace(scanProgress.Stage) ? modeText : scanProgress.Stage;
            var statusText = FormatScanProgressText(stageText, scanProgress);
            statusLabel.Text = statusText;
            progressForm.UpdateStatus(statusText);
        });
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("기존 DB 상태 확인 중...");
            var databaseStateStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var existingSignatureMap = await Task.Run(() =>
            {
                scanCancellationTokenSource.Token.ThrowIfCancellationRequested();
                return database.GetFolderScanSignatureMap(rootKind, mediaKind);
            }, scanCancellationTokenSource.Token);
            scanLog.Add($"기존 DB 상태 확인 완료: {existingSignatureMap.Count}개 / {databaseStateStopwatch.Elapsed:mm\\:ss\\.fff}");

            progressForm.UpdateStatus($"{modeText} 중...");
            using var scanWriteSession = database.BeginScanWriteSession();
            var summary = await scanner.ScanStreamingAsync(
                roots,
                scanMode,
                existingSignatureMap,
                result =>
                {
                    return !existingSignatureMap.TryGetValue(result.FolderPath, out var existingSignature)
                        || existingSignature.DirectoryModifiedAt is null
                        || result.DirectoryModifiedAt > existingSignature.DirectoryModifiedAt.Value
                        || result.FolderModifiedAt > existingSignature.FolderModifiedAt
                        || result.ImageCount != existingSignature.ImageCount
                        || result.TotalImageBytes != existingSignature.TotalImageBytes
                        || result.VideoCount != existingSignature.VideoCount
                        || result.TotalVideoBytes != existingSignature.TotalVideoBytes;
                },
                result => scanWriteSession.Save(result),
                progress,
                scanLog,
                scanCancellationTokenSource.Token);
            scanWriteSession.Commit();
            scanLog.Add($"변경분 DB 저장 완료: 저장 {summary.SavedFolders}개 / 변경 없음 {summary.SkippedFolders}개");

            var removedLegacyVideoFolders = database.RemoveLegacyAggregateVideoFolders(rootKind, mediaKind);
            if (removedLegacyVideoFolders > 0)
            {
                scanLog.Add($"기존 영상 폴더 묶음 정리 완료: {removedLegacyVideoFolders}개");
            }

            progressForm.UpdateStatus("누락 폴더 정리 중...");
            scanLog.Add("누락 폴더 정리 시작");
            var cleanupStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var cleanupSummary = await Task.Run(() =>
            {
                scanCancellationTokenSource.Token.ThrowIfCancellationRequested();
                return database.RemoveMissingFoldersAndImages(checkImageFiles: false, rootKind: rootKind, mediaKind: mediaKind);
            }, scanCancellationTokenSource.Token);
            scanLog.Add($"누락 폴더 정리 완료: 삭제 폴더 {cleanupSummary.RemovedFolders}개 / 삭제 이미지 {cleanupSummary.RemovedImages}개 / {cleanupStopwatch.Elapsed:mm\\:ss\\.fff}");
            summary.RemovedFolders = cleanupSummary.RemovedFolders;
            summary.RemovedImages = cleanupSummary.RemovedImages;
            if (AppSettings.Current.AutoRefreshPathStatusAfterScan && rootKind is null)
            {
                progressForm.UpdateStatus("경로 확인 캐시 갱신 중...");
                scanLog.Add("경로 확인 캐시 갱신 시작");
                var pathRefreshStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var pathProgress = new Progress<string>(message => progressForm.UpdateStatus(message));
                var missingPathCount = await Task.Run(
                    () => database.RefreshFolderPathStatus(pathProgress, scanCancellationTokenSource.Token),
                    scanCancellationTokenSource.Token);
                scanLog.Add($"경로 확인 캐시 갱신 완료: 깨진 경로 {missingPathCount}개 / {pathRefreshStopwatch.Elapsed:mm\\:ss\\.fff}");
            }
            else if (rootKind is not null)
            {
                scanLog.Add("루트 전용 스캔이므로 전체 경로 확인 캐시 갱신 생략");
            }

            var summaryText = FormatScanSummaryText(modeText, summary, totalStopwatch.Elapsed);
            scanLog.Add(summaryText);
            LoadTagFilters();
            await Task.Yield();
            if (rootKind == RootKind.Incoming && tabs.TabPages.Count > 5)
            {
                tabs.SelectedTab = tabs.TabPages[5];
            }

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
        statusLabel.Text = Localization.T("스캔 취소 요청 중...");
    }

    private static string FormatScanProgressText(string stageText, ScanProgress scanProgress)
    {
        return string.Format(
            Localization.T("scan.progress"),
            Localization.T(stageText),
            scanProgress.FoldersVisited,
            scanProgress.ImageFoldersFound,
            scanProgress.SavedFolders,
            scanProgress.SkippedFolders);
    }

    private static string FormatScanSummaryText(string modeText, ScanSummary summary, TimeSpan elapsed)
    {
        return string.Format(
            Localization.T("scan.summary"),
            Localization.T(modeText),
            summary.ImageFoldersFound,
            summary.SavedFolders,
            summary.SkippedFolders,
            summary.RemovedFolders,
            summary.RemovedImages,
            elapsed.ToString(@"mm\:ss"));
    }

    private void LoadFolders(long? folderIdToSelect = null, bool autoSelectFirst = true)
    {
        var selectedId = folderIdToSelect ?? selectedFolder?.Id;
        if (tabs.SelectedTab == randomTabPage)
        {
            RefreshRandomFolders(selectedId, autoSelectFirst);
            return;
        }

        if (folderIdToSelect is null)
        {
            currentPageIndex = 0;
        }

        LoadCurrentFolderPage();
        PopulateFolderList(selectedId, autoSelectFirst);
    }

    private void RefreshRandomFolders(long? selectedId, bool autoSelectFirst)
    {
        if (randomFolders.Count == 0)
        {
            folders = [];
            totalFolderCount = 0;
            PopulateFolderList(selectedId, autoSelectFirst);
            return;
        }

        var randomOrder = randomFolders
            .Select((folder, order) => new { folder.Id, Order = order })
            .ToDictionary(item => item.Id, item => item.Order);
        var refreshedFolders = database
            .GetFoldersByIds(randomOrder.Keys.ToList())
            .Where(folder => randomOrder.ContainsKey(folder.Id))
            .OrderBy(folder => randomOrder[folder.Id])
            .ToList();

        randomFolders = refreshedFolders;
        folders = randomFolders;
        totalFolderCount = folders.Count;
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
            5 when tabs.SelectedTab != randomTabPage => FolderListMode.NewRegistration,
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
        var tagFilterMode = GetCurrentTagFilterMode();
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

        var result = database.GetFolders(mode, sortMode, searchField, searchBox.Text, activeTagFilters, excludedTagFilters, tagFilterMode, quickFilterMode, IsVideoMode());
        return result;
    }

    private void LoadCurrentFolderPage()
    {
        var searchField = GetCurrentSearchField();
        var mode = GetCurrentFolderListMode();
        var sortMode = GetCurrentSortMode();
        var tagFilterMode = GetCurrentTagFilterMode();
        var quickFilterMode = GetCurrentQuickFilterMode();
        var result = database.GetFoldersPage(
            mode,
            sortMode,
            searchField,
            searchBox.Text,
            activeTagFilters,
            excludedTagFilters,
            tagFilterMode,
            quickFilterMode,
            currentPageIndex * pageSize,
            pageSize,
            sortDescending,
            IsVideoMode());
        if (result.TotalCount > 0 && result.Items.Count == 0 && currentPageIndex > 0)
        {
            currentPageIndex = Math.Max(0, (result.TotalCount - 1) / pageSize);
            result = database.GetFoldersPage(
                mode,
                sortMode,
                searchField,
                searchBox.Text,
                activeTagFilters,
                excludedTagFilters,
                tagFilterMode,
                quickFilterMode,
                currentPageIndex * pageSize,
                pageSize,
                sortDescending,
                IsVideoMode());
        }

        folders = result.Items;
        totalFolderCount = result.TotalCount;
    }

    private FolderSearchField GetCurrentSearchField()
    {
        return searchFieldComboBox.SelectedIndex switch
        {
            1 => FolderSearchField.Author,
            2 => FolderSearchField.Memo,
            3 => FolderSearchField.Path,
            4 => FolderSearchField.Series,
            _ => FolderSearchField.Name
        };
    }

    private FolderListMode GetCurrentFolderListMode()
    {
        return tabs.SelectedIndex switch
        {
            1 when tabs.SelectedTab != randomTabPage => FolderListMode.Favorites,
            2 when tabs.SelectedTab != randomTabPage => FolderListMode.Recent,
            3 when tabs.SelectedTab != randomTabPage => FolderListMode.Reserved,
            4 when tabs.SelectedTab != randomTabPage => FolderListMode.Series,
            5 when tabs.SelectedTab != randomTabPage => FolderListMode.NewRegistration,
            _ => FolderListMode.All
        };
    }

    private FolderSortMode GetCurrentSortMode()
    {
        if (GetCurrentFolderListMode() == FolderListMode.Recent)
        {
            return FolderSortMode.Recent;
        }

        return sortComboBox.SelectedIndex switch
        {
            1 => FolderSortMode.Name,
            2 => FolderSortMode.Author,
            3 => FolderSortMode.Score,
            4 => FolderSortMode.Recent,
            5 => FolderSortMode.Series,
            6 => FolderSortMode.ImageCount,
            _ => FolderSortMode.Date
        };
    }

    private QuickFilterMode GetCurrentQuickFilterMode()
    {
        return quickFilterComboBox.SelectedIndex switch
        {
            1 => QuickFilterMode.Unviewed,
            2 => QuickFilterMode.NoScore,
            3 => QuickFilterMode.NoTags,
            4 => QuickFilterMode.NoSeries,
            5 => QuickFilterMode.NoThumbnail,
            6 => QuickFilterMode.BrokenPath,
            _ => QuickFilterMode.All
        };
    }

    private TagFilterMode GetCurrentTagFilterMode()
    {
        return tagFilterModeComboBox.SelectedIndex switch
        {
            1 => TagFilterMode.And,
            2 => TagFilterMode.Or,
            _ => TagFilterMode.Contains
        };
    }

    private bool IsVideoMode()
    {
        return mediaModeTabs.SelectedIndex == 1;
    }

    private MediaKind GetCurrentMediaKind()
    {
        return IsVideoMode() ? MediaKind.Video : MediaKind.Image;
    }

    private void PopulateFolderList(long? selectedId, bool autoSelectFirst)
    {
        var viewport = CaptureFolderListViewport();
        if (selectedId is not null)
        {
            var selectedIndex = folders.FindIndex(folder => folder.Id == selectedId.Value);
            if (selectedIndex >= 0)
            {
                currentPageIndex = tabs.SelectedTab == randomTabPage ? selectedIndex / pageSize : currentPageIndex;
            }
        }

        ClampCurrentPage();
        var visibleFolders = GetCurrentPageFolders();
        ApplyListModeChrome();
        folderList.BeginUpdate();
        folderList.Items.Clear();
        foreach (var folder in visibleFolders)
        {
            var item = new ListViewItem();
            item.Tag = folder;
            UpdateFolderListItem(item, folder);
            folderList.Items.Add(item);
        }

        folderList.EndUpdate();
        UpdatePagingControls();
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
            if (selectedItem.Tag is FolderItem selectedFolderItem)
            {
                ShowFolder(selectedFolderItem);
            }

            RestoreFolderListViewport(viewport);
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

    private void RefreshFolderListItem(FolderItem folder)
    {
        var item = folderList.Items
            .Cast<ListViewItem>()
            .FirstOrDefault(listItem => listItem.Tag is FolderItem itemFolder && itemFolder.Id == folder.Id);
        if (item is null)
        {
            return;
        }

        UpdateFolderListItem(item, folder);
        UpdateListStatus();
    }

    private void ApplyListModeChrome()
    {
        if (folderList.Columns.Count <= 7)
        {
            return;
        }

        folderList.Columns[7].Text = IsVideoMode() ? "영상" : "이미지";
        if (sortComboBox.Items.Count > 6)
        {
            sortComboBox.Items[6] = IsVideoMode() ? "영상 수 순" : "이미지 수 순";
        }
    }

    private void UpdateFolderListItem(ListViewItem item, FolderItem folder)
    {
        item.Text = folder.DisplayName;
        SetListSubItem(item, 1, folder.Author ?? "");
        SetListSubItem(item, 2, folder.TagSummary);
        SetListSubItem(item, 3, folder.Score.ToString());
        SetListSubItem(item, 4, folder.SeriesName ?? "");
        SetListSubItem(item, 5, folder.SeriesOrder?.ToString() ?? "");
        SetListSubItem(item, 6, Shorten(folder.Memo, 80));
        SetListSubItem(item, 7, IsVideoMode() ? folder.VideoCount.ToString() : folder.ImageCount.ToString());
        SetListSubItem(item, 8, folder.FolderModifiedAt?.ToString("yyyy-MM-dd") ?? "");
        item.BackColor = string.IsNullOrWhiteSpace(folder.SeriesName)
            ? folderList.BackColor
            : Color.FromArgb(245, 250, 255);
    }

    private static void SetListSubItem(ListViewItem item, int index, string text)
    {
        while (item.SubItems.Count <= index)
        {
            item.SubItems.Add("");
        }

        item.SubItems[index].Text = text;
    }

    private FolderListViewport CaptureFolderListViewport()
    {
        if (folderList.Items.Count == 0)
        {
            return new FolderListViewport(null, 0);
        }

        try
        {
            var topItem = folderList.TopItem;
            long? topFolderId = topItem?.Tag is FolderItem folder ? folder.Id : null;
            return new FolderListViewport(topFolderId, topItem?.Index ?? 0);
        }
        catch
        {
            return new FolderListViewport(null, 0);
        }
    }

    private void RestoreFolderListViewport(FolderListViewport viewport)
    {
        if (folderList.Items.Count == 0)
        {
            return;
        }

        ListViewItem? topItem = null;
        if (viewport.TopFolderId is not null)
        {
            topItem = folderList.Items
                .Cast<ListViewItem>()
                .FirstOrDefault(item => item.Tag is FolderItem folder && folder.Id == viewport.TopFolderId.Value);
        }

        topItem ??= folderList.Items[Math.Clamp(viewport.TopIndex, 0, folderList.Items.Count - 1)];
        try
        {
            folderList.TopItem = topItem;
        }
        catch
        {
            topItem.EnsureVisible();
        }
    }

    private List<FolderItem> GetCurrentPageFolders()
    {
        if (folders.Count == 0)
        {
            return [];
        }

        if (tabs.SelectedTab == randomTabPage)
        {
            return folders
                .Skip(currentPageIndex * pageSize)
                .Take(pageSize)
                .ToList();
        }

        return folders;
    }

    private void MovePage(int pageIndex)
    {
        currentPageIndex = Math.Clamp(pageIndex, 0, GetLastPageIndex());
        selectedFolder = null;
        if (tabs.SelectedTab != randomTabPage)
        {
            LoadCurrentFolderPage();
        }

        PopulateFolderList(null, autoSelectFirst: true);
    }

    private int GetLastPageIndex()
    {
        var count = tabs.SelectedTab == randomTabPage
            ? folders.Count
            : totalFolderCount;
        if (count == 0)
        {
            return 0;
        }

        return (count - 1) / pageSize;
    }

    private void ClampCurrentPage()
    {
        currentPageIndex = Math.Clamp(currentPageIndex, 0, GetLastPageIndex());
    }

    private void UpdatePagingControls()
    {
        var count = tabs.SelectedTab == randomTabPage
            ? folders.Count
            : totalFolderCount;
        var totalPages = GetLastPageIndex() + 1;
        if (count == 0)
        {
            totalPages = 0;
        }

        var pageStart = count == 0 ? 0 : currentPageIndex * pageSize + 1;
        var pageEnd = count == 0 ? 0 : Math.Min(count, currentPageIndex * pageSize + GetCurrentPageFolders().Count);
        pageStatusLabel.Text = totalPages == 0
            ? "0 / 0"
            : $"{currentPageIndex + 1} / {totalPages}  ({pageStart:N0}-{pageEnd:N0} / {count:N0})";

        firstPageButton.Enabled = currentPageIndex > 0;
        previousPageButton.Enabled = currentPageIndex > 0;
        nextPageButton.Enabled = currentPageIndex < GetLastPageIndex();
        lastPageButton.Enabled = currentPageIndex < GetLastPageIndex();
    }

    private void ShowRandomFolders()
    {
        var mediaName = IsVideoMode() ? "영상" : "이미지";
        var tagFilterMode = GetCurrentTagFilterMode();
        var quickFilterMode = GetCurrentQuickFilterMode();
        var allCandidates = database.GetFolders(
            GetCurrentFolderListMode(),
            GetCurrentSortMode(),
            GetCurrentSearchField(),
            searchBox.Text,
            activeTagFilters,
            excludedTagFilters,
            tagFilterMode,
            quickFilterMode,
            IsVideoMode());
        if (allCandidates.Count == 0)
        {
            MessageBox.Show(this, $"랜덤으로 고를 {mediaName} 폴더가 없습니다.", "랜덤", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var seriesNames = allCandidates
            .Select(folder => folder.SeriesName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var seriesImageCounts = database.GetSeriesImageCounts(seriesNames);
        var firstSeriesFolders = database.GetFirstFoldersInSeries(seriesNames, IsVideoMode());
        var baseCandidates = BuildRandomCandidates(allCandidates, seriesImageCounts, firstSeriesFolders, 0, null, IsVideoMode());
        var candidateCount = baseCandidates.Count;
        if (candidateCount == 0)
        {
            MessageBox.Show(this, $"랜덤으로 고를 {mediaName} 폴더가 없습니다.", "랜덤", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new RandomRecommendForm(
            candidateCount,
            AppSettings.Current.RandomRecommendCount,
            AppSettings.Current.RandomRecommendMinImageCount,
            AppSettings.Current.RandomRecommendMaxImageCount,
            AppSettings.Current.RandomRecommendCycleEnabled);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        AppSettings.Current.RandomRecommendCount = dialog.RecommendCount;
        AppSettings.Current.RandomRecommendMinImageCount = dialog.MinImageCount;
        AppSettings.Current.RandomRecommendMaxImageCount = dialog.MaxImageCount ?? 0;
        AppSettings.Current.RandomRecommendCycleEnabled = dialog.CycleRandomEnabled;
        AppSettings.Save();
        if (dialog.CycleResetRequested)
        {
            cycleRandomSignature = null;
            cycleRandomUsedKeys.Clear();
        }

        var filteredCandidates = BuildRandomCandidates(allCandidates, seriesImageCounts, firstSeriesFolders, dialog.MinImageCount, dialog.MaxImageCount, IsVideoMode());
        if (filteredCandidates.Count == 0)
        {
            MessageBox.Show(this, $"{mediaName} 개수 조건에 맞는 랜덤 후보가 없습니다.", "랜덤", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var candidatePool = filteredCandidates;
        var cycleText = "";
        if (dialog.CycleRandomEnabled)
        {
            var nextSignature = BuildCycleRandomSignature(filteredCandidates);
            if (!string.Equals(cycleRandomSignature, nextSignature, StringComparison.Ordinal))
            {
                cycleRandomSignature = nextSignature;
                cycleRandomUsedKeys.Clear();
            }

            candidatePool = filteredCandidates
                .Where(folder => !cycleRandomUsedKeys.Contains(GetCycleRandomKey(folder)))
                .ToList();
            if (candidatePool.Count == 0)
            {
                cycleRandomUsedKeys.Clear();
                candidatePool = filteredCandidates;
            }
        }

        var targetCount = Math.Min(dialog.RecommendCount, candidatePool.Count);
        var recommendedFolders = candidatePool
            .OrderBy(_ => Random.Shared.Next())
            .Take(targetCount)
            .ToList();

        if (dialog.CycleRandomEnabled)
        {
            var resetAfterThisPick = candidatePool.Count <= dialog.RecommendCount;
            if (resetAfterThisPick)
            {
                cycleRandomUsedKeys.Clear();
                cycleText = $" / {Localization.T("순회 초기화")}";
            }
            else
            {
                foreach (var recommendedFolder in recommendedFolders)
                {
                    cycleRandomUsedKeys.Add(GetCycleRandomKey(recommendedFolder));
                }

                var remainingCount = filteredCandidates.Count - cycleRandomUsedKeys.Count;
                cycleText = $" / {string.Format(Localization.T("순회 남음 {0}개"), remainingCount.ToString("N0"))}";
            }
        }

        folders = recommendedFolders;
        randomFolders = folders;
        totalFolderCount = folders.Count;

        suppressTabChanged = true;
        if (!tabs.TabPages.Contains(randomTabPage))
        {
            tabs.TabPages.Add(randomTabPage);
        }

        tabs.SelectedTab = randomTabPage;
        suppressTabChanged = false;

        selectedFolder = null;
        PopulateFolderList(null, autoSelectFirst: true);
        var maxImageText = dialog.MaxImageCount is null ? "제한 없음" : dialog.MaxImageCount.Value.ToString("N0");
        var visibleCandidateCount = dialog.CycleRandomEnabled ? candidatePool.Count : filteredCandidates.Count;
        statusLabel.Text = $"랜덤 추천 {folders.Count}개 / 후보 {visibleCandidateCount:N0}개 / {mediaName} {dialog.MinImageCount:N0}-{maxImageText}{cycleText}";
    }

    private static string BuildCycleRandomSignature(IReadOnlyList<FolderItem> candidates)
    {
        var hashCode = new HashCode();
        foreach (var candidateKey in candidates.Select(GetCycleRandomKey).Order(StringComparer.OrdinalIgnoreCase))
        {
            hashCode.Add(candidateKey, StringComparer.OrdinalIgnoreCase);
        }

        return $"{candidates.Count}:{hashCode.ToHashCode()}";
    }

    private static string GetCycleRandomKey(FolderItem folder)
    {
        return string.IsNullOrWhiteSpace(folder.SeriesName)
            ? $"folder:{folder.Id}"
            : $"series:{folder.SeriesName.Trim()}";
    }

    private static List<FolderItem> BuildRandomCandidates(
        IReadOnlyList<FolderItem> candidates,
        IReadOnlyDictionary<string, int> seriesImageCounts,
        IReadOnlyDictionary<string, FolderItem> firstSeriesFolders,
        int minImageCount,
        int? maxImageCount,
        bool videoMode)
    {
        var result = new List<FolderItem>();
        var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var seriesName = candidate.SeriesName;
            var imageCount = videoMode
                ? candidate.VideoCount
                : string.IsNullOrWhiteSpace(seriesName)
                    ? candidate.ImageCount
                    : seriesImageCounts.GetValueOrDefault(seriesName, candidate.ImageCount);
            if (imageCount < minImageCount || (maxImageCount is not null && imageCount > maxImageCount.Value))
            {
                continue;
            }

            var key = string.IsNullOrWhiteSpace(seriesName) ? $"folder:{candidate.Id}" : $"series:{seriesName.Trim()}";
            if (!addedKeys.Add(key))
            {
                continue;
            }

            result.Add(!string.IsNullOrWhiteSpace(seriesName) && firstSeriesFolders.TryGetValue(seriesName, out var firstFolder)
                ? firstFolder
                : candidate);
        }

        return result;
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
        var isVideoMode = IsVideoMode();
        deleteFolderButton.Text = Localization.T(GetCurrentFolderListMode() == FolderListMode.NewRegistration ? "폴더 삭제" : "DB에서 제거");
        moveToMainRootButton.Visible = GetCurrentFolderListMode() == FolderListMode.NewRegistration;
        moveToMainRootButton.Enabled = moveToMainRootButton.Visible;
        viewButton.Visible = !isVideoMode;
        thumbnailButton.Visible = !isVideoMode;
        videoViewButton.Visible = isVideoMode;
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
        var videos = database.GetVideos(folder.Id).Where(video => File.Exists(video.Path)).ToList();
        var lastImageName = string.IsNullOrWhiteSpace(folder.LastImagePath) ? "-" : Path.GetFileName(folder.LastImagePath);
        var seriesText = GetSeriesStatsText(folder);
        statsLabel.Text = isVideoMode
            ? $"영상 {videos.Count:N0}개 / 용량 {FormatByteSize(folder.TotalVideoBytes)}"
            : string.Format(
                Localization.T("detail.stats"),
                folder.ImageCount,
                folder.ViewCount,
                seriesText,
                folder.LastViewedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-",
                lastImageName);
        videoViewButton.Enabled = videos.Count > 0;
        LoadThumbnailAsync(folder);
        loadingDetails = false;
    }

    private string GetSeriesStatsText(FolderItem folder)
    {
        if (string.IsNullOrWhiteSpace(folder.SeriesName))
        {
            return "";
        }

        var orderText = folder.SeriesOrder?.ToString() ?? "-";
        var maxOrder = database.GetSeriesMaxOrder(folder.SeriesName);
        var maxOrderText = maxOrder <= 0 ? "-" : maxOrder.ToString();
        return string.Format(Localization.T("detail.seriesStats"), folder.SeriesName, orderText, maxOrderText);
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
        var isVideoMode = IsVideoMode();
        viewButton.Visible = !isVideoMode;
        thumbnailButton.Visible = !isVideoMode;
        videoViewButton.Visible = isVideoMode;
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
        RefreshFolderListItem(selectedFolder);
        ShowFolder(selectedFolder);
    }

    private void OpenSelectedFolderInExplorer()
    {
        if (selectedFolder is null || !EntryPathExists(selectedFolder.Path))
        {
            MessageBox.Show(this, "열 수 있는 폴더가 없습니다.", "폴더 열기", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var explorerArguments = Directory.Exists(selectedFolder.Path)
            ? $"\"{selectedFolder.Path}\""
            : $"/select,\"{selectedFolder.Path}\"";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = explorerArguments,
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

    private async Task MoveSelectedFolderToMainRootAsync()
    {
        if (selectedFolder is null)
        {
            return;
        }

        await MoveFoldersToMainRootAsync([selectedFolder]);
    }

    private async Task MoveSelectedFoldersToMainRootAsync()
    {
        var selectedFolders = GetSelectedFolderItems();
        if (selectedFolders.Count == 0)
        {
            return;
        }

        if (GetCurrentFolderListMode() != FolderListMode.NewRegistration)
        {
            MessageBox.Show(this, "메인 이동은 신규등록 탭에서만 사용할 수 있습니다.", "메인으로 이동", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await MoveFoldersToMainRootAsync(selectedFolders);
    }

    private async Task MoveFoldersToMainRootAsync(IReadOnlyList<FolderItem> targetFolders)
    {
        var movableFolders = targetFolders.Where(folder => Directory.Exists(folder.Path)).ToList();
        if (movableFolders.Count == 0)
        {
            MessageBox.Show(this, "메인 이동은 폴더 항목만 사용할 수 있습니다.", "메인으로 이동", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var mainRoots = database.GetRoots(RootKind.Main, GetCurrentMediaKind()).Where(Directory.Exists).ToList();
        if (mainRoots.Count == 0)
        {
            MessageBox.Show(this, "이동할 메인 루트가 없습니다. 설정에서 메인 루트를 추가하세요.", "메인으로 이동", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var targetRoot = PromptRoot("메인으로 이동", "이동할 메인 루트", mainRoots);
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            return;
        }

        var movePlans = movableFolders
            .Select(folder => (Folder: folder, TargetPath: Path.Combine(targetRoot, new DirectoryInfo(folder.Path).Name)))
            .ToList();
        var conflicts = movePlans.Where(plan => Directory.Exists(plan.TargetPath) || File.Exists(plan.TargetPath)).ToList();
        if (conflicts.Count > 0)
        {
            var preview = string.Join(Environment.NewLine, conflicts.Take(5).Select(plan => plan.TargetPath));
            MessageBox.Show(this, $"대상 경로가 이미 존재하는 항목이 있어 이동을 중단합니다.\n\n{preview}", "메인으로 이동", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show(this, $"선택한 폴더 {movePlans.Count}개를 메인 루트로 이동하고 DB 경로를 즉시 갱신합니다.\n\n대상: {targetRoot}", "메인으로 이동", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        using var cancellationTokenSource = new CancellationTokenSource();
        using var progressForm = new ScanProgressForm(cancellationTokenSource.Cancel)
        {
            Text = "메인으로 이동"
        };
        IProgress<(int Completed, int Total, string Name)> progress = new Progress<(int Completed, int Total, string Name)>(moveProgress =>
        {
            var statusText = $"메인으로 이동 중... {moveProgress.Completed} / {moveProgress.Total}\n{moveProgress.Name}";
            statusLabel.Text = statusText.Replace(Environment.NewLine, " ");
            progressForm.UpdateStatus(statusText);
        });

        SetBusy(true);
        SetDetailsEnabled(false);
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus($"메인으로 이동 준비 중... 0 / {movePlans.Count}");
            var moveResult = await Task.Run(() =>
            {
                var movedCount = 0;
                foreach (var plan in movePlans)
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        return new FolderMoveResult(movedCount, IsCanceled: true, Exception: null);
                    }

                    try
                    {
                        var oldPath = plan.Folder.Path;
                        Directory.Move(oldPath, plan.TargetPath);
                        database.UpdatePathPrefix(oldPath, plan.TargetPath);
                        movedCount++;
                        progress.Report((movedCount, movePlans.Count, plan.Folder.DisplayName));
                    }
                    catch (Exception exception)
                    {
                        return new FolderMoveResult(movedCount, IsCanceled: false, Exception: exception);
                    }
                }

                return new FolderMoveResult(movedCount, IsCanceled: false, Exception: null);
            });

            ClearDetails();
            LoadFolders(null, autoSelectFirst: false);

            if (moveResult.IsCanceled)
            {
                statusLabel.Text = $"메인 이동 취소됨: {moveResult.MovedCount}개";
                return;
            }

            if (moveResult.Exception is not null)
            {
                MessageBox.Show(this, $"{moveResult.MovedCount}개 이동 후 실패했습니다.\n\n{moveResult.Exception.Message}", "메인으로 이동 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            statusLabel.Text = $"메인으로 이동됨: {moveResult.MovedCount}개";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "메인으로 이동 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LoadFolders(null, autoSelectFirst: false);
        }
        finally
        {
            progressForm.Close();
            SetBusy(false);
        }
    }

    private void OpenSelectedFolderItem()
    {
        if (IsVideoMode())
        {
            OpenVideoViewer();
            return;
        }

        OpenViewer();
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
        using var viewer = new ImageViewerForm(database, images, 0, isSeriesViewer);
        viewer.ShowDialog(this);
        database.UpdateLastImagePath(selectedFolder.Id, viewer.CurrentImagePath);
        RefreshFolderFromDatabase(selectedFolder.Id);
    }

    private void OpenVideoViewer()
    {
        if (selectedFolder is null)
        {
            return;
        }

        if (SaveSeriesMetadataBeforeViewing())
        {
            return;
        }

        var videos = ShouldOpenAsSeries(selectedFolder)
            ? database.GetSeriesVideos(selectedFolder.SeriesName!).Where(video => File.Exists(video.Path)).ToList()
            : database.GetVideos(selectedFolder.Id).Where(video => File.Exists(video.Path)).ToList();
        if (videos.Count == 0)
        {
            MessageBox.Show(this, "열 수 있는 영상이 없습니다. 스캔/동기화를 다시 실행해 보세요.", "영상 보기", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var viewer = new VideoViewerForm(videos);
        viewer.ShowDialog(this);
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
        RefreshFolderListItem(selectedFolder);
        ShowFolder(selectedFolder);
        return selectedFolder is null;
    }

    private void RefreshFolderFromDatabase(long folderId)
    {
        var refreshedFolder = database.GetFolder(folderId);
        if (refreshedFolder is null)
        {
            LoadFolders(null, autoSelectFirst: false);
            return;
        }

        selectedFolder = refreshedFolder;
        var folderIndex = folders.FindIndex(folder => folder.Id == refreshedFolder.Id);
        if (folderIndex >= 0)
        {
            folders[folderIndex] = refreshedFolder;
        }

        RefreshFolderListItem(refreshedFolder);
        ShowFolder(refreshedFolder);
    }

    private void DeleteSelectedFolder()
    {
        if (selectedFolder is null)
        {
            return;
        }

        if (GetCurrentFolderListMode() == FolderListMode.NewRegistration)
        {
            DeleteSelectedFolderFromDisk();
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

    private void DeleteSelectedFolderFromDisk()
    {
        if (selectedFolder is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"신규등록 폴더를 실제로 휴지통으로 이동하고 DB에서 제거합니다.\n\n{selectedFolder.Path}",
            "신규등록 폴더 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            MoveEntryToRecycleBin(selectedFolder.Path);

            database.DeleteFolder(selectedFolder.Id);
            statusLabel.Text = $"신규등록 폴더 삭제됨: {selectedFolder.DisplayName}";
            ClearDetails();
            LoadFolders(null, autoSelectFirst: false);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "폴더 삭제 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
        LoadThumbnailAsync(selectedFolder);
        SaveSelectedFolder();
    }

    private async void LoadThumbnailAsync(FolderItem folder)
    {
        thumbnailLoadCancellationTokenSource?.Cancel();
        thumbnailLoadCancellationTokenSource?.Dispose();
        thumbnailLoadCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = thumbnailLoadCancellationTokenSource.Token;

        thumbnailBox.Image?.Dispose();
        thumbnailBox.Image = null;
        var candidatePaths = GetThumbnailCandidatePaths(folder);
        if (candidatePaths.Count == 0)
        {
            return;
        }

        try
        {
            var result = await Task.Run(() =>
            {
                foreach (var candidatePath in candidatePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(candidatePath))
                    {
                        ImageLoader.LogFailure("thumbnail", ImageLoader.CreateFailure(candidatePath, "파일 없음"));
                        continue;
                    }

                    try
                    {
                        return new ThumbnailLoadResult(MediaThumbnailLoader.LoadThumbnailCopy(candidatePath, new Size(320, 320)), candidatePath);
                    }
                    catch (Exception exception)
                    {
                        ImageLoader.LogFailure("thumbnail", ImageLoader.CreateFailure(candidatePath, exception));
                    }
                }

                return null;
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                result?.Image.Dispose();
                return;
            }

            thumbnailBox.Image?.Dispose();
            thumbnailBox.Image = result?.Image;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ImageLoader.LogFailure("thumbnail", ImageLoader.CreateFailure(folder.ThumbnailPath ?? folder.Path, exception));
            thumbnailBox.Image = null;
        }
    }

    private List<string> GetThumbnailCandidatePaths(FolderItem folder)
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(folder.ThumbnailPath))
        {
            paths.Add(folder.ThumbnailPath);
        }

        try
        {
            paths.AddRange(database.GetImages(folder.Id).Select(image => image.Path));
            paths.AddRange(database.GetVideos(folder.Id).Select(video => video.Path));
        }
        catch (Exception exception)
        {
            ImageLoader.LogFailure("thumbnail", ImageLoader.CreateFailure(folder.Path, exception));
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record ThumbnailLoadResult(Image Image, string Path);

    private void SetDetailsEnabled(bool enabled)
    {
        foreach (var control in new Control[] { displayNameBox, authorBox, numberBox, seriesNameBox, seriesOrderBox, scoreBox, tagsBox, memoBox, favoriteCheckBox, reservedCheckBox, saveButton, viewButton, videoViewButton, thumbnailButton, openFolderButton, copyPathButton, moveToMainRootButton, deleteFolderButton })
        {
            control.Enabled = enabled;
        }
    }

    private void SetBusy(bool busy)
    {
        scanButton.Enabled = !busy;
        incomingScanButton.Enabled = !busy;
        fullScanButton.Enabled = !busy;
        randomButton.Enabled = !busy;
        searchBox.Enabled = !busy;
        searchFieldComboBox.Enabled = !busy;
        sortComboBox.Enabled = !busy;
        quickFilterComboBox.Enabled = !busy;
        tagFilterButton.Enabled = !busy;
        excludedTagFilterButton.Enabled = !busy;
        tagFilterModeComboBox.Enabled = !busy;
        clearTagFilterButton.Enabled = !busy;
        folderList.Enabled = !busy;
        firstPageButton.Enabled = !busy && currentPageIndex > 0;
        previousPageButton.Enabled = !busy && currentPageIndex > 0;
        nextPageButton.Enabled = !busy && currentPageIndex < GetLastPageIndex();
        lastPageButton.Enabled = !busy && currentPageIndex < GetLastPageIndex();
        pageSizeComboBox.Enabled = !busy;
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
        allTagNames.Clear();
        allTagNames.AddRange(tags);

        activeTagFilters.RemoveAll(activeTag => !tags.Contains(activeTag, StringComparer.OrdinalIgnoreCase));
        excludedTagFilters.RemoveAll(excludedTag => !tags.Contains(excludedTag, StringComparer.OrdinalIgnoreCase));

        updatingTagFilterListBox = true;
        tagFilterListBox.Items.Clear();
        excludedTagFilterListBox.Items.Clear();
        foreach (var tag in tags)
        {
            tagFilterListBox.Items.Add(tag, activeTagFilters.Contains(tag, StringComparer.OrdinalIgnoreCase));
            excludedTagFilterListBox.Items.Add(tag, excludedTagFilters.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }
        updatingTagFilterListBox = false;
        ResizeTagFilterDropDown();
        ResizeExcludedTagFilterDropDown();

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

        var existingTags = selectedFolders
            .SelectMany(folder => folder.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var text = PromptTags("태그 추가", "추가할 태그(쉼표로 구분)", string.Join(", ", existingTags), allTagNames);
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

        if (GetCurrentFolderListMode() == FolderListMode.NewRegistration)
        {
            DeleteSelectedFoldersFromDisk(selectedFolders);
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

    private void DeleteSelectedFoldersFromDisk(IReadOnlyList<FolderItem> selectedFolders)
    {
        var result = MessageBox.Show(
            this,
            $"선택한 신규등록 폴더 {selectedFolders.Count}개를 실제로 휴지통으로 이동하고 DB에서 제거합니다.",
            "신규등록 폴더 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        var deletedIds = new List<long>();
        var failedMessages = new List<string>();
        foreach (var folder in selectedFolders)
        {
            try
            {
                MoveEntryToRecycleBin(folder.Path);

                deletedIds.Add(folder.Id);
            }
            catch (Exception exception)
            {
                failedMessages.Add($"{folder.DisplayName}: {exception.Message}");
            }
        }

        if (deletedIds.Count > 0)
        {
            database.DeleteFolders(deletedIds);
        }

        ClearDetails();
        LoadTagFilters();
        LoadFolders(null, autoSelectFirst: false);
        statusLabel.Text = $"신규등록 폴더 삭제됨: {deletedIds.Count}개";
        if (failedMessages.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, failedMessages.Take(8)), "일부 폴더 삭제 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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

    private void ShowExcludedTagFilterMenu()
    {
        if (excludedTagFilterListBox.Items.Count == 0)
        {
            return;
        }

        if (!excludedTagFilterDropDown.Visible)
        {
            ResizeExcludedTagFilterDropDown();
            excludedTagFilterDropDown.Show(excludedTagFilterButton, new Point(0, excludedTagFilterButton.Height));
            excludedTagFilterListBox.Focus();
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

    private void SyncExcludedTagFiltersFromListBox()
    {
        if (updatingTagFilterListBox)
        {
            return;
        }

        excludedTagFilters.Clear();
        foreach (var checkedItem in excludedTagFilterListBox.CheckedItems)
        {
            if (checkedItem is string tag)
            {
                excludedTagFilters.Add(tag);
            }
        }

        UpdateTagFilterStatus();
        LoadFolders();
    }

    private void ClearTagFilter()
    {
        activeTagFilters.Clear();
        excludedTagFilters.Clear();
        updatingTagFilterListBox = true;
        for (var itemIndex = 0; itemIndex < tagFilterListBox.Items.Count; itemIndex++)
        {
            tagFilterListBox.SetItemChecked(itemIndex, false);
            excludedTagFilterListBox.SetItemChecked(itemIndex, false);
        }
        updatingTagFilterListBox = false;
        UpdateTagFilterStatus();
        LoadFolders();
        MoveFocusAwayFromTagFilter();
    }

    private void ToggleAllActiveTagFilters()
    {
        var checkAll = tagFilterListBox.CheckedItems.Count < tagFilterListBox.Items.Count;
        updatingTagFilterListBox = true;
        for (var itemIndex = 0; itemIndex < tagFilterListBox.Items.Count; itemIndex++)
        {
            tagFilterListBox.SetItemChecked(itemIndex, checkAll);
        }

        BeginInvoke(() =>
        {
            updatingTagFilterListBox = false;
            SyncActiveTagFiltersFromListBox();
        });
    }

    private void ToggleAllExcludedTagFilters()
    {
        var checkAll = excludedTagFilterListBox.CheckedItems.Count < excludedTagFilterListBox.Items.Count;
        updatingTagFilterListBox = true;
        for (var itemIndex = 0; itemIndex < excludedTagFilterListBox.Items.Count; itemIndex++)
        {
            excludedTagFilterListBox.SetItemChecked(itemIndex, checkAll);
        }

        BeginInvoke(() =>
        {
            updatingTagFilterListBox = false;
            SyncExcludedTagFiltersFromListBox();
        });
    }

    private void UpdateTagFilterStatus()
    {
        if (activeTagFilters.Count == 0 && excludedTagFilters.Count == 0)
        {
            tagFilterStatusLabel.Text = "";
            tagFilterButton.Text = Localization.T("toolbar.tag");
            excludedTagFilterButton.Text = Localization.T("toolbar.excludedTag");
            tagFilterButton.Font = Font;
            excludedTagFilterButton.Font = Font;
            tagFilterButton.ForeColor = SystemColors.ControlText;
            excludedTagFilterButton.ForeColor = SystemColors.ControlText;
            toolTip.SetToolTip(clearTagFilterButton, "태그 필터를 모두 비우고 전체 목록을 표시합니다.");
            UpdateTagFilterToggleButtons();
            return;
        }

        var tagSummary = string.Join(", ", activeTagFilters);
        var excludedTagSummary = string.Join(", ", excludedTagFilters);
        var modeText = tagFilterModeComboBox.SelectedIndex switch
        {
            1 => "AND",
            2 => "OR",
            _ => "포함"
        };
        tagFilterButton.Text = activeTagFilters.Count == 0 ? Localization.T("toolbar.tag") : ShortenTagSummary(tagSummary);
        excludedTagFilterButton.Text = excludedTagFilters.Count == 0 ? Localization.T("toolbar.excludedTag") : ShortenTagSummary(excludedTagSummary);
        tagFilterButton.Font = activeTagFilters.Count == 0 ? Font : new Font(Font, FontStyle.Bold);
        excludedTagFilterButton.Font = excludedTagFilters.Count == 0 ? Font : new Font(Font, FontStyle.Bold);
        tagFilterButton.ForeColor = activeTagFilters.Count == 0 ? SystemColors.ControlText : Color.FromArgb(25, 90, 170);
        excludedTagFilterButton.ForeColor = excludedTagFilters.Count == 0 ? SystemColors.ControlText : Color.FromArgb(180, 60, 45);
        var includeText = activeTagFilters.Count == 0 ? "포함 없음" : $"포함({modeText}): {tagSummary}";
        var excludeText = excludedTagFilters.Count == 0 ? "제외 없음" : $"제외: {excludedTagSummary}";
        tagFilterStatusLabel.Text = $"태그 필터 - {includeText} / {excludeText}";
        toolTip.SetToolTip(clearTagFilterButton, $"현재 태그 필터 - {includeText} / {excludeText}");
        UpdateTagFilterToggleButtons();
    }

    private void UpdateTagFilterToggleButtons()
    {
        toggleAllTagFilterButton.Text = tagFilterListBox.CheckedItems.Count < tagFilterListBox.Items.Count ? "전체 선택" : "전체 해제";
        toggleAllExcludedTagFilterButton.Text = excludedTagFilterListBox.CheckedItems.Count < excludedTagFilterListBox.Items.Count ? "전체 선택" : "전체 해제";
    }

    private void BuildTagFilterDropDown()
    {
        toggleAllTagFilterButton.Text = "전체 선택";
        toggleAllTagFilterButton.Dock = DockStyle.Top;
        toggleAllTagFilterButton.Height = 28;
        toggleAllTagFilterButton.Click += (_, _) => ToggleAllActiveTagFilters();

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

        var panel = new Panel();
        panel.Controls.Add(tagFilterListBox);
        panel.Controls.Add(toggleAllTagFilterButton);

        var host = new ToolStripControlHost(panel)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };
        tagFilterDropDown.Padding = Padding.Empty;
        tagFilterDropDown.Items.Add(host);
        tagFilterDropDown.AutoClose = true;
    }

    private void BuildExcludedTagFilterDropDown()
    {
        toggleAllExcludedTagFilterButton.Text = "전체 선택";
        toggleAllExcludedTagFilterButton.Dock = DockStyle.Top;
        toggleAllExcludedTagFilterButton.Height = 28;
        toggleAllExcludedTagFilterButton.Click += (_, _) => ToggleAllExcludedTagFilters();

        excludedTagFilterListBox.CheckOnClick = true;
        excludedTagFilterListBox.BorderStyle = BorderStyle.None;
        excludedTagFilterListBox.IntegralHeight = false;
        excludedTagFilterListBox.HorizontalScrollbar = true;
        excludedTagFilterListBox.ItemCheck += (_, _) => BeginInvoke(SyncExcludedTagFiltersFromListBox);
        excludedTagFilterListBox.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode == Keys.Escape)
            {
                MoveFocusAwayFromTagFilter();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
        };

        var panel = new Panel();
        panel.Controls.Add(excludedTagFilterListBox);
        panel.Controls.Add(toggleAllExcludedTagFilterButton);

        var host = new ToolStripControlHost(panel)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };
        excludedTagFilterDropDown.Padding = Padding.Empty;
        excludedTagFilterDropDown.Items.Add(host);
        excludedTagFilterDropDown.AutoClose = true;
    }

    private void BuildTagInputSuggestionDropDown()
    {
        tagInputSuggestionListBox.BorderStyle = BorderStyle.FixedSingle;
        tagInputSuggestionListBox.IntegralHeight = false;
        tagInputSuggestionListBox.Height = 150;
        tagInputSuggestionListBox.MouseClick += (_, _) => ApplySelectedTagSuggestion();
        tagInputSuggestionListBox.KeyDown += (_, keyEventArgs) => HandleTagInputSuggestionKeyDown(keyEventArgs);

        var host = new ToolStripControlHost(tagInputSuggestionListBox)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };
        tagInputSuggestionDropDown.Padding = Padding.Empty;
        tagInputSuggestionDropDown.Items.Add(host);
        tagInputSuggestionDropDown.AutoClose = false;
    }

    private void UpdateTagInputSuggestions()
    {
        if (!tagsBox.Focused || allTagNames.Count == 0)
        {
            tagInputSuggestionDropDown.Close();
            return;
        }

        var token = GetCurrentTagToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            tagInputSuggestionDropDown.Close();
            return;
        }

        var existingTags = tagsBox.Text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(tag => !tag.Equals(token, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = allTagNames
            .Where(tag => tag.StartsWith(token, StringComparison.OrdinalIgnoreCase) && !existingTags.Contains(tag))
            .Take(10)
            .ToList();
        if (matches.Count == 0)
        {
            tagInputSuggestionDropDown.Close();
            return;
        }

        tagInputSuggestionListBox.BeginUpdate();
        tagInputSuggestionListBox.Items.Clear();
        tagInputSuggestionListBox.Items.AddRange(matches.Cast<object>().ToArray());
        tagInputSuggestionListBox.SelectedIndex = 0;
        tagInputSuggestionListBox.EndUpdate();

        var width = Math.Max(tagsBox.Width, 180);
        var height = Math.Min(10, matches.Count) * Math.Max(tagInputSuggestionListBox.ItemHeight, 18) + 6;
        tagInputSuggestionListBox.Size = new Size(width, height);
        if (tagInputSuggestionDropDown.Items[0] is ToolStripControlHost host)
        {
            host.Size = tagInputSuggestionListBox.Size;
        }

        if (!tagInputSuggestionDropDown.Visible)
        {
            tagInputSuggestionDropDown.Show(tagsBox, new Point(0, tagsBox.Height));
        }
    }

    private void HandleTagInputSuggestionKeyDown(KeyEventArgs keyEventArgs)
    {
        if (!tagInputSuggestionDropDown.Visible)
        {
            return;
        }

        if (keyEventArgs.KeyCode == Keys.Down)
        {
            tagInputSuggestionListBox.SelectedIndex = Math.Min(tagInputSuggestionListBox.Items.Count - 1, tagInputSuggestionListBox.SelectedIndex + 1);
            keyEventArgs.Handled = true;
            keyEventArgs.SuppressKeyPress = true;
        }
        else if (keyEventArgs.KeyCode == Keys.Up)
        {
            tagInputSuggestionListBox.SelectedIndex = Math.Max(0, tagInputSuggestionListBox.SelectedIndex - 1);
            keyEventArgs.Handled = true;
            keyEventArgs.SuppressKeyPress = true;
        }
        else if (keyEventArgs.KeyCode is Keys.Enter or Keys.Tab)
        {
            ApplySelectedTagSuggestion();
            keyEventArgs.Handled = true;
            keyEventArgs.SuppressKeyPress = true;
        }
        else if (keyEventArgs.KeyCode == Keys.Escape)
        {
            tagInputSuggestionDropDown.Close();
            keyEventArgs.Handled = true;
            keyEventArgs.SuppressKeyPress = true;
        }
    }

    private string GetCurrentTagToken()
    {
        var selectionStart = Math.Clamp(tagsBox.SelectionStart, 0, tagsBox.Text.Length);
        var commaIndex = LastCommaBefore(selectionStart);
        var tokenStart = commaIndex < 0 ? 0 : commaIndex + 1;
        return tagsBox.Text[tokenStart..selectionStart].Trim();
    }

    private int LastCommaBefore(int selectionStart)
    {
        if (tagsBox.Text.Length == 0 || selectionStart <= 0)
        {
            return -1;
        }

        return tagsBox.Text.LastIndexOf(',', selectionStart - 1);
    }

    private void ApplySelectedTagSuggestion()
    {
        if (tagInputSuggestionListBox.SelectedItem is not string selectedTag)
        {
            return;
        }

        var selectionStart = Math.Clamp(tagsBox.SelectionStart, 0, tagsBox.Text.Length);
        var tokenStart = LastCommaBefore(selectionStart);
        tokenStart = tokenStart < 0 ? 0 : tokenStart + 1;
        while (tokenStart < tagsBox.Text.Length && char.IsWhiteSpace(tagsBox.Text[tokenStart]))
        {
            tokenStart++;
        }

        var tokenEnd = tagsBox.Text.IndexOf(',', selectionStart);
        tokenEnd = tokenEnd < 0 ? tagsBox.Text.Length : tokenEnd;
        var prefix = tagsBox.Text[..tokenStart];
        var suffix = tagsBox.Text[tokenEnd..];
        var separator = suffix.Length == 0 ? "" : " ";
        tagsBox.Text = prefix + selectedTag + separator + suffix;
        tagsBox.SelectionStart = (prefix + selectedTag).Length;
        tagInputSuggestionDropDown.Close();
        tagsBox.Focus();
    }

    private void ResizeTagFilterDropDown()
    {
        var visibleItemCount = Math.Clamp(tagFilterListBox.Items.Count, 1, 10);
        var itemHeight = Math.Max(tagFilterListBox.ItemHeight, 18);
        var width = Math.Max(tagFilterButton.Width, 160);
        var listHeight = visibleItemCount * itemHeight + 6;
        var height = listHeight + toggleAllTagFilterButton.Height;

        toggleAllTagFilterButton.Width = width;
        tagFilterListBox.Location = new Point(0, toggleAllTagFilterButton.Height);
        tagFilterListBox.Size = new Size(width, listHeight);
        if (tagFilterDropDown.Items[0] is ToolStripControlHost host)
        {
            host.Size = new Size(width, height);
            host.Control.Size = host.Size;
        }

        UpdateTagFilterToggleButtons();
    }

    private void ResizeExcludedTagFilterDropDown()
    {
        var visibleItemCount = Math.Clamp(excludedTagFilterListBox.Items.Count, 1, 10);
        var itemHeight = Math.Max(excludedTagFilterListBox.ItemHeight, 18);
        var width = Math.Max(excludedTagFilterButton.Width, 160);
        var listHeight = visibleItemCount * itemHeight + 6;
        var height = listHeight + toggleAllExcludedTagFilterButton.Height;

        toggleAllExcludedTagFilterButton.Width = width;
        excludedTagFilterListBox.Location = new Point(0, toggleAllExcludedTagFilterButton.Height);
        excludedTagFilterListBox.Size = new Size(width, listHeight);
        if (excludedTagFilterDropDown.Items[0] is ToolStripControlHost host)
        {
            host.Size = new Size(width, height);
            host.Control.Size = host.Size;
        }

        UpdateTagFilterToggleButtons();
    }

    private void MoveFocusAwayFromTagFilter()
    {
        tagFilterDropDown.Close();
        excludedTagFilterDropDown.Close();

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
        ApplyLocalization();
        LoadTagFilters();
        LoadFolders(selectedFolder?.Id);
        statusLabel.Text = "설정 변경사항을 반영했습니다.";
    }

    private void ApplyLocalization()
    {
        applyingLocalization = true;
        try
        {
            Localization.ApplyTo(this, toolTip);
            Text = Localization.T("app.title");
            if (MainMenuStrip?.Items.Count >= 3)
            {
                MainMenuStrip.Items[0].Text = Localization.T("menu.settings");
                MainMenuStrip.Items[1].Text = Localization.T("menu.checkUpdates");
                MainMenuStrip.Items[2].Text = Localization.T("menu.random");
            }

            scanButton.Text = Localization.T("toolbar.quickSync");
            incomingScanButton.Text = Localization.T("toolbar.incomingScan");
            fullScanButton.Text = Localization.T("toolbar.fullScan");
            randomButton.Text = Localization.T("toolbar.random");
            searchBox.PlaceholderText = Localization.T("toolbar.search");
            tagFilterButton.Text = activeTagFilters.Count == 0 ? Localization.T("toolbar.tag") : tagFilterButton.Text;
            excludedTagFilterButton.Text = excludedTagFilters.Count == 0 ? Localization.T("toolbar.excludedTag") : excludedTagFilterButton.Text;
            clearTagFilterButton.Text = Localization.T("toolbar.clear");
            if (mediaModeTabs.TabPages.Count >= 2)
            {
                mediaModeTabs.TabPages[0].Text = Localization.T("tabs.imageMode");
                mediaModeTabs.TabPages[1].Text = Localization.T("tabs.videoMode");
            }

            if (tabs.TabPages.Count >= 6)
            {
                tabs.TabPages[0].Text = Localization.T("tabs.all");
                tabs.TabPages[1].Text = Localization.T("tabs.favorites");
                tabs.TabPages[2].Text = Localization.T("tabs.recent");
                tabs.TabPages[3].Text = Localization.T("tabs.reserved");
                tabs.TabPages[4].Text = Localization.T("tabs.series");
                tabs.TabPages[5].Text = Localization.T("tabs.newRegistration");
            }

            randomTabPage.Text = Localization.T("menu.random");
            ApplyListModeChrome();
        }
        finally
        {
            applyingLocalization = false;
        }

        UpdateTagFilterStatus();
        UpdateListStatus();
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
            FlowDirection = FlowDirection.LeftToRight
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

    private static string? PromptTags(string title, string label, string initialValue, IReadOnlyList<string> allTags)
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 460,
            Height = 170,
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
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
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
        var suggestionDropDown = new ToolStripDropDown { Padding = Padding.Empty, AutoClose = false };
        var suggestionListBox = new ListBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        var host = new ToolStripControlHost(suggestionListBox)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };
        suggestionDropDown.Items.Add(host);

        void CloseSuggestions()
        {
            suggestionDropDown.Close();
        }

        string CurrentToken()
        {
            var selectionStart = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
            var commaIndex = selectionStart <= 0 ? -1 : textBox.Text.LastIndexOf(',', selectionStart - 1);
            var tokenStart = commaIndex < 0 ? 0 : commaIndex + 1;
            return textBox.Text[tokenStart..selectionStart].Trim();
        }

        void UpdateSuggestions()
        {
            if (!textBox.Focused || allTags.Count == 0)
            {
                CloseSuggestions();
                return;
            }

            var token = CurrentToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                CloseSuggestions();
                return;
            }

            var existing = textBox.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(tag => !tag.Equals(token, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matches = allTags
                .Where(tag => tag.StartsWith(token, StringComparison.OrdinalIgnoreCase) && !existing.Contains(tag))
                .Take(10)
                .ToList();
            if (matches.Count == 0)
            {
                CloseSuggestions();
                return;
            }

            suggestionListBox.BeginUpdate();
            suggestionListBox.Items.Clear();
            suggestionListBox.Items.AddRange(matches.Cast<object>().ToArray());
            suggestionListBox.SelectedIndex = 0;
            suggestionListBox.EndUpdate();

            var width = Math.Max(textBox.Width, 180);
            var height = Math.Min(10, matches.Count) * Math.Max(suggestionListBox.ItemHeight, 18) + 6;
            suggestionListBox.Size = new Size(width, height);
            host.Size = suggestionListBox.Size;
            if (!suggestionDropDown.Visible)
            {
                suggestionDropDown.Show(textBox, new Point(0, textBox.Height));
            }
        }

        void ApplySuggestion()
        {
            if (suggestionListBox.SelectedItem is not string selectedTag)
            {
                return;
            }

            var selectionStart = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
            var tokenStart = selectionStart <= 0 ? -1 : textBox.Text.LastIndexOf(',', selectionStart - 1);
            tokenStart = tokenStart < 0 ? 0 : tokenStart + 1;
            while (tokenStart < textBox.Text.Length && char.IsWhiteSpace(textBox.Text[tokenStart]))
            {
                tokenStart++;
            }

            var tokenEnd = textBox.Text.IndexOf(',', selectionStart);
            tokenEnd = tokenEnd < 0 ? textBox.Text.Length : tokenEnd;
            var prefix = textBox.Text[..tokenStart];
            var suffix = textBox.Text[tokenEnd..];
            var separator = suffix.Length == 0 ? "" : " ";
            textBox.Text = prefix + selectedTag + separator + suffix;
            textBox.SelectionStart = (prefix + selectedTag).Length;
            CloseSuggestions();
            textBox.Focus();
        }

        textBox.TextChanged += (_, _) => UpdateSuggestions();
        textBox.Leave += (_, _) => dialog.BeginInvoke(() =>
        {
            if (!suggestionListBox.Focused)
            {
                CloseSuggestions();
            }
        });
        textBox.KeyDown += (_, keyEventArgs) =>
        {
            if (!suggestionDropDown.Visible)
            {
                return;
            }

            if (keyEventArgs.KeyCode == Keys.Down)
            {
                suggestionListBox.SelectedIndex = Math.Min(suggestionListBox.Items.Count - 1, suggestionListBox.SelectedIndex + 1);
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
            else if (keyEventArgs.KeyCode == Keys.Up)
            {
                suggestionListBox.SelectedIndex = Math.Max(0, suggestionListBox.SelectedIndex - 1);
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
            else if (keyEventArgs.KeyCode is Keys.Enter or Keys.Tab)
            {
                ApplySuggestion();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
            else if (keyEventArgs.KeyCode == Keys.Escape)
            {
                CloseSuggestions();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
        };
        suggestionListBox.MouseClick += (_, _) => ApplySuggestion();

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

        var result = dialog.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
        suggestionDropDown.Dispose();
        return result;
    }

    private static string? PromptRoot(string title, string label, IReadOnlyList<string> roots)
    {
        using var dialog = new Form
        {
            Text = title,
            Width = 620,
            Height = 170,
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
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var labelControl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        comboBox.Items.AddRange(roots.Cast<object>().ToArray());
        comboBox.SelectedIndex = 0;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        var okButton = new Button { Text = "확인", DialogResult = DialogResult.OK, Width = 80 };
        var cancelButton = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Width = 80 };

        buttons.Controls.AddRange([okButton, cancelButton]);
        table.Controls.Add(labelControl, 0, 0);
        table.Controls.Add(comboBox, 0, 1);
        table.Controls.Add(buttons, 0, 2);
        dialog.Controls.Add(table);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        return dialog.ShowDialog() == DialogResult.OK ? comboBox.SelectedItem?.ToString() : null;
    }

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)Math.Max(0, bytes);
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:N0} {units[unitIndex]}" : $"{size:N1} {units[unitIndex]}";
    }

    private static bool EntryPathExists(string path)
    {
        return Directory.Exists(path) || File.Exists(path);
    }

    private static void MoveEntryToRecycleBin(string path)
    {
        if (Directory.Exists(path))
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return;
        }

        if (File.Exists(path))
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
    }
}

public sealed record SeriesGuessCandidate(FolderItem Folder, (string SeriesName, int SeriesOrder)? Guess);
