using NXOpen;
using NXOpen.Features;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Which body a feature belongs to.
///
/// Best guess: whatever Face or Edge the feature's own <c>GetEntities()</c> exposes. Not confirmed against a live
/// selected Bead feature — if this comes back null where it shouldn't, check what <c>GetEntities()</c> actually
/// returns for a Bead feature in your NX version (it may be Body-typed directly, in which case this can simplify).
///
/// Shared by <see cref="SelectedCurveSetValidator"/> (which body did the user point at?) and
/// <see cref="Beads.BeadSelectionExpander"/> (which of the part's beads sit on this body?) so the two cannot
/// disagree about what "this feature's body" means.</summary>
public static class FeatureBodyResolver
{
    public static Body? Resolve(Feature feature)
    {
        try
        {
            var entities = feature.GetEntities();
            return entities.OfType<Body>().FirstOrDefault()
                ?? entities.OfType<Face>().Select(f => f.GetBody()).FirstOrDefault()
                ?? entities.OfType<Edge>().Select(e => e.GetBody()).FirstOrDefault();
        }
        catch (NXException)
        {
            return null;
        }
    }
}
