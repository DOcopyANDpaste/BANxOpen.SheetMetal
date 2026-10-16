using System.Reflection;

namespace BANxOpen.SheetMetal.Common;

/// <summary>Finds this library's configuration directory (sheetmetal-settings.json, bead-settings.json, and the
/// generated cache\ beneath it). Materials, grades and Standards are not configured here: they come from NX's sheet
/// metal material standards file (see <c>SheetMetalMaterialTable</c>).
///
/// It has to be resolved rather than passed in by a caller, because more than one entry point reads it: the
/// bead dialog and the Material Assignment dialog both build their sheet metal rules from it. Two entry points
/// hardcoding their own relative path is how they end up reading two different copies.
///
/// Resolution order, per the coding-conventions skill's non-negotiable #4 (no hardcoded install paths):
///   1. %BANXOPEN_SHEETMETAL_CONFIG%, so a deployment can put config anywhere.
///   2. From this assembly's folder upwards, at each level: a config\ folder, then a sibling
///      BANxOpen.SheetMetal\config folder. The first finds a deployment that ships config beside the DLLs.
///      The second finds this repo's config from any dialog built in a sibling repo, which is the layout
///      the cross-repo project references already require — so a dev build needs no setup.
///
/// A folder only counts if it holds sheetmetal-settings.json, which marks it as this library's config. Without that
/// check the upward search would take the first unrelated folder that happens to be called "config".</summary>
public static class SheetMetalConfigLocator
{
    public const string EnvironmentVariable = "BANXOPEN_SHEETMETAL_CONFIG";

    private const string ConfigFolderName = "config";
    private const string RepoFolderName = "BANxOpen.SheetMetal";
    private const string MarkerFileName = "sheetmetal-settings.json";

    /// <summary>The configuration directory. Does not verify the other files inside it exist — callers
    /// report a missing file far more usefully than this can.</summary>
    /// <exception cref="DirectoryNotFoundException">No candidate directory exists. The message lists
    /// everywhere that was tried, since "config not found" with no path is close to undebuggable.</exception>
    /// <param name="text">Words the error shown at start-up; <see cref="SheetMetalText.Default"/> when null.</param>
    public static string Locate(SheetMetalText? text = null) =>
        LocateFrom(
            Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().Location).LocalPath) ?? "",
            Environment.GetEnvironmentVariable(EnvironmentVariable),
            text);

    /// <summary><see cref="Locate"/> with its inputs made explicit, so the search can be tested without
    /// depending on where the test assembly was built.</summary>
    public static string LocateFrom(string startDirectory, string? environmentValue, SheetMetalText? text = null)
    {
        var attempted = new List<string>();

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            // An explicit setting is trusted as given: a deployment pointing somewhere on purpose should get a
            // clear "file not found" for that place, not silently fall through to a different copy.
            attempted.Add($"{EnvironmentVariable}={environmentValue}");
            if (Directory.Exists(environmentValue))
                return environmentValue!;
        }

        for (var directory = string.IsNullOrEmpty(startDirectory) ? null : new DirectoryInfo(startDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory.FullName, ConfigFolderName),
                         Path.Combine(directory.FullName, RepoFolderName, ConfigFolderName),
                     })
            {
                attempted.Add(candidate);
                if (File.Exists(Path.Combine(candidate, MarkerFileName)))
                    return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            (text ?? SheetMetalText.Default).ConfigDirectoryNotFound(MarkerFileName, attempted, EnvironmentVariable));
    }

    /// <summary>See <see cref="SheetMetalSettings.Load"/>.</summary>
    public static string SettingsPath(SheetMetalText? text = null) => Path.Combine(Locate(text), MarkerFileName);

    /// <summary>The sheet metal material standards file, from <c>materialTablePath</c> in sheetmetal-settings.json.
    /// This is the only source for now. NX's Customer Defaults are not read.</summary>
    /// <exception cref="FileNotFoundException"><c>materialTablePath</c> is not set, or names a file that does not exist.</exception>
    public static string MaterialTablePath(SheetMetalText? text = null)
    {
        text ??= SheetMetalText.Default;
        var settingsPath = SettingsPath(text);
        var path = SheetMetalSettings.Load(settingsPath, text).MaterialTablePath
                   ?? throw new FileNotFoundException(text.MaterialTablePathNotSet(settingsPath));

        return File.Exists(path)
            ? path
            : throw new FileNotFoundException(text.MaterialTableFileMissing(settingsPath, path), path);
    }

    /// <summary>Optional: a missing file means default settings. See <c>BeadSettings.Load</c>.</summary>
    public static string BeadSettingsPath(SheetMetalText? text = null) => Path.Combine(Locate(text), "bead-settings.json");

    /// <summary>Where parsed SPEC workbooks are cached. Created on demand: it is generated output, so it
    /// is gitignored and will not exist on a fresh clone.</summary>
    public static string CacheDirectory(SheetMetalText? text = null)
    {
        var cache = Path.Combine(Locate(text), "cache");
        Directory.CreateDirectory(cache);
        return cache;
    }
}
