namespace BANxOpen.SheetMetal.SpecData;

/// <summary>One Standard a user can pick in the dialog's "Standard" list: a distinct Standard value from the sheet metal
/// material standards file (see <see cref="StandardFolderLayout"/>).</summary>
/// <param name="Id">The Standard value as written in the table. Stamped onto beads built to it.</param>
/// <param name="BeadSpecFolder">The folder holding the Standard's bead SPEC workbook. It need not exist: a Standard with
/// no bead SPECs simply has none.</param>
public sealed record StandardInfo(string Id, string DisplayName, string BeadSpecFolder);
