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
            var images = database.GetAllImages()
                .Where(image => File.Exists(image.Path))
                .ToList();

            progress?.Report($"중복 후보 확인 중... 이미지 {images.Count}개");
            var possibleDuplicates = images
                .GroupBy(image => new { FileName = image.FileName.ToUpperInvariant(), image.FileSize })
                .Where(group => group.Count() > 1)
                .SelectMany(group => group)
                .ToList();

            if (possibleDuplicates.Count == 0)
            {
                return null;
            }

            var hashed = new List<DuplicateImageCandidate>();
            for (var index = 0; index < possibleDuplicates.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image = possibleDuplicates[index];
                progress?.Report($"해시 계산 중... {index + 1} / {possibleDuplicates.Count}");

                var hash = ComputeMd5(image.Path);
                if (hash is null)
                {
                    continue;
                }

                hashed.Add(new DuplicateImageCandidate
                {
                    FileName = image.FileName,
                    FileSize = image.FileSize,
                    Hash = hash,
                    Path = image.Path
                });
            }

            var duplicates = hashed
                .GroupBy(candidate => new { candidate.FileName, candidate.FileSize, candidate.Hash })
                .Where(group => group.Count() > 1)
                .SelectMany(group => group.OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (duplicates.Count == 0)
            {
                return null;
            }

            var exportDirectory = Path.Combine(AppContext.BaseDirectory, "Exports");
            Directory.CreateDirectory(exportDirectory);
            var exportPath = Path.Combine(exportDirectory, $"duplicate_images_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
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

    private static void WriteXlsx(string path, IReadOnlyList<DuplicateImageCandidate> duplicates)
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
                <sheet name="중복 이미지" sheetId="1" r:id="rId1"/>
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

    private static string BuildSheetXml(IReadOnlyList<DuplicateImageCandidate> duplicates)
    {
        var builder = new StringBuilder();
        using var writer = XmlWriter.Create(builder, new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8,
            Indent = true
        });

        writer.WriteStartDocument(true);
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("cols");
        WriteColumn(writer, 1, 1, 32);
        WriteColumn(writer, 2, 2, 14);
        WriteColumn(writer, 3, 3, 36);
        WriteColumn(writer, 4, 4, 120);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");
        WriteRow(writer, 1, ["파일명", "크기", "MD5", "경로"]);

        for (var index = 0; index < duplicates.Count; index++)
        {
            var duplicate = duplicates[index];
            WriteRow(writer, index + 2, [
                duplicate.FileName,
                duplicate.FileSize.ToString(),
                duplicate.Hash,
                duplicate.Path
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
        writer.Write(content);
    }
}
