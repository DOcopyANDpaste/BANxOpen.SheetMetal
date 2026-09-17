using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Where each Standard's feature SPECs live. There is no mapping file: every Standard has a folder beside the
/// sheet metal material standards file, named exactly as the table's Standard column:
/// <code>
/// &lt;folder holding the standards file&gt;\
///   sheet_metal_material_table.csv
///   XX_Standard\Features\BEAD\&lt;bead SPEC workbook&gt;.xlsx
///   XX_Standard\Features\FLANGE_CUTOUT\
///   YY_Standard\...
/// </code>
/// Adding a Standard is a new table row and a new folder, never a code or config change.</summary>
public static class StandardFolderLayout
{
    public const string FeaturesFolderName = "Features";
    public const string BeadFolderName = "BEAD";

    private const string WorkbookPattern = "*.xlsx";

    /// <summary>Excel's lock file for an open workbook, which is not a workbook.</summary>
    private const string ExcelLockFilePrefix = "~$";

    /// <summary>Every Standard in <paramref name="table"/>, in file order.</summary>
    /// <exception cref="InvalidOperationException">The table was not read from a file, so it has no folder.</exception>
    /// <exception cref="InvalidDataException">A Standard value cannot be a folder name.</exception>
    public static IReadOnlyList<StandardInfo> ListStandards(SheetMetalMaterialTable table)
    {
        var root = table.SourcePath is { } path
            ? Path.GetDirectoryName(Path.GetFullPath(path)) ?? ""
            : throw new InvalidOperationException("The sheet metal material table was not read from a file, so its Standard folders cannot be found.");

        return table.Standards
            .Select(standard => new StandardInfo(standard, standard, BeadSpecFolder(root, standard)))
            .ToList();
    }

    /// <exception cref="InvalidDataException"><paramref name="standard"/> is not a plain folder name.</exception>
    public static string BeadSpecFolder(string rootDirectory, string standard)
    {
        if (standard.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || standard.Trim('.').Length == 0)
            throw new InvalidDataException($"Standard '{standard}' cannot be used as a folder name.");

        return Path.Combine(rootDirectory, standard, FeaturesFolderName, BeadFolderName);
    }

    /// <summary>The Standard's bead SPEC workbook, or null when it has none — the folder is missing or holds no
    /// workbook — which means no bead SPEC is allowed under that Standard.</summary>
    /// <exception cref="InvalidDataException">The folder holds more than one workbook.</exception>
    public static string? FindBeadWorkbook(StandardInfo standard)
    {
        if (!Directory.Exists(standard.BeadSpecFolder))
            return null;

        var workbooks = Directory.GetFiles(standard.BeadSpecFolder, WorkbookPattern)
            .Where(f => !Path.GetFileName(f).StartsWith(ExcelLockFilePrefix, StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // TODO(business): a Standard may later hold one workbook per material. Reading them all and appending their rows
        // belongs here; until that is agreed, more than one is refused rather than one being picked silently.
        if (workbooks.Count > 1)
            throw new InvalidDataException(
                $"Standard '{standard.Id}' has {workbooks.Count} bead SPEC workbooks in '{standard.BeadSpecFolder}' " +
                $"({string.Join(", ", workbooks.Select(Path.GetFileName))}). Only one is supported.");

        return workbooks.FirstOrDefault();
    }
}
