using System.Globalization;
using System.Text;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Reads the MATERIAL_TABLE block of an NX sheet metal material standards file (.txt or .csv), the format
/// documented in NX's own <c>UGII\materials\sheet_metal_material_table.txt</c>:
/// <code>
/// UNITS,ENGLISH
/// MATERIAL_TABLE
/// VERSION,2
/// Material_Name,Standard,PHYSICAL_MATERIAL_NAME,SheetMetal_Material,THICKNESS,BEND_RADIUS
/// unique,show,show,attribute,show,show
/// 2024-O_0.016,XX_Standard,Aluminum 2024,2024-O,0.016,@BEND_TABLE_1
/// END_OF_MATERIAL_TABLE
/// </code>
/// Only what the tool needs is read: UNITS and the material table. The tool, formula and bend tables are NX's business
/// and are skipped, as are <c>#</c> comment lines.
///
/// Cells may be separated by commas, which NX documents, or by tabs, which is what a table pasted out of a
/// spreadsheet has. The delimiter is decided once, from the table's header row: NX's own sample pads its commas with
/// tabs, and a tab-separated table can hold commas inside a cell (a physical material name such as
/// "Aluminum 2024, Temper O"), so neither can be guessed line by line.
///
/// Only ENGLISH files are accepted: bead SPEC workbooks and the thickness tolerance are in inches, and a METRIC table
/// compared against them would pass or fail by accident.</summary>
public static class SheetMetalMaterialTableParser
{
    private const string UnitsKeyword = "UNITS";
    private const string SupportedUnits = "ENGLISH";
    private const string TableStart = "MATERIAL_TABLE";
    private const string TableEnd = "END_OF_MATERIAL_TABLE";
    private const string VersionKeyword = "VERSION";
    private const string UniqueProperty = "unique";

    private static readonly string[] KnownProperties = { UniqueProperty, "show", "hide", "attribute" };

    /// <exception cref="InvalidDataException">The content is not a valid ENGLISH standards file. The message names the
    /// line at fault.</exception>
    /// <param name="text">Words the errors; <see cref="SheetMetalText.Default"/> when null.</param>
    public static IReadOnlyList<SheetMetalMaterialRow> Parse(byte[] content, SheetMetalText? text = null) => Parse(Decode(content), text);

    /// <inheritdoc cref="Parse(byte[])"/>
    public static IReadOnlyList<SheetMetalMaterialRow> Parse(string content, SheetMetalText? text = null)
    {
        text ??= SheetMetalText.Default;
        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        string? units = null;
        var index = 0;

        for (; index < lines.Length; index++)
        {
            if (IsSkippable(lines[index]))
                continue;

            var keyword = SplitKeywordLine(lines[index]);
            if (Is(keyword[0], UnitsKeyword))
                units = keyword.Length > 1 ? keyword[1] : "";
            else if (Is(keyword[0], TableStart))
                break;
        }

        if (index == lines.Length)
            throw new InvalidDataException(text.TableNoBlock(TableStart));

        if (units is null)
            throw new InvalidDataException(text.TableNoUnits(UnitsKeyword, TableStart, index + 1, SupportedUnits));

        if (!Is(units, SupportedUnits))
            throw new InvalidDataException(text.TableUnitsUnsupported(UnitsKeyword, units, SupportedUnits));

        return ReadTable(lines, index + 1, text);
    }

    private static IReadOnlyList<SheetMetalMaterialRow> ReadTable(string[] lines, int index, SheetMetalText text)
    {
        // VERSION, header row, property row — in that order, skipping comments and blank lines between them.
        index = NextContentLine(lines, index, text.TableExpectedHeaderRow, text);
        if (Is(SplitKeywordLine(lines[index])[0], VersionKeyword))
            index = NextContentLine(lines, index + 1, text.TableExpectedHeaderRow, text);

        var headerLine = index;
        var delimiter = lines[headerLine].Contains(',') ? ',' : '\t';
        var headers = TrimTrailingEmpty(SplitCells(lines[headerLine], delimiter));

        index = NextContentLine(lines, index + 1, text.TableExpectedPropertyRow, text);
        var propertyLine = index;
        var properties = TrimTrailingEmpty(SplitCells(lines[propertyLine], delimiter));

        var uniqueColumn = ValidateColumns(headers, properties, headerLine + 1, propertyLine + 1, text);

        var rows = new List<SheetMetalMaterialRow>();
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (index++; index < lines.Length; index++)
        {
            if (IsSkippable(lines[index]))
                continue;

            var lineNumber = index + 1;
            var cells = TrimTrailingEmpty(SplitCells(lines[index], delimiter));

            if (cells.Count == 1 && Is(cells[0], TableEnd))
                return rows;

            if (cells.Count > headers.Count)
                throw new InvalidDataException(text.TableRowWidth(lineNumber, cells.Count, headerLine + 1, headers.Count));

            var row = ReadRow(headers, cells, uniqueColumn, lineNumber, text);

            if (names.TryGetValue(row.Name, out var firstLine))
                throw new InvalidDataException(text.TableDuplicateName(lineNumber, row.Name, firstLine, headers[uniqueColumn]));

            names[row.Name] = lineNumber;
            rows.Add(row);
        }

        throw new InvalidDataException(text.TableNoEnd(TableStart, TableEnd));
    }

    private static int ValidateColumns(List<string> headers, List<string> properties, int headerLine, int propertyLine, SheetMetalText text)
    {
        if (headers.Count == 0)
            throw new InvalidDataException(text.TableHeaderEmpty(headerLine));

        var blank = headers.FindIndex(h => h.Length == 0);
        if (blank >= 0)
            throw new InvalidDataException(text.TableColumnUnnamed(headerLine, blank + 1));

        var duplicate = headers.GroupBy(h => h, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException(text.TableColumnRepeated(headerLine, duplicate.Key));

        if (properties.Count != headers.Count)
            throw new InvalidDataException(text.TablePropertyRowWidth(propertyLine, properties.Count, headers.Count));

        for (var i = 0; i < properties.Count; i++)
        {
            if (!KnownProperties.Any(p => Is(properties[i], p)))
                throw new InvalidDataException(text.TablePropertyUnknown(propertyLine, headers[i], properties[i], KnownProperties));
        }

        var unique = Enumerable.Range(0, properties.Count).Where(i => Is(properties[i], UniqueProperty)).ToList();
        if (unique.Count != 1)
            throw new InvalidDataException(text.TableUniqueCount(propertyLine, UniqueProperty, unique.Count));

        var missing = SheetMetalMaterialColumns.Required
            .Where(required => !headers.Any(h => Is(h, required)))
            .ToList();
        if (missing.Count > 0)
            throw new InvalidDataException(text.TableColumnsMissing(headerLine, missing));

        return unique[0];
    }

    private static SheetMetalMaterialRow ReadRow(List<string> headers, List<string> cells, int uniqueColumn, int lineNumber, SheetMetalText text)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
            columns[headers[i]] = i < cells.Count ? cells[i] : "";

        var name = columns[headers[uniqueColumn]];
        if (name.Length == 0)
            throw new InvalidDataException(text.TableUniqueEmpty(lineNumber, headers[uniqueColumn]));

        foreach (var required in SheetMetalMaterialColumns.Required)
        {
            if (columns[required].Length == 0)
                throw new InvalidDataException(text.TableValueEmpty(lineNumber, name, required));
        }

        var thicknessText = columns[SheetMetalMaterialColumns.Thickness];
        if (!double.TryParse(thicknessText, NumberStyles.Float, CultureInfo.InvariantCulture, out var thickness) || !(thickness > 0))
            throw new InvalidDataException(text.TableThicknessInvalid(lineNumber, name, SheetMetalMaterialColumns.Thickness, thicknessText));

        return new SheetMetalMaterialRow(
            name,
            columns[SheetMetalMaterialColumns.Standard],
            columns.TryGetValue(SheetMetalMaterialColumns.SheetMetalStandard, out var sheetMetalStandard) ? sheetMetalStandard : "",
            columns[SheetMetalMaterialColumns.PhysicalMaterialName],
            columns[SheetMetalMaterialColumns.SheetMetalMaterial],
            thickness,
            columns.TryGetValue(SheetMetalMaterialColumns.BendRadius, out var bendRadius) ? bendRadius : "",
            lineNumber,
            columns);
    }

    /// <summary>A UTF-8 or UTF-16 file with a byte order mark is read as such, as is UTF-8 without one. Anything else is
    /// read as Latin-1, the nearest encoding available on both net48 and net8.0 to the Windows code page a spreadsheet
    /// saves a .csv in, so a degree sign in a material name survives either way.
    /// VERIFY: which encoding NX itself expects the file in.</summary>
    private static string Decode(byte[] content)
    {
        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
            return Encoding.Unicode.GetString(content, 2, content.Length - 2);
        if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(content, 2, content.Length - 2);

        var offset = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(content, offset, content.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(28591).GetString(content, offset, content.Length - offset);
        }
    }

    private static int NextContentLine(string[] lines, int index, string expected, SheetMetalText text)
    {
        for (; index < lines.Length; index++)
        {
            if (!IsSkippable(lines[index]))
                return index;
        }

        throw new InvalidDataException(text.TableEndsBefore(TableStart, expected));
    }

    private static bool IsSkippable(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length == 0 || trimmed[0] == '#' || trimmed.All(c => c == ',' || char.IsWhiteSpace(c));
    }

    /// <summary>Keyword lines (UNITS, VERSION, MATERIAL_TABLE) come before the delimiter is known, and hold no cell
    /// that could contain either delimiter, so they split on both.</summary>
    private static string[] SplitKeywordLine(string line) =>
        line.Split(new[] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray();

    private static List<string> SplitCells(string line, char delimiter) =>
        line.Split(delimiter).Select(c => c.Trim()).ToList();

    private static List<string> TrimTrailingEmpty(List<string> cells)
    {
        while (cells.Count > 0 && cells[cells.Count - 1].Length == 0)
            cells.RemoveAt(cells.Count - 1);
        return cells;
    }

    private static bool Is(string value, string keyword) => string.Equals(value, keyword, StringComparison.OrdinalIgnoreCase);
}
