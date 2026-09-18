using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.SpecData;
using BANxOpen.SheetMetal.Tests.Materials;

namespace BANxOpen.SheetMetal.Tests.SpecData;

public class StandardFolderLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BANxOpenStandardFolders_" + Guid.NewGuid().ToString("N"));

    public StandardFolderLayoutTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private SheetMetalMaterialTable Table(params string[] standards) =>
        SheetMetalMaterialTable.FromRows(
            standards.Select((s, i) => TableRows.Row($"row{i}", "Aluminum 2024-O", "2024-O", standard: s)),
            Path.Combine(_root, "sheet_metal_material_table.csv"));

    private StandardInfo Standard(string id) => StandardFolderLayout.ListStandards(Table(id)).Single();

    private string BeadFolder(string standard)
    {
        var folder = Path.Combine(_root, standard, "Features", "BEAD");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string Touch(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "placeholder");
        return path;
    }

    [Fact]
    public void Lists_each_Standard_with_its_bead_folder_beside_the_table()
    {
        var standards = StandardFolderLayout.ListStandards(Table("XX_Standard", "YY_Standards"));

        Assert.Equal(new[] { "XX_Standard", "YY_Standards" }, standards.Select(s => s.Id));
        Assert.Equal(Path.Combine(_root, "XX_Standard", "Features", "BEAD"), standards[0].BeadSpecFolder);
    }

    [Fact]
    public void A_bead_SPEC_is_the_workbook_whose_name_contains_it()
    {
        var workbook = Touch(BeadFolder("XX_Standard"), "B1005010.xlsx");

        Assert.Equal(workbook, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B1005010"));
    }

    [Fact]
    public void A_revision_suffix_in_the_file_name_still_matches_the_SPEC()
    {
        var workbook = Touch(BeadFolder("XX_Standard"), "BA_Bead_B1005010_revC.xlsx");

        Assert.Equal(workbook, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B1005010"));
    }

    [Fact]
    public void Each_bead_SPEC_resolves_to_its_own_workbook()
    {
        var folder = BeadFolder("XX_Standard");
        var first = Touch(folder, "B1005010.xlsx");
        var second = Touch(folder, "S5010.xlsx");

        Assert.Equal(first, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B1005010"));
        Assert.Equal(second, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "S5010"));
    }

    [Fact]
    public void A_missing_bead_folder_means_no_bead_SPECs()
    {
        Assert.Null(StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B1005010"));
        Assert.Empty(StandardFolderLayout.ListBeadSpecWorkbooks(Standard("XX_Standard")));
    }

    [Fact]
    public void A_bead_folder_with_no_xlsx_means_no_bead_SPECs()
    {
        var folder = BeadFolder("XX_Standard");
        Touch(folder, "No-Standards.xls");
        Touch(folder, "~$B1005010.xlsx");

        Assert.Null(StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B1005010"));
        Assert.Empty(StandardFolderLayout.ListBeadSpecWorkbooks(Standard("XX_Standard")));
    }

    [Fact]
    public void A_SPEC_no_workbook_is_named_for_has_no_workbook()
    {
        Touch(BeadFolder("XX_Standard"), "B1005010.xlsx");

        Assert.Null(StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "S5010"));
    }

    [Fact]
    public void Excels_lock_file_is_not_a_second_workbook()
    {
        var folder = BeadFolder("XX_Standard");
        var workbook = Touch(folder, "B1005010.xlsx");
        Touch(folder, "~$B1005010.xlsx");

        Assert.Equal(workbook, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B1005010"));
        Assert.Equal(new[] { workbook }, StandardFolderLayout.ListBeadSpecWorkbooks(Standard("XX_Standard")));
    }

    [Fact]
    public void A_SPEC_name_matching_two_workbooks_is_refused_naming_them()
    {
        var folder = BeadFolder("XX_Standard");
        Touch(folder, "B1005010_revB.xlsx");
        Touch(folder, "B1005010_revC.xlsx");

        var ex = Assert.Throws<InvalidDataException>(
            () => StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B1005010"));

        Assert.Contains("B1005010_revB.xlsx", ex.Message);
        Assert.Contains("B1005010_revC.xlsx", ex.Message);
    }

    [Fact]
    public void A_workbook_named_exactly_as_the_SPEC_wins_over_ones_that_contain_it()
    {
        var folder = BeadFolder("XX_Standard");
        var exact = Touch(folder, "B100.xlsx");
        Touch(folder, "B1005010.xlsx");

        Assert.Equal(exact, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard"), "B100"));
    }

    [Fact]
    public void Every_workbook_in_the_folder_is_listed_for_the_all_SPECs_search()
    {
        var folder = BeadFolder("XX_Standard");
        var first = Touch(folder, "B1005010.xlsx");
        var second = Touch(folder, "S5010.xlsx");

        Assert.Equal(new[] { first, second }, StandardFolderLayout.ListBeadSpecWorkbooks(Standard("XX_Standard")));
    }

    [Fact]
    public void A_workbook_is_named_by_its_file_stem()
    {
        Assert.Equal("B1005010", StandardFolderLayout.WorkbookNameOf(@"C:\specs\XX\Features\BEAD\B1005010.xlsx"));
    }

    [Theory]
    [InlineData("XX/Standard")]
    [InlineData("..")]
    public void A_Standard_that_cannot_be_a_folder_name_is_refused(string standard)
    {
        Assert.Throws<InvalidDataException>(() => StandardFolderLayout.ListStandards(Table(standard)));
    }

    [Fact]
    public void A_table_not_read_from_a_file_has_no_folders()
    {
        var table = SheetMetalMaterialTable.FromRows(new[] { TableRows.Row("a", "M", "G") });

        Assert.Throws<InvalidOperationException>(() => StandardFolderLayout.ListStandards(table));
    }
}
