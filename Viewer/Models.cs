namespace Viewer;

public sealed class FolderItem
{
    public long Id { get; set; }
    public string Path { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Author { get; set; }
    public string? Number { get; set; }
    public string? SeriesName { get; set; }
    public int? SeriesOrder { get; set; }
    public int Score { get; set; }
    public string? Memo { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsReserved { get; set; }
    public int ViewCount { get; set; }
    public DateTime? LastViewedAt { get; set; }
    public string? LastImagePath { get; set; }
    public DateTime? FolderModifiedAt { get; set; }
    public int ImageCount { get; set; }
    public long TotalImageBytes { get; set; }
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = [];

    public string TagSummary => Tags.Count == 0 ? "" : string.Join(", ", Tags);
}

public sealed class ImageItem
{
    public long Id { get; set; }
    public long FolderId { get; set; }
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime ModifiedAt { get; set; }
    public int SortOrder { get; set; }
    public string? FolderDisplayName { get; set; }
    public int? FolderSeriesOrder { get; set; }
}

public sealed class DuplicateImageCandidate
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string Hash { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class FolderScanResult
{
    public required string FolderPath { get; init; }
    public required List<FileInfo> Images { get; init; }

    public DateTime FolderModifiedAt => Images.Count == 0 ? DateTime.MinValue : Images.Max(image => image.LastWriteTime);

    public int ImageCount => Images.Count;

    public long TotalImageBytes => Images.Sum(image => image.Length);
}

public sealed class ScanProgress
{
    public int FoldersVisited { get; init; }
    public int ImageFoldersFound { get; init; }
    public int SavedFolders { get; init; }
    public int SkippedFolders { get; init; }
    public string? CurrentPath { get; init; }
}

public sealed class ScanSummary
{
    public int FoldersVisited { get; set; }
    public int ImageFoldersFound { get; set; }
    public int SavedFolders { get; set; }
    public int SkippedFolders { get; set; }
    public int RemovedFolders { get; set; }
    public int RemovedImages { get; set; }
}

public sealed class FolderScanSignature
{
    public DateTime FolderModifiedAt { get; init; }
    public int ImageCount { get; init; }
    public long TotalImageBytes { get; init; }
}

public sealed class CleanupSummary
{
    public int RemovedFolders { get; set; }
    public int RemovedImages { get; set; }
}

public sealed class SeriesQualityIssue
{
    public string SeriesName { get; set; } = "";
    public string IssueType { get; set; } = "";
    public string Detail { get; set; } = "";
    public string FolderNames { get; set; } = "";
}

public sealed class ScanLog
{
    private readonly List<string> entries = [];

    public IReadOnlyList<string> Entries => entries;

    public void Add(string message)
    {
        entries.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }
}

public enum FolderListMode
{
    All,
    Favorites,
    Recent,
    Reserved,
    Series
}

public enum FolderSortMode
{
    Date,
    Name,
    Author,
    Score,
    Recent,
    Series,
    ImageCount
}

public enum FolderSearchField
{
    Name,
    Author,
    Memo,
    Path,
    Series
}

public enum TagFilterMode
{
    And,
    Or
}

public enum QuickFilterMode
{
    All,
    Unviewed,
    NoScore,
    NoTags,
    NoSeries,
    NoThumbnail,
    BrokenPath
}
