using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>How a bead's SPEC is known — or why it is not.</summary>
public enum BeadSpecStatus
{
    /// <summary>Stamped by the tool, and the stamped SPEC is in its workbook.</summary>
    Stamped,

    /// <summary>Stamped, but the SPEC is no longer in its Standard's workbooks.</summary>
    StampedSpecMissing,

    /// <summary>Not stamped; its geometry matches exactly one SPEC.</summary>
    Matched,

    /// <summary>Not stamped; its geometry could not be read.</summary>
    GeometryUnreadable,

    /// <summary>Not stamped; its geometry matches several SPECs.</summary>
    Ambiguous,

    /// <summary>Not stamped; its geometry matches no SPEC in any Standard.</summary>
    Unmatched,
}

/// <param name="StandardId">From the stamp, or from the matched SPEC. Null when the SPEC is not known.</param>
/// <param name="SpecId">As <paramref name="StandardId"/>.</param>
/// <param name="SpecFamily">Every row the bead's SPEC id names, ordered by thickness — one per sheet thickness the
/// SPEC is driven for. Empty unless <see cref="Status"/> is Stamped or Matched.
///
/// The family rather than a single row, because which row applies depends on the thickness of the sheet the bead
/// sits in, and that changes: NX re-thicknesses the sheet from the Sheet Metal Preferences' Material Table row and
/// rebuilds every bead into it. Resolve the row with <see cref="SpecAt"/> at the thickness being judged.</param>
/// <param name="Candidates">Every SPEC an ambiguous bead's geometry matches; empty otherwise.</param>
public sealed record BeadOnBody(
    string FeatureKey, string Name, BeadSpecStatus Status,
    string? StandardId, string? SpecId, IReadOnlyList<BeadSpecRow> SpecFamily, IReadOnlyList<BeadSpecRow> Candidates)
{
    public bool IsStamped => Status is BeadSpecStatus.Stamped or BeadSpecStatus.StampedSpecMissing;

    /// <summary>Whether this bead's SPEC is known well enough to restrict anything.</summary>
    public bool IsSpecKnown => SpecFamily.Count > 0;

    /// <summary>The family's row driven for a sheet of <paramref name="thickness"/>, or null when the SPEC is not
    /// driven for that thickness at all — in which case this bead cannot be rebuilt into such a sheet. Uses
    /// <see cref="ThicknessMatchRule"/>'s tolerance, so it agrees with what SPEC validation accepts.</summary>
    public BeadSpecRow? SpecAt(double thickness) =>
        SpecFamily.FirstOrDefault(row => ThicknessMatchRule.Matches(row.Thickness, thickness));

    /// <summary>The thicknesses the SPEC is driven for, as a range ("0.02 - 0.063") or a single value, for the
    /// message that says a thickness is not covered. Empty when the SPEC is not known.</summary>
    public string ThicknessCoverage => SpecFamily.Count switch
    {
        0 => string.Empty,
        1 => $"{SpecFamily[0].Thickness:0.####}",
        // FindAll orders by thickness, so the ends of the list are the ends of the range.
        _ => $"{SpecFamily[0].Thickness:0.####} - {SpecFamily[SpecFamily.Count - 1].Thickness:0.####}",
    };

    /// <summary>Why an unstamped bead's SPEC is not known, as a clause ("its geometry …"); null when it is known or
    /// the bead is stamped.</summary>
    public string? UnidentifiedReason => Status switch
    {
        BeadSpecStatus.GeometryUnreadable => "its geometry could not be read",
        // Named by workbook and SPEC: two workbooks of a Standard could carry the same SpecId.
        BeadSpecStatus.Ambiguous => $"its geometry matches several SPECs ({string.Join(", ", Candidates.Select(c => $"{c.WorkbookName}/{c.SpecId}"))})",
        BeadSpecStatus.Unmatched => "its geometry matches no SPEC in any Standard",
        _ => null,
    };
}

/// <summary>Which SPEC each bead on a body is built to: read off its stamp, or — for a bead with no stamp — identified
/// from its geometry (<see cref="BeadSpecMatcher"/>). The one place this is decided, so the material engine's bead
/// constraint and the bead dialog cannot disagree about a bead.</summary>
public static class BeadsOnBody
{
    public static IReadOnlyList<BeadOnBody> Resolve(BodyFeatureInventory inventory, IBeadSpecLookup specs, BeadSettings settings)
    {
        var beads = new List<BeadOnBody>();

        foreach (var stamp in inventory.Stamps)
        {
            var family = specs.FindAll(stamp.StandardId, stamp.SpecId);
            beads.Add(new BeadOnBody(
                stamp.FeatureKey, stamp.Name,
                family.Count == 0 ? BeadSpecStatus.StampedSpecMissing : BeadSpecStatus.Stamped,
                stamp.StandardId, stamp.SpecId, family, Array.Empty<BeadSpecRow>()));
        }

        // Read once: an unstamped bead has no Standard, so every Standard is searched.
        IReadOnlyList<BeadSpecRow>? allSpecs = null;

        foreach (var feature in inventory.UnstampedFeatures)
        {
            if (feature.Geometry is null)
            {
                beads.Add(new BeadOnBody(
                    feature.FeatureKey, feature.Name, BeadSpecStatus.GeometryUnreadable,
                    null, null, Array.Empty<BeadSpecRow>(), Array.Empty<BeadSpecRow>()));
                continue;
            }

            allSpecs ??= specs.AllSpecs();
            var match = BeadSpecMatcher.Match(feature.Geometry, allSpecs, settings);

            if (match.Single is { } row)
            {
                // Widened from the matched row to its whole family: matching reads the bead as it is today, but a
                // matched bead is rebuilt by a re-thicknessing exactly like a stamped one, so it must be judged by
                // the row driven for the NEW thickness — not by the one it happened to match at the old.
                //
                // FindAll comes back empty only when the id spans two workbooks, which it cannot resolve. The row is
                // in hand here either way, so the bead stays identified rather than being downgraded to Unmatched on
                // bad data that does not affect what this bead actually is.
                var family = specs.FindAll(row.StandardId, row.SpecId);
                beads.Add(new BeadOnBody(
                    feature.FeatureKey, feature.Name, BeadSpecStatus.Matched, row.StandardId, row.SpecId,
                    family.Count > 0 ? family : new[] { row }, Array.Empty<BeadSpecRow>()));
            }
            else
            {
                beads.Add(new BeadOnBody(
                    feature.FeatureKey, feature.Name,
                    match.IsAmbiguous ? BeadSpecStatus.Ambiguous : BeadSpecStatus.Unmatched,
                    null, null, Array.Empty<BeadSpecRow>(), match.Candidates));
            }
        }

        return beads;
    }
}
