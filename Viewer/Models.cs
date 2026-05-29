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
    public bool PathExists { get; set; } = true;
    public DateTime? PathCheckedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = [];

    public string TagSummary => Tags.Count == 0 ? "" : string.Join(", ", Tags);
}

public sealed class PagedFolderResult
{
    public List<FolderItem> Items { get; init; } = [];
    public int TotalCount { get; init; }
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
    public bool IsBookmarked { get; set; }
    public string? FolderDisplayName { get; set; }
    public string? FolderPath { get; set; }
    public DateTime? FolderModifiedAt { get; set; }
    public int FolderImageCount { get; set; }
    public long FolderTotalImageBytes { get; set; }
    public int? FolderSeriesOrder { get; set; }
}

public sealed class DuplicateImageCandidate
{
    public int GroupNumber { get; set; }
    public int GroupCount { get; set; }
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string Hash { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class DuplicateFolderCandidate
{
    public int GroupNumber { get; set; }
    public int GroupFolderCount { get; set; }
    public string FolderName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public int ImageCount { get; set; }
    public int MatchedImageCount { get; set; }
    public double MatchRate { get; set; }
    public long TotalImageBytes { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string DuplicateType { get; set; } = "완전";
    public string CleanupHint { get; set; } = "";
}

public sealed class FolderScanResult
{
    public required string FolderPath { get; init; }
    public required List<FileInfo> Images { get; init; }
    public DateTime DirectoryModifiedAt { get; init; }

    public DateTime FolderModifiedAt => Images.Count == 0 ? DateTime.MinValue : Images.Max(image => image.LastWriteTime);

    public int ImageCount => Images.Count;

    public long TotalImageBytes => Images.Sum(image => image.Length);
}

public sealed class ScanProgress
{
    public string Stage { get; init; } = "";
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
    public DateTime? DirectoryModifiedAt { get; init; }
    public DateTime FolderModifiedAt { get; init; }
    public int ImageCount { get; init; }
    public long TotalImageBytes { get; init; }
}

public enum ScanMode
{
    QuickSync,
    FullRescan
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

public sealed class DuplicateNameGroup
{
    public string DisplayName { get; set; } = "";
    public List<FolderItem> Folders { get; set; } = [];
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
    Series,
    NewRegistration
}

public enum RootKind
{
    Main,
    Incoming
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
    Contains,
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
