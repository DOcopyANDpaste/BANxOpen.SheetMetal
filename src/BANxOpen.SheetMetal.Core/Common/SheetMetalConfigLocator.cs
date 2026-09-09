using System.Reflection;

namespace BANxOpen.SheetMetal.Common;

/// <summary>Finds this library's configuration directory (standards.json, material-grade-map.json, and
/// the generated cache\ beneath it).
///
/// It has to be resolved rather than passed in by a caller, because after the split there is more than
/// one caller: the bead dialog reads the standards registry, and the Material Assignment dialog reads
/// the grade map through the bead constraint provider. Two entry points hardcoding their own relative
/// path is how they end up reading two different copies.
///
/// Resolution order, per the coding-conventions skill's non-negotiable #4 (no hardcoded install paths):
///   1. %BANXOPEN_SHEETMETAL_CONFIG%, so a deployment can put config anywhere.
///   2. A config\ folder beside this assembly, which is where a normal deployment lands.
///   3. Walking up from the assembly for a repo-root config\, which is what a dev build hits when the
///      assembly sits in bin\Debug\net48.</summary>
public static class SheetMetalConfigLocator
{
    public const string EnvironmentVariable = "BANXOPEN_SHEETMETAL_CONFIG";

    private const string ConfigFolderName = "config";

    /// <summary>The configuration directory. Does not verify the files inside it exist — callers report
    /// a missing file far more usefully than this can.</summary>
    /// <exception cref="DirectoryNotFoundException">No candidate directory exists. The message lists
    /// everywhere that was tried, since "config not found" with no path is close to undebuggable.</exception>
    public static string Locate()
    {
        var attempted = new List<string>();

        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            attempted.Add($"{EnvironmentVariable}={fromEnvironment}");
            if (Directory.Exists(fromEnvironment))
                return fromEnvironment;
        }

        var assemblyDirectory = Path.GetDirectoryName(
            new Uri(Assembly.GetExecutingAssembly().Location).LocalPath);

        if (!string.IsNullOrEmpty(assemblyDirectory))
        {
            var directory = new DirectoryInfo(assemblyDirectory);

            // Beside the assembly first, then upwards: a deployed build finds it immediately, a dev build
            // walks bin\Debug\net48 back to the repo root.
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, ConfigFolderName);
                attempted.Add(candidate);
                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the sheet metal config directory. Tried: {string.Join("; ", attempted)}. " +
            $"Set {EnvironmentVariable} to the directory holding standards.json and material-grade-map.json.");
    }

    public static string StandardsRegistryPath() => Path.Combine(Locate(), "standards.json");

    public static string MaterialGradeMapPath() => Path.Combine(Locate(), "material-grade-map.json");

    /// <summary>Where parsed SPEC workbooks are cached. Created on demand: it is generated output, so it
    /// is gitignored and will not exist on a fresh clone.</summary>
    public static string CacheDirectory()
    {
        var cache = Path.Combine(Locate(), "cache");
        Directory.CreateDirectory(cache);
        return cache;
    }
}