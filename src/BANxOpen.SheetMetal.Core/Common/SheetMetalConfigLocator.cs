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
    public static string Locate() =>
        LocateFrom(
            Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().Location).LocalPath) ?? "",
            Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary><see cref="Locate"/> with its inputs made explicit, so the search can be tested without
    /// depending on where the test assembly was built.</summary>
    public static string LocateFrom(string startDirectory, string? environmentValue)
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
            $"Could not locate the sheet metal config directory (a folder containing {MarkerFileName}). " +
            $"Tried: {string.Join("; ", attempted)}. Set {EnvironmentVariable} to that folder.");
    }

    /// <summary>See <see cref="SheetMetalSettings.Load"/>.</summary>
    public static string SettingsPath() => Path.Combine(Locate(), MarkerFileName);

    /// <summary>Optional: a missing file means default settings. See <c>BeadSettings.Load</c>.</summary>
    public static string BeadSettingsPath() => Path.Combine(Locate(), "bead-settings.json");

    /// <summary>Where parsed SPEC workbooks are cached. Created on demand: it is generated output, so it
    /// is gitignored and will not exist on a fresh clone.</summary>
    public static string CacheDirectory()
    {
        var cache = Path.Combine(Locate(), "cache");
        Directory.CreateDirectory(cache);
        return cache;
    }
}
