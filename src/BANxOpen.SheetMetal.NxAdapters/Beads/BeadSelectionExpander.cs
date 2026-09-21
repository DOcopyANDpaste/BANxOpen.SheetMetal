using NXOpen;
using NXOpen.Features;
using BANxOpen.Foundation.NxAdapters;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>One item the bead dialog works on after expansion: a curve or a Bead feature.</summary>
/// <param name="FromSketch">The sketch a curve was expanded from, when the user picked the sketch rather than the
/// curve itself. Null otherwise.</param>
public sealed record ExpandedSelectionItem(NXObject Item, Sketch? FromSketch);

/// <param name="Items">What the dialog works on, in selection order, with no object twice.</param>
/// <param name="Rejected">What the user picked that the dialog cannot build from — an edge, a body, a feature that is
/// neither a Bead nor a sketch. Left out of <see cref="Items"/>; returned only so the caller can trace it.</param>
public sealed record ExpandedSelection(IReadOnlyList<ExpandedSelectionItem> Items, IReadOnlyList<NXObject> Rejected);

/// <summary>Turns what the user picked into the per-item list the bead dialog works on: one item per curve or per
/// Bead feature.
///
/// The dialog's selection block accepts Bead features, sketches and curves. A sketch is replaced by its curves —
/// one bead per curve, the same "one curve maps to one Bead feature" rule <see cref="BeadFeatureService"/> builds
/// by — so everything downstream (traceback, the per-item status list, Apply) only ever sees curves and Bead
/// features. Anything else is rejected here rather than left to fail later.</summary>
public sealed class BeadSelectionExpander
{
    private readonly NxSessionContext _context;

    public BeadSelectionExpander(NxSessionContext context) => _context = context;

    /// <summary>The selection with every sketch replaced by its curves. Curves and Bead features pass through in
    /// order. Duplicates are removed: picking a sketch and one of its curves must not queue that curve twice.</summary>
    public ExpandedSelection Expand(IReadOnlyList<NXObject> selection)
    {
        var items = new List<ExpandedSelectionItem>();
        var rejected = new List<NXObject>();
        var seen = new HashSet<Tag>();

        void Add(NXObject item, Sketch? fromSketch)
        {
            if (seen.Add(item.Tag))
                items.Add(new ExpandedSelectionItem(item, fromSketch));
        }

        foreach (var item in selection)
        {
            switch (item)
            {
                case Sketch sketch:
                    foreach (var curve in CurvesOf(sketch))
                        Add(curve, sketch);
                    break;

                case SketchFeature sketchFeature when SketchOf(sketchFeature) is { } featureSketch:
                    foreach (var curve in CurvesOf(featureSketch))
                        Add(curve, featureSketch);
                    break;

                case Feature feature when BeadFeatureIdentity.IsBeadFeature(feature):
                    Add(feature, null);
                    break;

                // An edge is an IBaseCurve too, but the dialog builds from curves only.
                case Curve curve:
                    Add(curve, null);
                    break;

                default:
                    rejected.Add(item);
                    break;
            }
        }

        return new ExpandedSelection(items, rejected);
    }

    private Sketch? SketchOf(SketchFeature feature)
    {
        try
        {
            return feature.Sketch;
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read the sketch of feature '{feature.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The sketch's curves. Points, dimensions and anything else that is not a curve are left out.
    /// VERIFY: whether reference (construction) curves should be left out too — they are included for now.</summary>
    private IEnumerable<Curve> CurvesOf(Sketch sketch)
    {
        NXObject[] geometry;
        try
        {
            geometry = sketch.GetAllGeometry();
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read the geometry of sketch '{sketch.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return Array.Empty<Curve>();
        }

        return geometry.OfType<Curve>();
    }
}
