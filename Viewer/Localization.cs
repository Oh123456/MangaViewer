using System.Text.Json;

namespace Viewer;

public sealed record LanguageInfo(string Code, string DisplayName)
{
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(DisplayName) ? Code : DisplayName;
    }
}

public static class Localization
{
    private const string DefaultLanguageCode = "kr";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };
    private static readonly Dictionary<string, string> fallbackTexts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> currentTexts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<object, string> originalTexts = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<IndexedOwner, string> originalIndexedTexts = [];
    private static readonly Dictionary<Control, string> originalPlaceholders = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<(ToolTip ToolTip, Control Control), string> originalToolTips = [];

    public static string CurrentLanguageCode { get; private set; } = DefaultLanguageCode;

    public static string TranslationDirectory => Path.Combine(AppContext.BaseDirectory, "Translations");

    public static void Initialize(string? languageCode)
    {
        CurrentLanguageCode = NormalizeLanguageCode(languageCode);
        fallbackTexts.Clear();
        currentTexts.Clear();
        LoadLanguage(DefaultLanguageCode, fallbackTexts);
        if (!string.Equals(CurrentLanguageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            LoadLanguage(CurrentLanguageCode, currentTexts);
        }
    }

    public static void Reload()
    {
        Initialize(AppSettings.Current.LanguageCode);
    }

    public static string T(string key)
    {
        if (currentTexts.TryGetValue(key, out var currentText))
        {
            return currentText;
        }

        return fallbackTexts.TryGetValue(key, out var fallbackText) ? fallbackText : key;
    }

    public static void ApplyTo(Control root, ToolTip? toolTip = null)
    {
        ApplyControl(root, toolTip);
    }

    public static List<LanguageInfo> GetLanguages()
    {
        var manifestPath = Path.Combine(TranslationDirectory, "languages.json");
        try
        {
            if (File.Exists(manifestPath))
            {
                var languages = JsonSerializer.Deserialize<List<LanguageInfo>>(File.ReadAllText(manifestPath), JsonOptions);
                if (languages is not null && languages.Count > 0)
                {
                    return languages;
                }
            }
        }
        catch
        {
            // Fall through to directory discovery.
        }

        if (!Directory.Exists(TranslationDirectory))
        {
            return [new LanguageInfo("kr", "한국어"), new LanguageInfo("en", "English")];
        }

        return Directory.GetDirectories(TranslationDirectory)
            .Select(Path.GetFileName)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => new LanguageInfo(code!, code!))
            .OrderBy(language => language.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void LoadLanguage(string languageCode, Dictionary<string, string> target)
    {
        var languageDirectory = Path.Combine(TranslationDirectory, languageCode);
        if (!Directory.Exists(languageDirectory))
        {
            return;
        }

        foreach (var filePath in Directory.GetFiles(languageDirectory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath), JsonOptions);
                if (entries is null)
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    target[entry.Key] = entry.Value;
                }
            }
            catch
            {
                // A broken language file should not prevent the app from starting.
            }
        }
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode) ? DefaultLanguageCode : languageCode.Trim();
    }

    private static void ApplyControl(Control control, ToolTip? toolTip)
    {
        if (control is not ComboBox)
        {
            ApplyText(control, () => control.Text, value => control.Text = value);
        }
        if (control is TextBox textBox)
        {
            if (!originalPlaceholders.TryGetValue(textBox, out var placeholder))
            {
                placeholder = textBox.PlaceholderText;
                originalPlaceholders[textBox] = placeholder;
            }

            if (!string.IsNullOrWhiteSpace(placeholder))
            {
                textBox.PlaceholderText = T(placeholder);
            }
        }

        if (control is ListView listView)
        {
            foreach (ColumnHeader column in listView.Columns)
            {
                ApplyText(column, () => column.Text, value => column.Text = value);
            }
        }

        if (control is DataGridView dataGridView)
        {
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                ApplyText(column, () => column.HeaderText, value => column.HeaderText = value);
            }
        }

        if (control is ComboBox comboBox && comboBox.Items.Count > 0 && comboBox.Items.Cast<object>().All(item => item is string))
        {
            var selectedIndex = comboBox.SelectedIndex;
            for (var index = 0; index < comboBox.Items.Count; index++)
            {
                var item = comboBox.Items[index]?.ToString() ?? "";
                var key = GetOrStoreOriginal(comboBox.Items, index, item);
                comboBox.Items[index] = T(key);
            }

            if (selectedIndex >= 0 && selectedIndex < comboBox.Items.Count)
            {
                comboBox.SelectedIndex = selectedIndex;
            }
        }

        if (control is ToolStrip toolStrip)
        {
            foreach (ToolStripItem item in toolStrip.Items)
            {
                ApplyToolStripItem(item);
            }
        }

        if (control is TabControl tabControl)
        {
            foreach (TabPage tabPage in tabControl.TabPages)
            {
                ApplyText(tabPage, () => tabPage.Text, value => tabPage.Text = value);
            }
        }

        if (toolTip is not null)
        {
            var key = (toolTip, control);
            if (!originalToolTips.TryGetValue(key, out var originalToolTip))
            {
                originalToolTip = toolTip.GetToolTip(control) ?? "";
                originalToolTips[key] = originalToolTip;
            }

            if (!string.IsNullOrWhiteSpace(originalToolTip))
            {
                toolTip.SetToolTip(control, T(originalToolTip));
            }
        }

        foreach (Control child in control.Controls)
        {
            ApplyControl(child, toolTip);
        }
    }

    private static void ApplyToolStripItem(ToolStripItem item)
    {
        ApplyText(item, () => item.Text ?? "", value => item.Text = value);
        if (item is ToolStripDropDownItem dropDownItem)
        {
            foreach (ToolStripItem child in dropDownItem.DropDownItems)
            {
                ApplyToolStripItem(child);
            }
        }
    }

    private static void ApplyText(object owner, Func<string> getter, Action<string> setter)
    {
        var current = getter();
        if (string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        if (!originalTexts.TryGetValue(owner, out var original))
        {
            original = current;
            originalTexts[owner] = original;
        }

        setter(T(original));
    }

    private static string GetOrStoreOriginal(object owner, int index, string current)
    {
        var keyObject = new IndexedOwner(owner, index);
        if (originalIndexedTexts.TryGetValue(keyObject, out var original))
        {
            return original;
        }

        originalIndexedTexts[keyObject] = current;
        return current;
    }

    private sealed record IndexedOwner(object Owner, int Index);

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
