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
                    if (images.Count > 0)
                    {
                        if (loggedImageFolderCount < 100)
                        {
                            scanLog.Add($"이미지 폴더 발견: {directory} ({images.Count}개)");
                            loggedImageFolderCount++;
                        }

                        results.Add(new FolderScanResult
                        {
                            FolderPath = directory,
                            Images = images
                        });
                    }

                    var now = DateTime.Now;
                    if ((now - lastProgressReport).TotalMilliseconds >= 150)
                    {
                        lastProgressReport = now;
                        progress?.Report(new ScanProgress
                        {
                            FoldersVisited = foldersVisited,
                            ImageFoldersFound = results.Count,
                            CurrentPath = directory
                        });
                    }
                }
            }

            progress?.Report(new ScanProgress
            {
                FoldersVisited = foldersVisited,
                ImageFoldersFound = results.Count,
                CurrentPath = null
            });
            scanLog.Add($"스캔 완료: 방문 폴더 {foldersVisited}개, 이미지 폴더 {results.Count}개");
            return results;
        }, cancellationToken);
    }

    public Task<ScanSummary> ScanStreamingAsync(
        IEnumerable<string> roots,
        Func<FolderScanResult, bool> shouldSave,
        Action<FolderScanResult> saveResult,
        IProgress<ScanProgress>? progress,
        ScanLog scanLog,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var summary = new ScanSummary();
            var lastProgressReport = DateTime.MinValue;
            var loggedImageFolderCount = 0;

            foreach (var root in roots.Where(Directory.Exists))
            {
                scanLog.Add($"루트 스캔 시작: {root}");
                foreach (var directory in EnumerateDirectoriesSafe(root, scanLog))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    summary.FoldersVisited++;

                    var images = GetImages(directory);
                    if (images.Count > 0)
                    {
                        summary.ImageFoldersFound++;
                        var result = new FolderScanResult
                        {
                            FolderPath = directory,
                            Images = images
                        };

                        if (shouldSave(result))
                        {
                            saveResult(result);
                            summary.SavedFolders++;
                            if (loggedImageFolderCount < 100)
                            {
                                scanLog.Add($"저장된 이미지 폴더: {directory} ({images.Count}개)");
                                loggedImageFolderCount++;
                            }
                        }
                        else
                        {
                            summary.SkippedFolders++;
                        }
                    }

                    var now = DateTime.Now;
                    if ((now - lastProgressReport).TotalMilliseconds >= 150)
                    {
                        lastProgressReport = now;
                        progress?.Report(new ScanProgress
                        {
                            FoldersVisited = summary.FoldersVisited,
                            ImageFoldersFound = summary.ImageFoldersFound,
                            SavedFolders = summary.SavedFolders,
                            SkippedFolders = summary.SkippedFolders,
                            CurrentPath = directory
                        });
                    }
                }
            }

            progress?.Report(new ScanProgress
            {
                FoldersVisited = summary.FoldersVisited,
                ImageFoldersFound = summary.ImageFoldersFound,
                SavedFolders = summary.SavedFolders,
                SkippedFolders = summary.SkippedFolders,
                CurrentPath = null
            });
            scanLog.Add($"스캔 완료: 방문 폴더 {summary.FoldersVisited}개, 이미지 폴더 {summary.ImageFoldersFound}개, 저장 {summary.SavedFolders}개, 변경 없음 {summary.SkippedFolders}개");
            return summary;
        }, cancellationToken);
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
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
