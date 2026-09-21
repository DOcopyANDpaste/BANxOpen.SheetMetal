using BANxOpen.SheetMetal.Beads;
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
/// <param name="Spec">The SPEC row the bead is built to. Null unless <see cref="Status"/> is Stamped or Matched.</param>
/// <param name="Candidates">Every SPEC an ambiguous bead's geometry matches; empty otherwise.</param>
public sealed record BeadOnBody(
    string FeatureKey, string Name, BeadSpecStatus Status,
    string? StandardId, string? SpecId, BeadSpecRow? Spec, IReadOnlyList<BeadSpecRow> Candidates)
{
    public bool IsStamped => Status is BeadSpecStatus.Stamped or BeadSpecStatus.StampedSpecMissing;

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
            var row = specs.Find(stamp.StandardId, stamp.SpecId);
            beads.Add(new BeadOnBody(
                stamp.FeatureKey, stamp.Name,
                row is null ? BeadSpecStatus.StampedSpecMissing : BeadSpecStatus.Stamped,
                stamp.StandardId, stamp.SpecId, row, Array.Empty<BeadSpecRow>()));
        }

        // Read once: an unstamped bead has no Standard, so every Standard is searched.
        IReadOnlyList<BeadSpecRow>? allSpecs = null;

        foreach (var feature in inventory.UnstampedFeatures)
        {
            if (feature.Geometry is null)
            {
                beads.Add(new BeadOnBody(feature.FeatureKey, feature.Name, BeadSpecStatus.GeometryUnreadable, null, null, null, Array.Empty<BeadSpecRow>()));
                continue;
            }

            allSpecs ??= specs.AllSpecs();
            var match = BeadSpecMatcher.Match(feature.Geometry, allSpecs, settings);

            beads.Add(match.Single is { } row
                ? new BeadOnBody(feature.FeatureKey, feature.Name, BeadSpecStatus.Matched, row.StandardId, row.SpecId, row, Array.Empty<BeadSpecRow>())
                : new BeadOnBody(
                    feature.FeatureKey, feature.Name,
                    match.IsAmbiguous ? BeadSpecStatus.Ambiguous : BeadSpecStatus.Unmatched,
                    null, null, null, match.Candidates));
        }

        return beads;
    }
}
