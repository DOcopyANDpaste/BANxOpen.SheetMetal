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
    public void A_missing_file_uses_NXs_standards_file()
    {
        Assert.Null(SheetMetalSettings.Load(Path.Combine(_dir, "absent.json")).MaterialTablePath);
    }

    [Fact]
    public void A_null_path_uses_NXs_standards_file()
    {
        Assert.Null(SheetMetalSettings.Load(Write("""{ "materialTablePath": null }""")).MaterialTablePath);
    }

    [Fact]
    public void A_relative_path_resolves_against_the_settings_folder()
    {
        var settings = SheetMetalSettings.Load(Write("""{ "materialTablePath": "tables\\sheet_metal_material_table.csv" }"""));

        Assert.Equal(Path.Combine(_dir, "tables", "sheet_metal_material_table.csv"), settings.MaterialTablePath);
    }

    [Fact]
    public void An_absolute_path_is_used_as_given()
    {
        var settings = SheetMetalSettings.Load(Write("""{ "materialTablePath": "C:\\Standards\\sheet_metal_material_table.csv" }"""));

        Assert.Equal(@"C:\Standards\sheet_metal_material_table.csv", settings.MaterialTablePath);
    }

    [Fact]
    public void Invalid_JSON_is_an_error_not_a_silent_default()
    {
        Assert.Throws<InvalidDataException>(() => SheetMetalSettings.Load(Write("{ not json")));
    }
}
