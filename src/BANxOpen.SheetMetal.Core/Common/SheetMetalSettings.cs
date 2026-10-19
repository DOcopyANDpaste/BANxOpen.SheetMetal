using Newtonsoft.Json;

namespace BANxOpen.SheetMetal.Common;

/// <summary>Tool-wide sheet metal settings, read from <c>sheetmetal-settings.json</c> in the sheet metal root's
/// BA_NXOPEN_settings folder (see <see cref="SheetMetalConfigLocator"/>):
/// <code>
/// {
///   "materialTable": "sheet_metal_material_table.xml"
/// }
/// </code>
///
/// Almost everything is decided by NX's sheet metal material standards file instead; this only says which file it is.</summary>
public sealed class SheetMetalSettings
{
    public static SheetMetalSettings Default { get; } = new(null);

    public SheetMetalSettings(string? materialTable) => MaterialTable = materialTable;

    /// <summary>File name of the sheet metal material standards file, which lives in the sheet metal root. Null when
    /// not set, which <see cref="SheetMetalConfigLocator.MaterialTablePath"/> reports as an error.</summary>
    public string? MaterialTable { get; }

    /// <summary>Loads the settings file. A missing file yields <see cref="Default"/>. A file that is present but invalid
    /// is an error, since silently ignoring an edit someone made on purpose is worse than refusing to start.</summary>
    /// <param name="text">Words the errors shown at start-up; <see cref="SheetMetalText.Default"/> when null.</param>
    public static SheetMetalSettings Load(string path, SheetMetalText? text = null)
    {
        text ??= SheetMetalText.Default;
        if (!File.Exists(path))
            return Default;

        SettingsFile? file;
        try
        {
            file = JsonConvert.DeserializeObject<SettingsFile>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(text.SettingsInvalidJson(path, ex.Message), ex);
        }

        if (file is null)
            throw new InvalidDataException(text.SettingsEmpty(path));

        return string.IsNullOrWhiteSpace(file.MaterialTable)
            ? Default
            : new SheetMetalSettings(file.MaterialTable!.Trim());
    }

    private sealed class SettingsFile
    {
        public string? MaterialTable { get; set; }
    }
}
