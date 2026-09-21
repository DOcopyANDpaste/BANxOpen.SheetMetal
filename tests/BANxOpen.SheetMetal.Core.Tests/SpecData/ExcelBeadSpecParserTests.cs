using ClosedXML.Excel;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.SpecData;

public class ExcelBeadSpecParserTests
{
    private static Stream BuildSampleWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");

        // Row 1: group headers (deliberately not aligned to a single column — matches the screenshot,
        // where "Driving per spec" merges over several columns). The parser ignores row 1 entirely and
        // locates the real header by searching for a "SPEC" cell.
        sheet.Cell(1, 3).Value = "Driving per spec";
        sheet.Cell(1, 7).Value = "Allowed Material";

        // Row 2: real header row, including the workbook's own "TICHKNESS" typo and an ampersand header,
        // both of which the alias list must tolerate.
        sheet.Cell(2, 1).Value = "SPEC";
        sheet.Cell(2, 2).Value = "(R) & RAD S";
        sheet.Cell(2, 3).Value = "W";
        sheet.Cell(2, 4).Value = "H";
        sheet.Cell(2, 5).Value = "RAD P";
        sheet.Cell(2, 6).Value = "TICHKNESS";
        sheet.Cell(2, 7).Value = "2024-O";
        sheet.Cell(2, 8).Value = "5052-O";

        // Row 3+: data.
        sheet.Cell(3, 1).Value = "B1005010-1";
        sheet.Cell(3, 2).Value = 0.245;
        sheet.Cell(3, 3).Value = 0.625;
        sheet.Cell(3, 4).Value = 0.625;
        sheet.Cell(3, 5).Value = 0.188;
        sheet.Cell(3, 6).Value = 0.02;
        sheet.Cell(3, 7).Value = "YES";
        sheet.Cell(3, 8).Value = "-";

        sheet.Cell(4, 1).Value = "B1005010-2";
        sheet.Cell(4, 2).Value = 0.240;
        sheet.Cell(4, 3).Value = 0.625;
        sheet.Cell(4, 4).Value = 0.625;
        sheet.Cell(4, 5).Value = 0.188;
        sheet.Cell(4, 6).Value = 0.025;
        sheet.Cell(4, 7).Value = "YES";
        sheet.Cell(4, 8).Value = "YES";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Parse_ReadsAllRowsWithFixedAndDynamicMaterialColumns()
    {
        using var stream = BuildSampleWorkbook();
        var parser = new ExcelBeadSpecParser();

        var rows = parser.Parse("B1005010", "B1005010", stream);

        Assert.Equal(2, rows.Count);

        var first = rows[0];
        Assert.Equal("B1005010", first.StandardId);
        Assert.Equal("B1005010", first.WorkbookName);
        Assert.Equal("B1005010-1", first.SpecId);
        Assert.Equal(0.245, first.RadiusAndRadS);
        Assert.Equal(0.625, first.Width);
        Assert.Equal(0.625, first.Height);
        Assert.Equal(0.188, first.DieRadiusP);
        Assert.Equal(0.02, first.Thickness);
        Assert.True(first.AllowedMaterialGrades["2024-O"]);
        Assert.False(first.AllowedMaterialGrades["5052-O"]);

        Assert.True(rows[1].AllowedMaterialGrades["5052-O"]);
    }

    [Fact]
    public void Parse_MissingRequiredColumn_ThrowsWithColumnName()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = "SPEC";
        sheet.Cell(1, 2).Value = "(R) & RAD S";
        sheet.Cell(1, 3).Value = "W";
        sheet.Cell(1, 4).Value = "H";
        sheet.Cell(1, 5).Value = "RAD P";
        // No THICKNESS column at all.
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var parser = new ExcelBeadSpecParser();

        var ex = Assert.Throws<InvalidDataException>(() => parser.Parse("B1005010", "B1005010", stream));
        Assert.Contains("THICKNESS", ex.Message);
    }

    private static Stream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void WriteHeader(IXLWorksheet sheet, int row)
    {
        string[] headers = { "SPEC", "(R) & RAD S", "W", "H", "RAD P", "THICKNESS", "2024-O" };
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(row, c + 1).Value = headers[c];
    }

    [Fact]
    public void Parse_RichTextHeader_IsReadAsItsJoinedText()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        WriteHeader(sheet, 1);
        sheet.Cell(1, 6).Value = "";
        sheet.Cell(1, 6).CreateRichText().AddText("TICH").SetBold().AddText("KNESS");
        sheet.Cell(2, 1).Value = "B1005010-1";
        for (var c = 2; c <= 6; c++)
            sheet.Cell(2, c).Value = 0.5;
        sheet.Cell(2, 7).Value = "YES";

        using var stream = Save(workbook);
        var rows = new ExcelBeadSpecParser().Parse("STD", "B1005010", stream);

        Assert.Single(rows);
        Assert.Equal(0.5, rows[0].Thickness);
        Assert.True(rows[0].AllowedMaterialGrades["2024-O"]);
    }

    [Fact]
    public void Parse_TextInANumberColumn_ThrowsNamingTheCell()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        WriteHeader(sheet, 1);
        sheet.Cell(2, 1).Value = "B1005010-1";
        for (var c = 2; c <= 6; c++)
            sheet.Cell(2, c).Value = 0.5;
        sheet.Cell(2, 3).Value = "wide";

        using var stream = Save(workbook);

        var ex = Assert.Throws<InvalidDataException>(() => new ExcelBeadSpecParser().Parse("STD", "B1005010", stream));
        Assert.Contains("C2", ex.Message);
    }

    [Fact]
    public void Parse_ReadsOnlyTheFirstSheetAndStopsAtTheFirstEmptySpec()
    {
        using var workbook = new XLWorkbook();
        var first = workbook.Worksheets.Add("Specs");
        WriteHeader(first, 1);
        first.Cell(2, 1).Value = "B1005010-1";
        for (var c = 2; c <= 6; c++)
            first.Cell(2, c).Value = 0.5;
        // Row 3 left empty: the list ends there, whatever follows.
        first.Cell(4, 1).Value = "B1005010-9";
        for (var c = 2; c <= 6; c++)
            first.Cell(4, c).Value = 0.5;

        var second = workbook.Worksheets.Add("Notes");
        WriteHeader(second, 1);
        second.Cell(2, 1).Value = "NOT-A-SPEC";

        using var stream = Save(workbook);
        var rows = new ExcelBeadSpecParser().Parse("STD", "B1005010", stream);

        Assert.Equal(new[] { "B1005010-1" }, rows.Select(r => r.SpecId));
    }
}
