namespace BANxOpen.SheetMetal.Common;

/// <summary>Finds the sheet metal root directory and everything this library reads from it:
/// <code>
/// %UGII_USER_DIR% | %UGII_GROUP_DIR% | %UGII_SITE_DIR%
///   SHEETMETAL\                        sheet metal root: NX's material standards file lives here
///     BA_NXOPEN_settings\
///       sheetmetal-settings.json       names the standards file (file name only)
///       bead-settings.json
///       cache\                         generated
/// </code>
///
/// It has to be resolved rather than passed in by a caller, because more than one entry point reads it: the
/// bead dialog and the Material Assignment dialog both build their sheet metal rules from it. Two entry points
/// hardcoding their own path is how they end up reading two different copies.
///
/// Resolution order:
///   1. <see cref="RootDirectoryOverride"/>, when set.
///   2. SHEETMETAL under %UGII_USER_DIR%, then %UGII_GROUP_DIR%, then %UGII_SITE_DIR% — NX's own
///      user-over-group-over-site precedence, so a user can try out a change before it goes to the site.
///
/// A SHEETMETAL folder only counts if it holds BA_NXOPEN_settings\sheetmetal-settings.json. Without that check a
/// user directory with an unrelated SHEETMETAL folder would hide the site's real one.</summary>
public static class SheetMetalConfigLocator
{
    public const string RootFolderName = "SHEETMETAL";
    public const string SettingsFolderName = "BA_NXOPEN_settings";

    /// <summary>Searched in this order.</summary>
    public static IReadOnlyList<string> SearchVariables { get; } = new[] { "UGII_USER_DIR", "UGII_GROUP_DIR", "UGII_SITE_DIR" };

    private const string SettingsFileName = "sheetmetal-settings.json";

    /// <summary>A sheet metal root to use instead of searching the UGII directories. Trusted as given: pointing
    /// somewhere on purpose should get a clear "file not found" for that place, not silently fall through to a
    /// different copy.</summary>
    public static string? RootDirectoryOverride { get; set; }

    /// <summary>The sheet metal root directory. Does not verify the files inside it exist — callers report a
    /// missing file far more usefully than this can.</summary>
    /// <exception cref="DirectoryNotFoundException">No candidate directory qualifies. The message lists
    /// everywhere that was tried, since "config not found" with no path is close to undebuggable.</exception>
    /// <param name="text">Words the error shown at start-up; <see cref="SheetMetalText.Default"/> when null.</param>
    public static string Locate(SheetMetalText? text = null) =>
        LocateFrom(RootDirectoryOverride, Environment.GetEnvironmentVariable, text);

    /// <summary><see cref="Locate"/> with its inputs made explicit, so the search can be tested without
    /// depending on the machine's NX environment.</summary>
    public static string LocateFrom(string? rootOverride, Func<string, string?> environment, SheetMetalText? text = null)
    {
        if (!string.IsNullOrWhiteSpace(rootOverride))
            return rootOverride!;

        var attempted = new List<string>();
        foreach (var variable in SearchVariables)
        {
            var ugiiDirectory = environment(variable);
            if (string.IsNullOrWhiteSpace(ugiiDirectory))
            {
                attempted.Add($"{variable} (not set)");
                continue;
            }

            var candidate = Path.Combine(ugiiDirectory, RootFolderName);
            attempted.Add(candidate);
            if (File.Exists(SettingsPathIn(candidate)))
                return candidate;
        }

        throw new DirectoryNotFoundException(
            (text ?? SheetMetalText.Default).ConfigDirectoryNotFound(Path.Combine(RootFolderName, SettingsFolderName, SettingsFileName), attempted));
    }

    /// <summary>See <see cref="SheetMetalSettings.Load"/>.</summary>
    public static string SettingsPath(SheetMetalText? text = null) => SettingsPathIn(Locate(text));

    /// <summary>The sheet metal material standards file: the file named by <c>materialTable</c> in
    /// sheetmetal-settings.json, in the sheet metal root. This is the only source for now. NX's Customer Defaults are not read.</summary>
    /// <exception cref="FileNotFoundException"><c>materialTable</c> is not set, or names a file that does not exist.</exception>
    public static string MaterialTablePath(SheetMetalText? text = null)
    {
        text ??= SheetMetalText.Default;
        var root = Locate(text);
        var settingsPath = SettingsPathIn(root);
        var fileName = SheetMetalSettings.Load(settingsPath, text).MaterialTable
                       ?? throw new FileNotFoundException(text.MaterialTableNotSet(settingsPath));

        var path = Path.Combine(root, fileName);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException(text.MaterialTableFileMissing(settingsPath, path), path);
    }

    /// <summary>Optional: a missing file means default settings. See <c>BeadSettings.Load</c>.</summary>
    public static string BeadSettingsPath(SheetMetalText? text = null) =>
        Path.Combine(Locate(text), SettingsFolderName, "bead-settings.json");

    /// <summary>Where parsed SPEC workbooks are cached. Created on demand: it is generated output.</summary>
    public static string CacheDirectory(SheetMetalText? text = null)
    {
        var cache = Path.Combine(Locate(text), SettingsFolderName, "cache");
        Directory.CreateDirectory(cache);
        return cache;
    }

    private static string SettingsPathIn(string root) => Path.Combine(root, SettingsFolderName, SettingsFileName);
}
