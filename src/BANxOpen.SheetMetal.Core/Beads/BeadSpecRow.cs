namespace BANxOpen.SheetMetal.Beads;

/// <summary>One row of a Standard's SPEC workbook — the driving parameters for one Bead SPEC, plus which
/// material grades it's allowed on. Column names below match the Excel headers verbatim so the parser and
/// the domain model read the same vocabulary.</summary>
public sealed record BeadSpecRow(
    string StandardId,
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
