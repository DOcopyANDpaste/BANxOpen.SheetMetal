using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Tests.Common;

public class SheetMetalSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "BANxOpenSettings_" + Guid.NewGuid().ToString("N"));

    public SheetMetalSettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "sheetmetal-settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void A_missing_file_leaves_the_table_unset()
    {
        Assert.Null(SheetMetalSettings.Load(Path.Combine(_dir, "absent.json")).MaterialTable);
    }

    [Fact]
    public void A_null_table_is_unset()
    {
        Assert.Null(SheetMetalSettings.Load(Write("""{ "materialTable": null }""")).MaterialTable);
    }

    [Fact]
    public void The_table_file_name_is_read_as_given()
    {
        var settings = SheetMetalSettings.Load(Write("""{ "materialTable": "sheet_metal_material_table.xml" }"""));

        Assert.Equal("sheet_metal_material_table.xml", settings.MaterialTable);
    }

    [Fact]
    public void Invalid_JSON_is_an_error_not_a_silent_default()
    {
        Assert.Throws<InvalidDataException>(() => SheetMetalSettings.Load(Write("{ not json")));
    }
}
