using Microsoft.Data.Sqlite;

namespace Viewer;

public sealed class AppDatabase
{
    private readonly string connectionString;

    public AppDatabase()
    {
        DatabasePath = Path.Combine(AppContext.BaseDirectory, "viewer.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();
    }

    public string DatabasePath { get; }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Roots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL,
                Kind TEXT NOT NULL DEFAULT 'Main',
                MediaKind TEXT NOT NULL DEFAULT 'Image',
                CreatedAt TEXT NOT NULL,
                UNIQUE(Path, Kind, MediaKind)
            );

            CREATE TABLE IF NOT EXISTS Folders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                Author TEXT NULL,
                Number TEXT NULL,
                SeriesName TEXT NULL,
                SeriesOrder INTEGER NULL,
                Score INTEGER NOT NULL DEFAULT 0,
                Memo TEXT NULL,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                IsReserved INTEGER NOT NULL DEFAULT 0,
                ViewCount INTEGER NOT NULL DEFAULT 0,
                LastViewedAt TEXT NULL,
                LastImagePath TEXT NULL,
                DirectoryModifiedAt TEXT NULL,
                FolderModifiedAt TEXT NULL,
                ImageCount INTEGER NOT NULL DEFAULT 0,
                TotalImageBytes INTEGER NOT NULL DEFAULT 0,
                VideoCount INTEGER NOT NULL DEFAULT 0,
                TotalVideoBytes INTEGER NOT NULL DEFAULT 0,
                ThumbnailPath TEXT NULL,
                PathExists INTEGER NOT NULL DEFAULT 1,
                PathCheckedAt TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Images (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderId INTEGER NOT NULL,
                Path TEXT NOT NULL UNIQUE,
                FileName TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                ModifiedAt TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                IsBookmarked INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (FolderId) REFERENCES Folders(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Videos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderId INTEGER NOT NULL,
                Path TEXT NOT NULL UNIQUE,
                FileName TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                ModifiedAt TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                FOREIGN KEY (FolderId) REFERENCES Folders(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Tags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS FolderTags (
                FolderId INTEGER NOT NULL,
                TagId INTEGER NOT NULL,
                PRIMARY KEY (FolderId, TagId),
                FOREIGN KEY (FolderId) REFERENCES Folders(Id) ON DELETE CASCADE,
                FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
            );
            """;
        command.ExecuteNonQuery();
        MigrateRootsSchema(connection);
        EnsureColumn(connection, "Roots", "Kind", "TEXT NOT NULL DEFAULT 'Main'");
        EnsureColumn(connection, "Roots", "MediaKind", "TEXT NOT NULL DEFAULT 'Image'");
        EnsureColumn(connection, "Folders", "LastImagePath", "TEXT NULL");
        EnsureColumn(connection, "Folders", "IsReserved", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Folders", "SeriesName", "TEXT NULL");
        EnsureColumn(connection, "Folders", "SeriesOrder", "INTEGER NULL");
        EnsureColumn(connection, "Folders", "DirectoryModifiedAt", "TEXT NULL");
        EnsureColumn(connection, "Folders", "FolderModifiedAt", "TEXT NULL");
        EnsureColumn(connection, "Folders", "ImageCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Folders", "TotalImageBytes", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Folders", "VideoCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Folders", "TotalVideoBytes", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Folders", "PathExists", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "Folders", "PathCheckedAt", "TEXT NULL");
        EnsureColumn(connection, "Images", "IsBookmarked", "INTEGER NOT NULL DEFAULT 0");
        EnsureIndexes(connection);
        BackfillFolderModifiedAt(connection);
        BackfillFolderScanStats(connection);
    }

    public List<string> GetRoots(RootKind? kind = null, MediaKind? mediaKind = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var where = new List<string>();
        if (kind is not null)
        {
            where.Add("Kind = $kind");
            command.Parameters.AddWithValue("$kind", ToRootKind(kind.Value));
        }
        if (mediaKind is not null)
        {
            where.Add("MediaKind = $mediaKind");
            command.Parameters.AddWithValue("$mediaKind", ToMediaKind(mediaKind.Value));
        }

        command.CommandText = $"""
            SELECT Path
            FROM Roots
            {(where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where))}
            ORDER BY MediaKind, Kind, Path;
            """;

        using var reader = command.ExecuteReader();
        var roots = new List<string>();
        while (reader.Read())
        {
            roots.Add(reader.GetString(0));
        }

        return roots;
    }

    public void AddRoot(string path, RootKind kind = RootKind.Main, MediaKind mediaKind = MediaKind.Image)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Roots (Path, Kind, MediaKind, CreatedAt)
            VALUES ($path, $kind, $mediaKind, $createdAt)
            ON CONFLICT(Path, Kind, MediaKind) DO UPDATE SET
                CreatedAt = CreatedAt;
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$kind", ToRootKind(kind));
        command.Parameters.AddWithValue("$mediaKind", ToMediaKind(mediaKind));
        command.Parameters.AddWithValue("$createdAt", ToDb(DateTime.Now));
        command.ExecuteNonQuery();
    }

    public void DeleteRoots(IEnumerable<string> paths)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var path in paths)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Roots WHERE Path = $path;";
            command.Parameters.AddWithValue("$path", path);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeleteRoots(IEnumerable<(string Path, RootKind Kind, MediaKind MediaKind)> roots)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var root in roots)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Roots WHERE Path = $path AND Kind = $kind AND MediaKind = $mediaKind;";
            command.Parameters.AddWithValue("$path", root.Path);
            command.Parameters.AddWithValue("$kind", ToRootKind(root.Kind));
            command.Parameters.AddWithValue("$mediaKind", ToMediaKind(root.MediaKind));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void RenameRootPath(string oldPath, string newPath)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE Roots SET Path = $newPath WHERE Path = $oldPath;";
            command.Parameters.AddWithValue("$oldPath", oldPath);
            command.Parameters.AddWithValue("$newPath", newPath);
            command.ExecuteNonQuery();
        }

        UpdatePathPrefix(connection, transaction, oldPath, newPath);
        transaction.Commit();
    }

    public void UpdatePathPrefix(string oldPath, string newPath)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpdatePathPrefix(connection, transaction, oldPath, newPath);
        transaction.Commit();
    }

    public Dictionary<string, FolderScanSignature> GetFolderScanSignatureMap(RootKind? rootKind = null, MediaKind? mediaKind = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var where = new List<string> { "FolderModifiedAt IS NOT NULL" };
        ApplyRootKindFilter(where, rootKind, mediaKind);
        command.CommandText = $"""
            SELECT Path, DirectoryModifiedAt, FolderModifiedAt, ImageCount, TotalImageBytes, VideoCount, TotalVideoBytes
            FROM Folders
            WHERE {string.Join(" AND ", where)};
            """;
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, FolderScanSignature>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            var modifiedAt = FromDb(reader.GetString(2));
            if (modifiedAt is not null)
            {
                result[reader.GetString(0)] = new FolderScanSignature
                {
                    DirectoryModifiedAt = reader.IsDBNull(1) ? null : FromDb(reader.GetString(1)),
                    FolderModifiedAt = modifiedAt.Value,
                    ImageCount = reader.GetInt32(3),
                    TotalImageBytes = reader.GetInt64(4),
                    VideoCount = reader.GetInt32(5),
                    TotalVideoBytes = reader.GetInt64(6)
                };
            }
        }

        return result;
    }

    public List<FolderItem> GetFolders(FolderListMode mode, FolderSortMode sortMode, FolderSearchField searchField, string searchText, IReadOnlyList<string> tagFilters, IReadOnlyList<string> excludedTagFilters, TagFilterMode tagFilterMode, QuickFilterMode quickFilterMode = QuickFilterMode.All, bool videoMode = false)
    {
        return GetFoldersPage(mode, sortMode, searchField, searchText, tagFilters, excludedTagFilters, tagFilterMode, quickFilterMode, 0, int.MaxValue, videoMode: videoMode).Items;
    }

    public List<FolderItem> GetFoldersByIds(IReadOnlyList<long> folderIds)
    {
        var idList = folderIds
            .Distinct()
            .ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        using var connection = OpenConnection();
        var folders = new List<FolderItem>();
        foreach (var chunk in idList.Chunk(800))
        {
            using var command = connection.CreateCommand();
            var parameters = new List<string>();
            for (var index = 0; index < chunk.Length; index++)
            {
                var parameterName = $"$folderId{index}";
                parameters.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, chunk[index]);
            }

            command.CommandText = $"""
                SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt, VideoCount, TotalVideoBytes
                FROM Folders
                WHERE Id IN ({string.Join(", ", parameters)});
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add(ReadFolder(reader));
            }
        }

        var tags = GetTagsForFolders(connection, folders.Select(folder => folder.Id).ToList());
        foreach (var folder in folders)
        {
            if (tags.TryGetValue(folder.Id, out var folderTags))
            {
                folder.Tags = folderTags;
            }
        }

        return folders;
    }

    public PagedFolderResult GetFoldersPage(FolderListMode mode, FolderSortMode sortMode, FolderSearchField searchField, string searchText, IReadOnlyList<string> tagFilters, IReadOnlyList<string> excludedTagFilters, TagFilterMode tagFilterMode, QuickFilterMode quickFilterMode, int offset, int limit, bool descending = false, bool videoMode = false)
    {
        using var connection = OpenConnection();
        var folders = new List<FolderItem>();
        using (var command = connection.CreateCommand())
        {
            var where = new List<string>();
            if (mode == FolderListMode.Favorites)
            {
                where.Add("IsFavorite = 1");
            }
            else if (mode == FolderListMode.Recent)
            {
                where.Add("LastViewedAt IS NOT NULL");
            }
            else if (mode == FolderListMode.Reserved)
            {
                where.Add("IsReserved = 1");
            }
            else if (mode == FolderListMode.Series)
            {
                where.Add(BuildSeriesRepresentativeCondition(videoMode));
            }

            ApplyRootModeFilter(where, mode, videoMode);
            ApplyMediaModeFilter(where, videoMode);

            if (quickFilterMode == QuickFilterMode.Unviewed)
            {
                where.Add("LastViewedAt IS NULL");
            }
            else if (quickFilterMode == QuickFilterMode.NoScore)
            {
                where.Add("Score = 0");
            }
            else if (quickFilterMode == QuickFilterMode.NoTags)
            {
                where.Add("NOT EXISTS (SELECT 1 FROM FolderTags ft WHERE ft.FolderId = Folders.Id)");
            }
            else if (quickFilterMode == QuickFilterMode.NoSeries)
            {
                where.Add("(SeriesName IS NULL OR TRIM(SeriesName) = '')");
            }
            else if (quickFilterMode == QuickFilterMode.NoThumbnail)
            {
                where.Add("(ThumbnailPath IS NULL OR TRIM(ThumbnailPath) = '')");
            }
            else if (quickFilterMode == QuickFilterMode.BrokenPath)
            {
                where.Add("PathExists = 0");
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchColumn = searchField switch
                {
                    FolderSearchField.Author => "Author",
                    FolderSearchField.Memo => "Memo",
                    FolderSearchField.Path => "Path",
                    FolderSearchField.Series => "SeriesName",
                    _ => "DisplayName"
                };
                where.Add($"{searchColumn} LIKE $search");
                command.Parameters.AddWithValue("$search", $"%{searchText.Trim()}%");
            }

            if (tagFilterMode != TagFilterMode.Or)
            {
                for (var tagIndex = 0; tagIndex < tagFilters.Count; tagIndex++)
                {
                    var parameterName = $"$tagFilter{tagIndex}";
                    where.Add($"EXISTS (SELECT 1 FROM FolderTags ft JOIN Tags t ON t.Id = ft.TagId WHERE ft.FolderId = Folders.Id AND t.Name = {parameterName})");
                    command.Parameters.AddWithValue(parameterName, tagFilters[tagIndex]);
                }
            }
            else if (tagFilters.Count > 0)
            {
                var tagConditions = new List<string>();
                for (var tagIndex = 0; tagIndex < tagFilters.Count; tagIndex++)
                {
                    var parameterName = $"$tagFilter{tagIndex}";
                    tagConditions.Add($"t.Name = {parameterName}");
                    command.Parameters.AddWithValue(parameterName, tagFilters[tagIndex]);
                }

                where.Add($"EXISTS (SELECT 1 FROM FolderTags ft JOIN Tags t ON t.Id = ft.TagId WHERE ft.FolderId = Folders.Id AND ({string.Join(" OR ", tagConditions)}))");
            }

            for (var tagIndex = 0; tagIndex < excludedTagFilters.Count; tagIndex++)
            {
                var parameterName = $"$excludedTagFilter{tagIndex}";
                where.Add($"NOT EXISTS (SELECT 1 FROM FolderTags ft JOIN Tags t ON t.Id = ft.TagId WHERE ft.FolderId = Folders.Id AND t.Name = {parameterName})");
                command.Parameters.AddWithValue(parameterName, excludedTagFilters[tagIndex]);
            }

            var mediaCountColumn = videoMode ? "VideoCount" : "ImageCount";
            var orderBy = sortMode switch
            {
                FolderSortMode.Date when descending => "FolderModifiedAt ASC NULLS LAST, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Date => "FolderModifiedAt DESC NULLS LAST, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Name when descending => "DisplayName COLLATE NOCASE DESC, Path COLLATE NOCASE DESC",
                FolderSortMode.Name => "DisplayName COLLATE NOCASE ASC, Path COLLATE NOCASE ASC",
                FolderSortMode.Author when descending => "Author COLLATE NOCASE DESC, DisplayName COLLATE NOCASE DESC",
                FolderSortMode.Author => "Author COLLATE NOCASE ASC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Score when descending => "Score ASC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Score => "Score DESC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Series when descending => "SeriesName COLLATE NOCASE DESC NULLS LAST, SeriesOrder IS NULL, SeriesOrder DESC, DisplayName COLLATE NOCASE DESC",
                FolderSortMode.Series => "SeriesName COLLATE NOCASE ASC NULLS LAST, SeriesOrder IS NULL, SeriesOrder ASC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.ImageCount when descending => $"{mediaCountColumn} ASC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.ImageCount => $"{mediaCountColumn} DESC, DisplayName COLLATE NOCASE ASC",
                _ when descending => "LastViewedAt ASC NULLS LAST, UpdatedAt ASC",
                _ => "LastViewedAt DESC NULLS LAST, UpdatedAt DESC"
            };

            command.CommandText = $"""
                SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt, VideoCount, TotalVideoBytes
                FROM Folders
                {(where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where))}
                ORDER BY {orderBy}
                LIMIT $limit OFFSET $offset;
                """;
            command.Parameters.AddWithValue("$limit", limit);
            command.Parameters.AddWithValue("$offset", offset);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add(ReadFolder(reader));
            }
        }

        int totalCount;
        using (var countCommand = connection.CreateCommand())
        {
            var where = new List<string>();
            if (mode == FolderListMode.Favorites)
            {
                where.Add("IsFavorite = 1");
            }
            else if (mode == FolderListMode.Recent)
            {
                where.Add("LastViewedAt IS NOT NULL");
            }
            else if (mode == FolderListMode.Reserved)
            {
                where.Add("IsReserved = 1");
            }
            else if (mode == FolderListMode.Series)
            {
                where.Add(BuildSeriesRepresentativeCondition(videoMode));
            }

            ApplyRootModeFilter(where, mode, videoMode);
            ApplyMediaModeFilter(where, videoMode);

            if (quickFilterMode == QuickFilterMode.Unviewed)
            {
                where.Add("LastViewedAt IS NULL");
            }
            else if (quickFilterMode == QuickFilterMode.NoScore)
            {
                where.Add("Score = 0");
            }
            else if (quickFilterMode == QuickFilterMode.NoTags)
            {
                where.Add("NOT EXISTS (SELECT 1 FROM FolderTags ft WHERE ft.FolderId = Folders.Id)");
            }
            else if (quickFilterMode == QuickFilterMode.NoSeries)
            {
                where.Add("(SeriesName IS NULL OR TRIM(SeriesName) = '')");
            }
            else if (quickFilterMode == QuickFilterMode.NoThumbnail)
            {
                where.Add("(ThumbnailPath IS NULL OR TRIM(ThumbnailPath) = '')");
            }
            else if (quickFilterMode == QuickFilterMode.BrokenPath)
            {
                where.Add("PathExists = 0");
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchColumn = searchField switch
                {
                    FolderSearchField.Author => "Author",
                    FolderSearchField.Memo => "Memo",
                    FolderSearchField.Path => "Path",
                    FolderSearchField.Series => "SeriesName",
                    _ => "DisplayName"
                };
                where.Add($"{searchColumn} LIKE $search");
                countCommand.Parameters.AddWithValue("$search", $"%{searchText.Trim()}%");
            }

            if (tagFilterMode != TagFilterMode.Or)
            {
                for (var tagIndex = 0; tagIndex < tagFilters.Count; tagIndex++)
                {
                    var parameterName = $"$tagFilter{tagIndex}";
                    where.Add($"EXISTS (SELECT 1 FROM FolderTags ft JOIN Tags t ON t.Id = ft.TagId WHERE ft.FolderId = Folders.Id AND t.Name = {parameterName})");
                    countCommand.Parameters.AddWithValue(parameterName, tagFilters[tagIndex]);
                }
            }
            else if (tagFilters.Count > 0)
            {
                var tagConditions = new List<string>();
                for (var tagIndex = 0; tagIndex < tagFilters.Count; tagIndex++)
                {
                    var parameterName = $"$tagFilter{tagIndex}";
                    tagConditions.Add($"t.Name = {parameterName}");
                    countCommand.Parameters.AddWithValue(parameterName, tagFilters[tagIndex]);
                }

                where.Add($"EXISTS (SELECT 1 FROM FolderTags ft JOIN Tags t ON t.Id = ft.TagId WHERE ft.FolderId = Folders.Id AND ({string.Join(" OR ", tagConditions)}))");
            }

            for (var tagIndex = 0; tagIndex < excludedTagFilters.Count; tagIndex++)
            {
                var parameterName = $"$excludedTagFilter{tagIndex}";
                where.Add($"NOT EXISTS (SELECT 1 FROM FolderTags ft JOIN Tags t ON t.Id = ft.TagId WHERE ft.FolderId = Folders.Id AND t.Name = {parameterName})");
                countCommand.Parameters.AddWithValue(parameterName, excludedTagFilters[tagIndex]);
            }

            countCommand.CommandText = $"""
                SELECT COUNT(*)
                FROM Folders
                {(where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where))};
                """;
            totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
        }

        var tags = GetTagsForFolders(connection, folders.Select(folder => folder.Id).ToList());
        foreach (var folder in folders)
        {
            if (tags.TryGetValue(folder.Id, out var folderTags))
            {
                folder.Tags = folderTags;
            }
        }

        return new PagedFolderResult
        {
            Items = folders,
            TotalCount = totalCount
        };
    }

    public List<string> GetTags()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Tags ORDER BY Name COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var tags = new List<string>();
        while (reader.Read())
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    public List<string> GetSeriesNames()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT SeriesName
            FROM Folders
            WHERE SeriesName IS NOT NULL AND TRIM(SeriesName) <> ''
            ORDER BY SeriesName COLLATE NOCASE;
            """;
        using var reader = command.ExecuteReader();
        var seriesNames = new List<string>();
        while (reader.Read())
        {
            seriesNames.Add(reader.GetString(0));
        }

        return seriesNames;
    }

    public List<FolderItem> GetFoldersBySeries(string seriesName)
    {
        using var connection = OpenConnection();
        var folders = new List<FolderItem>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt, VideoCount, TotalVideoBytes
                FROM Folders
                WHERE SeriesName = $seriesName
                ORDER BY SeriesOrder IS NULL,
                         SeriesOrder ASC,
                         DisplayName COLLATE NOCASE ASC,
                         Id ASC;
                """;
            command.Parameters.AddWithValue("$seriesName", seriesName);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add(ReadFolder(reader));
            }
        }

        var tags = GetTagsForFolders(connection);
        foreach (var folder in folders)
        {
            if (tags.TryGetValue(folder.Id, out var folderTags))
            {
                folder.Tags = folderTags;
            }
        }

        return folders;
    }

    public int GetSeriesMaxOrder(string seriesName)
    {
        if (string.IsNullOrWhiteSpace(seriesName))
        {
            return 0;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(MAX(SeriesOrder), 0)
            FROM Folders
            WHERE SeriesName = $seriesName;
            """;
        command.Parameters.AddWithValue("$seriesName", seriesName);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public Dictionary<string, int> GetSeriesImageCounts(IEnumerable<string> seriesNames)
    {
        var names = seriesNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
        {
            return result;
        }

        using var connection = OpenConnection();
        foreach (var chunk in names.Chunk(800))
        {
            using var command = connection.CreateCommand();
            var parameters = new List<string>();
            for (var index = 0; index < chunk.Length; index++)
            {
                var parameterName = $"$seriesName{index}";
                parameters.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, chunk[index]);
            }

            command.CommandText = $"""
                SELECT SeriesName, SUM(ImageCount)
                FROM Folders
                WHERE SeriesName IN ({string.Join(", ", parameters)})
                GROUP BY SeriesName COLLATE NOCASE;
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result[reader.GetString(0)] = Convert.ToInt32(reader.GetInt64(1));
            }
        }

        return result;
    }

    public Dictionary<string, FolderItem> GetFirstFoldersInSeries(IEnumerable<string> seriesNames, bool videoMode = false)
    {
        var names = seriesNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new Dictionary<string, FolderItem>(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0)
        {
            return result;
        }

        using var connection = OpenConnection();
        foreach (var chunk in names.Chunk(800))
        {
            var folders = new List<FolderItem>();
            using (var command = connection.CreateCommand())
            {
                var parameters = new List<string>();
                for (var index = 0; index < chunk.Length; index++)
                {
                    var parameterName = $"$seriesName{index}";
                    parameters.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, chunk[index]);
                }

                var mediaCondition = videoMode ? "VideoCount > 0" : "ImageCount > 0";
                command.CommandText = $"""
                    SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt, VideoCount, TotalVideoBytes
                    FROM Folders
                    WHERE SeriesName IN ({string.Join(", ", parameters)})
                      AND {mediaCondition}
                    ORDER BY SeriesName COLLATE NOCASE ASC,
                             SeriesOrder IS NULL,
                             SeriesOrder ASC,
                             DisplayName COLLATE NOCASE ASC,
                             Id ASC;
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    folders.Add(ReadFolder(reader));
                }
            }

            var tags = GetTagsForFolders(connection, folders.Select(folder => folder.Id).ToList());
            foreach (var folder in folders)
            {
                if (string.IsNullOrWhiteSpace(folder.SeriesName) || result.ContainsKey(folder.SeriesName))
                {
                    continue;
                }

                if (tags.TryGetValue(folder.Id, out var folderTags))
                {
                    folder.Tags = folderTags;
                }

                result[folder.SeriesName] = folder;
            }
        }

        return result;
    }

    public List<DuplicateNameGroup> GetDuplicateNameGroups()
    {
        using var connection = OpenConnection();
        var duplicateNames = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT DisplayName
                FROM Folders
                GROUP BY DisplayName COLLATE NOCASE
                HAVING COUNT(*) > 1
                ORDER BY DisplayName COLLATE NOCASE;
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                duplicateNames.Add(reader.GetString(0));
            }
        }

        var result = new List<DuplicateNameGroup>();
        foreach (var displayName in duplicateNames)
        {
            var folders = new List<FolderItem>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt, VideoCount, TotalVideoBytes
                    FROM Folders
                    WHERE DisplayName = $displayName COLLATE NOCASE
                    ORDER BY FolderModifiedAt DESC NULLS LAST, Path COLLATE NOCASE ASC;
                    """;
                command.Parameters.AddWithValue("$displayName", displayName);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    folders.Add(ReadFolder(reader));
                }
            }

            if (folders.Count > 1)
            {
                result.Add(new DuplicateNameGroup
                {
                    DisplayName = displayName,
                    Folders = folders
                });
            }
        }

        return result;
    }

    public List<SeriesQualityIssue> GetSeriesQualityIssues()
    {
        var issues = new List<SeriesQualityIssue>();
        foreach (var seriesName in GetSeriesNames())
        {
            var folders = GetFoldersBySeries(seriesName);
            if (folders.Count == 0)
            {
                continue;
            }

            var missingOrderFolders = folders
                .Where(folder => folder.SeriesOrder is null || folder.SeriesOrder <= 0)
                .ToList();
            if (missingOrderFolders.Count > 0)
            {
                issues.Add(new SeriesQualityIssue
                {
                    SeriesName = seriesName,
                    IssueType = "편수 미지정",
                    Detail = "편수가 비어 있거나 0 이하입니다.",
                    FolderNames = string.Join(", ", missingOrderFolders.Select(folder => folder.DisplayName))
                });
            }

            var orderedFolders = folders
                .Where(folder => folder.SeriesOrder is > 0)
                .ToList();
            if (orderedFolders.Count == 0)
            {
                continue;
            }

            if (orderedFolders.All(folder => folder.SeriesOrder != 1))
            {
                issues.Add(new SeriesQualityIssue
                {
                    SeriesName = seriesName,
                    IssueType = "1편 없음",
                    Detail = "묶음에 1편으로 지정된 폴더가 없습니다.",
                    FolderNames = string.Join(", ", orderedFolders.Select(folder => folder.DisplayName))
                });
            }

            foreach (var duplicateGroup in orderedFolders.GroupBy(folder => folder.SeriesOrder).Where(group => group.Count() > 1))
            {
                issues.Add(new SeriesQualityIssue
                {
                    SeriesName = seriesName,
                    IssueType = "편수 중복",
                    Detail = $"{duplicateGroup.Key}편이 {duplicateGroup.Count()}개 있습니다.",
                    FolderNames = string.Join(", ", duplicateGroup.Select(folder => folder.DisplayName))
                });
            }

            var maxOrder = orderedFolders.Max(folder => folder.SeriesOrder ?? 0);
            var existingOrders = orderedFolders
                .Select(folder => folder.SeriesOrder ?? 0)
                .Where(order => order > 0)
                .ToHashSet();
            var missingOrders = Enumerable.Range(1, maxOrder)
                .Where(order => !existingOrders.Contains(order))
                .ToList();
            if (missingOrders.Count > 0)
            {
                issues.Add(new SeriesQualityIssue
                {
                    SeriesName = seriesName,
                    IssueType = "편수 누락",
                    Detail = $"누락된 편수: {string.Join(", ", missingOrders)}",
                    FolderNames = string.Join(", ", orderedFolders.Select(folder => folder.DisplayName))
                });
            }
        }

        return issues
            .OrderBy(issue => issue.SeriesName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.IssueType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void RenameTag(string oldName, string newName)
    {
        oldName = oldName.Trim();
        newName = newName.Trim();
        if (oldName.Length == 0 || newName.Length == 0 || string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        long oldTagId;
        using (var getOldTag = connection.CreateCommand())
        {
            getOldTag.Transaction = transaction;
            getOldTag.CommandText = "SELECT Id FROM Tags WHERE Name = $name;";
            getOldTag.Parameters.AddWithValue("$name", oldName);
            var result = getOldTag.ExecuteScalar();
            if (result is null || result == DBNull.Value)
            {
                return;
            }

            oldTagId = (long)result;
        }

        long? existingTagId = null;
        using (var getExistingTag = connection.CreateCommand())
        {
            getExistingTag.Transaction = transaction;
            getExistingTag.CommandText = "SELECT Id FROM Tags WHERE Name = $name;";
            getExistingTag.Parameters.AddWithValue("$name", newName);
            var result = getExistingTag.ExecuteScalar();
            if (result is not null && result != DBNull.Value)
            {
                existingTagId = (long)result;
            }
        }

        if (existingTagId is null)
        {
            using var rename = connection.CreateCommand();
            rename.Transaction = transaction;
            rename.CommandText = "UPDATE Tags SET Name = $newName WHERE Id = $oldTagId;";
            rename.Parameters.AddWithValue("$newName", newName);
            rename.Parameters.AddWithValue("$oldTagId", oldTagId);
            rename.ExecuteNonQuery();
        }
        else
        {
            using (var merge = connection.CreateCommand())
            {
                merge.Transaction = transaction;
                merge.CommandText = """
                    UPDATE OR IGNORE FolderTags
                    SET TagId = $existingTagId
                    WHERE TagId = $oldTagId;
                    """;
                merge.Parameters.AddWithValue("$existingTagId", existingTagId.Value);
                merge.Parameters.AddWithValue("$oldTagId", oldTagId);
                merge.ExecuteNonQuery();
            }

            using var deleteOld = connection.CreateCommand();
            deleteOld.Transaction = transaction;
            deleteOld.CommandText = "DELETE FROM Tags WHERE Id = $oldTagId;";
            deleteOld.Parameters.AddWithValue("$oldTagId", oldTagId);
            deleteOld.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeleteTag(string tagName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tags WHERE Name = $name;";
        command.Parameters.AddWithValue("$name", tagName.Trim());
        command.ExecuteNonQuery();
    }

    public void DeleteTags(IEnumerable<string> tagNames)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var tagName in tagNames)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Tags WHERE Name = $name;";
            command.Parameters.AddWithValue("$name", tagName.Trim());
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<ImageItem> GetImages(long folderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FolderId, Path, FileName, FileSize, ModifiedAt, SortOrder, IsBookmarked
            FROM Images
            WHERE FolderId = $folderId
            ORDER BY SortOrder ASC, FileName COLLATE NOCASE ASC;
            """;
        command.Parameters.AddWithValue("$folderId", folderId);

        using var reader = command.ExecuteReader();
        var images = new List<ImageItem>();
        while (reader.Read())
        {
            images.Add(new ImageItem
            {
                Id = reader.GetInt64(0),
                FolderId = reader.GetInt64(1),
                Path = reader.GetString(2),
                FileName = reader.GetString(3),
                FileSize = reader.GetInt64(4),
                ModifiedAt = FromDb(reader.GetString(5)) ?? DateTime.MinValue,
                SortOrder = reader.GetInt32(6),
                IsBookmarked = reader.GetInt32(7) == 1
            });
        }

        return images
            .OrderBy(image => image.FileName, NaturalStringComparer.OrdinalIgnoreCase)
            .ThenBy(image => image.SortOrder)
            .ToList();
    }

    public List<ImageItem> GetSeriesImages(string seriesName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Images.Id, Images.FolderId, Images.Path, Images.FileName, Images.FileSize, Images.ModifiedAt, Images.SortOrder, Images.IsBookmarked, Folders.DisplayName, Folders.SeriesOrder
            FROM Images
            JOIN Folders ON Folders.Id = Images.FolderId
            WHERE Folders.SeriesName = $seriesName
            ORDER BY Folders.SeriesOrder IS NULL,
                     Folders.SeriesOrder ASC,
                     Folders.DisplayName COLLATE NOCASE ASC,
                     Images.SortOrder ASC,
                     Images.FileName COLLATE NOCASE ASC;
            """;
        command.Parameters.AddWithValue("$seriesName", seriesName);

        using var reader = command.ExecuteReader();
        var images = new List<ImageItem>();
        while (reader.Read())
        {
            images.Add(new ImageItem
            {
                Id = reader.GetInt64(0),
                FolderId = reader.GetInt64(1),
                Path = reader.GetString(2),
                FileName = reader.GetString(3),
                FileSize = reader.GetInt64(4),
                ModifiedAt = FromDb(reader.GetString(5)) ?? DateTime.MinValue,
                SortOrder = reader.GetInt32(6),
                IsBookmarked = reader.GetInt32(7) == 1,
                FolderDisplayName = reader.IsDBNull(8) ? null : reader.GetString(8),
                FolderSeriesOrder = reader.IsDBNull(9) ? null : reader.GetInt32(9)
            });
        }

        return images
            .OrderBy(image => image.FolderSeriesOrder is null)
            .ThenBy(image => image.FolderSeriesOrder)
            .ThenBy(image => image.FolderDisplayName ?? "", NaturalStringComparer.OrdinalIgnoreCase)
            .ThenBy(image => image.FileName, NaturalStringComparer.OrdinalIgnoreCase)
            .ThenBy(image => image.SortOrder)
            .ToList();
    }

    public void SetImageBookmark(long imageId, bool isBookmarked)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Images
            SET IsBookmarked = $isBookmarked
            WHERE Id = $imageId;
            """;
        command.Parameters.AddWithValue("$imageId", imageId);
        command.Parameters.AddWithValue("$isBookmarked", isBookmarked ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public List<VideoItem> GetVideos(long folderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FolderId, Path, FileName, FileSize, ModifiedAt, SortOrder
            FROM Videos
            WHERE FolderId = $folderId
            ORDER BY SortOrder ASC, FileName COLLATE NOCASE ASC;
            """;
        command.Parameters.AddWithValue("$folderId", folderId);

        using var reader = command.ExecuteReader();
        var videos = new List<VideoItem>();
        while (reader.Read())
        {
            videos.Add(new VideoItem
            {
                Id = reader.GetInt64(0),
                FolderId = reader.GetInt64(1),
                Path = reader.GetString(2),
                FileName = reader.GetString(3),
                FileSize = reader.GetInt64(4),
                ModifiedAt = FromDb(reader.GetString(5)) ?? DateTime.MinValue,
                SortOrder = reader.GetInt32(6)
            });
        }

        return videos
            .OrderBy(video => video.FileName, NaturalStringComparer.OrdinalIgnoreCase)
            .ThenBy(video => video.SortOrder)
            .ToList();
    }

    public List<VideoItem> GetSeriesVideos(string seriesName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Videos.Id, Videos.FolderId, Videos.Path, Videos.FileName, Videos.FileSize, Videos.ModifiedAt, Videos.SortOrder, Folders.DisplayName, Folders.SeriesOrder
            FROM Videos
            JOIN Folders ON Folders.Id = Videos.FolderId
            WHERE Folders.SeriesName = $seriesName
            ORDER BY Folders.SeriesOrder IS NULL,
                     Folders.SeriesOrder ASC,
                     Folders.DisplayName COLLATE NOCASE ASC,
                     Videos.SortOrder ASC,
                     Videos.FileName COLLATE NOCASE ASC;
            """;
        command.Parameters.AddWithValue("$seriesName", seriesName);

        using var reader = command.ExecuteReader();
        var videos = new List<VideoItem>();
        while (reader.Read())
        {
            videos.Add(new VideoItem
            {
                Id = reader.GetInt64(0),
                FolderId = reader.GetInt64(1),
                Path = reader.GetString(2),
                FileName = reader.GetString(3),
                FileSize = reader.GetInt64(4),
                ModifiedAt = FromDb(reader.GetString(5)) ?? DateTime.MinValue,
                SortOrder = reader.GetInt32(6),
                FolderDisplayName = reader.IsDBNull(7) ? null : reader.GetString(7),
                FolderSeriesOrder = reader.IsDBNull(8) ? null : reader.GetInt32(8)
            });
        }

        return videos
            .OrderBy(video => video.FolderSeriesOrder is null)
            .ThenBy(video => video.FolderSeriesOrder)
            .ThenBy(video => video.FolderDisplayName ?? "", NaturalStringComparer.OrdinalIgnoreCase)
            .ThenBy(video => video.SortOrder)
            .ThenBy(video => video.FileName, NaturalStringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public FolderItem? GetFirstFolderInSeries(string seriesName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt, VideoCount, TotalVideoBytes
            FROM Folders
            WHERE SeriesName = $seriesName
            ORDER BY SeriesOrder IS NULL,
                     SeriesOrder ASC,
                     DisplayName COLLATE NOCASE ASC,
                     Id ASC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$seriesName", seriesName);

        FolderItem folder;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                return null;
            }

            folder = ReadFolder(reader);
        }

        var tags = GetTagsForFolders(connection);
        if (tags.TryGetValue(folder.Id, out var folderTags))
        {
            folder.Tags = folderTags;
        }

        return folder;
    }

    public FolderItem? GetFolder(long folderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt, VideoCount, TotalVideoBytes
            FROM Folders
            WHERE Id = $folderId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$folderId", folderId);

        FolderItem folder;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                return null;
            }

            folder = ReadFolder(reader);
        }

        var tags = GetTagsForFolders(connection, [folder.Id]);
        if (tags.TryGetValue(folder.Id, out var folderTags))
        {
            folder.Tags = folderTags;
        }

        return folder;
    }

    public List<ImageItem> GetAllImages()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Images.Id, Images.FolderId, Images.Path, Images.FileName, Images.FileSize, Images.ModifiedAt, Images.SortOrder,
                   Folders.DisplayName, Folders.Path, Folders.FolderModifiedAt, Folders.ImageCount, Folders.TotalImageBytes
            FROM Images
            JOIN Folders ON Folders.Id = Images.FolderId
            ORDER BY Images.FileName COLLATE NOCASE ASC, Images.FileSize ASC, Images.Path COLLATE NOCASE ASC;
            """;

        using var reader = command.ExecuteReader();
        var images = new List<ImageItem>();
        while (reader.Read())
        {
            images.Add(new ImageItem
            {
                Id = reader.GetInt64(0),
                FolderId = reader.GetInt64(1),
                Path = reader.GetString(2),
                FileName = reader.GetString(3),
                FileSize = reader.GetInt64(4),
                ModifiedAt = FromDb(reader.GetString(5)) ?? DateTime.MinValue,
                SortOrder = reader.GetInt32(6),
                FolderDisplayName = reader.IsDBNull(7) ? null : reader.GetString(7),
                FolderPath = reader.IsDBNull(8) ? null : reader.GetString(8),
                FolderModifiedAt = reader.IsDBNull(9) ? null : FromDb(reader.GetString(9)),
                FolderImageCount = reader.GetInt32(10),
                FolderTotalImageBytes = reader.GetInt64(11)
            });
        }

        return images;
    }

    public void SaveFolder(FolderItem folder)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Folders
                SET DisplayName = $displayName,
                    Author = $author,
                    Number = $number,
                    SeriesName = $seriesName,
                    SeriesOrder = $seriesOrder,
                    Score = $score,
                    Memo = $memo,
                    IsFavorite = $isFavorite,
                    IsReserved = $isReserved,
                    ThumbnailPath = $thumbnailPath,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$displayName", folder.DisplayName);
            command.Parameters.AddWithValue("$author", DbValue(folder.Author));
            command.Parameters.AddWithValue("$number", DbValue(folder.Number));
            command.Parameters.AddWithValue("$seriesName", DbValue(folder.SeriesName));
            command.Parameters.AddWithValue("$seriesOrder", folder.SeriesOrder is null ? DBNull.Value : folder.SeriesOrder.Value);
            command.Parameters.AddWithValue("$score", folder.Score);
            command.Parameters.AddWithValue("$memo", DbValue(folder.Memo));
            command.Parameters.AddWithValue("$isFavorite", folder.IsFavorite ? 1 : 0);
            command.Parameters.AddWithValue("$isReserved", folder.IsReserved ? 1 : 0);
            command.Parameters.AddWithValue("$thumbnailPath", DbValue(folder.ThumbnailPath));
            command.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
            command.Parameters.AddWithValue("$id", folder.Id);
            command.ExecuteNonQuery();
        }

        ReplaceTags(connection, transaction, folder.Id, folder.Tags);
        transaction.Commit();
    }

    public void AddTagsToFolders(IEnumerable<long> folderIds, IEnumerable<string> tags)
    {
        var idList = folderIds.Distinct().ToList();
        var tagList = tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (idList.Count == 0 || tagList.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var folderId in idList)
        {
            ReplaceTags(connection, transaction, folderId, GetExistingTags(connection, transaction, folderId).Concat(tagList));
        }

        transaction.Commit();
    }

    public void UpdateFoldersFlags(IEnumerable<long> folderIds, bool? isFavorite, bool? isReserved)
    {
        var idList = folderIds.Distinct().ToList();
        if (idList.Count == 0 || (isFavorite is null && isReserved is null))
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var folderId in idList)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                UPDATE Folders
                SET {(isFavorite is null ? "" : "IsFavorite = $isFavorite,")}
                    {(isReserved is null ? "" : "IsReserved = $isReserved,")}
                    UpdatedAt = $updatedAt
                WHERE Id = $folderId;
                """;
            if (isFavorite is not null)
            {
                command.Parameters.AddWithValue("$isFavorite", isFavorite.Value ? 1 : 0);
            }

            if (isReserved is not null)
            {
                command.Parameters.AddWithValue("$isReserved", isReserved.Value ? 1 : 0);
            }

            command.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
            command.Parameters.AddWithValue("$folderId", folderId);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void AssignSeries(string seriesName, IEnumerable<SeriesAssignment> assignments, string? existingSeriesName = null, bool clearExistingSeries = false)
    {
        var assignmentList = assignments.ToList();
        if (string.IsNullOrWhiteSpace(seriesName) || assignmentList.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        if (clearExistingSeries && !string.IsNullOrWhiteSpace(existingSeriesName))
        {
            using var clear = connection.CreateCommand();
            clear.Transaction = transaction;
            clear.CommandText = """
                UPDATE Folders
                SET SeriesName = NULL,
                    SeriesOrder = NULL,
                    UpdatedAt = $updatedAt
                WHERE SeriesName = $seriesName;
                """;
            clear.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
            clear.Parameters.AddWithValue("$seriesName", existingSeriesName.Trim());
            clear.ExecuteNonQuery();
        }

        foreach (var assignment in assignmentList)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Folders
                SET SeriesName = $seriesName,
                    SeriesOrder = $seriesOrder,
                    UpdatedAt = $updatedAt
                WHERE Id = $folderId;
                """;
            command.Parameters.AddWithValue("$seriesName", seriesName.Trim());
            command.Parameters.AddWithValue("$seriesOrder", assignment.SeriesOrder);
            command.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
            command.Parameters.AddWithValue("$folderId", assignment.FolderId);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void ClearSeries(IEnumerable<long> folderIds)
    {
        var idList = folderIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var folderId in idList)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Folders
                SET SeriesName = NULL,
                    SeriesOrder = NULL,
                    UpdatedAt = $updatedAt
                WHERE Id = $folderId;
                """;
            command.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
            command.Parameters.AddWithValue("$folderId", folderId);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeleteFolders(IEnumerable<long> folderIds)
    {
        var idList = folderIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var folderId in idList)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Folders WHERE Id = $folderId;";
            command.Parameters.AddWithValue("$folderId", folderId);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void UpsertScannedFolder(FolderScanResult result)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertScannedFolder(connection, transaction, result);
        transaction.Commit();
    }

    public void UpsertScannedFolders(IReadOnlyList<FolderScanResult> results, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        for (var index = 0; index < results.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpsertScannedFolder(connection, transaction, results[index]);
            progress?.Report(index + 1);
        }

        transaction.Commit();
    }

    public ScanWriteSession BeginScanWriteSession()
    {
        return new ScanWriteSession(this);
    }

    private static void UpsertScannedFolder(SqliteConnection connection, SqliteTransaction transaction, FolderScanResult result)
    {
        var now = DateTime.Now;
        var folderModifiedAt = result.FolderModifiedAt;
        var thumbnailPath = result.Images.Count > 0
            ? result.Images[0].FullName
            : result.Videos.FirstOrDefault()?.FullName;
        var folderId = GetFolderId(connection, transaction, result.FolderPath);
        if (folderId is null)
        {
            var parsed = result.VideoCount > 0 && result.ImageCount == 0
                ? (DisplayName: Path.GetFileNameWithoutExtension(result.FolderPath), Author: (string?)null, Number: (string?)null)
                : FolderNameParser.Parse(new DirectoryInfo(result.FolderPath).Name);
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Folders (Path, DisplayName, Author, Number, DirectoryModifiedAt, FolderModifiedAt, ImageCount, TotalImageBytes, VideoCount, TotalVideoBytes, ThumbnailPath, PathExists, PathCheckedAt, CreatedAt, UpdatedAt)
                VALUES ($path, $displayName, $author, $number, $directoryModifiedAt, $folderModifiedAt, $imageCount, $totalImageBytes, $videoCount, $totalVideoBytes, $thumbnailPath, 1, $pathCheckedAt, $createdAt, $updatedAt);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$path", result.FolderPath);
            insert.Parameters.AddWithValue("$displayName", parsed.DisplayName);
            insert.Parameters.AddWithValue("$author", DbValue(parsed.Author));
            insert.Parameters.AddWithValue("$number", DbValue(parsed.Number));
            insert.Parameters.AddWithValue("$directoryModifiedAt", ToDb(result.DirectoryModifiedAt));
            insert.Parameters.AddWithValue("$folderModifiedAt", ToDb(folderModifiedAt));
            insert.Parameters.AddWithValue("$imageCount", result.ImageCount);
            insert.Parameters.AddWithValue("$totalImageBytes", result.TotalImageBytes);
            insert.Parameters.AddWithValue("$videoCount", result.VideoCount);
            insert.Parameters.AddWithValue("$totalVideoBytes", result.TotalVideoBytes);
            insert.Parameters.AddWithValue("$thumbnailPath", DbValue(thumbnailPath));
            insert.Parameters.AddWithValue("$pathCheckedAt", ToDb(now));
            insert.Parameters.AddWithValue("$createdAt", ToDb(now));
            insert.Parameters.AddWithValue("$updatedAt", ToDb(now));
            folderId = (long)(insert.ExecuteScalar() ?? 0L);
        }
        else
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE Folders
                SET ThumbnailPath = COALESCE(ThumbnailPath, $thumbnailPath),
                    DirectoryModifiedAt = $directoryModifiedAt,
                    FolderModifiedAt = $folderModifiedAt,
                    ImageCount = $imageCount,
                    TotalImageBytes = $totalImageBytes,
                    VideoCount = $videoCount,
                    TotalVideoBytes = $totalVideoBytes,
                    PathExists = 1,
                    PathCheckedAt = $pathCheckedAt,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            update.Parameters.AddWithValue("$thumbnailPath", DbValue(thumbnailPath));
            update.Parameters.AddWithValue("$directoryModifiedAt", ToDb(result.DirectoryModifiedAt));
            update.Parameters.AddWithValue("$folderModifiedAt", ToDb(folderModifiedAt));
            update.Parameters.AddWithValue("$imageCount", result.ImageCount);
            update.Parameters.AddWithValue("$totalImageBytes", result.TotalImageBytes);
            update.Parameters.AddWithValue("$videoCount", result.VideoCount);
            update.Parameters.AddWithValue("$totalVideoBytes", result.TotalVideoBytes);
            update.Parameters.AddWithValue("$pathCheckedAt", ToDb(now));
            update.Parameters.AddWithValue("$updatedAt", ToDb(now));
            update.Parameters.AddWithValue("$id", folderId.Value);
            update.ExecuteNonQuery();
        }

        var existingBookmarks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        using (var bookmarkCommand = connection.CreateCommand())
        {
            bookmarkCommand.Transaction = transaction;
            bookmarkCommand.CommandText = "SELECT Path, IsBookmarked FROM Images WHERE FolderId = $folderId AND IsBookmarked = 1;";
            bookmarkCommand.Parameters.AddWithValue("$folderId", folderId.Value);
            using var reader = bookmarkCommand.ExecuteReader();
            while (reader.Read())
            {
                existingBookmarks[reader.GetString(0)] = reader.GetInt32(1) == 1;
            }
        }

        using (var deleteImages = connection.CreateCommand())
        {
            deleteImages.Transaction = transaction;
            deleteImages.CommandText = "DELETE FROM Images WHERE FolderId = $folderId;";
            deleteImages.Parameters.AddWithValue("$folderId", folderId.Value);
            deleteImages.ExecuteNonQuery();
        }

        using (var deleteVideos = connection.CreateCommand())
        {
            deleteVideos.Transaction = transaction;
            deleteVideos.CommandText = "DELETE FROM Videos WHERE FolderId = $folderId;";
            deleteVideos.Parameters.AddWithValue("$folderId", folderId.Value);
            deleteVideos.ExecuteNonQuery();
        }

        for (var i = 0; i < result.Images.Count; i++)
        {
            var image = result.Images[i];
            using var insertImage = connection.CreateCommand();
            insertImage.Transaction = transaction;
            insertImage.CommandText = """
                INSERT INTO Images (FolderId, Path, FileName, FileSize, ModifiedAt, SortOrder, IsBookmarked)
                VALUES ($folderId, $path, $fileName, $fileSize, $modifiedAt, $sortOrder, $isBookmarked)
                ON CONFLICT(Path) DO UPDATE SET
                    FolderId = excluded.FolderId,
                    FileName = excluded.FileName,
                    FileSize = excluded.FileSize,
                    ModifiedAt = excluded.ModifiedAt,
                    SortOrder = excluded.SortOrder,
                    IsBookmarked = excluded.IsBookmarked;
                """;
            insertImage.Parameters.AddWithValue("$folderId", folderId.Value);
            insertImage.Parameters.AddWithValue("$path", image.FullName);
            insertImage.Parameters.AddWithValue("$fileName", image.Name);
            insertImage.Parameters.AddWithValue("$fileSize", image.Length);
            insertImage.Parameters.AddWithValue("$modifiedAt", ToDb(image.LastWriteTime));
            insertImage.Parameters.AddWithValue("$sortOrder", i);
            insertImage.Parameters.AddWithValue("$isBookmarked", existingBookmarks.GetValueOrDefault(image.FullName) ? 1 : 0);
            insertImage.ExecuteNonQuery();
        }

        for (var videoIndex = 0; videoIndex < result.Videos.Count; videoIndex++)
        {
            var video = result.Videos[videoIndex];
            using var insertVideo = connection.CreateCommand();
            insertVideo.Transaction = transaction;
            insertVideo.CommandText = """
                INSERT INTO Videos (FolderId, Path, FileName, FileSize, ModifiedAt, SortOrder)
                VALUES ($folderId, $path, $fileName, $fileSize, $modifiedAt, $sortOrder)
                ON CONFLICT(Path) DO UPDATE SET
                    FolderId = excluded.FolderId,
                    FileName = excluded.FileName,
                    FileSize = excluded.FileSize,
                    ModifiedAt = excluded.ModifiedAt,
                    SortOrder = excluded.SortOrder;
                """;
            insertVideo.Parameters.AddWithValue("$folderId", folderId.Value);
            insertVideo.Parameters.AddWithValue("$path", video.FullName);
            insertVideo.Parameters.AddWithValue("$fileName", video.Name);
            insertVideo.Parameters.AddWithValue("$fileSize", video.Length);
            insertVideo.Parameters.AddWithValue("$modifiedAt", ToDb(video.LastWriteTime));
            insertVideo.Parameters.AddWithValue("$sortOrder", videoIndex);
            insertVideo.ExecuteNonQuery();
        }

    }

    public sealed class ScanWriteSession : IDisposable
    {
        private const int BatchSize = 500;

        private readonly SqliteConnection connection;
        private SqliteTransaction transaction;
        private int pendingWrites;
        private bool committed;

        public ScanWriteSession(AppDatabase database)
        {
            connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA temp_store = MEMORY;";
            command.ExecuteNonQuery();
            transaction = connection.BeginTransaction();
        }

        public void Save(FolderScanResult result)
        {
            UpsertScannedFolder(connection, transaction, result);
            pendingWrites++;
            if (pendingWrites >= BatchSize)
            {
                CommitCurrentBatch();
            }
        }

        public void Commit()
        {
            transaction.Commit();
            committed = true;
        }

        public void Dispose()
        {
            if (!committed)
            {
                transaction.Rollback();
            }

            transaction.Dispose();
            connection.Dispose();
        }

        private void CommitCurrentBatch()
        {
            transaction.Commit();
            transaction.Dispose();
            transaction = connection.BeginTransaction();
            pendingWrites = 0;
        }
    }

    public CleanupSummary RemoveMissingFoldersAndImages(bool checkImageFiles = true, RootKind? rootKind = null, MediaKind? mediaKind = null)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var summary = new CleanupSummary();
        var folders = new List<(long Id, string Path)>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            var where = new List<string>();
            ApplyRootKindFilter(where, rootKind, mediaKind);
            command.CommandText = where.Count == 0
                ? "SELECT Id, Path FROM Folders;"
                : $"SELECT Id, Path FROM Folders WHERE {string.Join(" AND ", where)};";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        foreach (var folder in folders)
        {
            if (!EntryPathExists(folder.Path))
            {
                DeleteFolder(connection, transaction, folder.Id);
                summary.RemovedFolders++;
                continue;
            }
        }

        if (checkImageFiles)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var where = new List<string>();
            ApplyRootKindFilter(where, rootKind, mediaKind);
            command.CommandText = $"""
                SELECT Images.Id, Images.Path
                FROM Images
                JOIN Folders ON Folders.Id = Images.FolderId
                {(where.Count == 0 ? "" : $"WHERE {string.Join(" AND ", where)}")};
                """;
            using var deleteImage = connection.CreateCommand();
            deleteImage.Transaction = transaction;
            deleteImage.CommandText = "DELETE FROM Images WHERE Id = $id;";
            var idParameter = deleteImage.Parameters.Add("$id", SqliteType.Integer);
            var missing = new List<long>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!File.Exists(reader.GetString(1)))
                    {
                        missing.Add(reader.GetInt64(0));
                    }
                }
            }

            foreach (var imageId in missing)
            {
                idParameter.Value = imageId;
                deleteImage.ExecuteNonQuery();
                summary.RemovedImages++;
            }

            using var videoCommand = connection.CreateCommand();
            videoCommand.Transaction = transaction;
            var videoWhere = new List<string>();
            ApplyRootKindFilter(videoWhere, rootKind, mediaKind);
            videoCommand.CommandText = $"""
                SELECT Videos.Id, Videos.Path
                FROM Videos
                JOIN Folders ON Folders.Id = Videos.FolderId
                {(videoWhere.Count == 0 ? "" : $"WHERE {string.Join(" AND ", videoWhere)}")};
                """;
            using var deleteVideo = connection.CreateCommand();
            deleteVideo.Transaction = transaction;
            deleteVideo.CommandText = "DELETE FROM Videos WHERE Id = $id;";
            var videoIdParameter = deleteVideo.Parameters.Add("$id", SqliteType.Integer);
            var missingVideos = new List<long>();
            using (var reader = videoCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!File.Exists(reader.GetString(1)))
                    {
                        missingVideos.Add(reader.GetInt64(0));
                    }
                }
            }

            foreach (var videoId in missingVideos)
            {
                videoIdParameter.Value = videoId;
                deleteVideo.ExecuteNonQuery();
            }
        }

        using (var deleteEmptyFolders = connection.CreateCommand())
        {
            deleteEmptyFolders.Transaction = transaction;
            var where = new List<string>
            {
                "NOT EXISTS (SELECT 1 FROM Images WHERE Images.FolderId = Folders.Id)",
                "NOT EXISTS (SELECT 1 FROM Videos WHERE Videos.FolderId = Folders.Id)"
            };
            ApplyRootKindFilter(where, rootKind, mediaKind);
            deleteEmptyFolders.CommandText = $"DELETE FROM Folders WHERE {string.Join(" AND ", where)};";
            summary.RemovedFolders += deleteEmptyFolders.ExecuteNonQuery();
        }

        transaction.Commit();
        return summary;
    }

    public int RefreshFolderPathStatus(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        using var connection = OpenConnection();
        var folders = new List<(long Id, string Path)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id, Path FROM Folders ORDER BY Path COLLATE NOCASE;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        using var transaction = connection.BeginTransaction();
        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE Folders
            SET PathExists = $pathExists,
                PathCheckedAt = $pathCheckedAt
            WHERE Id = $id;
            """;
        var pathExistsParameter = update.Parameters.Add("$pathExists", SqliteType.Integer);
        var pathCheckedAtParameter = update.Parameters.Add("$pathCheckedAt", SqliteType.Text);
        var idParameter = update.Parameters.Add("$id", SqliteType.Integer);
        var missingCount = 0;
        var now = ToDb(DateTime.Now);
        for (var index = 0; index < folders.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = folders[index];
            var exists = EntryPathExists(folder.Path);
            if (!exists)
            {
                missingCount++;
            }

            pathExistsParameter.Value = exists ? 1 : 0;
            pathCheckedAtParameter.Value = now;
            idParameter.Value = folder.Id;
            update.ExecuteNonQuery();
            if ((index + 1) % 250 == 0)
            {
                progress?.Report($"경로 확인 중... {index + 1:N0} / {folders.Count:N0}");
            }
        }

        transaction.Commit();
        progress?.Report($"경로 확인 완료: 깨진 경로 {missingCount:N0}개 / 전체 {folders.Count:N0}개");
        return missingCount;
    }

    public int RemoveLegacyAggregateVideoFolders(RootKind? rootKind = null, MediaKind? mediaKind = null)
    {
        using var connection = OpenConnection();
        var folders = new List<(long Id, string Path)>();
        using (var command = connection.CreateCommand())
        {
            var where = new List<string>
            {
                "ImageCount = 0",
                "VideoCount > 0"
            };
            ApplyRootKindFilter(where, rootKind, mediaKind);
            command.CommandText = $"""
                SELECT Id, Path
                FROM Folders
                WHERE {string.Join(" AND ", where)};
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        var legacyFolderIds = folders
            .Where(folder => Directory.Exists(folder.Path))
            .Select(folder => folder.Id)
            .ToList();
        if (legacyFolderIds.Count == 0)
        {
            return 0;
        }

        using var transaction = connection.BeginTransaction();
        foreach (var folderId in legacyFolderIds)
        {
            DeleteFolder(connection, transaction, folderId);
        }

        transaction.Commit();
        return legacyFolderIds.Count;
    }

    private static void DeleteFolder(SqliteConnection connection, SqliteTransaction transaction, long folderId)
    {
        using var deleteFolder = connection.CreateCommand();
        deleteFolder.Transaction = transaction;
        deleteFolder.CommandText = "DELETE FROM Folders WHERE Id = $id;";
        deleteFolder.Parameters.AddWithValue("$id", folderId);
        deleteFolder.ExecuteNonQuery();
    }

    private static bool EntryPathExists(string path)
    {
        return Directory.Exists(path) || File.Exists(path);
    }

    public int ClearBrokenThumbnails()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ThumbnailPath
            FROM Folders
            WHERE ThumbnailPath IS NOT NULL AND TRIM(ThumbnailPath) <> '';
            """;
        var brokenFolderIds = new List<long>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var thumbnailPath = reader.GetString(1);
                if (!File.Exists(thumbnailPath))
                {
                    brokenFolderIds.Add(reader.GetInt64(0));
                }
            }
        }

        if (brokenFolderIds.Count == 0)
        {
            return 0;
        }

        using var transaction = connection.BeginTransaction();
        foreach (var folderId in brokenFolderIds)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE Folders
                SET ThumbnailPath = NULL,
                    UpdatedAt = $updatedAt
                WHERE Id = $folderId;
                """;
            update.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
            update.Parameters.AddWithValue("$folderId", folderId);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
        return brokenFolderIds.Count;
    }

    public void Optimize()
    {
        using var connection = OpenConnection();
        using (var optimize = connection.CreateCommand())
        {
            optimize.CommandText = "PRAGMA optimize;";
            optimize.ExecuteNonQuery();
        }

        using var vacuum = connection.CreateCommand();
        vacuum.CommandText = "VACUUM;";
        vacuum.ExecuteNonQuery();
    }

    public void MarkFolderViewed(long folderId, string? lastImagePath)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Folders
            SET ViewCount = ViewCount + 1,
                LastViewedAt = $lastViewedAt,
                LastImagePath = COALESCE($lastImagePath, LastImagePath),
                UpdatedAt = $updatedAt
            WHERE Id = $folderId;
            """;
        var now = ToDb(DateTime.Now);
        command.Parameters.AddWithValue("$lastViewedAt", now);
        command.Parameters.AddWithValue("$lastImagePath", DbValue(lastImagePath));
        command.Parameters.AddWithValue("$updatedAt", now);
        command.Parameters.AddWithValue("$folderId", folderId);
        command.ExecuteNonQuery();

        using var prune = connection.CreateCommand();
        prune.Transaction = transaction;
        prune.CommandText = """
            UPDATE Folders
            SET LastViewedAt = NULL,
                LastImagePath = NULL
            WHERE Id IN (
                SELECT Id
                FROM Folders
                WHERE LastViewedAt IS NOT NULL
                ORDER BY LastViewedAt DESC, Id DESC
                LIMIT -1 OFFSET 100
            );
            """;
        prune.ExecuteNonQuery();
        transaction.Commit();
    }

    public void UpdateLastImagePath(long folderId, string? lastImagePath)
    {
        if (string.IsNullOrWhiteSpace(lastImagePath))
        {
            return;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Folders
            SET LastImagePath = $lastImagePath,
                UpdatedAt = $updatedAt
            WHERE Id = $folderId;
            """;
        command.Parameters.AddWithValue("$lastImagePath", lastImagePath);
        command.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
        command.Parameters.AddWithValue("$folderId", folderId);
        command.ExecuteNonQuery();
    }

    public void DeleteFolder(long folderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Folders WHERE Id = $folderId;";
        command.Parameters.AddWithValue("$folderId", folderId);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static void MigrateRootsSchema(SqliteConnection connection)
    {
        using var infoCommand = connection.CreateCommand();
        infoCommand.CommandText = "PRAGMA table_info(Roots);";
        var hasMediaKind = false;
        using (var reader = infoCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "MediaKind", StringComparison.OrdinalIgnoreCase))
                {
                    hasMediaKind = true;
                    break;
                }
            }
        }

        if (hasMediaKind)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = """
                CREATE TABLE Roots_New (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Path TEXT NOT NULL,
                    Kind TEXT NOT NULL DEFAULT 'Main',
                    MediaKind TEXT NOT NULL DEFAULT 'Image',
                    CreatedAt TEXT NOT NULL,
                    UNIQUE(Path, Kind, MediaKind)
                );
                """;
            create.ExecuteNonQuery();
        }

        using (var copy = connection.CreateCommand())
        {
            copy.Transaction = transaction;
            copy.CommandText = """
                INSERT OR IGNORE INTO Roots_New (Path, Kind, MediaKind, CreatedAt)
                SELECT Path, Kind, 'Image', CreatedAt
                FROM Roots;
                """;
            copy.ExecuteNonQuery();
        }

        using (var drop = connection.CreateCommand())
        {
            drop.Transaction = transaction;
            drop.CommandText = "DROP TABLE Roots;";
            drop.ExecuteNonQuery();
        }

        using (var rename = connection.CreateCommand())
        {
            rename.Transaction = transaction;
            rename.CommandText = "ALTER TABLE Roots_New RENAME TO Roots;";
            rename.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void ApplyRootModeFilter(List<string> where, FolderListMode mode, bool videoMode)
    {
        var mediaKind = videoMode ? MediaKind.Video : MediaKind.Image;
        var rootKind = mode == FolderListMode.NewRegistration ? RootKind.Incoming : RootKind.Main;
        var rootCondition = BuildRootCondition("Folders.Path", rootKind, mediaKind);

        where.Add(rootCondition);
    }

    private static void ApplyMediaModeFilter(List<string> where, bool videoMode)
    {
        where.Add(videoMode ? "VideoCount > 0" : "ImageCount > 0");
    }

    private static string BuildSeriesRepresentativeCondition(bool videoMode)
    {
        var mediaCondition = videoMode ? "FirstSeriesFolder.VideoCount > 0" : "FirstSeriesFolder.ImageCount > 0";
        return $"""
            SeriesName IS NOT NULL
            AND TRIM(SeriesName) <> ''
            AND Id = (
                SELECT FirstSeriesFolder.Id
                FROM Folders FirstSeriesFolder
                WHERE FirstSeriesFolder.SeriesName = Folders.SeriesName
                  AND {mediaCondition}
                ORDER BY FirstSeriesFolder.SeriesOrder IS NULL,
                         FirstSeriesFolder.SeriesOrder ASC,
                         FirstSeriesFolder.DisplayName COLLATE NOCASE ASC,
                         FirstSeriesFolder.Id ASC
                LIMIT 1
            )
            """;
    }

    private static void ApplyRootKindFilter(List<string> where, RootKind? rootKind, MediaKind? mediaKind = null)
    {
        if (rootKind is null && mediaKind is null)
        {
            return;
        }

        where.Add(BuildRootCondition("Folders.Path", rootKind, mediaKind));
    }

    private static string BuildRootCondition(string pathExpression, RootKind? rootKind, MediaKind? mediaKind)
    {
        var kindCondition = rootKind is null ? "" : $"AND MatchingRoots.Kind = '{ToRootKind(rootKind.Value)}'";
        var mediaCondition = mediaKind is null ? "" : $"AND MatchingRoots.MediaKind = '{ToMediaKind(mediaKind.Value)}'";
        return $"""
            EXISTS (
                SELECT 1
                FROM Roots MatchingRoots
                WHERE 1 = 1
                  {kindCondition}
                  {mediaCondition}
                  AND (
                      {pathExpression} = MatchingRoots.Path
                      OR substr({pathExpression}, 1, length(MatchingRoots.Path || '\')) = MatchingRoots.Path || '\'
                  )
            )
            """;
    }

    private static void UpdatePathPrefix(SqliteConnection connection, SqliteTransaction transaction, string oldPath, string newPath)
    {
        var oldPrefix = EnsureTrailingSeparator(oldPath);
        var newPrefix = EnsureTrailingSeparator(newPath);
        using var folders = connection.CreateCommand();
        folders.Transaction = transaction;
        folders.CommandText = """
            UPDATE Folders
            SET Path = CASE
                    WHEN Path = $oldPath THEN $newPath
                    ELSE $newPrefix || substr(Path, length($oldPrefix) + 1)
                END,
                ThumbnailPath = CASE
                    WHEN ThumbnailPath = $oldPath THEN $newPath
                    WHEN ThumbnailPath IS NOT NULL AND substr(ThumbnailPath, 1, length($oldPrefix)) = $oldPrefix THEN $newPrefix || substr(ThumbnailPath, length($oldPrefix) + 1)
                    ELSE ThumbnailPath
                END,
                LastImagePath = CASE
                    WHEN LastImagePath = $oldPath THEN $newPath
                    WHEN LastImagePath IS NOT NULL AND substr(LastImagePath, 1, length($oldPrefix)) = $oldPrefix THEN $newPrefix || substr(LastImagePath, length($oldPrefix) + 1)
                    ELSE LastImagePath
                END,
                PathExists = 1,
                PathCheckedAt = $pathCheckedAt,
                UpdatedAt = $updatedAt
            WHERE Path = $oldPath
               OR substr(Path, 1, length($oldPrefix)) = $oldPrefix;
            """;
        AddPathPrefixParameters(folders, oldPath, newPath, oldPrefix, newPrefix);
        folders.ExecuteNonQuery();

        using var images = connection.CreateCommand();
        images.Transaction = transaction;
        images.CommandText = """
            UPDATE Images
            SET Path = CASE
                    WHEN Path = $oldPath THEN $newPath
                    ELSE $newPrefix || substr(Path, length($oldPrefix) + 1)
                END
            WHERE Path = $oldPath
               OR substr(Path, 1, length($oldPrefix)) = $oldPrefix;
            """;
        AddPathPrefixParameters(images, oldPath, newPath, oldPrefix, newPrefix);
        images.ExecuteNonQuery();

        using var videos = connection.CreateCommand();
        videos.Transaction = transaction;
        videos.CommandText = """
            UPDATE Videos
            SET Path = CASE
                    WHEN Path = $oldPath THEN $newPath
                    ELSE $newPrefix || substr(Path, length($oldPrefix) + 1)
                END
            WHERE Path = $oldPath
               OR substr(Path, 1, length($oldPrefix)) = $oldPrefix;
            """;
        AddPathPrefixParameters(videos, oldPath, newPath, oldPrefix, newPrefix);
        videos.ExecuteNonQuery();
    }

    private static void AddPathPrefixParameters(SqliteCommand command, string oldPath, string newPath, string oldPrefix, string newPrefix)
    {
        command.Parameters.AddWithValue("$oldPath", oldPath);
        command.Parameters.AddWithValue("$newPath", newPath);
        command.Parameters.AddWithValue("$oldPrefix", oldPrefix);
        command.Parameters.AddWithValue("$newPrefix", newPrefix);
        command.Parameters.AddWithValue("$pathCheckedAt", ToDb(DateTime.Now));
        command.Parameters.AddWithValue("$updatedAt", ToDb(DateTime.Now));
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string ToRootKind(RootKind kind)
    {
        return kind == RootKind.Incoming ? "Incoming" : "Main";
    }

    private static string ToMediaKind(MediaKind kind)
    {
        return kind == MediaKind.Video ? "Video" : "Image";
    }

    private static FolderItem ReadFolder(SqliteDataReader reader)
    {
        var folder = new FolderItem
        {
            Id = reader.GetInt64(0),
            Path = reader.GetString(1),
            DisplayName = reader.GetString(2),
            Author = reader.IsDBNull(3) ? null : reader.GetString(3),
            Number = reader.IsDBNull(4) ? null : reader.GetString(4),
            SeriesName = reader.IsDBNull(5) ? null : reader.GetString(5),
            SeriesOrder = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            Score = reader.GetInt32(7),
            Memo = reader.IsDBNull(8) ? null : reader.GetString(8),
            IsFavorite = reader.GetInt32(9) == 1,
            IsReserved = reader.GetInt32(10) == 1,
            ViewCount = reader.GetInt32(11),
            LastViewedAt = reader.IsDBNull(12) ? null : FromDb(reader.GetString(12)),
            LastImagePath = reader.IsDBNull(13) ? null : reader.GetString(13),
            FolderModifiedAt = reader.IsDBNull(14) ? null : FromDb(reader.GetString(14)),
            ImageCount = reader.GetInt32(15),
            TotalImageBytes = reader.GetInt64(16),
            ThumbnailPath = reader.IsDBNull(17) ? null : reader.GetString(17),
            PathExists = reader.GetInt32(18) == 1,
            PathCheckedAt = reader.IsDBNull(19) ? null : FromDb(reader.GetString(19)),
            CreatedAt = FromDb(reader.GetString(20)) ?? DateTime.MinValue,
            UpdatedAt = FromDb(reader.GetString(21)) ?? DateTime.MinValue
        };

        if (reader.FieldCount > 22)
        {
            folder.VideoCount = reader.GetInt32(22);
            folder.TotalVideoBytes = reader.GetInt64(23);
        }

        return folder;
    }

    private static void BackfillFolderModifiedAt(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Folders
            SET FolderModifiedAt = MAX(
                COALESCE((SELECT MAX(ModifiedAt) FROM Images WHERE Images.FolderId = Folders.Id), '0001-01-01 00:00:00'),
                COALESCE((SELECT MAX(ModifiedAt) FROM Videos WHERE Videos.FolderId = Folders.Id), '0001-01-01 00:00:00')
            )
            WHERE FolderModifiedAt IS NULL
              AND (
                  EXISTS (SELECT 1 FROM Images WHERE Images.FolderId = Folders.Id)
                  OR EXISTS (SELECT 1 FROM Videos WHERE Videos.FolderId = Folders.Id)
              );
            """;
        command.ExecuteNonQuery();
    }

    private static void BackfillFolderScanStats(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Folders
            SET ImageCount = (
                    SELECT COUNT(*)
                    FROM Images
                    WHERE Images.FolderId = Folders.Id
                ),
                TotalImageBytes = (
                    SELECT COALESCE(SUM(FileSize), 0)
                    FROM Images
                    WHERE Images.FolderId = Folders.Id
                ),
                VideoCount = (
                    SELECT COUNT(*)
                    FROM Videos
                    WHERE Videos.FolderId = Folders.Id
                ),
                TotalVideoBytes = (
                    SELECT COALESCE(SUM(FileSize), 0)
                    FROM Videos
                    WHERE Videos.FolderId = Folders.Id
                )
            WHERE ImageCount = 0
              AND (
                  EXISTS (SELECT 1 FROM Images WHERE Images.FolderId = Folders.Id)
                  OR EXISTS (SELECT 1 FROM Videos WHERE Videos.FolderId = Folders.Id)
              );
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        alter.ExecuteNonQuery();
    }

    private static void EnsureIndexes(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_roots_path_kind_media ON Roots(Path, Kind, MediaKind);
            CREATE INDEX IF NOT EXISTS idx_folders_path ON Folders(Path);
            CREATE INDEX IF NOT EXISTS idx_folders_directorymodifiedat ON Folders(DirectoryModifiedAt);
            CREATE INDEX IF NOT EXISTS idx_folders_foldermodifiedat ON Folders(FolderModifiedAt);
            CREATE INDEX IF NOT EXISTS idx_folders_pathexists ON Folders(PathExists);
            CREATE INDEX IF NOT EXISTS idx_folders_displayname ON Folders(DisplayName COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS idx_folders_author ON Folders(Author COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS idx_folders_series ON Folders(SeriesName COLLATE NOCASE, SeriesOrder);
            CREATE INDEX IF NOT EXISTS idx_folders_score ON Folders(Score);
            CREATE INDEX IF NOT EXISTS idx_folders_lastviewedat ON Folders(LastViewedAt);
            CREATE INDEX IF NOT EXISTS idx_images_folderid ON Images(FolderId);
            CREATE INDEX IF NOT EXISTS idx_images_path ON Images(Path);
            CREATE INDEX IF NOT EXISTS idx_images_filename_size ON Images(FileName COLLATE NOCASE, FileSize);
            CREATE INDEX IF NOT EXISTS idx_images_bookmarked ON Images(IsBookmarked);
            CREATE INDEX IF NOT EXISTS idx_videos_folderid ON Videos(FolderId);
            CREATE INDEX IF NOT EXISTS idx_videos_path ON Videos(Path);
            CREATE INDEX IF NOT EXISTS idx_videos_filename_size ON Videos(FileName COLLATE NOCASE, FileSize);
            CREATE INDEX IF NOT EXISTS idx_foldertags_tagid ON FolderTags(TagId);
            """;
        command.ExecuteNonQuery();
    }

    private static Dictionary<long, List<string>> GetTagsForFolders(SqliteConnection connection, IReadOnlyList<long>? folderIds = null)
    {
        if (folderIds is not null && folderIds.Count == 0)
        {
            return [];
        }

        using var command = connection.CreateCommand();
        var where = "";
        if (folderIds is not null)
        {
            var parameters = new List<string>();
            for (var index = 0; index < folderIds.Count; index++)
            {
                var parameterName = $"$folderId{index}";
                parameters.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, folderIds[index]);
            }

            where = $"WHERE ft.FolderId IN ({string.Join(", ", parameters)})";
        }

        command.CommandText = $"""
            SELECT ft.FolderId, t.Name
            FROM FolderTags ft
            JOIN Tags t ON t.Id = ft.TagId
            {where}
            ORDER BY t.Name COLLATE NOCASE;
            """;
        using var reader = command.ExecuteReader();
        var result = new Dictionary<long, List<string>>();
        while (reader.Read())
        {
            var folderId = reader.GetInt64(0);
            if (!result.TryGetValue(folderId, out var tags))
            {
                tags = [];
                result[folderId] = tags;
            }

            tags.Add(reader.GetString(1));
        }

        return result;
    }

    private static List<string> GetExistingTags(SqliteConnection connection, SqliteTransaction transaction, long folderId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Tags.Name
            FROM FolderTags
            JOIN Tags ON Tags.Id = FolderTags.TagId
            WHERE FolderTags.FolderId = $folderId
            ORDER BY Tags.Name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$folderId", folderId);
        using var reader = command.ExecuteReader();
        var tags = new List<string>();
        while (reader.Read())
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    private static long? GetFolderId(SqliteConnection connection, SqliteTransaction transaction, string path)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM Folders WHERE Path = $path;";
        command.Parameters.AddWithValue("$path", path);
        var result = command.ExecuteScalar();
        return result is null || result == DBNull.Value ? null : (long)result;
    }

    private static void ReplaceTags(SqliteConnection connection, SqliteTransaction transaction, long folderId, IEnumerable<string> tags)
    {
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM FolderTags WHERE FolderId = $folderId;";
            delete.Parameters.AddWithValue("$folderId", folderId);
            delete.ExecuteNonQuery();
        }

        foreach (var tag in tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            long tagId;
            using (var insertTag = connection.CreateCommand())
            {
                insertTag.Transaction = transaction;
                insertTag.CommandText = """
                    INSERT INTO Tags (Name) VALUES ($name)
                    ON CONFLICT(Name) DO NOTHING;
                    SELECT Id FROM Tags WHERE Name = $name;
                    """;
                insertTag.Parameters.AddWithValue("$name", tag);
                tagId = (long)(insertTag.ExecuteScalar() ?? 0L);
            }

            using var insertFolderTag = connection.CreateCommand();
            insertFolderTag.Transaction = transaction;
            insertFolderTag.CommandText = "INSERT OR IGNORE INTO FolderTags (FolderId, TagId) VALUES ($folderId, $tagId);";
            insertFolderTag.Parameters.AddWithValue("$folderId", folderId);
            insertFolderTag.Parameters.AddWithValue("$tagId", tagId);
            insertFolderTag.ExecuteNonQuery();
        }
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static string ToDb(DateTime value) => value.ToString("O");

    private static DateTime? FromDb(string? value) => DateTime.TryParse(value, out var parsed) ? parsed : null;
}

