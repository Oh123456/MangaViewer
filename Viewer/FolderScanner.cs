namespace Viewer;

public sealed class FolderScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".webp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".wmv",
        ".webm",
        ".m4v"
    };

    public Task<List<FolderScanResult>> ScanAsync(IEnumerable<string> roots, IProgress<ScanProgress>? progress, ScanLog scanLog, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var results = new List<FolderScanResult>();
            var foldersVisited = 0;
            var lastProgressReport = DateTime.MinValue;
            var loggedImageFolderCount = 0;

            foreach (var root in roots.Where(Directory.Exists))
            {
                scanLog.Add($"루트 스캔 시작: {root}");
                foreach (var directory in EnumerateDirectoriesSafe(root, scanLog))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foldersVisited++;
                    var images = GetImages(directory);
                    var videos = GetVideos(directory);
                    if (images.Count > 0 || videos.Count > 0)
                    {
                        var directoryModifiedAt = GetDirectoryModifiedAt(directory);
                        if (loggedImageFolderCount < 100)
                        {
                            scanLog.Add($"미디어 폴더 발견: {directory} (이미지 {images.Count}개 / 영상 {videos.Count}개)");
                            loggedImageFolderCount++;
                        }

                        if (images.Count > 0)
                        {
                            results.Add(new FolderScanResult
                            {
                                FolderPath = directory,
                                DirectoryModifiedAt = directoryModifiedAt,
                                Images = images,
                                Videos = []
                            });
                        }

                        foreach (var video in videos)
                        {
                            results.Add(CreateVideoScanResult(video));
                        }
                    }

                    var now = DateTime.Now;
                    if ((now - lastProgressReport).TotalMilliseconds >= 150)
                    {
                        lastProgressReport = now;
                        progress?.Report(new ScanProgress
                        {
                            Stage = "폴더 탐색 중",
                            FoldersVisited = foldersVisited,
                            ImageFoldersFound = results.Count,
                            CurrentPath = directory
                        });
                    }
                }
            }

            progress?.Report(new ScanProgress
            {
                Stage = "폴더 탐색 완료",
                FoldersVisited = foldersVisited,
                ImageFoldersFound = results.Count,
                CurrentPath = null
            });
            scanLog.Add($"스캔 완료: 방문 폴더 {foldersVisited}개, 미디어 폴더 {results.Count}개");
            return results;
        }, cancellationToken);
    }

    public Task<ScanSummary> ScanStreamingAsync(
        IEnumerable<string> roots,
        ScanMode scanMode,
        IReadOnlyDictionary<string, FolderScanSignature> existingSignatureMap,
        Func<FolderScanResult, bool> shouldSave,
        Action<FolderScanResult> saveResult,
        IProgress<ScanProgress>? progress,
        ScanLog scanLog,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var summary = new ScanSummary();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var lastProgressReport = DateTime.MinValue;
            var loggedImageFolderCount = 0;
            var modeText = scanMode == ScanMode.QuickSync ? "빠른 동기화" : "전체 재스캔";
            scanLog.Add($"{modeText} 시작");

            foreach (var root in roots.Where(Directory.Exists))
            {
                scanLog.Add($"루트 스캔 시작: {root}");
                var rootStopwatch = System.Diagnostics.Stopwatch.StartNew();
                foreach (var directory in EnumerateDirectoriesSafe(root, scanLog))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    summary.FoldersVisited++;

                    var directoryModifiedAt = GetDirectoryModifiedAt(directory);
                    if (scanMode == ScanMode.QuickSync
                        && existingSignatureMap.TryGetValue(directory, out var existingSignature)
                        && existingSignature.DirectoryModifiedAt is not null
                        && directoryModifiedAt <= existingSignature.DirectoryModifiedAt.Value
                        && existingSignature.VideoCount == 0)
                    {
                        summary.ImageFoldersFound++;
                        summary.SkippedFolders++;
                        ReportProgressIfNeeded(progress, ref lastProgressReport, summary, directory, "변경 확인 중");
                        continue;
                    }

                    var images = GetImages(directory);
                    var videos = GetVideos(directory);
                    if (images.Count > 0 || videos.Count > 0)
                    {
                        if (images.Count > 0)
                        {
                            summary.ImageFoldersFound++;
                            var result = new FolderScanResult
                            {
                                FolderPath = directory,
                                DirectoryModifiedAt = directoryModifiedAt,
                                Images = images,
                                Videos = []
                            };

                            if (shouldSave(result))
                            {
                                saveResult(result);
                                summary.SavedFolders++;
                                if (loggedImageFolderCount < 100)
                                {
                                    scanLog.Add($"저장된 이미지 폴더: {directory} (이미지 {images.Count}개)");
                                    loggedImageFolderCount++;
                                }
                            }
                            else
                            {
                                summary.SkippedFolders++;
                            }
                        }

                        foreach (var video in videos)
                        {
                            summary.ImageFoldersFound++;
                            var videoResult = CreateVideoScanResult(video);
                            if (shouldSave(videoResult))
                            {
                                saveResult(videoResult);
                                summary.SavedFolders++;
                                if (loggedImageFolderCount < 100)
                                {
                                    scanLog.Add($"저장된 영상 파일: {video.FullName}");
                                    loggedImageFolderCount++;
                                }
                            }
                            else
                            {
                                summary.SkippedFolders++;
                            }
                        }
                    }

                    ReportProgressIfNeeded(progress, ref lastProgressReport, summary, directory, "폴더 탐색 중");
                }

                scanLog.Add($"루트 스캔 완료: {root} / {rootStopwatch.Elapsed:mm\\:ss\\.fff}");
            }

            progress?.Report(new ScanProgress
            {
                Stage = "스캔 완료",
                FoldersVisited = summary.FoldersVisited,
                ImageFoldersFound = summary.ImageFoldersFound,
                SavedFolders = summary.SavedFolders,
                SkippedFolders = summary.SkippedFolders,
                CurrentPath = null
            });
            scanLog.Add($"스캔 완료: 방문 폴더 {summary.FoldersVisited}개, 미디어 폴더 {summary.ImageFoldersFound}개, 저장 {summary.SavedFolders}개, 변경 없음 {summary.SkippedFolders}개 / {stopwatch.Elapsed:mm\\:ss\\.fff}");
            return summary;
        }, cancellationToken);
    }

    private static void ReportProgressIfNeeded(IProgress<ScanProgress>? progress, ref DateTime lastProgressReport, ScanSummary summary, string directory, string stage)
    {
        var now = DateTime.Now;
        if ((now - lastProgressReport).TotalMilliseconds < 150)
        {
            return;
        }

        lastProgressReport = now;
        progress?.Report(new ScanProgress
        {
            Stage = stage,
            FoldersVisited = summary.FoldersVisited,
            ImageFoldersFound = summary.ImageFoldersFound,
            SavedFolders = summary.SavedFolders,
            SkippedFolders = summary.SkippedFolders,
            CurrentPath = directory
        });
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string root, ScanLog scanLog)
    {
        yield return root;

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] children;
            try
            {
                children = Directory.GetDirectories(current);
            }
            catch (Exception exception)
            {
                scanLog.Add($"폴더 접근 실패: {current} / {exception.Message}");
                continue;
            }

            foreach (var child in children)
            {
                yield return child;
                pending.Push(child);
            }
        }
    }

    private static List<FileInfo> GetImages(string directory)
    {
        try
        {
            return Directory.GetFiles(directory)
                .Select(path => new FileInfo(path))
                .Where(file => ImageExtensions.Contains(file.Extension))
                .OrderBy(file => file.Name, NaturalStringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<FileInfo> GetVideos(string directory)
    {
        try
        {
            return Directory.GetFiles(directory)
                .Select(path => new FileInfo(path))
                .Where(file => VideoExtensions.Contains(file.Extension))
                .OrderBy(file => file.Name, NaturalStringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static FolderScanResult CreateVideoScanResult(FileInfo video)
    {
        return new FolderScanResult
        {
            FolderPath = video.FullName,
            DirectoryModifiedAt = video.LastWriteTime,
            Images = [],
            Videos = [video]
        };
    }

    private static DateTime GetDirectoryModifiedAt(string directory)
    {
        try
        {
            return Directory.GetLastWriteTime(directory);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}
