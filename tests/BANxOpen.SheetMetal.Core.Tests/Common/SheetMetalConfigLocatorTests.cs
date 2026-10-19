using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Tests.Common;

public class SheetMetalConfigLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BANxOpenLocatorTests_" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _saved = SheetMetalConfigLocator.SearchVariables.ToDictionary(v => v, Environment.GetEnvironmentVariable);

    public SheetMetalConfigLocatorTests()
    {
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("UGII_USER_DIR", Dir("user"));
        Environment.SetEnvironmentVariable("UGII_GROUP_DIR", Dir("group"));
        Environment.SetEnvironmentVariable("UGII_SITE_DIR", Dir("site"));
        SheetMetalConfigLocator.RootDirectory = null!;
    }

    public void Dispose()
    {
        foreach (var saved in _saved)
            Environment.SetEnvironmentVariable(saved.Key, saved.Value);
        SheetMetalConfigLocator.RootDirectory = null!;

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string Dir(params string[] parts)
    {
        var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>A sheet metal root under <paramref name="ugiiDirectory"/> holding the settings file.</summary>
    private string SheetMetalRootIn(string ugiiDirectory, string settingsJson = "{}")
    {
        var settings = Dir(ugiiDirectory, "SHEETMETAL", "BA_NXOPEN_settings");
        File.WriteAllText(Path.Combine(settings, "sheetmetal-settings.json"), settingsJson);
        return Path.Combine(_root, ugiiDirectory, "SHEETMETAL");
    }

    [Fact]
    public void User_wins_over_group_and_site()
    {
        var user = SheetMetalRootIn("user");
        SheetMetalRootIn("group");
        SheetMetalRootIn("site");

        Assert.Equal(user, SheetMetalConfigLocator.RootDirectory);
    }

    [Fact]
    public void Group_wins_over_site()
    {
        var group = SheetMetalRootIn("group");
        SheetMetalRootIn("site");

        Assert.Equal(group, SheetMetalConfigLocator.RootDirectory);
    }

    [Fact]
    public void Ignores_a_SHEETMETAL_folder_without_the_settings_file()
    {
        Dir("user", "SHEETMETAL"); // somebody else's sheet metal files, not ours
        var site = SheetMetalRootIn("site");

        Assert.Equal(site, SheetMetalConfigLocator.RootDirectory);
    }

    [Fact]
    public void Skips_unset_variables()
    {
        Environment.SetEnvironmentVariable("UGII_USER_DIR", null);
        var site = SheetMetalRootIn("site");

        Assert.Equal(site, SheetMetalConfigLocator.RootDirectory);
    }

    [Fact]
    public void A_set_root_wins_over_anything_found_by_searching()
    {
        SheetMetalRootIn("user");
        var configured = Dir("shared", "SHEETMETAL");

        SheetMetalConfigLocator.RootDirectory = configured;

        Assert.Equal(configured, SheetMetalConfigLocator.RootDirectory);
    }

    [Fact]
    public void The_material_table_is_in_the_root_not_the_settings_folder()
    {
        var root = SheetMetalRootIn("site", """{ "materialTable": "sheet_metal_material_table.xml" }""");
        File.WriteAllText(Path.Combine(root, "sheet_metal_material_table.xml"), "");

        Assert.Equal(Path.Combine(root, "sheet_metal_material_table.xml"), SheetMetalConfigLocator.MaterialTablePath);
    }

    [Fact]
    public void Reports_everywhere_it_looked_when_nothing_is_found()
    {
        Environment.SetEnvironmentVariable("UGII_GROUP_DIR", null);

        var ex = Assert.Throws<DirectoryNotFoundException>(() => SheetMetalConfigLocator.RootDirectory);

        Assert.Contains(Path.Combine(_root, "user", "SHEETMETAL"), ex.Message);
        Assert.Contains("UGII_GROUP_DIR", ex.Message);
        Assert.Contains(Path.Combine(_root, "site", "SHEETMETAL"), ex.Message);
    }
}
