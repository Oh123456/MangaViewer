using System.Text.RegularExpressions;

namespace Viewer;

public static partial class FolderNameParser
{
    public static (string DisplayName, string? Author, string? Number) Parse(string folderName)
    {
        var name = folderName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ("Untitled", null, null);
        }

        var match = FullPattern().Match(name);
        if (!match.Success)
        {
            match = AuthorOnlyPattern().Match(name);
        }

        if (!match.Success)
        {
            match = NumberOnlyPattern().Match(name);
        }

        if (!match.Success)
        {
            return (name, null, null);
        }

        var displayName = match.Groups["name"].Value.Trim();
        var author = GetGroupValue(match, "author");
        var number = GetGroupValue(match, "number");

        return (string.IsNullOrWhiteSpace(displayName) ? name : displayName, author, number);
    }

    private static string? GetGroupValue(Match match, string groupName)
    {
        var group = match.Groups[groupName];
        return group.Success && !string.IsNullOrWhiteSpace(group.Value) ? group.Value.Trim() : null;
    }

    [GeneratedRegex(@"^\[(?<author>[^\]]+)\]\s*(?<name>.+?)\s*\((?<number>[^)]+)\)\s*$")]
    private static partial Regex FullPattern();

    [GeneratedRegex(@"^\[(?<author>[^\]]+)\]\s*(?<name>.+?)\s*$")]
    private static partial Regex AuthorOnlyPattern();

    [GeneratedRegex(@"^(?<name>.+?)\s*\((?<number>[^)]+)\)\s*$")]
    private static partial Regex NumberOnlyPattern();
}
