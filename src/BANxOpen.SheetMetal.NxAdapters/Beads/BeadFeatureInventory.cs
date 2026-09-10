using NXOpen;
using NXOpen.Features;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Reads which bead SPECs are stamped on the features of one body — the NX half of
/// <see cref="BeadMaterialConstraintProvider"/>.
///
/// Stamp reading goes through <see cref="BeadTracebackService.Trace"/> rather than reading attributes here, so
/// a pattern instance (which NX does not copy attributes onto) resolves to its stamped original exactly the
/// way the bead dialog resolves it. Two readers of the same stamp would eventually disagree about pattern
/// members, and a pattern member is the case most likely to be missed.
///
/// A body is matched by <see cref="BodyResolver.GetBodyId"/>, the same identity the material engine uses,
/// so "this body" means the same object to both.
///
/// Every feature in the part is scanned per call. That is fine for one body; a caller planning many
/// candidate materials against the same body should wrap the provider built on this in
/// <c>CachingFeatureConstraintProvider</c>, so the scan happens once per query rather than once per
/// candidate.</summary>
public sealed class BeadFeatureInventory : IFeatureInventory
{
    private readonly NxSessionContext _context;
    private readonly BeadTracebackService _traceback;

    public BeadFeatureInventory(NxSessionContext context, BeadTracebackService? traceback = null)
    {
        _context = context;
        _traceback = traceback ?? new BeadTracebackService(context);
    }

    public BodyFeatureInventory Read(BodyId bodyId)
    {
        var stamps = new List<FeatureSpecStamp>();
        var unstamped = new List<string>();

        Feature[] features;
        try
        {
            features = _context.WorkPart.Features.ToArray();
        }
        catch (NXException ex)
        {
            // Reporting "no features" here would silently lift every bead restriction on the body. Surface it
            // as an unreadable feature instead, so the provider blocks and the user sees why.
            _context.Log.Error($"Could not enumerate features while checking bead SPECs on body '{bodyId}': NX {ex.ErrorCode}: {ex.Message}");
            return new BodyFeatureInventory(stamps, new[] { "(features could not be read)" });
        }

        foreach (var feature in features)
        {
            if (!BelongsToBody(feature, bodyId))
                continue;

            var trace = _traceback.Trace(feature);

            if (trace.Result.Found && trace.Result.StandardId is { } standardId && trace.Result.SpecId is { } specId)
                stamps.Add(new FeatureSpecStamp(standardId, specId));
            else if (trace.Result.HasUnstampedFeature)
                unstamped.Add(DisplayName(feature));
            // Neither: not a bead feature at all.
        }

        return new BodyFeatureInventory(stamps, unstamped);
    }

    private bool BelongsToBody(Feature feature, BodyId bodyId)
    {
        try
        {
            return feature.GetBodies().Any(body => BodyResolver.GetBodyId(body) == bodyId);
        }
        catch (NXException)
        {
            // Some feature types (datums, sketches) have no body and throw rather than return empty. They are
            // never beads, so skipping them is correct rather than a fail-open.
            return false;
        }
    }

    private static string DisplayName(Feature feature)
    {
        try
        {
            return feature.GetFeatureName();
        }
        catch (NXException)
        {
            return feature.JournalIdentifier;
        }
    }
}
