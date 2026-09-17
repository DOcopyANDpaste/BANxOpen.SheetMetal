using System.Text;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SheetMetalMaterialTableParserTests
{
    /// <summary>The business's sample table: tab-separated, as pasted out of a spreadsheet.</summary>
    private static readonly string[] SampleHeader =
    {
        "Material_Name", "SheetMetal_Standard", "Standard", "PHYSICAL_MATERIAL_NAME", "SheetMetal_Material", "  Cat", "Type",
        "THICKNESS", "BEND_RADIUS", "NEUTRAL_FACTOR", "RELIEF_DEPTH", "RELIEF_WIDTH", "JOGGLE_RADIUS", "JOGGLE_LENGTH",
        "Lightning_Hole_Standards_HolesValues", "LightningHole_Stds_File", "Lightning_Oblong_Standards_HolesValues",
        "LightningOblong_Std_File", "BEAD_Values", "BEAD_Std_File", "DRG_NAME", "THICKNESS_SELECTED_STANDARD", "MIN_BEND_RADIUS",
    };

    private static readonly string[] SampleProperties =
    {
        "unique", "attribute", "show", "show", "attribute", "hide", "hide", "show", "show", "show", "hide", "hide", "hide",
        "hide", "attribute", "attribute", "attribute", "attribute", "attribute", "attribute", "hide", "attribute", "attribute",
    };

    private static string SampleRow(string thickness, string standard) => string.Join("\t",
        $"17-7PH ANNEALED BENT COLD 90°_{thickness}", standard, standard, "17-7PH ANNEALED", "17-7PH ANNEALED", "XX", "XX",
        thickness, "0.03", "@NF_DIN_FORMULA_1", "1", "1", "1", "1", "0",
        @"\BA_Standard\Settings\SheetMetalStandards\No_Standards.xls", "0",
        @"\BA_Standard\Settings\SheetMetalStandards\No_Standards.xls", "0",
        @"\BA_Standard\Settings\SheetMetalStandards\No_Standards.xls", "0", thickness, "0.03");

    private static string Sample(string units = "UNITS\tENGLISH") => string.Join("\r\n",
        units,
        "",
        "MATERIAL_TABLE" + new string('\t', 22),
        "VERSION\t2" + new string('\t', 21),
        string.Join("\t", SampleHeader),
        string.Join("\t", SampleProperties),
        SampleRow("0.01", "XX_Standards"),
        SampleRow("0.012", "YY_Standards"),
        SampleRow("0.016", "XX_Standards"),
        "END_OF_MATERIAL_TABLE");

    /// <summary>NX's own format: commas, padded with tabs, among comments and blocks the tool does not read.</summary>
    private const string NxStyle = """
        # a comment, with, commas
        VERSION,2
        UNITS,ENGLISH
        ANGLE_REFERENCE,OUTSIDE
        PARAMETERS
        FEATURE,ANGLE
        END_OF_PARAMETERS
        MATERIAL_TABLE
        VERSION,2
        Material_Name,   	Standard,  	PHYSICAL_MATERIAL_NAME,  	SheetMetal_Material, THICKNESS, BEND_RADIUS,
        unique,          	show,      	show,                    	attribute,           show,      show,
        #2024-O_0.010,   	XX_Standard,	Aluminum 2024-O,         	2024-O,              0.010,     0.03
        2024-O_0.016,    	XX_Standard,	Aluminum 2024-O,         	2024-O,              0.016,     @Die1_Punch4_TABLE
        7075-T6_0.020,   	YY_Standard,	Aluminum 7075-T6,        	7075-T6,             0.020,     0.09,

        END_OF_MATERIAL_TABLE
        TABLE
        NAME,Die1_Punch4_TABLE
        EMPTY,	1.2,	1.2
        END_OF_TABLE
        """;

    [Fact]
    public void Reads_the_business_sample_table()
    {
        var rows = SheetMetalMaterialTableParser.Parse(Sample());

        Assert.Equal(3, rows.Count);
        var first = rows[0];
        Assert.Equal("17-7PH ANNEALED BENT COLD 90°_0.01", first.Name);
        Assert.Equal("XX_Standards", first.Standard);
        Assert.Equal("XX_Standards", first.SheetMetalStandard);
        Assert.Equal("17-7PH ANNEALED", first.PhysicalMaterialName);
        Assert.Equal("17-7PH ANNEALED", first.Grade);
        Assert.Equal(0.01, first.Thickness);
        Assert.Equal("0.03", first.BendRadius);
        Assert.Equal(7, first.LineNumber);
        Assert.Equal("@NF_DIN_FORMULA_1", first.ValueOf("neutral_factor"));
        Assert.Equal("XX", first.ValueOf("Cat"));
        Assert.Null(first.ValueOf("NOT_A_COLUMN"));
    }

    [Fact]
    public void Reads_NX_style_commas_skipping_comments_and_other_blocks()
    {
        var rows = SheetMetalMaterialTableParser.Parse(NxStyle);

        Assert.Equal(new[] { "2024-O_0.016", "7075-T6_0.020" }, rows.Select(r => r.Name));
        Assert.Equal("@Die1_Punch4_TABLE", rows[0].BendRadius);
        Assert.Equal("Aluminum 7075-T6", rows[1].PhysicalMaterialName);
    }

    [Fact]
    public void A_tab_separated_cell_may_contain_a_comma()
    {
        var content = string.Join("\n",
            "UNITS,ENGLISH", "MATERIAL_TABLE",
            "Material_Name\tStandard\tPHYSICAL_MATERIAL_NAME\tSheetMetal_Material\tTHICKNESS",
            "unique\tshow\tshow\tattribute\tshow",
            "2024-O_0.020\tXX\tAluminum 2024, Temper O\t2024-O\t0.02",
            "END_OF_MATERIAL_TABLE");

        Assert.Equal("Aluminum 2024, Temper O", Assert.Single(SheetMetalMaterialTableParser.Parse(content)).PhysicalMaterialName);
    }

    [Fact]
    public void Decodes_a_Latin1_file_so_a_degree_sign_survives()
    {
        var rows = SheetMetalMaterialTableParser.Parse(Encoding.GetEncoding(28591).GetBytes(Sample()));

        Assert.Equal("17-7PH ANNEALED BENT COLD 90°_0.01", rows[0].Name);
    }

    [Fact]
    public void Decodes_UTF8_with_a_byte_order_mark()
    {
        var rows = SheetMetalMaterialTableParser.Parse(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Sample())).ToArray());

        Assert.Equal("17-7PH ANNEALED BENT COLD 90°_0.01", rows[0].Name);
    }

    [Fact]
    public void Refuses_a_METRIC_file()
    {
        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(Sample("UNITS,METRIC")));

        Assert.Contains("METRIC", ex.Message);
        Assert.Contains("ENGLISH", ex.Message);
    }

    [Fact]
    public void Refuses_a_file_with_no_UNITS_line()
    {
        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(Sample("# no units here")));

        Assert.Contains("UNITS", ex.Message);
    }

    [Fact]
    public void Refuses_a_file_with_no_material_table()
    {
        Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse("UNITS,ENGLISH\nTABLE\nEND_OF_TABLE"));
    }

    [Fact]
    public void Refuses_a_table_with_no_end_line()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            SheetMetalMaterialTableParser.Parse(Sample().Replace("END_OF_MATERIAL_TABLE", "")));

        Assert.Contains("END_OF_MATERIAL_TABLE", ex.Message);
    }

    [Fact]
    public void Refuses_a_repeated_unique_value_naming_both_lines()
    {
        var content = Sample().Replace(SampleRow("0.016", "XX_Standards"), SampleRow("0.01", "XX_Standards"));

        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(content));

        Assert.Contains("line 9", ex.Message);
        Assert.Contains("line 7", ex.Message);
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("PHYSICAL_MATERIAL_NAME")]
    [InlineData("SheetMetal_Material")]
    [InlineData("THICKNESS")]
    public void Refuses_a_table_missing_a_required_column(string column)
    {
        var content = Sample().Replace("\t" + column + "\t", "\tRenamed_" + column + "\t");

        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(content));

        Assert.Contains(column, ex.Message);
        Assert.Contains("line 5", ex.Message);
    }

    [Fact]
    public void Refuses_a_row_with_an_empty_required_value()
    {
        var content = Sample().Replace("\t17-7PH ANNEALED\t17-7PH ANNEALED\tXX\tXX\t0.012", "\t17-7PH ANNEALED\t\tXX\tXX\t0.012");

        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(content));

        Assert.Contains("line 8", ex.Message);
        Assert.Contains("SheetMetal_Material", ex.Message);
    }

    [Theory]
    [InlineData("thin")]
    [InlineData("@THICKNESS_TABLE")]
    [InlineData("0")]
    public void Refuses_a_thickness_that_is_not_a_positive_number(string thickness)
    {
        var content = NxStyle.Replace("0.016,     @Die1", thickness + ",     @Die1");

        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(content));

        Assert.Contains("THICKNESS", ex.Message);
        Assert.Contains(thickness, ex.Message);
    }

    [Fact]
    public void Refuses_an_unknown_column_property()
    {
        var content = NxStyle.Replace("attribute,           show", "visible,             show");

        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(content));

        Assert.Contains("visible", ex.Message);
    }

    [Fact]
    public void Refuses_a_table_without_exactly_one_unique_column()
    {
        var content = NxStyle.Replace("show,      	show,                    	attribute", "unique,    	show,                    	attribute");

        Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(content));
    }

    [Fact]
    public void Refuses_a_row_with_more_values_than_columns()
    {
        var content = NxStyle.Replace("0.09,", "0.09, extra");

        var ex = Assert.Throws<InvalidDataException>(() => SheetMetalMaterialTableParser.Parse(content));

        Assert.Contains("values", ex.Message);
    }
}
