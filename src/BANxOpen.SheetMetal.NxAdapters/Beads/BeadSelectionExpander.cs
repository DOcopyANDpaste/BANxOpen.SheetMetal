using NXOpen;
using NXOpen.Features;
using BANxOpen.Foundation.NxAdapters;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>One thing the bead dialog works on after expansion: either a chain of curves, which becomes one bead,
/// or an existing Bead feature.</summary>
/// <param name="Curves">The chain's curves; empty for a Bead feature.</param>
/// <param name="BeadFeature">The Bead feature the user picked; null for a chain.</param>
/// <param name="FromSketch">The sketch every curve of the chain belongs to, when there is one — for naming the chain.</param>
public sealed record ExpandedSelectionItem(IReadOnlyList<NXObject> Curves, Feature? BeadFeature, Sketch? FromSketch)
{
    public bool IsChain => BeadFeature is null;
}

/// <param name="Items">What the dialog works on, in selection order, with no chain or feature twice.</param>
/// <param name="Rejected">What the user picked that the dialog cannot build from. Left out of <see cref="Items"/>;
/// returned only so the caller can trace it.</param>
/// <param name="Notes">How each block's objects were read, for the caller's trace.</param>
public sealed record ExpandedSelection(
    IReadOnlyList<ExpandedSelectionItem> Items, IReadOnlyList<NXObject> Rejected, IReadOnlyList<string> Notes);

/// <summary>Turns what the user picked in the bead dialog's two selection blocks into the items it works on.
///
/// The curve block (a Super Section) collects curves, including curves of a sketch drawn on the fly. Each section
/// it returns is one chain, and one chain becomes one bead — the rule <see cref="BeadFeatureService"/> builds by.
/// The feature block picks existing Bead features; a feature that is not a Bead is rejected here rather than left to
/// fail later.</summary>
public sealed class BeadSelectionExpander
{
    private readonly NxSessionContext _context;

    public BeadSelectionExpander(NxSessionContext context) => _context = context;

    /// <param name="curveBlockObjects">What the curve block returned.</param>
    /// <param name="featureBlockObjects">What the Bead feature block returned.</param>
    public ExpandedSelection Expand(IReadOnlyList<NXObject> curveBlockObjects, IReadOnlyList<NXObject> featureBlockObjects)
    {
        var items = new List<ExpandedSelectionItem>();
        var rejected = new List<NXObject>();
        var notes = new List<string>();
        var seenChains = new HashSet<string>();
        var seenFeatures = new HashSet<Tag>();

        void AddChain(IReadOnlyList<NXObject> curves, string source)
        {
            if (curves.Count == 0)
            {
                notes.Add($"{source}: no curves");
                return;
            }

            var key = string.Join(",", curves.Select(c => c.Tag.ToString()).OrderBy(t => t, StringComparer.Ordinal));
            if (!seenChains.Add(key))
            {
                notes.Add($"{source}: same curves as a chain already listed, left out");
                return;
            }

            items.Add(new ExpandedSelectionItem(curves, null, SketchContainingAll(curves)));
            notes.Add($"{source}: chain of {curves.Count} curve(s)");
        }

        // VERIFY: which objects SuperSection.GetSelectedObjects() returns. A Section is read as one chain; bare
        // curves, if that is what comes back, are taken together as one chain.
        var looseCurves = new List<NXObject>();
        foreach (var item in curveBlockObjects)
        {
            switch (item)
            {
                case Section section:
                    AddChain(CurvesOf(section, rejected), $"Section {section.JournalIdentifier}");
                    break;

                case Sketch sketch:
                    AddChain(CurvesOf(sketch), $"Sketch {sketch.Name}");
                    break;

                case Curve curve:
                    looseCurves.Add(curve);
                    break;

                default:
                    rejected.Add(item);
                    break;
            }
        }

        if (looseCurves.Count > 0)
            AddChain(looseCurves, "Bare curves from the curve block");

        foreach (var item in featureBlockObjects)
        {
            if (item is Feature feature && BeadFeatureIdentity.IsBeadFeature(feature))
            {
                if (seenFeatures.Add(feature.Tag))
                    items.Add(new ExpandedSelectionItem(Array.Empty<NXObject>(), feature, null));
            }
            else
            {
                rejected.Add(item);
            }
        }

        return new ExpandedSelection(items, rejected, notes);
    }

    /// <summary>The section's output curves. Anything else it outputs (an edge, when the block's rules allow one) is
    /// rejected: beads are built from curves only.</summary>
    private List<NXObject> CurvesOf(Section section, List<NXObject> rejected)
    {
        NXObject[] output;
        try
        {
            section.GetOutputCurves(out output);
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read the curves of section '{section.JournalIdentifier}': NX {ex.ErrorCode}: {ex.Message}");
            return new List<NXObject>();
        }

        var curves = new List<NXObject>();
        foreach (var item in output)
        {
            if (item is Curve)
                curves.Add(item);
            else
                rejected.Add(item);
        }

        return curves;
    }

    private List<NXObject> CurvesOf(Sketch sketch)
    {
        try
        {
            return sketch.GetAllGeometry().OfType<Curve>().Cast<NXObject>().ToList();
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read the geometry of sketch '{sketch.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return new List<NXObject>();
        }
    }

    /// <summary>The sketch holding every curve of the chain, or null when there is none — the chain is then named by
    /// its curves alone.</summary>
    private Sketch? SketchContainingAll(IReadOnlyList<NXObject> curves)
    {
        var tags = new HashSet<Tag>(curves.Select(c => c.Tag));

        foreach (Sketch sketch in _context.WorkPart.Sketches)
        {
            NXObject[] geometry;
            try
            {
                geometry = sketch.GetAllGeometry();
            }
            catch (NXException)
            {
                continue;
            }

            var inSketch = new HashSet<Tag>(geometry.Select(g => g.Tag));
            if (tags.IsSubsetOf(inSketch))
                return sketch;
        }

        return null;
    }
}
