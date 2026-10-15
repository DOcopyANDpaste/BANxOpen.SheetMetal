using BANxOpen.SheetMetal.Beads.Rules;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>What a SPEC-driven feature allows of the sheet it sits in: the thickness it was built at, and the grades
/// that row allows.
///
/// Pinned to the BUILT thickness, not to every thickness the SPEC family covers: the feature's driving values (a
/// bead's <c>Bead_{Spec}_Depth/_Radius/_DieRadius</c> expressions) hold that one row's values, so any other
/// thickness leaves the feature on the wrong row's geometry until it is re-applied.</summary>
public sealed record SheetMetalAllowance(double BuiltThickness, IReadOnlyList<string> Grades)
{
    /// <summary>Same thickness comparison as SPEC validation (<see cref="ThicknessMatchRule"/>); grade comparison
    /// ignores case, like <c>BeadSpecRow.IsAllowedFor</c>'s column lookup.</summary>
    public bool IsSatisfiedBy(double thickness, string grade) =>
        ThicknessMatchRule.Matches(thickness, BuiltThickness) && AllowsGrade(grade);

    public bool AllowsGrade(string grade) => Grades.Any(g => string.Equals(g, grade, StringComparison.OrdinalIgnoreCase));

    public bool MatchesThickness(double thickness) => ThicknessMatchRule.Matches(thickness, BuiltThickness);

    public string ThicknessText => ThicknessMatchRule.Format(BuiltThickness);

    public string GradesText => Grades.Count == 0 ? "no grade" : string.Join(", ", Grades);
}

/// <summary>A feature that relies on a requirement — highlighted by its check in NX, and named in its messages.</summary>
/// <param name="FeatureKey">The feature's tag, as the feature inventory reports it.</param>
/// <param name="Label">The feature as the user knows it, e.g. "B1005010-2_BEAD(12)".</param>
public sealed record RequirementUser(string FeatureKey, string Label);

/// <summary>One SPEC's requirement on the sheet: every feature built to the SPEC shares it, as they share the SPEC's
/// driving expressions. Written into the part as native NX Requirements (<see cref="SheetMetalRequirementMapper"/>)
/// and read back by every tool that judges the sheet metal.</summary>
/// <param name="Domain">The feature domain that imposed it, e.g. "SHEETMETAL.BEAD".</param>
/// <param name="FeatureNoun">How messages name the source, e.g. "Bead SPEC".</param>
/// <param name="Source">Where the allowance was generated from (the SPEC workbook), for the record. Empty when
/// unknown.</param>
public sealed record SheetMetalRequirement(
    string Domain,
    string FeatureNoun,
    string StandardId,
    string SpecId,
    SheetMetalAllowance Allowance,
    IReadOnlyList<RequirementUser> Users,
    string Source)
{
    /// <summary>The requirement's key within its domain — the same Standard + SPEC identity the feature's driving
    /// expressions are named by.</summary>
    public string Key => KeyFor(StandardId, SpecId);

    public string Label => $"{FeatureNoun} {SpecId} (Standard {StandardId})";

    public static string KeyFor(string standardId, string specId) => $"{standardId}|{specId}";
}
