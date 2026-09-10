using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Tests.Common;

public class SheetMetalConfigLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BANxOpenLocatorTests_" + Guid.NewGuid().ToString("N"));

    public SheetMetalConfigLocatorTests() => Directory.CreateDirectory(_root);

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

    private string ConfigAt(params string[] parts)
    {
        var dir = Dir(parts);
        File.WriteAllText(Path.Combine(dir, "material-grade-map.json"), "{}");
        return dir;
    }

    [Fact]
    public void Finds_config_deployed_beside_the_assemblies()
    {
        var bin = Dir("deploy");
        var config = ConfigAt("deploy", "config");

        Assert.Equal(config, SheetMetalConfigLocator.LocateFrom(bin, environmentValue: null));
    }

    [Fact]
    public void Finds_the_repo_config_from_this_repos_own_build_output()
    {
        var config = ConfigAt("BANxOpen.SheetMetal", "config");
        var bin = Dir("BANxOpen.SheetMetal", "tests", "Some.Tests", "bin", "Debug", "net8.0");

        Assert.Equal(config, SheetMetalConfigLocator.LocateFrom(bin, environmentValue: null));
    }

    [Fact]
    public void Finds_the_sheet_metal_repo_config_from_a_dialog_built_in_a_sibling_repo()
    {
        // The dialogs live in their own repos; the DLL that runs this search is copied into their bin folders.
        var config = ConfigAt("BANxOpen.SheetMetal", "config");
        var bin = Dir("NXOPEN.Material", "BANxOpen.Ui.MaterialAssignment", "bin", "Debug", "net48");

        Assert.Equal(config, SheetMetalConfigLocator.LocateFrom(bin, environmentValue: null));
    }

    [Fact]
    public void Ignores_an_unrelated_folder_called_config()
    {
        Dir("NXOPEN.Material", "config"); // no grade map: somebody else's config
        var real = ConfigAt("BANxOpen.SheetMetal", "config");
        var bin = Dir("NXOPEN.Material", "BANxOpen.Ui.MaterialAssignment", "bin", "Debug", "net48");

        Assert.Equal(real, SheetMetalConfigLocator.LocateFrom(bin, environmentValue: null));
    }

    [Fact]
    public void The_environment_variable_wins_over_anything_found_by_searching()
    {
        ConfigAt("deploy", "config");
        var configured = Dir("shared", "sheetmetal");

        Assert.Equal(configured, SheetMetalConfigLocator.LocateFrom(Dir("deploy"), configured));
    }

    [Fact]
    public void Reports_everywhere_it_looked_when_nothing_is_found()
    {
        var bin = Dir("nowhere", "bin");

        var ex = Assert.Throws<DirectoryNotFoundException>(() => SheetMetalConfigLocator.LocateFrom(bin, environmentValue: null));

        Assert.Contains(Path.Combine(bin, "config"), ex.Message);
        Assert.Contains(SheetMetalConfigLocator.EnvironmentVariable, ex.Message);
    }
}
