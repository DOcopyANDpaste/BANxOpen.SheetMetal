using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>The requirements a body's beads impose, one per SPEC — the same grouping as the SPEC's shared driving
/// expressions (<c>Bead_{Spec}_*</c>), since every bead on a SPEC carries the same row's values.
///
/// Which thickness a SPEC was built at, first match wins:
/// <list type="number">
/// <item>The row applied in this run (<c>rebuilt</c>): Apply just rewrote the SPEC's shared expressions, so every bead
/// on it now carries that row.</item>
/// <item>The thickness the part's existing requirement already records — kept, never re-derived from the current
/// sheet. Otherwise refreshing after a native thickness change would quietly re-bless beads still on the old row.</item>
/// <item>For a SPEC with no requirement yet (beads built before requirements existed), the family row at the sheet's
/// current thickness — the best available baseline, said so in the result's notes.</item>
/// </list>
/// The allowed (grade, thickness) pairs follow the same precedence: the applied SPEC's whole family, else the existing
/// requirement's (re-applying the beads is what brings a revised workbook into the part), else the baseline's
/// family.</summary>
public static class BeadRequirementFactory
{
    public const string FeatureNoun = "Bead SPEC";

    public sealed record Result(IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Notes);

    /// <param name="beads">Every bead on the body, as <see cref="BeadsOnBody.Resolve"/> reports them.</param>
    /// <param name="previous">The bead requirements the part already holds.</param>
    /// <param name="rebuilt">The SPEC rows applied in this run, by (Standard, SPEC id). Empty when nothing was built.</param>
    /// <param name="currentThickness">The sheet's thickness now, for the baseline of a SPEC with no requirement. Null
    /// when unknown, in which case such a SPEC gets no requirement.</param>
    /// <param name="text">Words the notes; <see cref="SheetMetalText.Default"/> when null.</param>
    public static Result Create(
        IReadOnlyList<BeadOnBody> beads,
        IReadOnlyList<SheetMetalRequirement> previous,
        IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> rebuilt,
        double? currentThickness,
        SheetMetalText? text = null)
    {
        text ??= SheetMetalText.Default;
        var requirements = new List<SheetMetalRequirement>();
        var notes = new List<string>();

        var previousByKey = previous
            .Where(p => p.Domain == BeadMaterialConstraintProvider.ProviderDomainId)
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Unidentified beads (no stamp, geometry matching no single SPEC) belong to no SPEC, so to no requirement.
        var identified = beads
            .Where(b => b is { StandardId: not null, SpecId: not null }
                        && b.Status is BeadSpecStatus.Stamped or BeadSpecStatus.StampedSpecMissing or BeadSpecStatus.Matched)
            .GroupBy(b => (StandardId: b.StandardId!, SpecId: b.SpecId!));

        foreach (var group in identified.OrderBy(g => g.Key.StandardId, StringComparer.Ordinal).ThenBy(g => g.Key.SpecId, StringComparer.Ordinal))
        {
            var (standardId, specId) = group.Key;
            var key = SheetMetalRequirement.KeyFor(standardId, specId);
            var users = group
                .OrderBy(b => b.Name, StringComparer.Ordinal)
                .Select(b => new RequirementUser(
                    b.FeatureKey, b.Status == BeadSpecStatus.Matched ? $"{b.Name} (identified by geometry)" : b.Name))
                .ToList();

            SheetMetalAllowance? allowance = null;
            var source = string.Empty;

            if (rebuilt.TryGetValue(group.Key, out var applied))
            {
                allowance = SheetMetalAllowance.FromFamily(FamilyOf(group, applied), applied.Thickness);
                source = applied.WorkbookName;
            }
            else if (previousByKey.TryGetValue(key, out var existing))
            {
                allowance = existing.Allowance;
                source = existing.Source;
            }
            else if (currentThickness is { } thickness
                     && group.Select(b => b.SpecAt(thickness)).FirstOrDefault(row => row is not null) is { } baseline)
            {
                allowance = SheetMetalAllowance.FromFamily(FamilyOf(group, baseline), baseline.Thickness);
                source = $"{baseline.WorkbookName} (baseline from the sheet's thickness when first recorded)";
                notes.Add(text.RequirementRecordedAtCurrentThickness(FeatureNoun, specId, standardId, ThicknessMatchRule.Format(thickness)));
            }
            else
            {
                notes.Add(currentThickness is { } t
                    ? text.RequirementNotRecordedNotDriven(FeatureNoun, specId, standardId, ThicknessMatchRule.Format(t))
                    : text.RequirementNotRecordedThicknessUnknown(FeatureNoun, specId, standardId));
            }

            if (allowance is not null)
            {
                requirements.Add(new SheetMetalRequirement(
                    BeadMaterialConstraintProvider.ProviderDomainId, FeatureNoun, standardId, specId, allowance, users, source));
            }
        }

        return new Result(requirements, notes);
    }

    /// <summary>The SPEC family the group's beads resolved to, or the one row when none of them carries it.</summary>
    private static IReadOnlyList<BeadSpecRow> FamilyOf(IEnumerable<BeadOnBody> group, BeadSpecRow row) =>
        group.Select(b => b.SpecFamily).FirstOrDefault(f => f.Count > 0) ?? new[] { row };
}
