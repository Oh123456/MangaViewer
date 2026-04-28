namespace Viewer;

public sealed class ImageViewerForm : Form
{
    private readonly List<ImageItem> images;
    private readonly PictureBox pictureBox = new();
    private readonly Label statusLabel = new();
    private readonly Button previousButton = new();
    private readonly Button nextButton = new();
    private readonly Button fullscreenButton = new();
    private readonly TextBox pageBox = new();
    private readonly Label pageTotalLabel = new();
    private readonly CheckBox fitCheckBox = new();
    private int index;
    private Image? currentImage;
    private bool isFullscreen;
    private FormBorderStyle previousBorderStyle;
    private FormWindowState previousWindowState;
    private Rectangle previousBounds;

    public string? CurrentImagePath => images.Count == 0 ? null : images[index].Path;

    public ImageViewerForm(List<ImageItem> imageItems, int startIndex = 0)
    {
        images = imageItems;
        index = Math.Clamp(startIndex, 0, Math.Max(0, imageItems.Count - 1));

        Text = "이미지 뷰어";
        Width = 1200;
        Height = 800;
        KeyPreview = true;
        BackColor = Color.FromArgb(30, 30, 30);
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
        StyleToolbarButton(previousButton);

        nextButton.Text = "다음";
        nextButton.Width = 78;
        nextButton.Click += (_, _) => MoveImage(1);
        StyleToolbarButton(nextButton);

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
        StyleToolbarButton(fullscreenButton);

        fitCheckBox.Text = "맞춤 보기";
        fitCheckBox.Checked = true;
        fitCheckBox.AutoSize = true;
        fitCheckBox.ForeColor = Color.White;
        fitCheckBox.CheckedChanged += (_, _) => ApplySizeMode();

        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.White;
        statusLabel.Padding = new Padding(14, 7, 0, 0);

        toolbar.Controls.AddRange([previousButton, nextButton, pageTitleLabel, pageBox, pageLabel, pageTotalLabel, fullscreenButton, fitCheckBox, statusLabel]);

        pictureBox.Dock = DockStyle.Fill;
        pictureBox.BackColor = Color.FromArgb(20, 20, 20);
        pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox.DoubleClick += (_, _) => ToggleFullscreen();

        Controls.Add(pictureBox);
        Controls.Add(toolbar);

        KeyDown += OnKeyDown;
        MouseWheel += (_, mouseEventArgs) => MoveImage(mouseEventArgs.Delta < 0 ? 1 : -1);
        FormClosed += (_, _) => currentImage?.Dispose();
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
        else if (keyEventArgs.KeyCode == Keys.Escape)
        {
            if (isFullscreen)
            {
                ToggleFullscreen();
            }
            else
            {
                Close();
            }

            keyEventArgs.Handled = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
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

        if (keyData == Keys.F11)
        {
            ToggleFullscreen();
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

    private void LoadCurrentImage()
    {
        currentImage?.Dispose();
        currentImage = null;
        pictureBox.Image = null;

        if (images.Count == 0)
        {
            statusLabel.Text = "이미지가 없습니다.";
            return;
        }

        var image = images[index];
        try
        {
            using var stream = new FileStream(image.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            currentImage = Image.FromStream(stream);
            pictureBox.Image = currentImage;
            statusLabel.Text = $"{image.FileName}  {currentImage.Width}x{currentImage.Height}";
        }
        catch (Exception exception)
        {
            statusLabel.Text = $"이미지를 열 수 없습니다: {exception.Message}";
        }

        previousButton.Enabled = index > 0;
        nextButton.Enabled = index < images.Count - 1;
        pageTotalLabel.Text = Math.Max(1, images.Count).ToString();
        pageBox.Text = (index + 1).ToString();
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
            previousBounds = Bounds;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            fullscreenButton.Text = "창모드";
            isFullscreen = true;
            return;
        }

        TopMost = false;
        FormBorderStyle = previousBorderStyle;
        WindowState = previousWindowState;
        if (previousWindowState == FormWindowState.Normal)
        {
            Bounds = previousBounds;
        }

        fullscreenButton.Text = "전체화면";
        isFullscreen = false;
    }

    private static void StyleToolbarButton(Button button)
    {
        button.UseVisualStyleBackColor = true;
        button.ForeColor = Color.Black;
        button.Height = 28;
        button.Margin = new Padding(3, 2, 6, 2);
    }
}
