namespace BANxOpen.SheetMetal.Beads;

/// <summary>One row of a Standard's SPEC workbook — the driving parameters for one Bead SPEC, plus which
/// material grades it's allowed on. Column names below match the Excel headers verbatim so the parser and
/// the domain model read the same vocabulary.</summary>
/// <param name="WorkbookName">The file stem of the workbook this row was read from. A Standard holds one
/// workbook per bead SPEC (see <c>StandardFolderLayout</c>), so this says which of them a row belongs to —
/// needed because two workbooks under one Standard could otherwise contribute rows that look identical.
/// It is provenance, not an identifier the business owns: the UI's bead SPEC name is matched to it by the
/// same "the file name contains the SPEC name" rule the workbook was found by.</param>
public sealed record BeadSpecRow(
    string StandardId,
    string WorkbookName,
    string SpecId,
    double RadiusAndRadS,
    double Width,
    double Height,
    double DieRadiusP,
    double Thickness,
    IReadOnlyDictionary<string, bool> AllowedMaterialGrades)
{
    /// <summary>True only when the grade is a recognized column AND marked YES. An unrecognized grade
    /// label (not a column in this Standard's workbook at all) is treated the same as "not allowed" by the
    /// caller, but the two cases are distinguished by <see cref="Rules.MaterialAllowedRule"/> so the error
    /// message can say which one happened.</summary>
    public bool IsAllowedFor(string materialGradeLabel) =>
        AllowedMaterialGrades.TryGetValue(materialGradeLabel, out var allowed) && allowed;
}
