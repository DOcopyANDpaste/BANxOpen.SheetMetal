using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.Common;
using NXOpen;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Finds NX's sheet metal material standards file, so the tool reads the same file NX's Sheet Metal Preferences
/// do.
///
/// Resolution order:
///   1. <c>materialTablePath</c> in sheetmetal-settings.json, when set — for pointing a development session at a test
///      table without touching NX's configuration.
///   2. The Customer Default NX itself uses (<see cref="CustomerDefaultOption"/>).</summary>
public static class SheetMetalMaterialTableLocator
{
    /// <summary>The Customer Default holding NX's material standards file.
    /// TODO(VERIFY): replace with the real option name — open Customer Defaults → Sheet Metal, find the material standards
    /// file setting, and use "Find Default" to show its option name. Until then only the settings override works.</summary>
    public const string CustomerDefaultOption = "TODO_VERIFY_SheetMetal_MaterialStandardsFile";

    /// <exception cref="FileNotFoundException">Neither source names an existing file. The message names both.</exception>
    public static string Locate(NxSessionContext context, SheetMetalSettings settings)
    {
        if (settings.MaterialTablePath is { } overridePath)
        {
            // An explicit setting is trusted as given: pointing somewhere on purpose should get a clear "not found" for
            // that place, not silently fall back to NX's file.
            return File.Exists(overridePath)
                ? overridePath
                : throw new FileNotFoundException(
                    $"The sheet metal material standards file set in {Path.GetFileName(SheetMetalConfigLocator.SettingsPath())} " +
                    $"(materialTablePath) does not exist: {overridePath}", overridePath);
        }

        string? nxPath;
        try
        {
            nxPath = Environment.ExpandEnvironmentVariables(context.Session.OptionsManager.GetStringValue(CustomerDefaultOption) ?? "");
        }
        catch (NXException ex)
        {
            context.Log.Error($"Reading Customer Default '{CustomerDefaultOption}' failed: NX {ex.ErrorCode}: {ex.Message}");
            nxPath = null;
        }

        if (!string.IsNullOrWhiteSpace(nxPath) && File.Exists(nxPath))
            return nxPath!;

        throw new FileNotFoundException(
            "Could not find the sheet metal material standards file. " +
            (string.IsNullOrWhiteSpace(nxPath)
                ? $"NX's Customer Default '{CustomerDefaultOption}' could not be read or is empty"
                : $"NX's Customer Default '{CustomerDefaultOption}' names a file that does not exist ({nxPath})") +
            $". Set materialTablePath in {SheetMetalConfigLocator.SettingsPath()} to the file's path.");
    }
}
