using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>The requirements a body's beads impose, one per SPEC — the same grouping as the SPEC's shared driving
/// expressions (<c>Bead_{Std}_{Spec}_*</c>), since every bead on a SPEC carries the same row's values.
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
/// The allowed grades follow the same precedence: an applied row's grades, else the existing requirement's (re-applying
/// the beads is what brings a revised workbook into the part), else the baseline row's.</summary>
public static class BeadRequirementFactory
{
    public const string FeatureNoun = "Bead SPEC";

    public sealed record Result(IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Notes);

    /// <param name="beads">Every bead on the body, as <see cref="BeadsOnBody.Resolve"/> reports them.</param>
    /// <param name="previous">The bead requirements the part already holds.</param>
    /// <param name="rebuilt">The SPEC rows applied in this run, by (Standard, SPEC id). Empty when nothing was built.</param>
    /// <param name="currentThickness">The sheet's thickness now, for the baseline of a SPEC with no requirement. Null
    /// when unknown, in which case such a SPEC gets no requirement.</param>
    public static Result Create(
        IReadOnlyList<BeadOnBody> beads,
        IReadOnlyList<SheetMetalRequirement> previous,
        IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> rebuilt,
        double? currentThickness)
    {
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
                allowance = AllowanceOf(applied);
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
                allowance = AllowanceOf(baseline);
                source = $"{baseline.WorkbookName} (baseline from the sheet's thickness when first recorded)";
                notes.Add($"{FeatureNoun} {specId} (Standard {standardId}) had no requirement; it was recorded at the " +
                          $"sheet's current {SheetMetalAllowance.FormatThickness(thickness)} in.");
            }
            else
            {
                notes.Add($"{FeatureNoun} {specId} (Standard {standardId}) has no requirement, and none could be " +
                          (currentThickness is { } t
                              ? $"recorded: the SPEC is not driven for the sheet's {SheetMetalAllowance.FormatThickness(t)} in. Re-apply its beads at a thickness it covers."
                              : "recorded: the sheet's thickness is unknown."));
            }

            if (allowance is not null)
            {
                requirements.Add(new SheetMetalRequirement(
                    BeadMaterialConstraintProvider.ProviderDomainId, FeatureNoun, standardId, specId, allowance, users, source));
            }
        }

        return new Result(requirements, notes);
    }

    /// <summary>The grades a SPEC row marks YES, in the workbook's column order.</summary>
    public static SheetMetalAllowance AllowanceOf(BeadSpecRow row) =>
        new(row.Thickness, row.AllowedMaterialGrades.Where(kv => kv.Value).Select(kv => kv.Key).ToList());
}
