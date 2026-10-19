namespace BANxOpen.SheetMetal.Common;

/// <summary>Finds the sheet metal root directory and everything this library reads from it:
/// <code>
/// %UGII_USER_DIR% | %UGII_GROUP_DIR% | %UGII_SITE_DIR%
///   SHEETMETAL\                        sheet metal root: NX's material standards file lives here
///     BA_NXOPEN_settings\
///       sheetmetal-settings.json       names the standards file (file name only)
///       bead-settings.json
/// %TEMP%\BANxOpen.SheetMetal\cache\    generated, so kept out of the shared UGII directories
/// </code>
///
/// It has to be resolved rather than passed in by a caller, because more than one entry point reads it: the
/// bead dialog and the Material Assignment dialog both build their sheet metal rules from it. Two entry points
/// hardcoding their own path is how they end up reading two different copies.
///
/// The root is SHEETMETAL under %UGII_USER_DIR%, then %UGII_GROUP_DIR%, then %UGII_SITE_DIR% — NX's own
/// user-over-group-over-site precedence. A SHEETMETAL folder only counts if it holds
/// BA_NXOPEN_settings\sheetmetal-settings.json, so an unrelated SHEETMETAL folder does not hide the real one.</summary>
public static class SheetMetalConfigLocator
{
    public const string RootFolderName = "SHEETMETAL";
    public const string SettingsFolderName = "BA_NXOPEN_settings";

    /// <summary>Searched in this order.</summary>
    public static IReadOnlyList<string> SearchVariables { get; } = new[] { "UGII_USER_DIR", "UGII_GROUP_DIR", "UGII_SITE_DIR" };

    private const string SettingsFileName = "sheetmetal-settings.json";
    private const string ApplicationFolderName = "BANxOpen.SheetMetal";

    private static string? _rootDirectory;

    /// <summary>The sheet metal root directory, located on first use and kept. Set it to use a root of your own
    /// (trusted as given); set it to null to locate again.</summary>
    /// <exception cref="DirectoryNotFoundException">No UGII directory holds a qualifying SHEETMETAL folder. The
    /// message lists everywhere that was tried.</exception>
    public static string RootDirectory
    {
        get => _rootDirectory ??= Locate();
        set => _rootDirectory = value;
    }

    public static string SettingsDirectory => Path.Combine(RootDirectory, SettingsFolderName);

    /// <summary>See <see cref="SheetMetalSettings.Load"/>.</summary>
    public static string SettingsPath => Path.Combine(SettingsDirectory, SettingsFileName);

    /// <summary>Optional: a missing file means default settings. See <c>BeadSettings.Load</c>.</summary>
    public static string BeadSettingsPath => Path.Combine(SettingsDirectory, "bead-settings.json");

    /// <summary>The sheet metal material standards file: the file named by <c>materialTable</c> in
    /// sheetmetal-settings.json, in <see cref="RootDirectory"/>. NX's Customer Defaults are not read.</summary>
    /// <exception cref="FileNotFoundException"><c>materialTable</c> is not set, or names a file that does not exist.</exception>
    public static string MaterialTablePath
    {
        get
        {
            var text = SheetMetalText.Default;
            var fileName = SheetMetalSettings.Load(SettingsPath, text).MaterialTable
                           ?? throw new FileNotFoundException(text.MaterialTableNotSet(SettingsPath));

            var path = Path.Combine(RootDirectory, fileName);
            return File.Exists(path)
                ? path
                : throw new FileNotFoundException(text.MaterialTableFileMissing(SettingsPath, path), path);
        }
    }

    /// <summary>Where parsed SPEC workbooks are cached: %TEMP%\BANxOpen.SheetMetal\cache. Generated output, so it
    /// lives per machine in TEMP rather than in a sheet metal root that a whole group or site may share. Created on demand.</summary>
    public static string CacheDirectory
    {
        get
        {
            var temp = Environment.GetEnvironmentVariable("TEMP");
            var cache = Path.Combine(string.IsNullOrWhiteSpace(temp) ? Path.GetTempPath() : temp!, ApplicationFolderName, "cache");
            Directory.CreateDirectory(cache);
            return cache;
        }
    }

    private static string Locate()
    {
        var attempted = new List<string>();
        foreach (var variable in SearchVariables)
        {
            var ugiiDirectory = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(ugiiDirectory))
            {
                attempted.Add($"{variable} (not set)");
                continue;
            }

            var candidate = Path.Combine(ugiiDirectory, RootFolderName);
            attempted.Add(candidate);
            if (File.Exists(Path.Combine(candidate, SettingsFolderName, SettingsFileName)))
                return candidate;
        }

        throw new DirectoryNotFoundException(SheetMetalText.Default.ConfigDirectoryNotFound(
            Path.Combine(RootFolderName, SettingsFolderName, SettingsFileName), attempted));
    }
}
