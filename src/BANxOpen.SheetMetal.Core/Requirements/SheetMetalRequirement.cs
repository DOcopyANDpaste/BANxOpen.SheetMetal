using System.Globalization;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>One sheet a SPEC-driven feature allows: a grade at a thickness.</summary>
public sealed record MaterialThicknessPair(string Grade, double Thickness)
{
    public const char Separator = '@';

    /// <summary>How the pair is held in the part's requirement, e.g. "2024-O@0.0400". Fixed to four decimals so it
    /// reads the same as the NX formula it is checked against
    /// (<see cref="SheetMetalRequirementNames.PairExpressionFormula"/>).</summary>
    public string Key => $"{Grade}{Separator}{FormatKeyThickness(Thickness)}";

    public static string FormatKeyThickness(double thickness) => thickness.ToString("0.0000", CultureInfo.InvariantCulture);

    public static MaterialThicknessPair? ParseKey(string key)
    {
        var at = key.LastIndexOf(Separator);
        return at > 0 && double.TryParse(key.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var thickness)
            ? new MaterialThicknessPair(key.Substring(0, at), thickness)
            : null;
    }
}

/// <summary>What a SPEC-driven feature allows of the sheet it sits in: every (grade, thickness) pair its SPEC family
/// marks YES, and the thickness it was actually built at.
///
/// The pairs cover the whole family, so a sheet moved to another of the family's thicknesses with an allowed grade
/// still passes. The feature's driving values (a bead's <c>Bead_{Spec}_Depth/_Radius/_DieRadius</c> expressions) hold
/// the built row's values, though, so <see cref="BuiltThickness"/> is kept to say when it needs re-applying.</summary>
public sealed record SheetMetalAllowance(double BuiltThickness, IReadOnlyList<MaterialThicknessPair> Pairs)
{
    /// <summary>The pairs a SPEC family's rows mark YES, in thickness then workbook column order.</summary>
    public static SheetMetalAllowance FromFamily(IEnumerable<BeadSpecRow> family, double builtThickness) =>
        new(builtThickness,
            family.OrderBy(row => row.Thickness)
                .SelectMany(row => row.AllowedMaterialGrades.Where(kv => kv.Value).Select(kv => new MaterialThicknessPair(kv.Key, row.Thickness)))
                .ToList());

    /// <summary>Only <paramref name="grades"/> at the built thickness — what a requirement written before pairs held.</summary>
    public static SheetMetalAllowance AtBuiltThickness(double builtThickness, IEnumerable<string> grades) =>
        new(builtThickness, grades.Select(g => new MaterialThicknessPair(g, builtThickness)).ToList());

    /// <summary>Same thickness comparison as SPEC validation (<see cref="ThicknessMatchRule"/>); grade comparison
    /// ignores case, like <c>BeadSpecRow.IsAllowedFor</c>'s column lookup.</summary>
    public bool IsSatisfiedBy(double thickness, string grade) =>
        Pairs.Any(p => ThicknessMatchRule.Matches(p.Thickness, thickness) && string.Equals(p.Grade, grade, StringComparison.OrdinalIgnoreCase));

    public bool AllowsThickness(double thickness) => Pairs.Any(p => ThicknessMatchRule.Matches(p.Thickness, thickness));

    public IReadOnlyList<string> GradesAt(double thickness) =>
        Pairs.Where(p => ThicknessMatchRule.Matches(p.Thickness, thickness)).Select(p => p.Grade).ToList();

    public bool MatchesBuiltThickness(double thickness) => ThicknessMatchRule.Matches(thickness, BuiltThickness);

    public string ThicknessText => ThicknessMatchRule.Format(BuiltThickness);

    public string GradesTextAt(double thickness) => GradesAt(thickness) is { Count: > 0 } grades ? string.Join(", ", grades) : "no grade";

    /// <summary>"2024-O, 5052-O at 0.04 in; 2024-O at 0.05 in".</summary>
    public string PairsText => Pairs.Count == 0
        ? "no material"
        : string.Join("; ", Pairs
            .GroupBy(p => ThicknessMatchRule.Format(p.Thickness))
            .Select(g => $"{string.Join(", ", g.Select(p => p.Grade))} at {g.Key} in"));

    /// <summary>Same pairs, ignoring order and grade case.</summary>
    public bool SamePairsAs(SheetMetalAllowance other) =>
        new HashSet<string>(Pairs.Select(p => p.Key), StringComparer.OrdinalIgnoreCase).SetEquals(other.Pairs.Select(p => p.Key));
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
    public string Key => KeyFor(ShortStandard, SpecId);

    public string Label => $"{FeatureNoun} {SpecId} (Standard {ShortStandard})";

    private string ShortStandard => StandardId.Replace("_Standards", string.Empty, StringComparison.OrdinalIgnoreCase);

    public static string KeyFor(string standardId, string specId) => $"{standardId}|{specId}";
}
