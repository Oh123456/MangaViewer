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
                Path TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL
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
                FolderModifiedAt TEXT NULL,
                ImageCount INTEGER NOT NULL DEFAULT 0,
                TotalImageBytes INTEGER NOT NULL DEFAULT 0,
                ThumbnailPath TEXT NULL,
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
        EnsureColumn(connection, "Folders", "LastImagePath", "TEXT NULL");
        EnsureColumn(connection, "Folders", "IsReserved", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Folders", "SeriesName", "TEXT NULL");
        EnsureColumn(connection, "Folders", "SeriesOrder", "INTEGER NULL");
        EnsureColumn(connection, "Folders", "FolderModifiedAt", "TEXT NULL");
        EnsureColumn(connection, "Folders", "ImageCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Folders", "TotalImageBytes", "INTEGER NOT NULL DEFAULT 0");
        BackfillFolderModifiedAt(connection);
        BackfillFolderScanStats(connection);
    }

    public List<string> GetRoots()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Path FROM Roots ORDER BY Path;";
        using var reader = command.ExecuteReader();
        var roots = new List<string>();
        while (reader.Read())
        {
            roots.Add(reader.GetString(0));
        }

        return roots;
    }

    public void AddRoot(string path)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO Roots (Path, CreatedAt)
            VALUES ($path, $createdAt);
            """;
        command.Parameters.AddWithValue("$path", path);
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

    public Dictionary<string, FolderScanSignature> GetFolderScanSignatureMap()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Path, FolderModifiedAt, ImageCount, TotalImageBytes FROM Folders WHERE FolderModifiedAt IS NOT NULL;";
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, FolderScanSignature>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            var modifiedAt = FromDb(reader.GetString(1));
            if (modifiedAt is not null)
            {
                result[reader.GetString(0)] = new FolderScanSignature
                {
                    FolderModifiedAt = modifiedAt.Value,
                    ImageCount = reader.GetInt32(2),
                    TotalImageBytes = reader.GetInt64(3)
                };
            }
        }

        return result;
    }

    public List<FolderItem> GetFolders(FolderListMode mode, FolderSortMode sortMode, FolderSearchField searchField, string searchText, IReadOnlyList<string> tagFilters, TagFilterMode tagFilterMode, QuickFilterMode quickFilterMode = QuickFilterMode.All)
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
                where.Add("""
                    SeriesName IS NOT NULL
                    AND TRIM(SeriesName) <> ''
                    AND Id = (
                        SELECT FirstSeriesFolder.Id
                        FROM Folders FirstSeriesFolder
                        WHERE FirstSeriesFolder.SeriesName = Folders.SeriesName
                        ORDER BY FirstSeriesFolder.SeriesOrder IS NULL,
                                 FirstSeriesFolder.SeriesOrder ASC,
                                 FirstSeriesFolder.DisplayName COLLATE NOCASE ASC,
                                 FirstSeriesFolder.Id ASC
                        LIMIT 1
                    )
                    """);
            }

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

            if (tagFilterMode == TagFilterMode.And)
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

            var orderBy = sortMode switch
            {
                FolderSortMode.Date => "FolderModifiedAt DESC NULLS LAST, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Name => "DisplayName COLLATE NOCASE ASC, Path COLLATE NOCASE ASC",
                FolderSortMode.Author => "Author COLLATE NOCASE ASC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Score => "Score DESC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.Series => "SeriesName COLLATE NOCASE ASC NULLS LAST, SeriesOrder IS NULL, SeriesOrder ASC, DisplayName COLLATE NOCASE ASC",
                FolderSortMode.ImageCount => "ImageCount DESC, DisplayName COLLATE NOCASE ASC",
                _ => "LastViewedAt DESC NULLS LAST, UpdatedAt DESC"
            };

            command.CommandText = $"""
                SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, CreatedAt, UpdatedAt
                FROM Folders
                {(where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where))}
                ORDER BY {orderBy};
                """;

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
                SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, CreatedAt, UpdatedAt
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
            SELECT Id, FolderId, Path, FileName, FileSize, ModifiedAt, SortOrder
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
                SortOrder = reader.GetInt32(6)
            });
        }

        return images;
    }

    public List<ImageItem> GetSeriesImages(string seriesName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Images.Id, Images.FolderId, Images.Path, Images.FileName, Images.FileSize, Images.ModifiedAt, Images.SortOrder, Folders.DisplayName, Folders.SeriesOrder
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
                FolderDisplayName = reader.IsDBNull(7) ? null : reader.GetString(7),
                FolderSeriesOrder = reader.IsDBNull(8) ? null : reader.GetInt32(8)
            });
        }

        return images;
    }

    public FolderItem? GetFirstFolderInSeries(string seriesName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, DisplayName, Author, Number, SeriesName, SeriesOrder, Score, Memo, IsFavorite, IsReserved, ViewCount, LastViewedAt, LastImagePath, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, CreatedAt, UpdatedAt
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

    public List<ImageItem> GetAllImages()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FolderId, Path, FileName, FileSize, ModifiedAt, SortOrder
            FROM Images
            ORDER BY FileName COLLATE NOCASE ASC, FileSize ASC, Path COLLATE NOCASE ASC;
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
                SortOrder = reader.GetInt32(6)
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
        var folderId = GetFolderId(connection, transaction, result.FolderPath);
        if (folderId is null)
        {
            var parsed = FolderNameParser.Parse(new DirectoryInfo(result.FolderPath).Name);
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Folders (Path, DisplayName, Author, Number, FolderModifiedAt, ImageCount, TotalImageBytes, ThumbnailPath, CreatedAt, UpdatedAt)
                VALUES ($path, $displayName, $author, $number, $folderModifiedAt, $imageCount, $totalImageBytes, $thumbnailPath, $createdAt, $updatedAt);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$path", result.FolderPath);
            insert.Parameters.AddWithValue("$displayName", parsed.DisplayName);
            insert.Parameters.AddWithValue("$author", DbValue(parsed.Author));
            insert.Parameters.AddWithValue("$number", DbValue(parsed.Number));
            insert.Parameters.AddWithValue("$folderModifiedAt", ToDb(folderModifiedAt));
            insert.Parameters.AddWithValue("$imageCount", result.ImageCount);
            insert.Parameters.AddWithValue("$totalImageBytes", result.TotalImageBytes);
            insert.Parameters.AddWithValue("$thumbnailPath", result.Images[0].FullName);
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
                    FolderModifiedAt = $folderModifiedAt,
                    ImageCount = $imageCount,
                    TotalImageBytes = $totalImageBytes,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            update.Parameters.AddWithValue("$thumbnailPath", result.Images[0].FullName);
            update.Parameters.AddWithValue("$folderModifiedAt", ToDb(folderModifiedAt));
            update.Parameters.AddWithValue("$imageCount", result.ImageCount);
            update.Parameters.AddWithValue("$totalImageBytes", result.TotalImageBytes);
            update.Parameters.AddWithValue("$updatedAt", ToDb(now));
            update.Parameters.AddWithValue("$id", folderId.Value);
            update.ExecuteNonQuery();
        }

        using (var deleteImages = connection.CreateCommand())
        {
            deleteImages.Transaction = transaction;
            deleteImages.CommandText = "DELETE FROM Images WHERE FolderId = $folderId;";
            deleteImages.Parameters.AddWithValue("$folderId", folderId.Value);
            deleteImages.ExecuteNonQuery();
        }

        for (var i = 0; i < result.Images.Count; i++)
        {
            var image = result.Images[i];
            using var insertImage = connection.CreateCommand();
            insertImage.Transaction = transaction;
            insertImage.CommandText = """
                INSERT INTO Images (FolderId, Path, FileName, FileSize, ModifiedAt, SortOrder)
                VALUES ($folderId, $path, $fileName, $fileSize, $modifiedAt, $sortOrder)
                ON CONFLICT(Path) DO UPDATE SET
                    FolderId = excluded.FolderId,
                    FileName = excluded.FileName,
                    FileSize = excluded.FileSize,
                    ModifiedAt = excluded.ModifiedAt,
                    SortOrder = excluded.SortOrder;
                """;
            insertImage.Parameters.AddWithValue("$folderId", folderId.Value);
            insertImage.Parameters.AddWithValue("$path", image.FullName);
            insertImage.Parameters.AddWithValue("$fileName", image.Name);
            insertImage.Parameters.AddWithValue("$fileSize", image.Length);
            insertImage.Parameters.AddWithValue("$modifiedAt", ToDb(image.LastWriteTime));
            insertImage.Parameters.AddWithValue("$sortOrder", i);
            insertImage.ExecuteNonQuery();
        }

    }

    public sealed class ScanWriteSession : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;
        private bool committed;

        public ScanWriteSession(AppDatabase database)
        {
            connection = database.OpenConnection();
            transaction = connection.BeginTransaction();
        }

        public void Save(FolderScanResult result)
        {
            UpsertScannedFolder(connection, transaction, result);
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
    }

    public CleanupSummary RemoveMissingFoldersAndImages()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var summary = new CleanupSummary();
        var folders = new List<(long Id, string Path)>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT Id, Path FROM Folders;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                folders.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path))
            {
                using var deleteFolder = connection.CreateCommand();
                deleteFolder.Transaction = transaction;
                deleteFolder.CommandText = "DELETE FROM Folders WHERE Id = $id;";
                deleteFolder.Parameters.AddWithValue("$id", folder.Id);
                deleteFolder.ExecuteNonQuery();
                summary.RemovedFolders++;
                continue;
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT Id, Path FROM Images;";
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
                using var deleteImage = connection.CreateCommand();
                deleteImage.Transaction = transaction;
                deleteImage.CommandText = "DELETE FROM Images WHERE Id = $id;";
                deleteImage.Parameters.AddWithValue("$id", imageId);
                deleteImage.ExecuteNonQuery();
                summary.RemovedImages++;
            }
        }

        using (var deleteEmptyFolders = connection.CreateCommand())
        {
            deleteEmptyFolders.Transaction = transaction;
            deleteEmptyFolders.CommandText = "DELETE FROM Folders WHERE NOT EXISTS (SELECT 1 FROM Images WHERE Images.FolderId = Folders.Id);";
            summary.RemovedFolders += deleteEmptyFolders.ExecuteNonQuery();
        }

        transaction.Commit();
        return summary;
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
        using var command = connection.CreateCommand();
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

    private static FolderItem ReadFolder(SqliteDataReader reader)
    {
        return new FolderItem
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
            CreatedAt = FromDb(reader.GetString(18)) ?? DateTime.MinValue,
            UpdatedAt = FromDb(reader.GetString(19)) ?? DateTime.MinValue
        };
    }

    private static void BackfillFolderModifiedAt(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Folders
            SET FolderModifiedAt = (
                SELECT MAX(ModifiedAt)
                FROM Images
                WHERE Images.FolderId = Folders.Id
            )
            WHERE FolderModifiedAt IS NULL
              AND EXISTS (SELECT 1 FROM Images WHERE Images.FolderId = Folders.Id);
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
                )
            WHERE ImageCount = 0
              AND EXISTS (SELECT 1 FROM Images WHERE Images.FolderId = Folders.Id);
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

    private static Dictionary<long, List<string>> GetTagsForFolders(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ft.FolderId, t.Name
            FROM FolderTags ft
            JOIN Tags t ON t.Id = ft.TagId
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
