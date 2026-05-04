using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace Viewer;

public sealed record UpdateCheckResult(
    bool IsConfigured,
    bool HasUpdate,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseName,
    string? ReleasePageUrl,
    string? AssetName,
    string? AssetDownloadUrl,
    string? Body,
    string? ErrorMessage);

public static class UpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string CurrentVersion
    {
        get
        {
            var informationalVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var plusIndex = informationalVersion?.IndexOf('+') ?? -1;
            if (plusIndex > 0)
            {
                informationalVersion = informationalVersion![..plusIndex];
            }

            return string.IsNullOrWhiteSpace(informationalVersion) ? "0.0.0" : informationalVersion!;
        }
    }

    public static async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        var releaseApiUrl = AppSettings.Current.UpdateReleaseApiUrl.Trim();
        if (string.IsNullOrWhiteSpace(releaseApiUrl))
        {
            return new UpdateCheckResult(false, false, CurrentVersion, null, null, null, null, null, null, Localization.T("업데이트 URL이 설정되지 않았습니다."));
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Viewer-UpdateChecker/1.0");
            using var response = await httpClient.GetAsync(releaseApiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream, JsonOptions, cancellationToken);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new UpdateCheckResult(true, false, CurrentVersion, null, null, null, null, null, null, Localization.T("릴리즈 정보를 읽을 수 없습니다."));
            }

            var latestVersion = NormalizeVersionText(release.TagName);
            var hasUpdate = IsNewerVersion(latestVersion, CurrentVersion);
            var asset = PickUpdateAsset(release.Assets);
            return new UpdateCheckResult(true, hasUpdate, CurrentVersion, latestVersion, release.Name, release.HtmlUrl, asset?.Name, asset?.BrowserDownloadUrl, release.Body, null);
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult(true, false, CurrentVersion, null, null, null, null, null, null, exception.Message);
        }
    }

    public static async Task<string> DownloadUpdateAsync(UpdateCheckResult update, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.AssetDownloadUrl))
        {
            throw new InvalidOperationException(Localization.T("다운로드 가능한 업데이트 파일이 없습니다."));
        }

        var updatesDirectory = Path.Combine(AppContext.BaseDirectory, "Updates");
        Directory.CreateDirectory(updatesDirectory);

        var fileName = string.IsNullOrWhiteSpace(update.AssetName)
            ? $"Viewer_{update.LatestVersion}.zip"
            : SanitizeFileName(update.AssetName);
        var destinationPath = Path.Combine(updatesDirectory, fileName);

        progress?.Report(Localization.T("업데이트 파일 다운로드 중..."));
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Viewer-UpdateDownloader/1.0");
        await using var sourceStream = await httpClient.GetStreamAsync(update.AssetDownloadUrl, cancellationToken);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        progress?.Report(Localization.T("업데이트 다운로드 완료"));

        return destinationPath;
    }

    public static void OpenReleasePage(string releasePageUrl)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = releasePageUrl,
            UseShellExecute = true
        });
    }

    public static void OpenUpdatesFolder()
    {
        var updatesDirectory = Path.Combine(AppContext.BaseDirectory, "Updates");
        Directory.CreateDirectory(updatesDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = updatesDirectory,
            UseShellExecute = true
        });
    }

    public static void LaunchUpdater(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            throw new FileNotFoundException(Localization.T("업데이트 파일을 찾을 수 없습니다."), zipPath);
        }

        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var updaterSourcePath = Path.Combine(appDirectory, "Updater.exe");
        if (!File.Exists(updaterSourcePath))
        {
            throw new FileNotFoundException(Localization.T("Updater.exe를 찾을 수 없습니다."), updaterSourcePath);
        }

        var updaterRuntimeDirectory = Path.Combine(appDirectory, "Updates", "UpdaterRuntime");
        if (Directory.Exists(updaterRuntimeDirectory))
        {
            Directory.Delete(updaterRuntimeDirectory, recursive: true);
        }

        Directory.CreateDirectory(updaterRuntimeDirectory);
        foreach (var updaterFile in Directory.GetFiles(appDirectory, "Updater.*"))
        {
            File.Copy(updaterFile, Path.Combine(updaterRuntimeDirectory, Path.GetFileName(updaterFile)), overwrite: true);
        }

        var updaterRuntimePath = Path.Combine(updaterRuntimeDirectory, "Updater.exe");
        var executableName = Path.GetFileName(Application.ExecutablePath);
        var startInfo = new ProcessStartInfo
        {
            FileName = updaterRuntimePath,
            WorkingDirectory = updaterRuntimeDirectory,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add("--zip");
        startInfo.ArgumentList.Add(zipPath);
        startInfo.ArgumentList.Add("--app-dir");
        startInfo.ArgumentList.Add(appDirectory);
        startInfo.ArgumentList.Add("--exe");
        startInfo.ArgumentList.Add(executableName);
        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());

        Process.Start(startInfo);
    }

    private static GitHubReleaseAsset? PickUpdateAsset(List<GitHubReleaseAsset>? assets)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        return assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            .OrderByDescending(asset => asset.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
            .ThenByDescending(asset => asset.Name?.Contains("Viewer", StringComparison.OrdinalIgnoreCase) == true)
            .FirstOrDefault();
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        var latestParts = ParseVersionParts(latestVersion);
        var currentParts = ParseVersionParts(currentVersion);
        var length = Math.Max(latestParts.Count, currentParts.Count);
        for (var index = 0; index < length; index++)
        {
            var latest = index < latestParts.Count ? latestParts[index] : 0;
            var current = index < currentParts.Count ? currentParts[index] : 0;
            if (latest > current)
            {
                return true;
            }

            if (latest < current)
            {
                return false;
            }
        }

        return false;
    }

    private static string NormalizeVersionText(string versionText)
    {
        return versionText.Trim().TrimStart('v', 'V');
    }

    private static List<int> ParseVersionParts(string versionText)
    {
        var normalized = NormalizeVersionText(versionText);
        var parts = new List<int>();
        foreach (var token in normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var digits = new string(token.TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var value))
            {
                parts.Add(value);
            }
        }

        return parts.Count == 0 ? [0] : parts;
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        public string? Body { get; set; }

        public List<GitHubReleaseAsset>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
