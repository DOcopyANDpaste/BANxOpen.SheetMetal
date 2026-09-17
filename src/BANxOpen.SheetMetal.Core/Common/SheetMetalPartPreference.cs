using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Common;

/// <summary>The material and thickness half of a part's NX Sheet Metal Preferences.
///
/// NX keeps one set of preferences per PART, not per body, so every sheet metal body in the part is governed by
/// the same values. <see cref="SheetMetalBodyCount"/> travels with them for that reason: the tool assumes one
/// sheet metal body per part, and warns when a part breaks that assumption.
///
/// The material NX stores is a row name from its sheet metal material standards file. <see cref="Row"/> is that row,
/// which says which physical material, grade and Standard the preferences stand for.</summary>
/// <param name="MaterialName">The material saved with the preferences, or null when none is set.</param>
/// <param name="IsMaterialTableEntry">Whether Parameter Entry is Material Table. NX only uses the preferences'
/// material in that mode.</param>
/// <param name="Thickness">The preferences' material thickness, in part units. NX fills it from the row.</param>
/// <param name="SheetMetalBodyCount">How many sheet metal bodies the part holds.</param>
/// <param name="Row">The standards file row named <paramref name="MaterialName"/>, or null when there is no material
/// or the file does not list it.</param>
public sealed record SheetMetalPartPreference(
    string? MaterialName,
    bool IsMaterialTableEntry,
    double Thickness,
    int SheetMetalBodyCount,
    SheetMetalMaterialRow? Row = null)
{
    /// <summary>Whether NX is actually using <paramref name="materialName"/>: it is the saved material AND Parameter Entry
    /// is Material Table. A matching name in any other entry mode is ignored by NX, so it does not count.</summary>
    public bool UsesMaterial(string materialName) =>
        IsMaterialTableEntry && string.Equals(MaterialName, materialName, StringComparison.OrdinalIgnoreCase);
}
