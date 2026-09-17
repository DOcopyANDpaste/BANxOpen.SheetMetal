using Newtonsoft.Json;

namespace BANxOpen.SheetMetal.Common;

/// <summary>Tool-wide sheet metal settings, read from <c>sheetmetal-settings.json</c> in the sheet metal config directory:
/// <code>
/// {
///   "materialTablePath": null
/// }
/// </code>
///
/// Almost everything is decided by NX's sheet metal material standards file instead; this only says where to find it
/// when NX's own setting should not be used — a test table during development, for instance.</summary>
public sealed class SheetMetalSettings
{
    public static SheetMetalSettings Default { get; } = new(null);

    public SheetMetalSettings(string? materialTablePath) => MaterialTablePath = materialTablePath;

    /// <summary>The sheet metal material standards file to use instead of the one NX is configured with. Null to use
    /// NX's. A relative path is resolved against the folder holding the settings file, not the working directory,
    /// which inside NX is unrelated to where the config lives.</summary>
    public string? MaterialTablePath { get; }

    /// <summary>Loads the settings file. A missing file yields <see cref="Default"/>. A file that is present but invalid
    /// is an error, since silently ignoring an edit someone made on purpose is worse than refusing to start.</summary>
    public static SheetMetalSettings Load(string path)
    {
        if (!File.Exists(path))
            return Default;

        SettingsFile? file;
        try
        {
            file = JsonConvert.DeserializeObject<SettingsFile>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Sheet metal settings at '{path}' are not valid JSON: {ex.Message}", ex);
        }

        if (file is null)
            throw new InvalidDataException($"Sheet metal settings at '{path}' are empty.");

        if (string.IsNullOrWhiteSpace(file.MaterialTablePath))
            return Default;

        var settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        return new SheetMetalSettings(Path.GetFullPath(Path.Combine(settingsDirectory, file.MaterialTablePath!)));
    }

    private sealed class SettingsFile
    {
        public string? MaterialTablePath { get; set; }
    }
}
