using ClosedXML.Excel;
using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Parses one SPEC workbook (the sheet in the screenshot: a "Driving per spec" column group —
/// SPEC, (R) &amp; RAD S, W, H, RAD P, THICKNESS — followed by an "Allowed Material" column group with one
/// YES/-/blank column per material grade). The six driving columns are matched by a small alias list so
/// harmless header variation (the workbook's own "TICHKNESS" typo, an ampersand or extra space) doesn't
/// break the import; everything to their right, read from the header row itself rather than hardcoded, is
/// treated as a material-grade column — so a new grade can be added in Excel alone.</summary>
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
        using var workbook = new XLWorkbook(workbookStream);
        var sheet = workbook.Worksheets.First();

        var headerRow = FindHeaderRow(sheet);
        var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber
            ?? throw new InvalidDataException("SPEC workbook header row has no columns.");

        var columnsByAlias = new Dictionary<string, int>();
        for (var col = 1; col <= lastColumn; col++)
        {
            var normalized = Normalize(headerRow.Cell(col).GetString());
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
            var label = headerRow.Cell(col).GetString().Trim();
            if (label.Length > 0)
                gradeColumns.Add((col, label));
        }

        var rows = new List<BeadSpecRow>();
        var row = headerRow.RowNumber() + 1;
        while (!sheet.Cell(row, specCol).IsEmpty())
        {
            var grades = gradeColumns.ToDictionary(
                g => g.Label,
                g => IsYes(sheet.Cell(row, g.Column).GetString()));

            rows.Add(new BeadSpecRow(
                standardId,
                workbookName,
                sheet.Cell(row, specCol).GetString().Trim(),
                sheet.Cell(row, radiusCol).GetDouble(),
                sheet.Cell(row, widthCol).GetDouble(),
                sheet.Cell(row, heightCol).GetDouble(),
                sheet.Cell(row, dieRadiusCol).GetDouble(),
                sheet.Cell(row, thicknessCol).GetDouble(),
                grades));

            row++;
        }

        return rows;
    }

    private static IXLRow FindHeaderRow(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed())
        {
            if (row.CellsUsed().Any(c => Normalize(c.GetString()) == "SPEC"))
                return row;
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
