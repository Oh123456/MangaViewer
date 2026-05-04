namespace Viewer;

public sealed class ImageViewerForm : Form
{
    private static bool lastFitToWindow = AppSettings.Current.ViewerFitToWindow;
    private static bool lastFullscreen = AppSettings.Current.ViewerFullscreen;
    private static FormWindowState lastWindowState = AppSettings.Current.ViewerWindow.WindowState;
    private static Rectangle lastWindowBounds = AppSettings.Current.ViewerWindow.Bounds;
    private static bool hasLastWindowBounds = AppSettings.Current.ViewerWindow.HasBounds;

    private readonly List<ImageItem> images;
    private readonly PictureBox pictureBox = new();
    private readonly Label statusLabel = new();
    private readonly Button previousButton = new();
    private readonly Button nextButton = new();
    private readonly Button firstFolderButton = new();
    private readonly Button previousFolderButton = new();
    private readonly Button nextFolderButton = new();
    private readonly Button lastFolderButton = new();
    private readonly Button fullscreenButton = new();
    private readonly TextBox pageBox = new();
    private readonly Label pageTotalLabel = new();
    private readonly CheckBox fitCheckBox = new();
    private readonly List<Button> toolbarButtons = [];
    private readonly bool enableFolderNavigation;
    private readonly List<long> folderOrder;
    private int index;
    private Image? currentImage;
    private bool isFullscreen;
    private FormBorderStyle previousBorderStyle;
    private FormWindowState previousWindowState;
    private Rectangle previousBounds;

    public string? CurrentImagePath => images.Count == 0 ? null : images[index].Path;

    public ImageViewerForm(List<ImageItem> imageItems, int startIndex = 0, bool enableFolderNavigation = false)
    {
        images = imageItems;
        this.enableFolderNavigation = enableFolderNavigation;
        folderOrder = imageItems
            .Select(image => image.FolderId)
            .Distinct()
            .ToList();
        index = Math.Clamp(startIndex, 0, Math.Max(0, imageItems.Count - 1));

        Text = "이미지 뷰어";
        AppIcons.ApplyTo(this);
        Width = 1200;
        Height = 800;
        KeyPreview = true;
        BackColor = Color.FromArgb(30, 30, 30);
        if (hasLastWindowBounds)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = lastWindowBounds;
        }

        previousBorderStyle = FormBorderStyle;
        previousWindowState = WindowState;
        previousBounds = Bounds;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(45, 45, 45)
        };

        previousButton.Text = "이전";
        previousButton.Width = 78;
        previousButton.Click += (_, _) => MoveImage(-1);
        StyleToolbarButtonInstance(previousButton);

        nextButton.Text = "다음";
        nextButton.Width = 78;
        nextButton.Click += (_, _) => MoveImage(1);
        StyleToolbarButtonInstance(nextButton);

        firstFolderButton.Text = "첫 편";
        firstFolderButton.Width = 70;
        firstFolderButton.Click += (_, _) => MoveToFolderEdge(first: true);
        firstFolderButton.Visible = enableFolderNavigation;
        StyleToolbarButtonInstance(firstFolderButton);

        previousFolderButton.Text = "이전 편";
        previousFolderButton.Width = 82;
        previousFolderButton.Click += (_, _) => MoveFolder(-1);
        previousFolderButton.Visible = enableFolderNavigation;
        StyleToolbarButtonInstance(previousFolderButton);

        nextFolderButton.Text = "다음 편";
        nextFolderButton.Width = 82;
        nextFolderButton.Click += (_, _) => MoveFolder(1);
        nextFolderButton.Visible = enableFolderNavigation;
        StyleToolbarButtonInstance(nextFolderButton);

        lastFolderButton.Text = "마지막 편";
        lastFolderButton.Width = 88;
        lastFolderButton.Click += (_, _) => MoveToFolderEdge(first: false);
        lastFolderButton.Visible = enableFolderNavigation;
        StyleToolbarButtonInstance(lastFolderButton);

        var pageTitleLabel = new Label
        {
            Text = "페이지",
            AutoSize = true,
            ForeColor = Color.White,
            Padding = new Padding(12, 7, 4, 0)
        };

        pageBox.Width = 82;
        pageBox.Text = "1";
        pageBox.TextAlign = HorizontalAlignment.Right;
        pageBox.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode == Keys.Enter)
            {
                CommitPageBox();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
        };
        pageBox.KeyPress += (_, keyPressEventArgs) =>
        {
            if (!char.IsControl(keyPressEventArgs.KeyChar) && !char.IsDigit(keyPressEventArgs.KeyChar))
            {
                keyPressEventArgs.Handled = true;
            }
        };
        pageBox.Leave += (_, _) => CommitPageBox();

        var pageLabel = new Label
        {
            Text = "/",
            AutoSize = true,
            ForeColor = Color.White,
            Padding = new Padding(4, 7, 4, 0)
        };

        pageTotalLabel.Text = Math.Max(1, imageItems.Count).ToString();
        pageTotalLabel.AutoSize = true;
        pageTotalLabel.ForeColor = Color.White;
        pageTotalLabel.Padding = new Padding(0, 7, 14, 0);

        fullscreenButton.Text = "전체화면";
        fullscreenButton.Width = 90;
        fullscreenButton.Click += (_, _) => ToggleFullscreen();
        StyleToolbarButtonInstance(fullscreenButton);

        fitCheckBox.Text = "맞춤 보기";
        fitCheckBox.Checked = lastFitToWindow;
        fitCheckBox.AutoSize = true;
        fitCheckBox.ForeColor = Color.White;
        fitCheckBox.CheckedChanged += (_, _) =>
        {
            lastFitToWindow = fitCheckBox.Checked;
            AppSettings.Current.ViewerFitToWindow = lastFitToWindow;
            AppSettings.Save();
            ApplySizeMode();
        };

        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.White;
        statusLabel.Padding = new Padding(14, 7, 0, 0);

        toolbar.Controls.AddRange([previousButton, nextButton, firstFolderButton, previousFolderButton, nextFolderButton, lastFolderButton, pageTitleLabel, pageBox, pageLabel, pageTotalLabel, fullscreenButton, fitCheckBox, statusLabel]);

        pictureBox.Dock = DockStyle.Fill;
        pictureBox.BackColor = Color.FromArgb(20, 20, 20);
        pictureBox.SizeMode = PictureBoxSizeMode.Zoom;

        Controls.Add(pictureBox);
        Controls.Add(toolbar);
        Localization.ApplyTo(this);

        KeyDown += OnKeyDown;
        MouseWheel += (_, mouseEventArgs) => MoveImage(mouseEventArgs.Delta < 0 ? 1 : -1);
        FormClosing += (_, _) => SaveWindowPlacement();
        FormClosed += (_, _) => currentImage?.Dispose();
        Shown += (_, _) =>
        {
            if (lastFullscreen && !isFullscreen)
            {
                ToggleFullscreen();
            }
            else if (lastWindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Maximized;
            }
        };
        ApplySizeMode();
        LoadCurrentImage();
    }

    private void OnKeyDown(object? sender, KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs.KeyCode == Keys.Left)
        {
            MoveImage(-1);
            keyEventArgs.Handled = true;
        }
        else if (keyEventArgs.KeyCode == Keys.Right || keyEventArgs.KeyCode == Keys.Space || keyEventArgs.KeyCode == Keys.Enter || keyEventArgs.KeyCode == Keys.PageDown)
        {
            MoveImage(1);
            keyEventArgs.Handled = true;
        }
        else if (keyEventArgs.KeyCode == Keys.Back || keyEventArgs.KeyCode == Keys.PageUp)
        {
            MoveImage(-1);
            keyEventArgs.Handled = true;
        }
        else if (keyEventArgs.KeyCode == Keys.F11)
        {
            ToggleFullscreen();
            keyEventArgs.Handled = true;
        }
        else if (keyEventArgs.KeyCode == Keys.Up && enableFolderNavigation)
        {
            MoveFolder(-1);
            keyEventArgs.Handled = true;
        }
        else if (keyEventArgs.KeyCode == Keys.Down && enableFolderNavigation)
        {
            MoveFolder(1);
            keyEventArgs.Handled = true;
        }
        else if (keyEventArgs.KeyCode == Keys.Escape)
        {
            Close();
            keyEventArgs.Handled = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.F11)
        {
            ToggleFullscreen();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        if (pageBox.ContainsFocus)
        {
            if (keyData == Keys.Enter)
            {
                CommitPageBox();
                return true;
            }

            return base.ProcessCmdKey(ref message, keyData);
        }

        if (keyData is Keys.Left or Keys.PageUp or Keys.Back)
        {
            MoveImage(-1);
            return true;
        }

        if (keyData is Keys.Right or Keys.PageDown or Keys.Space or Keys.Enter)
        {
            MoveImage(1);
            return true;
        }

        if (enableFolderNavigation && keyData == Keys.Up)
        {
            MoveFolder(-1);
            return true;
        }

        if (enableFolderNavigation && keyData == Keys.Down)
        {
            MoveFolder(1);
            return true;
        }

        if (enableFolderNavigation && keyData == Keys.Home)
        {
            MoveToFolderEdge(first: true);
            return true;
        }

        if (enableFolderNavigation && keyData == Keys.End)
        {
            MoveToFolderEdge(first: false);
            return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    private void MoveImage(int delta)
    {
        if (images.Count == 0)
        {
            return;
        }

        index = Math.Clamp(index + delta, 0, images.Count - 1);
        LoadCurrentImage();
    }

    private void MoveFolder(int delta)
    {
        if (!enableFolderNavigation || images.Count == 0)
        {
            return;
        }

        var currentFolderId = images[index].FolderId;
        if (delta < 0)
        {
            for (var imageIndex = index - 1; imageIndex >= 0; imageIndex--)
            {
                if (images[imageIndex].FolderId != currentFolderId)
                {
                    var targetFolderId = images[imageIndex].FolderId;
                    index = images.FindIndex(image => image.FolderId == targetFolderId);
                    LoadCurrentImage();
                    return;
                }
            }

            return;
        }

        for (var imageIndex = index + 1; imageIndex < images.Count; imageIndex++)
        {
            if (images[imageIndex].FolderId != currentFolderId)
            {
                index = imageIndex;
                LoadCurrentImage();
                return;
            }
        }
    }

    private void MoveToFolderEdge(bool first)
    {
        if (!enableFolderNavigation || images.Count == 0)
        {
            return;
        }

        var folderId = first ? folderOrder.FirstOrDefault() : folderOrder.LastOrDefault();
        var targetIndex = images.FindIndex(image => image.FolderId == folderId);
        if (targetIndex < 0)
        {
            return;
        }

        index = targetIndex;
        LoadCurrentImage();
    }

    private void LoadCurrentImage()
    {
        currentImage?.Dispose();
        currentImage = null;
        pictureBox.Image = null;

        if (images.Count == 0)
        {
            statusLabel.Text = Localization.T("이미지가 없습니다.");
            return;
        }

        var image = images[index];
        try
        {
            currentImage = ImageLoader.LoadBitmapCopy(image.Path);
            pictureBox.Image = currentImage;
            var folderPositionText = GetFolderPositionText(image);
            statusLabel.Text = $"{folderPositionText}{image.FileName}  {currentImage.Width}x{currentImage.Height}";
        }
        catch (Exception exception)
        {
            ImageLoader.LogFailure("image_load", ImageLoader.CreateFailure(image.Path, exception));
            statusLabel.Text = $"{Localization.T("이미지를 열 수 없습니다")}: {exception.Message}";
        }

        previousButton.Enabled = index > 0;
        nextButton.Enabled = index < images.Count - 1;
        firstFolderButton.Enabled = enableFolderNavigation && folderOrder.Count > 1 && images[index].FolderId != folderOrder.First();
        previousFolderButton.Enabled = enableFolderNavigation && HasAdjacentFolder(-1);
        nextFolderButton.Enabled = enableFolderNavigation && HasAdjacentFolder(1);
        lastFolderButton.Enabled = enableFolderNavigation && folderOrder.Count > 1 && images[index].FolderId != folderOrder.Last();
        UpdateToolbarButtonStyles();
        pageTotalLabel.Text = Math.Max(1, images.Count).ToString();
        pageBox.Text = (index + 1).ToString();
    }

    private bool HasAdjacentFolder(int delta)
    {
        if (images.Count == 0)
        {
            return false;
        }

        var currentFolderId = images[index].FolderId;
        if (delta < 0)
        {
            return images.Take(index).Any(image => image.FolderId != currentFolderId);
        }

        return images.Skip(index + 1).Any(image => image.FolderId != currentFolderId);
    }

    private string GetFolderPositionText(ImageItem image)
    {
        if (!enableFolderNavigation || folderOrder.Count <= 1)
        {
            return "";
        }

        var folderIndex = folderOrder.IndexOf(image.FolderId);
        if (folderIndex < 0)
        {
            return "";
        }

        var title = string.IsNullOrWhiteSpace(image.FolderDisplayName) ? "" : $"{image.FolderDisplayName}  ";
        return $"{folderIndex + 1}/{folderOrder.Count}{Localization.T("편")}  {title}";
    }

    private void ApplySizeMode()
    {
        pictureBox.SizeMode = fitCheckBox.Checked ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.CenterImage;
    }

    private void CommitPageBox()
    {
        if (images.Count == 0)
        {
            pictureBox.Focus();
            return;
        }

        var page = int.TryParse(pageBox.Text, out var parsed) ? parsed : index + 1;
        page = Math.Clamp(page, 1, images.Count);

        pageBox.Text = page.ToString();

        index = page - 1;
        LoadCurrentImage();
        pictureBox.Focus();
    }

    private void ToggleFullscreen()
    {
        if (!isFullscreen)
        {
            previousBorderStyle = FormBorderStyle;
            previousWindowState = WindowState;
            previousBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var screenBounds = Screen.FromControl(this).Bounds;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = screenBounds;
            TopMost = true;
            fullscreenButton.Text = Localization.T("창모드");
            isFullscreen = true;
            lastFullscreen = true;
            AppSettings.Current.ViewerFullscreen = true;
            AppSettings.Save();
            UpdateToolbarButtonStyles();
            return;
        }

        TopMost = false;
        FormBorderStyle = previousBorderStyle;
        WindowState = previousWindowState;
        if (previousWindowState == FormWindowState.Normal)
        {
            Bounds = previousBounds;
        }

        fullscreenButton.Text = Localization.T("전체화면");
        isFullscreen = false;
        lastFullscreen = false;
        AppSettings.Current.ViewerFullscreen = false;
        AppSettings.Save();
        UpdateToolbarButtonStyles();
    }

    private void SaveWindowPlacement()
    {
        if (isFullscreen)
        {
            AppSettings.Current.ViewerFullscreen = true;
            AppSettings.Save();
            return;
        }

        lastWindowState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
        if (WindowState == FormWindowState.Normal)
        {
            lastWindowBounds = Bounds;
            hasLastWindowBounds = true;
        }

        var placement = AppSettings.Current.ViewerWindow;
        placement.WindowState = lastWindowState;
        placement.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        AppSettings.Current.ViewerFullscreen = false;
        AppSettings.Current.ViewerFitToWindow = lastFitToWindow;
        AppSettings.Save();
    }

    private static void StyleToolbarButton(Button button)
    {
        button.UseVisualStyleBackColor = true;
        button.ForeColor = Color.Black;
        button.Height = 28;
        button.Margin = new Padding(3, 2, 6, 2);
    }

    private void StyleToolbarButtonInstance(Button button)
    {
        toolbarButtons.Add(button);
        StyleToolbarButton(button);
        button.EnabledChanged += (_, _) => UpdateToolbarButtonStyle(button);
        UpdateToolbarButtonStyle(button);
    }

    private void UpdateToolbarButtonStyles()
    {
        foreach (var button in toolbarButtons)
        {
            UpdateToolbarButtonStyle(button);
        }
    }

    private static void UpdateToolbarButtonStyle(Button button)
    {
        button.UseVisualStyleBackColor = false;
        if (button.Enabled)
        {
            button.BackColor = Color.FromArgb(245, 245, 245);
            button.ForeColor = Color.Black;
            return;
        }

        button.BackColor = Color.FromArgb(80, 80, 80);
        button.ForeColor = Color.FromArgb(170, 170, 170);
    }
}
