namespace BANxOpen.SheetMetal.Common;

/// <summary>The material and thickness half of a part's NX Sheet Metal Preferences.
///
/// NX keeps one set of preferences per PART, not per body, so every sheet metal body in the part is governed by
/// the same values. <see cref="SheetMetalBodyCount"/> travels with them for that reason: the tool assumes one
/// sheet metal body per part, and warns when a part breaks that assumption.
///
/// The material NX stores is a name from its sheet metal material standards table. That name is the grade label
/// (see <c>MaterialGradeMap</c>), the same label the SPEC workbooks use, so no second mapping exists.</summary>
/// <param name="MaterialName">The material saved with the preferences, or null when none is set.</param>
/// <param name="IsMaterialTableEntry">Whether Parameter Entry is Material Table. NX only uses the preferences'
/// material in that mode.</param>
/// <param name="Thickness">The preferences' material thickness, in part units.</param>
/// <param name="StandardsTableMaterials">Every material name the NX material standards table defines — the only
/// names the preferences can be set to.</param>
/// <param name="SheetMetalBodyCount">How many sheet metal bodies the part holds.</param>
public sealed record SheetMetalPartPreference(
    string? MaterialName,
    bool IsMaterialTableEntry,
    double Thickness,
    IReadOnlyList<string> StandardsTableMaterials,
    int SheetMetalBodyCount)
{
    /// <summary>Whether the standards table has an entry for <paramref name="grade"/>. Case is ignored, as it is
    /// everywhere a grade label is compared with a preferences value.</summary>
    public bool DefinesMaterial(string grade) =>
        StandardsTableMaterials.Any(name => string.Equals(name, grade, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether NX is actually using <paramref name="grade"/>: it is the saved material AND Parameter Entry
    /// is Material Table. A matching name in any other entry mode is ignored by NX, so it does not count.</summary>
    public bool UsesMaterial(string grade) =>
        IsMaterialTableEntry && string.Equals(MaterialName, grade, StringComparison.OrdinalIgnoreCase);
}
