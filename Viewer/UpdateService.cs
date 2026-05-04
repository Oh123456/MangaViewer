using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Viewer;

public sealed record UpdateCheckResult(
    bool IsConfigured,
    bool HasUpdate,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseName,
    string? ReleasePageUrl,
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
            return new UpdateCheckResult(false, false, CurrentVersion, null, null, null, null, Localization.T("업데이트 URL이 설정되지 않았습니다."));
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
                return new UpdateCheckResult(true, false, CurrentVersion, null, null, null, null, Localization.T("릴리즈 정보를 읽을 수 없습니다."));
            }

            var latestVersion = NormalizeVersionText(release.TagName);
            var hasUpdate = IsNewerVersion(latestVersion, CurrentVersion);
            return new UpdateCheckResult(true, hasUpdate, CurrentVersion, latestVersion, release.Name, release.HtmlUrl, release.Body, null);
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult(true, false, CurrentVersion, null, null, null, null, exception.Message);
        }
    }

    public static void OpenReleasePage(string releasePageUrl)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = releasePageUrl,
            UseShellExecute = true
        });
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
    }
}
