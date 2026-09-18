namespace BANxOpen.SheetMetal.Materials;

/// <summary>The Standard a dialog has chosen for the Sheet Metal Preferences, shared between that dialog and
/// <see cref="SheetMetalRowChoiceProvider"/>. The dialog sets <see cref="Selected"/>; the provider only offers rows
/// of that Standard.
///
/// A mutable holder rather than a value on the rule context because the Standard is a sheet metal concept the shared
/// material engine has no reason to know about. One is built per dialog session, in <c>SheetMetalServices</c>.
///
/// Null means no Standard was chosen, and every Standard's rows are offered — which is what a caller that has no
/// Standard picker, such as the bead dialog, gets.</summary>
public sealed class SheetMetalStandardSelection
{
    private readonly SheetMetalMaterialTable _table;

    public SheetMetalStandardSelection(SheetMetalMaterialTable table) => _table = table;

    /// <summary>Every Standard in the sheet metal material standards file, in file order.</summary>
    public IReadOnlyList<string> Standards => _table.Standards;

    public string? Selected { get; set; }

    /// <summary>The rows made of <paramref name="physicalMaterialName"/> under <see cref="Selected"/>, in file order —
    /// or under every Standard when none is selected.</summary>
    public IReadOnlyList<SheetMetalMaterialRow> RowsFor(string physicalMaterialName)
    {
        var rows = _table.RowsForPhysicalMaterial(physicalMaterialName);
        return Selected is not { } standard
            ? rows
            : rows.Where(r => string.Equals(r.Standard, standard, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
