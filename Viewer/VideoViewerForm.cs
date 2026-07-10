using System.Diagnostics;

namespace Viewer;

public sealed class VideoViewerForm : Form
{
    private readonly List<VideoItem> videos;
    private readonly ListView videoList = new();
    private readonly Button playButton = new();
    private readonly Label statusLabel = new();

    public VideoViewerForm(List<VideoItem> videos)
    {
        this.videos = videos;

        Text = "영상 뷰어";
        AppIcons.ApplyTo(this);
        Width = 760;
        Height = 520;
        MinimumSize = new Size(560, 360);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        videoList.Dock = DockStyle.Fill;
        videoList.View = View.Details;
        videoList.FullRowSelect = true;
        videoList.HideSelection = false;
        videoList.MultiSelect = false;
        videoList.Columns.Add("이름", 420);
        videoList.Columns.Add("크기", 110);
        videoList.Columns.Add("수정일", 150);
        videoList.DoubleClick += (_, _) => PlaySelectedVideo();
        videoList.KeyDown += (_, keyEventArgs) =>
        {
            if (keyEventArgs.KeyCode == Keys.Enter)
            {
                PlaySelectedVideo();
                keyEventArgs.Handled = true;
                keyEventArgs.SuppressKeyPress = true;
            }
        };

        foreach (var video in videos)
        {
            var item = new ListViewItem(video.FileName) { Tag = video };
            item.SubItems.Add(FormatFileSize(video.FileSize));
            item.SubItems.Add(video.ModifiedAt.ToString("yyyy-MM-dd HH:mm"));
            videoList.Items.Add(item);
        }

        if (videoList.Items.Count > 0)
        {
            videoList.Items[0].Selected = true;
        }

        playButton.Text = "재생";
        playButton.Width = 110;
        playButton.Height = 30;
        playButton.Click += (_, _) => PlaySelectedVideo();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        buttonPanel.Controls.Add(playButton);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Text = $"영상 {videos.Count:N0}개";

        root.Controls.Add(videoList, 0, 0);
        root.Controls.Add(buttonPanel, 0, 1);
        root.Controls.Add(statusLabel, 0, 2);
        Controls.Add(root);

        Shown += (_, _) => videoList.Focus();
    }

    private void PlaySelectedVideo()
    {
        if (videoList.SelectedItems.Count == 0 || videoList.SelectedItems[0].Tag is not VideoItem video)
        {
            return;
        }

        try
        {
            PlayVideo(video.Path);
            statusLabel.Text = $"재생: {video.FileName}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "영상 재생 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void PlayVideo(string videoPath)
    {
        var playerPath = AppSettings.Current.VideoPlayerPath;
        if (!string.IsNullOrWhiteSpace(playerPath) && File.Exists(playerPath))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = playerPath,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(videoPath);
            Process.Start(startInfo);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = videoPath,
            UseShellExecute = true
        });
    }

    private static string FormatFileSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)size;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
