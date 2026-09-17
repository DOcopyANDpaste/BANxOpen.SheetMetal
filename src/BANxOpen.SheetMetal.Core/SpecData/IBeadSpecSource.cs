using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>The seam between Core and wherever spec data actually lives. The only implementation today is
/// <see cref="FileSystemBeadSpecSource"/> (the sheet metal material standards file + Standard folders + Excel
/// workbooks), but nothing above this interface knows that.</summary>
public interface IBeadSpecSource
{
    IReadOnlyList<StandardInfo> ListStandards();

    /// <summary>The Standard's bead SPEC workbook, or null when the Standard has none.</summary>
    string? FindWorkbook(StandardInfo standard);

    IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard, string workbookPath);
}
