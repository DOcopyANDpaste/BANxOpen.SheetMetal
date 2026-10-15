namespace BANxOpen.SheetMetal.Tests.Materials;

public class SheetMetalMaterialRowTests
{
    [Fact]
    public void Describe_lists_standard_grade_thickness_and_bend_radius()
    {
        var row = TableRows.Row("5052-O_0.063", "Aluminum 5052-O", "5052-O", thickness: 0.063);

        Assert.Equal("XX_Standard · 5052-O · THK 0.063 · BR 0.06", row.Describe());
    }

    [Fact]
    public void Describe_leaves_out_blank_columns()
    {
        var row = TableRows.Row("r", "Aluminum 5052-O", "", thickness: 0.02, standard: "") with { BendRadius = " " };

        Assert.Equal("THK 0.02", row.Describe());
    }
}
