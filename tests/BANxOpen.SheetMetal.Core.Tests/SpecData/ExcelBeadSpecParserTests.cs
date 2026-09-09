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

        var rows = parser.Parse("B1005010", stream);

        Assert.Equal(2, rows.Count);

        var first = rows[0];
        Assert.Equal("B1005010", first.StandardId);
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

        var ex = Assert.Throws<InvalidDataException>(() => parser.Parse("B1005010", stream));
        Assert.Contains("THICKNESS", ex.Message);
    }
}
