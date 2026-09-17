namespace BANxOpen.SheetMetal.Materials;

/// <summary>One row of the MATERIAL_TABLE in NX's sheet metal material standards file: one material the Sheet Metal
/// Preferences can be set to.
///
/// A physical material usually has several rows — the same grade at different thicknesses or bend radii — so the row,
/// not the physical material, is what gets picked.</summary>
/// <param name="Name">The table's unique column (Material_Name in NX's samples). The value passed to
/// <c>SheetMetalPreferencesBuilder.SetMaterial</c>, and the value NX reports back for the preferences.</param>
/// <param name="Standard">See <see cref="SheetMetalMaterialColumns.Standard"/>.</param>
/// <param name="SheetMetalStandard">See <see cref="SheetMetalMaterialColumns.SheetMetalStandard"/>. Empty when the
/// table has no such column.</param>
/// <param name="PhysicalMaterialName">The NX physical material library name this row is made of.</param>
/// <param name="Grade">The SheetMetal_Material value, matched against the SPEC workbooks' grade columns.</param>
/// <param name="Thickness">In the table's units, which are always inches (the parser refuses METRIC).</param>
/// <param name="BendRadius">Kept as written: it may be a number or a pointer to a table or formula (<c>@NAME</c>).
/// Empty when the table has no such column.</param>
/// <param name="LineNumber">Where the row is in the file, for messages.</param>
/// <param name="Columns">Every cell of the row by column name, ignoring case, as written.</param>
public sealed record SheetMetalMaterialRow(
    string Name,
    string Standard,
    string SheetMetalStandard,
    string PhysicalMaterialName,
    string Grade,
    double Thickness,
    string BendRadius,
    int LineNumber,
    IReadOnlyDictionary<string, string> Columns)
{
    /// <summary>The cell under <paramref name="column"/>, or null when the table has no such column.</summary>
    public string? ValueOf(string column) => Columns.TryGetValue(column, out var value) ? value : null;
}
