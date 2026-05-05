using System.Text.Json;

namespace Viewer;

public sealed class AppSettings
{
    public const string DefaultUpdateReleaseApiUrl = "https://api.github.com/repos/Oh123456/MangaViewer/releases/latest";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static AppSettings? current;

    public WindowPlacement MainWindow { get; set; } = new();
    public WindowPlacement ViewerWindow { get; set; } = new();
    public List<int> FolderListColumnWidths { get; set; } = [];
    public bool ViewerFitToWindow { get; set; } = true;
    public bool ViewerFullscreen { get; set; }
    public bool ViewerAsyncImageLoading { get; set; } = true;
    public bool AutoRefreshPathStatusAfterScan { get; set; }
    public int PartialDuplicateThresholdPercent { get; set; } = 80;
    public string LanguageCode { get; set; } = "kr";
    public bool AutoCheckForUpdates { get; set; } = true;
    public string UpdateReleaseApiUrl { get; set; } = DefaultUpdateReleaseApiUrl;

    public static AppSettings Current => current ??= Load();

    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "viewer.settings.json");

    public static void Save()
    {
        Directory.CreateDirectory(AppContext.BaseDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, JsonOptions));
    }

    public static void Reload()
    {
        current = Load();
    }

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return Normalize(new AppSettings());
            }

            return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings());
        }
        catch
        {
            return Normalize(new AppSettings());
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.UpdateReleaseApiUrl))
        {
            settings.UpdateReleaseApiUrl = DefaultUpdateReleaseApiUrl;
        }

        return settings;
    }
}

public sealed class WindowPlacement
{
    public bool HasBounds { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public FormWindowState WindowState { get; set; } = FormWindowState.Normal;

    public Rectangle Bounds
    {
        get => new(X, Y, Width, Height);
        set
        {
            HasBounds = true;
            X = value.X;
            Y = value.Y;
            Width = value.Width;
            Height = value.Height;
        }
    }
}
