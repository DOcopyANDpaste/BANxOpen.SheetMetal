using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>The real <see cref="IBeadSpecSource"/>: Standards come from the sheet metal material standards file, each
/// Standard's workbook from its folder (<see cref="StandardFolderLayout"/>), and the rows from the workbook via
/// <see cref="ExcelBeadSpecParser"/>. Kept as a thin composition so each piece stays independently testable.</summary>
public sealed class FileSystemBeadSpecSource : IBeadSpecSource
{
    private readonly SheetMetalMaterialTable _table;
    private readonly ExcelBeadSpecParser _parser;

    public FileSystemBeadSpecSource(SheetMetalMaterialTable table, ExcelBeadSpecParser parser)
    {
        _table = table;
        _parser = parser;
    }

    public IReadOnlyList<StandardInfo> ListStandards() => StandardFolderLayout.ListStandards(_table);

    public string? FindWorkbook(StandardInfo standard, string beadSpecName) =>
        StandardFolderLayout.FindBeadWorkbook(standard, beadSpecName);

    public IReadOnlyList<string> ListWorkbooks(StandardInfo standard) =>
        StandardFolderLayout.ListBeadSpecWorkbooks(standard);

    public IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard, string workbookPath)
    {
        using var stream = File.OpenRead(workbookPath);
        return _parser.Parse(standard.Id, StandardFolderLayout.WorkbookNameOf(workbookPath), stream);
    }
}
