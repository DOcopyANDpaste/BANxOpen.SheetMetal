using NXOpen;
using NXOpen.Features;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.NxAdapters.Common;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Turns "the user picked the sheet metal body" into the same per-item list picking its curves would
/// have produced: every Bead feature sitting on that body, in the part's feature order.
///
/// The dialog's selection block accepts a body or curves (its own label says as much), and everything
/// downstream — traceback, the per-item status list, Apply — works on one item per bead. Expanding here rather
/// than in the presenter keeps that downstream code from having to know a Body is a possible selection at all.
///
/// The Bead features themselves are returned, not their section curves. Traceback accepts a Feature directly,
/// and <see cref="BeadFeatureService.CreateOrUpdate"/> ignores the curve when it is updating an existing
/// feature — which is the only thing a body selection can ever lead to, since a body says nothing about where
/// a bead the user has not drawn yet would go.
///
/// A body with no beads on it expands to nothing. That is not an error: the body is still resolved and its
/// profile still read, so the dialog can say there is nothing to update rather than refusing the selection.</summary>
public sealed class BeadSelectionExpander
{
    private readonly NxSessionContext _context;

    public BeadSelectionExpander(NxSessionContext context) => _context = context;

    /// <summary>The selection with every <see cref="Body"/> replaced by the Bead features on it. Everything else
    /// passes through untouched and in order. Duplicates are removed: selecting a body and one of its beads, or
    /// two bodies sharing nothing, must not queue the same feature twice.</summary>
    public IReadOnlyList<NXObject> Expand(IReadOnlyList<NXObject> selection)
    {
        if (!selection.OfType<Body>().Any())
            return selection;

        var expanded = new List<NXObject>();
        var seen = new HashSet<Tag>();

        foreach (var item in selection)
        {
            if (item is Body body)
            {
                foreach (var bead in BeadsOn(body))
                {
                    if (seen.Add(bead.Tag))
                        expanded.Add(bead);
                }

                continue;
            }

            if (seen.Add(item.Tag))
                expanded.Add(item);
        }

        return expanded;
    }

    private IEnumerable<Feature> BeadsOn(Body body)
    {
        foreach (Feature feature in _context.WorkPart.Features)
        {
            if (!BeadFeatureIdentity.IsBeadFeature(feature))
                continue;

            if (FeatureBodyResolver.Resolve(feature) is { } featureBody && featureBody.Tag.Equals(body.Tag))
                yield return feature;
        }
    }
}
