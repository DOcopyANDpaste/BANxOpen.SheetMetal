namespace BANxOpen.SheetMetal.Materials;

/// <summary>The MATERIAL_TABLE columns of NX's sheet metal material standards file that the tool reads by name. Every
/// other column is kept verbatim in <see cref="SheetMetalMaterialRow.Columns"/>, so a column the business adds later
/// (a sign-off status, a lightning hole standard) is available without a parser change.
///
/// Names are compared ignoring case: NX's own keywords are upper case, the company columns are not.</summary>
public static class SheetMetalMaterialColumns
{
    /// <summary>The Standard a row belongs to. It fills the bead dialog's Standard list, and names the Standard's
    /// folder beside the table (see <c>StandardFolderLayout</c>).
    ///
    /// TODO(business): confirm that Standard, not SheetMetal_Standard, is what the user picks, and that the pick is
    /// made once per part.</summary>
    public const string Standard = "Standard";

    /// <summary>Written to the part as an attribute by NX. Not used by the tool.</summary>
    public const string SheetMetalStandard = "SheetMetal_Standard";

    /// <summary>The NX keyword linking a row to the NX physical material library.</summary>
    public const string PhysicalMaterialName = "PHYSICAL_MATERIAL_NAME";

    /// <summary>The grade: matched against the "Allowed Material" column headers of the bead SPEC workbooks.</summary>
    public const string SheetMetalMaterial = "SheetMetal_Material";

    public const string Thickness = "THICKNESS";

    public const string BendRadius = "BEND_RADIUS";

    /// <summary>Columns every row must fill, since a row without them can be neither picked nor checked.</summary>
    public static IReadOnlyList<string> Required { get; } = new[] { Standard, PhysicalMaterialName, SheetMetalMaterial, Thickness };
}
