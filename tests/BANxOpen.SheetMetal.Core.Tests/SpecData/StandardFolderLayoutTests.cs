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
    public void The_one_workbook_in_the_bead_folder_is_the_Standards_workbook()
    {
        var workbook = Touch(BeadFolder("XX_Standard"), "Beads.xlsx");

        Assert.Equal(workbook, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard")));
    }

    [Fact]
    public void A_missing_bead_folder_means_no_bead_SPECs()
    {
        Assert.Null(StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard")));
    }

    [Fact]
    public void A_bead_folder_with_no_xlsx_means_no_bead_SPECs()
    {
        var folder = BeadFolder("XX_Standard");
        Touch(folder, "No-Standards.xls");
        Touch(folder, "~$Beads.xlsx");

        Assert.Null(StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard")));
    }

    [Fact]
    public void Excels_lock_file_is_not_a_second_workbook()
    {
        var folder = BeadFolder("XX_Standard");
        var workbook = Touch(folder, "Beads.xlsx");
        Touch(folder, "~$Beads.xlsx");

        Assert.Equal(workbook, StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard")));
    }

    [Fact]
    public void More_than_one_workbook_is_refused_naming_them()
    {
        var folder = BeadFolder("XX_Standard");
        Touch(folder, "Aluminum.xlsx");
        Touch(folder, "Steel.xlsx");

        var ex = Assert.Throws<InvalidDataException>(() => StandardFolderLayout.FindBeadWorkbook(Standard("XX_Standard")));

        Assert.Contains("Aluminum.xlsx", ex.Message);
        Assert.Contains("Steel.xlsx", ex.Message);
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
