using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Tests.Common;

public class SheetMetalConfigLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BANxOpenLocatorTests_" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _environment = new();

    public SheetMetalConfigLocatorTests()
    {
        Directory.CreateDirectory(_root);
        _environment["UGII_USER_DIR"] = Dir("user");
        _environment["UGII_GROUP_DIR"] = Dir("group");
        _environment["UGII_SITE_DIR"] = Dir("site");
    }

    public void Dispose()
    {
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
    private string SheetMetalRootIn(string ugiiDirectory)
    {
        var settings = Dir(ugiiDirectory, "SHEETMETAL", "BA_NXOPEN_settings");
        File.WriteAllText(Path.Combine(settings, "sheetmetal-settings.json"), "{}");
        return Path.Combine(_root, ugiiDirectory, "SHEETMETAL");
    }

    private string Locate(string? rootOverride = null) =>
        SheetMetalConfigLocator.LocateFrom(rootOverride, v => _environment.TryGetValue(v, out var value) ? value : null);

    [Fact]
    public void User_wins_over_group_and_site()
    {
        var user = SheetMetalRootIn("user");
        SheetMetalRootIn("group");
        SheetMetalRootIn("site");

        Assert.Equal(user, Locate());
    }

    [Fact]
    public void Group_wins_over_site()
    {
        var group = SheetMetalRootIn("group");
        SheetMetalRootIn("site");

        Assert.Equal(group, Locate());
    }

    [Fact]
    public void Falls_back_to_site()
    {
        Assert.Equal(SheetMetalRootIn("site"), Locate());
    }

    [Fact]
    public void Ignores_a_SHEETMETAL_folder_without_the_settings_file()
    {
        Dir("user", "SHEETMETAL"); // somebody else's sheet metal files, not ours
        var site = SheetMetalRootIn("site");

        Assert.Equal(site, Locate());
    }

    [Fact]
    public void Skips_unset_variables()
    {
        _environment["UGII_USER_DIR"] = null;
        var site = SheetMetalRootIn("site");

        Assert.Equal(site, Locate());
    }

    [Fact]
    public void The_override_wins_over_anything_found_by_searching()
    {
        SheetMetalRootIn("user");
        var configured = Dir("shared", "SHEETMETAL");

        Assert.Equal(configured, Locate(configured));
    }

    [Fact]
    public void Reports_everywhere_it_looked_when_nothing_is_found()
    {
        _environment["UGII_GROUP_DIR"] = null;

        var ex = Assert.Throws<DirectoryNotFoundException>(() => Locate());

        Assert.Contains(Path.Combine(_root, "user", "SHEETMETAL"), ex.Message);
        Assert.Contains("UGII_GROUP_DIR", ex.Message);
        Assert.Contains(Path.Combine(_root, "site", "SHEETMETAL"), ex.Message);
    }
}
