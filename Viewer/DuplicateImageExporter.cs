using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Viewer;

public sealed class DuplicateImageExporter
{
    private readonly AppDatabase database;

    public DuplicateImageExporter(AppDatabase database)
    {
        this.database = database;
    }

    public Task<string?> ExportAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var partialDuplicateThreshold = Math.Clamp(AppSettings.Current.PartialDuplicateThresholdPercent, 50, 100) / 100.0;
            var images = database.GetAllImages()
                .Where(image => File.Exists(image.Path))
                .ToList();

            progress?.Report($"중복 폴더 후보 확인 중... 이미지 {images.Count}개 / 부분 기준 {partialDuplicateThreshold:P0}");
            var folderGroups = images
                .GroupBy(image => new
                {
                    image.FolderId,
                    FolderName = image.FolderDisplayName ?? "",
                    FolderPath = image.FolderPath ?? "",
                    image.FolderImageCount,
                    image.FolderTotalImageBytes,
                    image.FolderModifiedAt
                })
                .Where(group => group.Key.FolderImageCount > 0)
                .ToList();

            if (folderGroups.Count == 0)
            {
                return null;
            }

            var snapshots = new List<FolderHashSnapshot>();
            var hashToFolderIds = new Dictionary<string, List<long>>(StringComparer.Ordinal);
            for (var index = 0; index < folderGroups.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var folderGroup = folderGroups[index];
                progress?.Report($"폴더 해시 계산 중... {index + 1} / {folderGroups.Count}");
                var hashes = new List<string>();
                var failed = false;
                foreach (var image in folderGroup.OrderBy(image => image.SortOrder).ThenBy(image => image.FileName, StringComparer.OrdinalIgnoreCase))
                {
                    var hash = ComputeMd5(image.Path);
                    if (hash is null)
                    {
                        failed = true;
                        break;
                    }

                    hashes.Add($"{image.FileSize}:{hash}");
                }

                if (failed || hashes.Count == 0)
                {
                    continue;
                }

                var hashSet = hashes.ToHashSet(StringComparer.Ordinal);
                var snapshot = new FolderHashSnapshot(
                    folderGroup.Key.FolderId,
                    folderGroup.Key.FolderName,
                    folderGroup.Key.FolderPath,
                    folderGroup.Key.FolderImageCount,
                    folderGroup.Key.FolderTotalImageBytes,
                    folderGroup.Key.FolderModifiedAt,
                    hashSet,
                    string.Join("|", hashSet.OrderBy(hash => hash, StringComparer.Ordinal)));
                snapshots.Add(snapshot);

                foreach (var hash in hashSet)
                {
                    if (!hashToFolderIds.TryGetValue(hash, out var folderIds))
                    {
                        folderIds = [];
                        hashToFolderIds[hash] = folderIds;
                    }

                    folderIds.Add(snapshot.FolderId);
                }
            }

            var snapshotMap = snapshots.ToDictionary(snapshot => snapshot.FolderId);
            var duplicates = new List<DuplicateFolderCandidate>();
            var nextGroupNumber = 1;
            var completeDuplicateFolderIds = new HashSet<long>();
            foreach (var group in snapshots
                .GroupBy(snapshot => snapshot.Signature)
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.First().ImageCount)
                .ThenByDescending(group => group.First().TotalImageBytes))
            {
                var ordered = group.OrderByDescending(snapshot => snapshot.ModifiedAt ?? DateTime.MinValue)
                    .ThenBy(snapshot => snapshot.FolderPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                for (var index = 0; index < ordered.Count; index++)
                {
                    var snapshot = ordered[index];
                    completeDuplicateFolderIds.Add(snapshot.FolderId);
                    duplicates.Add(CreateDuplicateFolder(snapshot, nextGroupNumber, ordered.Count, "완전", snapshot.ImageCount, 100, index == 0));
                }

                nextGroupNumber++;
            }

            progress?.Report("부분 중복 폴더 계산 중...");
            var pairMatches = new Dictionary<(long Left, long Right), int>();
            foreach (var folderIds in hashToFolderIds.Values.Where(ids => ids.Count > 1))
            {
                var orderedIds = folderIds.Distinct().OrderBy(id => id).ToList();
                for (var leftIndex = 0; leftIndex < orderedIds.Count; leftIndex++)
                {
                    for (var rightIndex = leftIndex + 1; rightIndex < orderedIds.Count; rightIndex++)
                    {
                        var key = (orderedIds[leftIndex], orderedIds[rightIndex]);
                        pairMatches.TryGetValue(key, out var count);
                        pairMatches[key] = count + 1;
                    }
                }
            }

            var partialPairs = pairMatches
                .Select(pair =>
                {
                    var left = snapshotMap[pair.Key.Left];
                    var right = snapshotMap[pair.Key.Right];
                    var rate = pair.Value / (double)Math.Min(left.ImageCount, right.ImageCount);
                    return new PartialDuplicatePair(left.FolderId, right.FolderId, pair.Value, rate);
                })
                .Where(pair => pair.MatchRate >= partialDuplicateThreshold && pair.MatchRate < 1)
                .OrderByDescending(pair => pair.MatchRate)
                .ThenByDescending(pair => pair.MatchedImageCount)
                .ToList();

            var partialGroups = BuildPartialGroups(partialPairs);
            foreach (var group in partialGroups)
            {
                var ordered = group
                    .Select(folderId => snapshotMap[folderId])
                    .OrderByDescending(snapshot => snapshot.ModifiedAt ?? DateTime.MinValue)
                    .ThenBy(snapshot => snapshot.FolderPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                for (var index = 0; index < ordered.Count; index++)
                {
                    var snapshot = ordered[index];
                    var bestPair = partialPairs
                        .Where(pair => pair.LeftFolderId == snapshot.FolderId || pair.RightFolderId == snapshot.FolderId)
                        .OrderByDescending(pair => pair.MatchRate)
                        .First();
                    duplicates.Add(CreateDuplicateFolder(snapshot, nextGroupNumber, ordered.Count, $"부분 {bestPair.MatchRate:P0}", bestPair.MatchedImageCount, bestPair.MatchRate * 100, index == 0));
                }

                nextGroupNumber++;
            }

            if (duplicates.Count == 0)
            {
                return null;
            }

            var exportDirectory = Path.Combine(AppContext.BaseDirectory, "Exports");
            Directory.CreateDirectory(exportDirectory);
            var exportPath = Path.Combine(exportDirectory, $"duplicate_folders_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            WriteXlsx(exportPath, duplicates);
            return exportPath;
        }, cancellationToken);
    }

    private static string? ComputeMd5(string path)
    {
        try
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(md5.ComputeHash(stream));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteXlsx(string path, IReadOnlyList<DuplicateFolderCandidate> duplicates)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        AddTextEntry(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            </Types>
            """);
        AddTextEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        AddTextEntry(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """);
        AddTextEntry(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="중복 폴더" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """);
        AddTextEntry(archive, "xl/styles.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
              <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
              <borders count="1"><border/></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>
            </styleSheet>
            """);
        AddTextEntry(archive, "xl/worksheets/sheet1.xml", BuildSheetXml(duplicates));
    }

    private static string BuildSheetXml(IReadOnlyList<DuplicateFolderCandidate> duplicates)
    {
        var builder = new StringBuilder();
        using var stringWriter = new Utf8StringWriter(builder);
        using var writer = XmlWriter.Create(stringWriter, new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8,
            Indent = true
        });

        writer.WriteStartDocument(true);
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("dimension");
        writer.WriteAttributeString("ref", $"A1:K{duplicates.Count + 3}");
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "3");
        writer.WriteAttributeString("topLeftCell", "A4");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr");
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteEndElement();
        writer.WriteStartElement("cols");
        WriteColumn(writer, 1, 1, 10);
        WriteColumn(writer, 2, 2, 12);
        WriteColumn(writer, 3, 3, 42);
        WriteColumn(writer, 4, 4, 12);
        WriteColumn(writer, 5, 5, 16);
        WriteColumn(writer, 6, 6, 18);
        WriteColumn(writer, 7, 7, 18);
        WriteColumn(writer, 8, 8, 14);
        WriteColumn(writer, 9, 9, 18);
        WriteColumn(writer, 10, 10, 20);
        WriteColumn(writer, 11, 11, 120);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");
        var groupCount = duplicates.Select(duplicate => duplicate.GroupNumber).Distinct().Count();
        WriteRow(writer, 1, ["요약", $"중복 폴더 그룹 {groupCount}개", $"중복 폴더 {duplicates.Count}개", $"생성 {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "", "", "", "", "", "", ""]);
        WriteRow(writer, 2, ["", "", "", "", "", "", "", "", "", "", ""]);
        WriteRow(writer, 3, ["그룹", "타입", "그룹 폴더", "폴더명", "이미지", "겹친 이미지", "중복률", "총 용량", "수정일", "정리 후보", "폴더 경로"]);

        for (var index = 0; index < duplicates.Count; index++)
        {
            var duplicate = duplicates[index];
            WriteRow(writer, index + 4, [
                duplicate.GroupNumber.ToString(),
                duplicate.DuplicateType,
                duplicate.GroupFolderCount.ToString(),
                duplicate.FolderName,
                duplicate.ImageCount.ToString(),
                duplicate.MatchedImageCount.ToString(),
                $"{duplicate.MatchRate:0.##}%",
                duplicate.TotalImageBytes.ToString(),
                duplicate.ModifiedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                duplicate.CleanupHint,
                duplicate.FolderPath
            ]);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        return builder.ToString();
    }

    private static void WriteColumn(XmlWriter writer, int min, int max, double width)
    {
        writer.WriteStartElement("col");
        writer.WriteAttributeString("min", min.ToString());
        writer.WriteAttributeString("max", max.ToString());
        writer.WriteAttributeString("width", width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int rowIndex, IReadOnlyList<string> values)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowIndex.ToString());
        for (var columnIndex = 0; columnIndex < values.Count; columnIndex++)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", $"{GetColumnName(columnIndex + 1)}{rowIndex}");
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is");
            writer.WriteElementString("t", values[columnIndex]);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static string GetColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = "";
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    private sealed class Utf8StringWriter(StringBuilder builder) : StringWriter(builder)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    private static DuplicateFolderCandidate CreateDuplicateFolder(FolderHashSnapshot snapshot, int groupNumber, int groupFolderCount, string duplicateType, int matchedImageCount, double matchRate, bool keepCandidate)
    {
        return new DuplicateFolderCandidate
        {
            GroupNumber = groupNumber,
            GroupFolderCount = groupFolderCount,
            FolderName = snapshot.FolderName,
            FolderPath = snapshot.FolderPath,
            ImageCount = snapshot.ImageCount,
            MatchedImageCount = matchedImageCount,
            MatchRate = matchRate,
            TotalImageBytes = snapshot.TotalImageBytes,
            ModifiedAt = snapshot.ModifiedAt,
            DuplicateType = duplicateType,
            CleanupHint = keepCandidate ? "보존 후보" : "삭제 후보 검토"
        };
    }

    private static List<HashSet<long>> BuildPartialGroups(IReadOnlyList<PartialDuplicatePair> pairs)
    {
        var parent = new Dictionary<long, long>();
        foreach (var pair in pairs)
        {
            parent.TryAdd(pair.LeftFolderId, pair.LeftFolderId);
            parent.TryAdd(pair.RightFolderId, pair.RightFolderId);
            Union(parent, pair.LeftFolderId, pair.RightFolderId);
        }

        return parent.Keys
            .GroupBy(folderId => Find(parent, folderId))
            .Select(group => group.ToHashSet())
            .Where(group => group.Count > 1)
            .ToList();
    }

    private static void Union(Dictionary<long, long> parent, long left, long right)
    {
        var leftRoot = Find(parent, left);
        var rightRoot = Find(parent, right);
        if (leftRoot != rightRoot)
        {
            parent[rightRoot] = leftRoot;
        }
    }

    private static long Find(Dictionary<long, long> parent, long folderId)
    {
        if (parent[folderId] == folderId)
        {
            return folderId;
        }

        parent[folderId] = Find(parent, parent[folderId]);
        return parent[folderId];
    }

    private sealed record FolderHashSnapshot(long FolderId, string FolderName, string FolderPath, int ImageCount, long TotalImageBytes, DateTime? ModifiedAt, HashSet<string> Hashes, string Signature);

    private sealed record PartialDuplicatePair(long LeftFolderId, long RightFolderId, int MatchedImageCount, double MatchRate);
}
