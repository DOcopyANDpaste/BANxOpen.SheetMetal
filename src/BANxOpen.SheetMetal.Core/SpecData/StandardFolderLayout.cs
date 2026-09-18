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

    /// <summary>Every bead SPEC workbook in the Standard's folder, ordered by name. Empty when the folder is missing
    /// or holds none, which means no bead SPEC is allowed under that Standard.
    ///
    /// A Standard holds one workbook per bead SPEC — the SPEC name is part of the file name, so
    /// <c>B1005010</c> is the workbook whose name contains "B1005010". Callers that want one SPEC use
    /// <see cref="FindBeadWorkbook"/>; this is for the searches that have to read all of them, such as identifying a
    /// bead that carries no stamp.</summary>
    public static IReadOnlyList<string> ListBeadSpecWorkbooks(StandardInfo standard)
    {
        if (!Directory.Exists(standard.BeadSpecFolder))
            return Array.Empty<string>();

        return Directory.GetFiles(standard.BeadSpecFolder, WorkbookPattern)
            .Where(f => !Path.GetFileName(f).StartsWith(ExcelLockFilePrefix, StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The workbook holding <paramref name="beadSpecName"/>'s SPECs — the one file in the Standard's folder
    /// whose name contains that SPEC name — or null when the Standard has no such workbook.
    ///
    /// Matching on "contains" rather than an exact file name is deliberate: it lets the business keep a revision or
    /// date suffix in the file name without a code change. The cost is that a loose name can match two files, which is
    /// refused rather than resolved by picking one, since picking silently would quietly build beads to the wrong
    /// SPEC. A file named exactly as the SPEC wins outright, so "B100" is not refused just because "B1005010.xlsx"
    /// sits beside it.</summary>
    /// <exception cref="InvalidDataException">No file is named exactly as the SPEC and more than one contains it.</exception>
    public static string? FindBeadWorkbook(StandardInfo standard, string beadSpecName)
    {
        if (string.IsNullOrWhiteSpace(beadSpecName))
            return null;

        var workbooks = ListBeadSpecWorkbooks(standard);
        var exact = workbooks.FirstOrDefault(f =>
            string.Equals(WorkbookNameOf(f), beadSpecName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var matches = workbooks
            .Where(f => WorkbookNameOf(f).IndexOf(beadSpecName, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        if (matches.Count > 1)
            throw new InvalidDataException(
                $"Bead SPEC '{beadSpecName}' matches {matches.Count} workbooks in '{standard.BeadSpecFolder}' " +
                $"({string.Join(", ", matches.Select(Path.GetFileName))}). Exactly one file name may contain that SPEC name.");

        return matches.FirstOrDefault();
    }

    /// <summary>The name a workbook's rows are tagged with — its file stem. See
    /// <see cref="Beads.BeadSpecRow.WorkbookName"/>.</summary>
    public static string WorkbookNameOf(string workbookPath) => Path.GetFileNameWithoutExtension(workbookPath);
}
