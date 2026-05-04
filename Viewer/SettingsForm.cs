namespace Viewer;

public sealed class SettingsForm : Form
{
    private readonly AppDatabase database;
    private readonly Action refreshMainWindow;
    private readonly ListBox rootListBox = new();
    private readonly ListBox tagListBox = new();
    private readonly Button addRootButton = new();
    private readonly Button addIncomingRootButton = new();
    private readonly Button deleteRootButton = new();
    private readonly Button renameRootButton = new();
    private readonly Button openDatabaseFolderButton = new();
    private readonly Button openLogFolderButton = new();
    private readonly Button exportDuplicatesButton = new();
    private readonly Button openExportFolderButton = new();
    private readonly Button backupButton = new();
    private readonly Button restoreButton = new();
    private readonly Button cleanupMissingButton = new();
    private readonly Button refreshPathStatusButton = new();
    private readonly Button cleanupThumbnailsButton = new();
    private readonly Button inspectSeriesButton = new();
    private readonly Button inspectDuplicateNamesButton = new();
    private readonly Button optimizeDatabaseButton = new();
    private readonly CheckBox autoRefreshPathStatusCheckBox = new();
    private readonly NumericUpDown partialDuplicateThresholdBox = new();
    private readonly ComboBox languageComboBox = new();
    private readonly CheckBox autoCheckForUpdatesCheckBox = new();
    private readonly TextBox updateReleaseApiUrlBox = new();
    private readonly Button checkUpdatesButton = new();
    private readonly Button renameTagButton = new();
    private readonly Button deleteTagButton = new();
    private CancellationTokenSource? duplicateExportCancellationTokenSource;

    public SettingsForm(AppDatabase database, Action refreshMainWindow)
    {
        this.database = database;
        this.refreshMainWindow = refreshMainWindow;

        Text = Localization.T("settings.title");
        AppIcons.ApplyTo(this);
        Width = 660;
        Height = 600;
        MinimumSize = new Size(560, 480);
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        ApplyLocalization();
        LoadRoots();
        LoadTags();
    }

    private void BuildUi()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var rootsPage = new TabPage("루트");
        var filePage = new TabPage("파일");
        var maintenancePage = new TabPage("유지관리");
        var updatePage = new TabPage("업데이트");
        var tagsPage = new TabPage("태그");

        addRootButton.Text = "메인 루트 추가";
        addRootButton.Click += (_, _) => AddRoot(RootKind.Main);
        addIncomingRootButton.Text = "신규 루트 추가";
        addIncomingRootButton.Click += (_, _) => AddRoot(RootKind.Incoming);
        deleteRootButton.Text = "루트 삭제";
        deleteRootButton.Click += (_, _) => DeleteSelectedRoots();
        renameRootButton.Text = "이름 변경";
        renameRootButton.Click += (_, _) => RenameSelectedRoot();

        var rootsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        rootsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        rootsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        rootsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootsLayout.Controls.Add(CreateButtonPanel(addRootButton, addIncomingRootButton, renameRootButton, deleteRootButton), 0, 0);
        rootsLayout.Controls.Add(new Label
        {
            Text = "루트 경로",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        rootListBox.Dock = DockStyle.Fill;
        rootListBox.IntegralHeight = false;
        rootListBox.SelectionMode = SelectionMode.MultiExtended;
        rootListBox.HorizontalScrollbar = true;
        rootsLayout.Controls.Add(rootListBox, 0, 2);
        rootsPage.Controls.Add(rootsLayout);

        openDatabaseFolderButton.Text = "DB 위치";
        openDatabaseFolderButton.Click += (_, _) => OpenDatabaseFolder();
        openLogFolderButton.Text = "로그 위치";
        openLogFolderButton.Click += (_, _) => OpenLogFolder();
        exportDuplicatesButton.Text = "중복 폴더";
        exportDuplicatesButton.Click += async (_, _) => await ExportDuplicatesAsync();
        openExportFolderButton.Text = "내보내기 위치";
        openExportFolderButton.Click += (_, _) => OpenExportFolder();
        backupButton.Text = "백업";
        backupButton.Click += async (_, _) => await BackupFilesAsync();
        restoreButton.Text = "복원";
        restoreButton.Height = 30;
        restoreButton.Click += async (_, _) => await RestoreFileAsync();
        partialDuplicateThresholdBox.Minimum = 50;
        partialDuplicateThresholdBox.Maximum = 100;
        partialDuplicateThresholdBox.Value = Math.Clamp(AppSettings.Current.PartialDuplicateThresholdPercent, 50, 100);
        partialDuplicateThresholdBox.Width = 70;
        partialDuplicateThresholdBox.ValueChanged += (_, _) =>
        {
            AppSettings.Current.PartialDuplicateThresholdPercent = (int)partialDuplicateThresholdBox.Value;
            AppSettings.Save();
        };

        var fileLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 4,
            ColumnCount = 1
        };
        fileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fileLayout.Controls.Add(CreateButtonPanel(openDatabaseFolderButton, openLogFolderButton, openExportFolderButton, exportDuplicatesButton, backupButton, restoreButton), 0, 0);
        fileLayout.Controls.Add(CreatePartialDuplicateThresholdPanel(), 0, 1);
        fileLayout.Controls.Add(CreateLanguagePanel(), 0, 2);
        fileLayout.Controls.Add(new Label
        {
            Text = "DB와 설정 파일 백업/복원, 중복 폴더 내보내기를 관리합니다. 부분 중복 기준은 중복 폴더 검사에 사용됩니다.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        }, 0, 3);
        filePage.Controls.Add(fileLayout);

        cleanupMissingButton.Text = "누락 정리";
        cleanupMissingButton.Click += async (_, _) => await CleanupMissingAsync();
        refreshPathStatusButton.Text = "경로 확인";
        refreshPathStatusButton.Click += async (_, _) => await RefreshPathStatusAsync();
        cleanupThumbnailsButton.Text = "썸네일 정리";
        cleanupThumbnailsButton.Click += async (_, _) => await CleanupThumbnailsAsync();
        inspectSeriesButton.Text = "묶음 검사";
        inspectSeriesButton.Click += async (_, _) => await InspectSeriesAsync();
        inspectDuplicateNamesButton.Text = "이름 중복";
        inspectDuplicateNamesButton.Click += async (_, _) => await InspectDuplicateNamesAsync();
        optimizeDatabaseButton.Text = "DB 최적화";
        optimizeDatabaseButton.Click += async (_, _) => await OptimizeDatabaseAsync();
        autoRefreshPathStatusCheckBox.Text = "스캔 후 경로 확인";
        autoRefreshPathStatusCheckBox.AutoSize = true;
        autoRefreshPathStatusCheckBox.Checked = AppSettings.Current.AutoRefreshPathStatusAfterScan;
        autoRefreshPathStatusCheckBox.CheckedChanged += (_, _) =>
        {
            AppSettings.Current.AutoRefreshPathStatusAfterScan = autoRefreshPathStatusCheckBox.Checked;
            AppSettings.Save();
        };
        var maintenanceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        maintenanceLayout.Controls.Add(CreateButtonPanel(cleanupMissingButton, refreshPathStatusButton, cleanupThumbnailsButton, inspectSeriesButton, inspectDuplicateNamesButton, optimizeDatabaseButton), 0, 0);
        maintenanceLayout.Controls.Add(autoRefreshPathStatusCheckBox, 0, 1);
        maintenanceLayout.Controls.Add(new Label
        {
            Text = "오래 걸릴 수 있는 작업은 진행도 창을 표시합니다.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        }, 0, 2);
        maintenancePage.Controls.Add(maintenanceLayout);

        autoCheckForUpdatesCheckBox.Text = "시작 시 업데이트 확인";
        autoCheckForUpdatesCheckBox.AutoSize = true;
        autoCheckForUpdatesCheckBox.Checked = AppSettings.Current.AutoCheckForUpdates;
        autoCheckForUpdatesCheckBox.CheckedChanged += (_, _) =>
        {
            AppSettings.Current.AutoCheckForUpdates = autoCheckForUpdatesCheckBox.Checked;
            AppSettings.Save();
        };

        updateReleaseApiUrlBox.Text = AppSettings.Current.UpdateReleaseApiUrl;
        updateReleaseApiUrlBox.Dock = DockStyle.Fill;
        updateReleaseApiUrlBox.PlaceholderText = "https://api.github.com/repos/OWNER/REPO/releases/latest";
        updateReleaseApiUrlBox.Leave += (_, _) => SaveUpdateReleaseApiUrl();

        checkUpdatesButton.Text = "업데이트 확인";
        checkUpdatesButton.Width = 120;
        checkUpdatesButton.Height = 30;
        checkUpdatesButton.Click += async (_, _) =>
        {
            SaveUpdateReleaseApiUrl();
            await CheckForUpdatesAsync(showNoUpdateMessage: true);
        };

        var updateLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 5,
            ColumnCount = 1
        };
        updateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        updateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        updateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        updateLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        updateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        updateLayout.Controls.Add(autoCheckForUpdatesCheckBox, 0, 0);
        updateLayout.Controls.Add(new Label
        {
            Text = "GitHub Releases API URL",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        updateLayout.Controls.Add(updateReleaseApiUrlBox, 0, 2);
        updateLayout.Controls.Add(CreateButtonPanel(checkUpdatesButton), 0, 3);
        updateLayout.Controls.Add(new Label
        {
            Text = "예: https://api.github.com/repos/OWNER/REPO/releases/latest",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        }, 0, 4);
        updatePage.Controls.Add(updateLayout);

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

        var tagButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        deleteTagButton.Text = "삭제";
        deleteTagButton.Width = 90;
        deleteTagButton.Height = 30;
        deleteTagButton.Click += (_, _) => DeleteTag();

        renameTagButton.Text = "이름 변경";
        renameTagButton.Width = 100;
        renameTagButton.Height = 30;
        renameTagButton.Click += (_, _) => RenameTag();

        tagButtons.Controls.AddRange([deleteTagButton, renameTagButton]);

        var tagLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        tagLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tagLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tagLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        tagLayout.Controls.Add(tagLabel, 0, 0);
        tagLayout.Controls.Add(tagListBox, 0, 1);
        tagLayout.Controls.Add(tagButtons, 0, 2);
        tagsPage.Controls.Add(tagLayout);

        rootsLayout.MouseDown += (_, _) => ClearListSelections();
        tagLayout.MouseDown += (_, _) => ClearListSelections();
        tagLabel.MouseDown += (_, _) => ClearListSelections();
        tagButtons.MouseDown += (_, _) => ClearListSelections();

        tabs.TabPages.AddRange([rootsPage, filePage, maintenancePage, updatePage, tagsPage]);
        Controls.Add(tabs);
    }

    private static FlowLayoutPanel CreateButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };
        foreach (var button in buttons)
        {
            button.Width = Math.Max(button.Width, 112);
            button.Height = 30;
            button.Margin = new Padding(4);
            panel.Controls.Add(button);
        }

        return panel;
    }

    private Control CreatePartialDuplicateThresholdPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        panel.Controls.Add(new Label
        {
            Text = "부분 중복 기준",
            AutoSize = true,
            Padding = new Padding(0, 5, 8, 0)
        });
        panel.Controls.Add(partialDuplicateThresholdBox);
        panel.Controls.Add(new Label
        {
            Text = "%",
            AutoSize = true,
            Padding = new Padding(4, 5, 0, 0)
        });
        return panel;
    }

    private Control CreateLanguagePanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        panel.Controls.Add(new Label
        {
            Text = Localization.T("settings.language"),
            AutoSize = true,
            Padding = new Padding(0, 7, 8, 0)
        });

        languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        languageComboBox.Width = 160;
        var languages = Localization.GetLanguages();
        languageComboBox.Items.AddRange(languages.Cast<object>().ToArray());
        var selectedLanguage = languages.FindIndex(language => string.Equals(language.Code, AppSettings.Current.LanguageCode, StringComparison.OrdinalIgnoreCase));
        languageComboBox.SelectedIndex = selectedLanguage >= 0 ? selectedLanguage : 0;
        languageComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (languageComboBox.SelectedItem is not LanguageInfo language)
            {
                return;
            }

            AppSettings.Current.LanguageCode = language.Code;
            AppSettings.Save();
            Localization.Initialize(language.Code);
            ApplyLocalization();
            refreshMainWindow();
        };
        panel.Controls.Add(languageComboBox);
        return panel;
    }

    private void ApplyLocalization()
    {
        Localization.ApplyTo(this);
        Text = Localization.T("settings.title");
    }

    private void SaveUpdateReleaseApiUrl()
    {
        var releaseApiUrl = updateReleaseApiUrlBox.Text.Trim();
        if (string.Equals(AppSettings.Current.UpdateReleaseApiUrl, releaseApiUrl, StringComparison.Ordinal))
        {
            return;
        }

        AppSettings.Current.UpdateReleaseApiUrl = releaseApiUrl;
        AppSettings.Save();
    }

    private async Task CheckForUpdatesAsync(bool showNoUpdateMessage)
    {
        checkUpdatesButton.Enabled = false;
        try
        {
            var result = await UpdateService.CheckLatestAsync();
            if (!result.IsConfigured)
            {
                MessageBox.Show(this, result.ErrorMessage, Localization.T("업데이트 확인"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                MessageBox.Show(this, result.ErrorMessage, Localization.T("업데이트 확인 실패"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            var message = CreateUpdatePromptMessage(result);
            var dialogResult = MessageBox.Show(this, message, Localization.T("업데이트 발견"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (dialogResult == DialogResult.Yes)
            {
                await DownloadOrOpenUpdateAsync(result);
            }
        }
        finally
        {
            checkUpdatesButton.Enabled = true;
        }
    }

    private static string CreateUpdatePromptMessage(UpdateCheckResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.AssetDownloadUrl))
        {
            return string.Format(
                Localization.T("새 버전이 있습니다.\n\n현재 버전: {0}\n최신 버전: {1}\n파일: {2}\n\n업데이트 파일을 다운로드할까요?"),
                result.CurrentVersion,
                result.LatestVersion,
                result.AssetName);
        }

        return string.Format(
            Localization.T("새 버전이 있습니다.\n\n현재 버전: {0}\n최신 버전: {1}\n\n릴리즈 페이지를 열까요?"),
            result.CurrentVersion,
            result.LatestVersion);
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

    private void LoadRoots()
    {
        rootListBox.Items.Clear();
        foreach (var rootPath in database.GetRoots(RootKind.Main))
        {
            rootListBox.Items.Add(new RootListItem(rootPath, RootKind.Main));
        }

        foreach (var rootPath in database.GetRoots(RootKind.Incoming))
        {
            rootListBox.Items.Add(new RootListItem(rootPath, RootKind.Incoming));
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

    private void AddRoot(RootKind kind)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = kind == RootKind.Incoming ? "신규등록 루트 폴더를 선택하세요" : "메인 라이브러리 루트 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        database.AddRoot(dialog.SelectedPath, kind);
        LoadRoots();
        refreshMainWindow();
    }

    private void DeleteSelectedRoots()
    {
        var selectedRoots = rootListBox.SelectedItems.Cast<RootListItem>().ToList();
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

        database.DeleteRoots(selectedRoots.Select(root => root.Path));
        LoadRoots();
        refreshMainWindow();
    }

    private void RenameSelectedRoot()
    {
        if (rootListBox.SelectedItems.Count != 1 || rootListBox.SelectedItem is not RootListItem root)
        {
            MessageBox.Show(this, "이름을 변경할 루트를 하나만 선택하세요.", "루트 이름 변경", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!Directory.Exists(root.Path))
        {
            MessageBox.Show(this, "실제 폴더가 존재하지 않아 이름을 변경할 수 없습니다.", "루트 이름 변경", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var directory = new DirectoryInfo(root.Path);
        var newName = PromptText("루트 이름 변경", "새 폴더 이름", directory.Name);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName.Trim(), directory.Name, StringComparison.Ordinal))
        {
            return;
        }

        var parentPath = directory.Parent?.FullName;
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            MessageBox.Show(this, "드라이브 루트는 이름을 변경할 수 없습니다.", "루트 이름 변경", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var newPath = Path.Combine(parentPath, newName.Trim());
        if (Directory.Exists(newPath) || File.Exists(newPath))
        {
            MessageBox.Show(this, "같은 이름의 폴더나 파일이 이미 있습니다.", "루트 이름 변경", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show(this, $"실제 폴더 이름과 DB 경로를 함께 변경합니다.\n\n{root.Path}\n→ {newPath}", "루트 이름 변경", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            Directory.Move(root.Path, newPath);
            database.RenameRootPath(root.Path, newPath);
            LoadRoots();
            refreshMainWindow();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "루트 이름 변경 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

    private async Task ExportDuplicatesAsync()
    {
        exportDuplicatesButton.Enabled = false;
        duplicateExportCancellationTokenSource?.Dispose();
        duplicateExportCancellationTokenSource = new CancellationTokenSource();
        using var progressForm = new ScanProgressForm(() => duplicateExportCancellationTokenSource.Cancel())
        {
            Text = "중복 폴더 내보내기"
        };

        try
        {
            var exporter = new DuplicateImageExporter(database);
            var progress = new Progress<string>(message => progressForm.UpdateStatus(message));
            progressForm.Show(this);
            progressForm.UpdateStatus("중복 폴더 확인 중...");
            var exportPath = await exporter.ExportAsync(progress, duplicateExportCancellationTokenSource.Token);

            if (exportPath is null)
            {
                MessageBox.Show(this, "중복 폴더가 발견되지 않았습니다. 엑셀 파일은 만들지 않았습니다.", "중복 폴더", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                this,
                $"중복 폴더 목록을 내보냈습니다.\n\n{exportPath}\n\n파일 위치를 열까요?",
                "중복 폴더",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                OpenPathInExplorer(exportPath);
            }
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(this, "중복 폴더 내보내기를 취소했습니다.", "중복 폴더", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "중복 폴더 내보내기 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            progressForm.Close();
            exportDuplicatesButton.Enabled = true;
            duplicateExportCancellationTokenSource?.Dispose();
            duplicateExportCancellationTokenSource = null;
        }
    }

    private static void OpenExportFolder()
    {
        var exportDirectory = Path.Combine(AppContext.BaseDirectory, "Exports");
        Directory.CreateDirectory(exportDirectory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{exportDirectory}\"",
            UseShellExecute = true
        });
    }

    private async Task BackupFilesAsync()
    {
        using var progressForm = new ScanProgressForm(() => { }) { Text = "백업" };
        (string backupDirectory, List<string> copied) backupResult;
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("백업 파일을 만드는 중...");
            backupResult = await Task.Run(() =>
            {
                var backupDirectory = Path.Combine(AppContext.BaseDirectory, "Backups", $"viewer_backup_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(backupDirectory);

                var copied = new List<string>();
                if (File.Exists(database.DatabasePath))
                {
                    var targetPath = Path.Combine(backupDirectory, "viewer.db");
                    File.Copy(database.DatabasePath, targetPath, overwrite: true);
                    copied.Add("DB");
                }

                if (File.Exists(AppSettings.SettingsPath))
                {
                    var targetPath = Path.Combine(backupDirectory, "viewer.settings.json");
                    File.Copy(AppSettings.SettingsPath, targetPath, overwrite: true);
                    copied.Add("설정");
                }

                return (backupDirectory, copied);
            });
        }
        finally
        {
            progressForm.Close();
        }

        if (backupResult.copied.Count == 0)
        {
            MessageBox.Show(this, "백업할 DB/설정 파일이 없습니다.", "백업", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var openResult = MessageBox.Show(
            this,
            $"{string.Join(", ", backupResult.copied)} 백업을 만들었습니다.\n\n{backupResult.backupDirectory}\n\n폴더를 열까요?",
            "백업",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
        if (openResult == DialogResult.Yes)
        {
            OpenPathInExplorer(backupResult.backupDirectory);
        }
    }

    private async Task RestoreFileAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "복원할 DB 또는 설정 파일을 선택하세요",
            Filter = "Viewer backup files|viewer.db;viewer.settings.json;*.db;*.json|All files|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var fileName = Path.GetFileName(dialog.FileName);
        if (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            var result = MessageBox.Show(this, "현재 DB를 선택한 파일로 교체합니다. 계속할까요?", "DB 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }

            using var progressForm = new ScanProgressForm(() => { }) { Text = "DB 복원" };
            try
            {
                progressForm.Show(this);
                progressForm.UpdateStatus("현재 DB를 자동 백업하는 중...");
                await Task.Run(() =>
                {
                    CreateBackupSnapshot("before_restore");
                    File.Copy(dialog.FileName, database.DatabasePath, overwrite: true);
                    database.Initialize();
                });
            }
            finally
            {
                progressForm.Close();
            }

            refreshMainWindow();
            MessageBox.Show(this, "DB를 복원했습니다. 복원 전 파일은 Backups 폴더에 자동 백업했습니다.", "DB 복원", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var progressForm = new ScanProgressForm(() => { }) { Text = "설정 복원" };
            try
            {
                progressForm.Show(this);
                progressForm.UpdateStatus("현재 설정을 자동 백업하는 중...");
                await Task.Run(() =>
                {
                    CreateBackupSnapshot("before_restore");
                    File.Copy(dialog.FileName, AppSettings.SettingsPath, overwrite: true);
                });
            }
            finally
            {
                progressForm.Close();
            }

            AppSettings.Reload();
            MessageBox.Show(this, "설정을 복원했습니다. 복원 전 파일은 Backups 폴더에 자동 백업했습니다.", "설정 복원", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show(this, "복원할 수 있는 파일은 .db 또는 .json입니다.", "복원", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CleanupMissingAsync()
    {
        using var progressForm = new ScanProgressForm(() => { }) { Text = "누락 정리" };
        CleanupSummary summary;
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("누락된 폴더와 이미지를 정리하는 중...");
            summary = await Task.Run(() => database.RemoveMissingFoldersAndImages());
        }
        finally
        {
            progressForm.Close();
        }

        refreshMainWindow();
        MessageBox.Show(this, $"누락 폴더 {summary.RemovedFolders}개, 누락 이미지 {summary.RemovedImages}개를 정리했습니다.", "누락 정리", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CleanupThumbnailsAsync()
    {
        using var progressForm = new ScanProgressForm(() => { }) { Text = "썸네일 정리" };
        int count;
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("깨진 썸네일 경로를 확인하는 중...");
            count = await Task.Run(() => database.ClearBrokenThumbnails());
        }
        finally
        {
            progressForm.Close();
        }

        refreshMainWindow();
        MessageBox.Show(this, $"깨진 썸네일 경로 {count}개를 정리했습니다.", "썸네일 정리", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RefreshPathStatusAsync()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var progressForm = new ScanProgressForm(() => cancellationTokenSource.Cancel()) { Text = "경로 확인" };
        int missingCount;
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("폴더 경로 존재 여부를 확인하는 중...");
            var progress = new Progress<string>(message => progressForm.UpdateStatus(message));
            missingCount = await Task.Run(() => database.RefreshFolderPathStatus(progress, cancellationTokenSource.Token), cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(this, "경로 확인을 취소했습니다.", "경로 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        finally
        {
            progressForm.Close();
        }

        refreshMainWindow();
        MessageBox.Show(this, $"깨진 경로 {missingCount}개를 표시했습니다. 메인 화면의 빠른 필터 `깨진 경로`에서 확인할 수 있습니다.", "경로 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task InspectSeriesAsync()
    {
        using var progressForm = new ScanProgressForm(() => { }) { Text = "묶음 품질 검사" };
        List<SeriesQualityIssue> issues;
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("묶음 편수 문제를 검사하는 중...");
            issues = await Task.Run(database.GetSeriesQualityIssues);
        }
        finally
        {
            progressForm.Close();
        }

        if (issues.Count == 0)
        {
            MessageBox.Show(this, "묶음 품질 문제가 발견되지 않았습니다.", "묶음 품질 검사", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var resultForm = new SeriesQualityIssuesForm(issues);
        resultForm.ShowDialog(this);
    }

    private async Task InspectDuplicateNamesAsync()
    {
        using var progressForm = new ScanProgressForm(() => { }) { Text = "이름 중복 검사" };
        List<DuplicateNameGroup> groups;
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("같은 이름의 폴더를 찾는 중...");
            groups = await Task.Run(database.GetDuplicateNameGroups);
        }
        finally
        {
            progressForm.Close();
        }

        if (groups.Count == 0)
        {
            MessageBox.Show(this, "같은 이름으로 등록된 폴더가 없습니다.", "이름 중복", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var resultForm = new DuplicateNameGroupsForm(groups, refreshMainWindow);
        resultForm.ShowDialog(this);
    }

    private async Task OptimizeDatabaseAsync()
    {
        using var progressForm = new ScanProgressForm(() => { }) { Text = "DB 최적화" };
        try
        {
            progressForm.Show(this);
            progressForm.UpdateStatus("DB를 최적화하는 중...");
            await Task.Run(() => database.Optimize());
        }
        finally
        {
            progressForm.Close();
        }

        MessageBox.Show(this, "DB 최적화를 완료했습니다.", "DB 최적화", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void OpenPathInExplorer(string path)
    {
        var arguments = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true
        });
    }

    private void CreateBackupSnapshot(string prefix)
    {
        var backupDirectory = Path.Combine(AppContext.BaseDirectory, "Backups", $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(backupDirectory);
        if (File.Exists(database.DatabasePath))
        {
            File.Copy(database.DatabasePath, Path.Combine(backupDirectory, "viewer.db"), overwrite: true);
        }

        if (File.Exists(AppSettings.SettingsPath))
        {
            File.Copy(AppSettings.SettingsPath, Path.Combine(backupDirectory, "viewer.settings.json"), overwrite: true);
        }
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

    private sealed record RootListItem(string Path, RootKind Kind)
    {
        public override string ToString()
        {
            var kindText = Kind == RootKind.Incoming ? "신규" : "메인";
            return $"[{kindText}] {Path}";
        }
    }
}
