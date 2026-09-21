using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Parses one SPEC workbook (the sheet in the screenshot: a "Driving per spec" column group —
/// SPEC, (R) &amp; RAD S, W, H, RAD P, THICKNESS — followed by an "Allowed Material" column group with one
/// YES/-/blank column per material grade). The six driving columns are matched by a small alias list so
/// harmless header variation (the workbook's own "TICHKNESS" typo, an ampersand or extra space) doesn't
/// break the import; everything to their right, read from the header row itself rather than hardcoded, is
/// treated as a material-grade column — so a new grade can be added in Excel alone.
///
/// Read with DocumentFormat.OpenXml directly rather than ClosedXML. Only cell values are needed, and ClosedXML
/// brings System.Memory, System.Buffers, System.Numerics.Vectors and SixLabors.Fonts, which reference each other
/// at versions no single set of DLLs satisfies without binding redirects — and NX, the host, never reads ours.
/// OpenXml depends on framework assemblies only.</summary>
public sealed class ExcelBeadSpecParser
{
    private static readonly string[] SpecAliases = { "SPEC" };
    private static readonly string[] RadiusAliases = { "R&RADS", "RRADS", "RANDRADS" };
    private static readonly string[] WidthAliases = { "W" };
    private static readonly string[] HeightAliases = { "H" };
    private static readonly string[] DieRadiusAliases = { "RADP" };
    private static readonly string[] ThicknessAliases = { "THICKNESS", "TICHKNESS" };

    /// <param name="workbookName">Tagged onto every row as <see cref="BeadSpecRow.WorkbookName"/>, so a row still
    /// says which of a Standard's bead SPEC workbooks it came from once rows from several are in one list.</param>
    public IReadOnlyList<BeadSpecRow> Parse(string standardId, string workbookName, Stream workbookStream)
    {
        var sheet = ReadFirstSheet(workbookStream);

        var headerRow = FindHeaderRow(sheet);
        var header = sheet[headerRow];
        var lastColumn = header.Where(kv => kv.Value.Length > 0).Select(kv => kv.Key).DefaultIfEmpty(0).Max();
        if (lastColumn == 0)
            throw new InvalidDataException("SPEC workbook header row has no columns.");

        var columnsByAlias = new Dictionary<string, int>();
        for (var col = 1; col <= lastColumn; col++)
        {
            var normalized = Normalize(Text(header, col));
            if (normalized.Length > 0 && !columnsByAlias.ContainsKey(normalized))
                columnsByAlias[normalized] = col;
        }

        var specCol = RequireColumn(columnsByAlias, SpecAliases, "SPEC");
        var radiusCol = RequireColumn(columnsByAlias, RadiusAliases, "(R) & RAD S");
        var widthCol = RequireColumn(columnsByAlias, WidthAliases, "W");
        var heightCol = RequireColumn(columnsByAlias, HeightAliases, "H");
        var dieRadiusCol = RequireColumn(columnsByAlias, DieRadiusAliases, "RAD P");
        var thicknessCol = RequireColumn(columnsByAlias, ThicknessAliases, "THICKNESS");

        var fixedColumns = new HashSet<int> { specCol, radiusCol, widthCol, heightCol, dieRadiusCol, thicknessCol };
        var gradeColumns = new List<(int Column, string Label)>();
        for (var col = 1; col <= lastColumn; col++)
        {
            if (fixedColumns.Contains(col))
                continue;
            var label = Text(header, col).Trim();
            if (label.Length > 0)
                gradeColumns.Add((col, label));
        }

        var rows = new List<BeadSpecRow>();
        var rowNumber = headerRow + 1;
        while (sheet.TryGetValue(rowNumber, out var cells) && Text(cells, specCol).Length > 0)
        {
            var grades = gradeColumns.ToDictionary(
                g => g.Label,
                g => IsYes(Text(cells, g.Column)));

            rows.Add(new BeadSpecRow(
                standardId,
                workbookName,
                Text(cells, specCol).Trim(),
                Number(cells, radiusCol, rowNumber),
                Number(cells, widthCol, rowNumber),
                Number(cells, heightCol, rowNumber),
                Number(cells, dieRadiusCol, rowNumber),
                Number(cells, thicknessCol, rowNumber),
                grades));

            rowNumber++;
        }

        return rows;
    }

    /// <summary>The first worksheet (in the workbook's own tab order) as row number → column number → cell text.
    /// Empty cells are simply absent.</summary>
    private static SortedDictionary<int, Dictionary<int, string>> ReadFirstSheet(Stream workbookStream)
    {
        SpreadsheetDocument document;
        try
        {
            document = SpreadsheetDocument.Open(workbookStream, false);
        }
        catch (OpenXmlPackageException ex)
        {
            throw new InvalidDataException($"Not a readable .xlsx workbook: {ex.Message}", ex);
        }

        using (document)
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("The workbook has no workbook part.");
            var firstSheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
                ?? throw new InvalidDataException("The workbook has no worksheets.");
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id!.Value!);

            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
                .Elements<SharedStringItem>()
                .Select(ItemText)
                .ToList() ?? new List<string>();

            var sheet = new SortedDictionary<int, Dictionary<int, string>>();
            var nextRow = 1;
            foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
            {
                var rowNumber = row.RowIndex is { } index ? (int)index.Value : nextRow;
                nextRow = rowNumber + 1;

                var cells = new Dictionary<int, string>();
                var nextColumn = 1;
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = cell.CellReference?.Value is { } reference ? ColumnOf(reference) : nextColumn;
                    nextColumn = column + 1;

                    var text = CellText(cell, sharedStrings);
                    if (text.Length > 0)
                        cells[column] = text;
                }

                sheet[rowNumber] = cells;
            }

            return sheet;
        }
    }

    private static string CellText(Cell cell, IReadOnlyList<string> sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString is { } inline ? ItemText(inline) : "";

        var raw = cell.CellValue?.Text ?? "";
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i >= 0 && i < sharedStrings.Count
                ? sharedStrings[i]
                : "";
        }

        if (cell.DataType?.Value == CellValues.Boolean)
            return raw == "1" ? "TRUE" : "FALSE";

        // Numbers (stored invariant), formula string results and errors: the stored value is the text.
        return raw;
    }

    /// <summary>A shared or inline string's text: its plain text, or its rich-text runs joined — never the phonetic
    /// guide, which a plain InnerText would include.</summary>
    private static string ItemText(OpenXmlCompositeElement item) =>
        item.GetFirstChild<Text>() is { } text
            ? text.Text
            : string.Concat(item.Elements<Run>().Select(r => r.Text?.Text ?? ""));

    /// <summary>1-based column number of a cell reference such as "AB12".</summary>
    private static int ColumnOf(string cellReference)
    {
        var column = 0;
        foreach (var c in cellReference)
        {
            if (!char.IsLetter(c))
                break;
            column = column * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return column;
    }

    private static string Text(IReadOnlyDictionary<int, string> cells, int column) =>
        cells.TryGetValue(column, out var text) ? text : "";

    private static double Number(IReadOnlyDictionary<int, string> cells, int column, int rowNumber)
    {
        var text = Text(cells, column).Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        throw new InvalidDataException(
            $"SPEC workbook cell {ColumnName(column)}{rowNumber} should be a number but reads '{text}'.");
    }

    private static string ColumnName(int column)
    {
        var name = "";
        for (; column > 0; column = (column - 1) / 26)
            name = (char)('A' + (column - 1) % 26) + name;
        return name;
    }

    private static int FindHeaderRow(SortedDictionary<int, Dictionary<int, string>> sheet)
    {
        // KeyValuePair has no Deconstruct on net48.
        foreach (var row in sheet)
        {
            if (row.Value.Values.Any(text => Normalize(text) == "SPEC"))
                return row.Key;
        }

        throw new InvalidDataException("Could not find the header row (a cell reading 'SPEC') in the workbook.");
    }

    private static int RequireColumn(IReadOnlyDictionary<string, int> columnsByAlias, string[] aliases, string displayName)
    {
        foreach (var alias in aliases)
        {
            if (columnsByAlias.TryGetValue(alias, out var column))
                return column;
        }

        throw new InvalidDataException(
            $"SPEC workbook is missing the '{displayName}' column (looked for header text matching: {string.Join(", ", aliases)}).");
    }

    private static bool IsYes(string cellText) =>
        string.Equals(cellText.Trim(), "YES", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string text) =>
        new string(text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
