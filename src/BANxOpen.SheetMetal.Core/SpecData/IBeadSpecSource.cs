using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>The seam between Core and wherever spec data actually lives. The only implementation today is
/// <see cref="FileSystemBeadSpecSource"/> (the sheet metal material standards file + Standard folders + Excel
/// workbooks), but nothing above this interface knows that.</summary>
public interface IBeadSpecSource
{
    IReadOnlyList<StandardInfo> ListStandards();

    /// <summary>The workbook holding <paramref name="beadSpecName"/>'s SPECs, or null when the Standard has none for
    /// that SPEC.</summary>
    string? FindWorkbook(StandardInfo standard, string beadSpecName);

    /// <summary>Every bead SPEC workbook the Standard holds — for the searches that must read all of them rather than
    /// one named SPEC's.</summary>
    IReadOnlyList<string> ListWorkbooks(StandardInfo standard);

    IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard, string workbookPath);
}
