using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>The seam between Core and wherever spec data actually lives. The only implementation today is
/// <see cref="FileSystemBeadSpecSource"/> (Standards registry JSON + Excel workbooks), but nothing above
/// this interface knows that.</summary>
public interface IBeadSpecSource
{
    IReadOnlyList<StandardInfo> ListStandards();

    IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard);
}
