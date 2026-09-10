using NXOpen;
using NXOpen.Features;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Reads which bead SPECs are on the features of one body — the NX half of
/// <see cref="BeadMaterialConstraintProvider"/>.
///
/// Stamp reading goes through <see cref="BeadTracebackService.Trace"/> rather than reading attributes here, so
/// a pattern instance (which NX does not copy attributes onto) resolves to its stamped original exactly the
/// way the bead dialog resolves it. Two readers of the same stamp would eventually disagree about pattern
/// members, and a pattern member is the case most likely to be missed.
///
/// A bead with no stamp is measured with <see cref="BeadGeometryReader"/> so the provider can try to identify
/// which SPEC it was built to.
///
/// A body is matched by <see cref="BodyResolver.GetBodyId"/>, the same identity the material engine uses.
///
/// Every feature in the part is scanned per call. That is fine for one body; a caller planning many candidate
/// materials against the same body should wrap the provider built on this in
/// <c>CachingFeatureConstraintProvider</c>, so the scan happens once per query rather than once per
/// candidate.</summary>
public sealed class BeadFeatureInventory : IFeatureInventory
{
    private readonly NxSessionContext _context;
    private readonly BeadTracebackService _traceback;
    private readonly BeadGeometryReader _geometry;

    public BeadFeatureInventory(
        NxSessionContext context, BeadTracebackService? traceback = null, BeadGeometryReader? geometry = null)
    {
        _context = context;
        _traceback = traceback ?? new BeadTracebackService(context);
        _geometry = geometry ?? new BeadGeometryReader(context);
    }

    public BodyFeatureInventory Read(BodyId bodyId)
    {
        Feature[] features;
        try
        {
            features = _context.WorkPart.Features.ToArray();
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Could not enumerate features while checking bead SPECs on body '{bodyId}': NX {ex.ErrorCode}: {ex.Message}");
            return BodyFeatureInventory.Unreadable($"NX {ex.ErrorCode}: {ex.Message}");
        }

        var stamps = new List<FeatureSpecStamp>();
        var unstamped = new List<UnstampedFeature>();

        foreach (var feature in features)
        {
            var body = BodyOf(feature, bodyId);
            if (body is null)
                continue;

            var trace = _traceback.Trace(feature);

            if (trace.Result.Found && trace.Result.StandardId is { } standardId && trace.Result.SpecId is { } specId)
                stamps.Add(new FeatureSpecStamp(standardId, specId));
            else if (trace.Result.HasUnstampedFeature)
                unstamped.Add(new UnstampedFeature(DisplayName(feature), _geometry.Read(trace.ExistingFeature ?? feature, body)));
            // Neither: not a bead feature at all.
        }

        return new BodyFeatureInventory(stamps, unstamped);
    }

    /// <summary>The body with <paramref name="bodyId"/> among the feature's bodies, or null if it is not one of
    /// them.</summary>
    private static Body? BodyOf(Feature feature, BodyId bodyId)
    {
        try
        {
            return feature.GetBodies().FirstOrDefault(body => BodyResolver.GetBodyId(body) == bodyId);
        }
        catch (NXException)
        {
            // Some feature types (datums, sketches) have no body and throw rather than return empty. They are
            // never beads, so skipping them is correct rather than a fail-open.
            return null;
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
