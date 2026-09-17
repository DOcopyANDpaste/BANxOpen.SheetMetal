using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SheetMetalMaterialTableTests
{
    private static readonly SheetMetalMaterialTable Table = TableRows.AluminumTable();

    [Fact]
    public void Standards_are_distinct_in_the_order_they_first_appear()
    {
        var table = SheetMetalMaterialTable.FromRows(new[]
        {
            TableRows.Row("a", "M", "G", standard: "YY_Standard"),
            TableRows.Row("b", "M", "G", standard: "XX_Standard"),
            TableRows.Row("c", "M", "G", standard: "yy_standard"),
        });

        Assert.Equal(new[] { "YY_Standard", "XX_Standard" }, table.Standards);
    }

    [Fact]
    public void Rows_for_a_Standard_keep_file_order_and_ignore_case()
    {
        Assert.Equal(new[] { "2024-O_0.020", "5052-O_0.020", "2024-O_0.032" }, Table.RowsFor("xx_standard").Select(r => r.Name));
    }

    [Fact]
    public void Finds_a_row_by_name_ignoring_case()
    {
        Assert.Equal("2024-O_0.032", Table.Find("2024-o_0.032")?.Name);
        Assert.Null(Table.Find("Retired"));
        Assert.Null(Table.Find(null));
    }

    [Fact]
    public void The_automatic_row_for_a_physical_material_is_its_first_in_file_order()
    {
        Assert.Equal("2024-O_0.020", Table.FirstRowForPhysicalMaterial("Aluminum 2024-O")?.Name);
        Assert.Equal("2024-O", Table.GradeForPhysicalMaterial("aluminum 2024-o"));
        Assert.Null(Table.FirstRowForPhysicalMaterial("Titanium Grade 5"));
        Assert.Null(Table.GradeForPhysicalMaterial("Titanium Grade 5"));
    }

    [Fact]
    public void Load_names_the_file_in_a_parse_error()
    {
        var path = Path.Combine(Path.GetTempPath(), $"smt-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "UNITS,METRIC\nMATERIAL_TABLE\n");
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTable.Load(path));

            Assert.Contains(path, ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_of_a_missing_file_throws_FileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => SheetMetalMaterialTable.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.csv")));
    }
}
