using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SheetMetalRowFilterTests
{
    private static readonly IReadOnlyList<SheetMetalMaterialRow> Rows = TableRows.AluminumTable().Rows;

    private static IEnumerable<string> Names(IEnumerable<SheetMetalMaterialRow> rows) => rows.Select(r => r.Name);

    [Fact]
    public void No_filter_lets_every_row_through_in_file_order()
    {
        Assert.True(SheetMetalRowFilter.None.IsEmpty);
        Assert.Equal(Names(Rows), Names(SheetMetalRowFilter.None.Apply(Rows)));
    }

    [Fact]
    public void Each_filter_narrows_on_its_own_column_ignoring_case()
    {
        Assert.Equal(new[] { "7075-T6_0.020" },
            Names(new SheetMetalRowFilter("yy_standard", null, null, null).Apply(Rows)));
        Assert.Equal(new[] { "2024-O_0.020", "2024-O_0.032" },
            Names(new SheetMetalRowFilter(null, "aluminum 2024-o", null, null).Apply(Rows)));
        Assert.Equal(new[] { "2024-O_0.032" },
            Names(new SheetMetalRowFilter(null, null, 0.032, null).Apply(Rows)));
        Assert.Empty(new SheetMetalRowFilter(null, null, null, "@BEND_TABLE_1").Apply(Rows));
    }

    [Fact]
    public void Filters_combine()
    {
        var filter = new SheetMetalRowFilter("XX_Standard", "Aluminum 2024-O", 0.02, "0.06");

        Assert.False(filter.IsEmpty);
        Assert.Equal(new[] { "2024-O_0.020" }, Names(filter.Apply(Rows)));
    }

    [Fact]
    public void Thickness_matches_within_tolerance()
    {
        Assert.Equal(new[] { "2024-O_0.032" }, Names(new SheetMetalRowFilter(null, null, 0.0324, null).Apply(Rows)));
        Assert.Empty(new SheetMetalRowFilter(null, null, 0.0330, null).Apply(Rows));
    }

    [Fact]
    public void Options_cascade_from_the_other_filters_but_not_their_own()
    {
        var filter = new SheetMetalRowFilter("XX_Standard", null, 0.032, null);

        // Thickness 0.032 leaves only the 2024-O row's material.
        Assert.Equal(new[] { "Aluminum 2024-O" }, filter.PhysicalMaterialOptions(Rows));

        // The Standard dropdown ignores its own filter, so its current value and its siblings stay listed — but
        // YY_Standard has no 0.032 row, so it drops out.
        Assert.Equal(new[] { "XX_Standard" }, filter.StandardOptions(Rows));

        // Likewise the thickness dropdown still offers 0.020 under XX_Standard.
        Assert.Equal(new[] { 0.02, 0.032 }, filter.ThicknessOptions(Rows));
    }

    [Fact]
    public void Unfiltered_options_list_every_value_once()
    {
        Assert.Equal(new[] { "XX_Standard", "YY_Standard" }, SheetMetalRowFilter.None.StandardOptions(Rows));
        Assert.Equal(new[] { "Aluminum 2024-O", "Aluminum 5052-O", "Aluminum 7075-T6" },
            SheetMetalRowFilter.None.PhysicalMaterialOptions(Rows));
        Assert.Equal(new[] { 0.02, 0.032 }, SheetMetalRowFilter.None.ThicknessOptions(Rows));
        Assert.Equal(new[] { "0.06" }, SheetMetalRowFilter.None.BendRadiusOptions(Rows));
    }

    [Fact]
    public void Thicknesses_within_tolerance_are_one_option()
    {
        var rows = new[]
        {
            TableRows.Row("a", "M", "G", thickness: 0.0320),
            TableRows.Row("b", "M", "G", thickness: 0.0322),
            TableRows.Row("c", "M", "G", thickness: 0.0100),
        };

        Assert.Equal(new[] { 0.01, 0.032 }, SheetMetalRowFilter.None.ThicknessOptions(rows));
    }

    [Fact]
    public void Numeric_bend_radii_sort_before_table_pointers()
    {
        var rows = new[] { "@BEND_TABLE_2", "0.12", "@bend_table_1", "0.06", "" }
            .Select((radius, i) => TableRows.Row($"r{i}", "M", "G") with { BendRadius = radius })
            .ToList();

        Assert.Equal(new[] { "0.06", "0.12", "@bend_table_1", "@BEND_TABLE_2" },
            SheetMetalRowFilter.None.BendRadiusOptions(rows));
    }
}
