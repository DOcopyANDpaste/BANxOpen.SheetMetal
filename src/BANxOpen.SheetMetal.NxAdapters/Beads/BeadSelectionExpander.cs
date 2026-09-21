using NXOpen;
using NXOpen.Features;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.Beads;

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
/// The curve block (a Super Section) collects curves, including curves of a sketch drawn on the fly. What it returns
/// is split into connected chains (<see cref="CurveChainGrouper"/>), and one chain becomes one bead — the rule
/// <see cref="BeadFeatureService"/> builds by.
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

        // Each source is split into its connected chains: a chain is one bead, so two separate lines picked
        // together are two beads, not one bead NX cannot build.
        void AddChains(IReadOnlyList<NXObject> curves, string source)
        {
            if (curves.Count == 0)
            {
                notes.Add($"{source}: no curves");
                return;
            }

            var chains = SplitIntoChains(curves);
            if (chains.Count > 1)
                notes.Add($"{source}: {curves.Count} curve(s) split into {chains.Count} connected chain(s)");

            foreach (var chain in chains)
                AddChain(chain, source);
        }

        void AddChain(IReadOnlyList<NXObject> curves, string source)
        {
            var key = string.Join(",", curves.Select(c => c.Tag.ToString()).OrderBy(t => t, StringComparer.Ordinal));
            if (!seenChains.Add(key))
            {
                notes.Add($"{source}: same curves as a chain already listed, left out");
                return;
            }

            items.Add(new ExpandedSelectionItem(curves, null, SketchContainingAll(curves)));
            notes.Add($"{source}: chain of {curves.Count} curve(s)");
        }

        // VERIFY: which objects SuperSection.GetSelectedObjects() returns. A Section's curves and bare curves, if
        // that is what comes back, are each split into their connected chains.
        var looseCurves = new List<NXObject>();
        foreach (var item in curveBlockObjects)
        {
            switch (item)
            {
                case Section section:
                    AddChains(CurvesOf(section, rejected), $"Section {section.JournalIdentifier}");
                    break;

                case Sketch sketch:
                    AddChains(CurvesOf(sketch), $"Sketch {sketch.Name}");
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
            AddChains(looseCurves, "Bare curves from the curve block");

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

    /// <summary>The curves as connected chains, by <see cref="CurveChainGrouper"/> at the part's distance tolerance.
    /// A curve whose ends cannot be read is kept as a chain of its own rather than dropped: the user picked it,
    /// and the bead builder then says why it cannot be used.</summary>
    private List<IReadOnlyList<NXObject>> SplitIntoChains(IReadOnlyList<NXObject> curves)
    {
        if (curves.Count == 1)
            return new List<IReadOnlyList<NXObject>> { curves };

        var tolerance = _context.WorkPart.Preferences.Modeling.DistanceToleranceData;
        var ends = new List<CurveEnds>();
        var unreadable = new List<NXObject>();

        for (var i = 0; i < curves.Count; i++)
        {
            if (TryReadEnds(curves[i], out var start, out var end))
                ends.Add(new CurveEnds(i, start[0], start[1], start[2], end[0], end[1], end[2]));
            else
                unreadable.Add(curves[i]);
        }

        var chains = CurveChainGrouper.Group(ends, tolerance)
            .Select(ids => (IReadOnlyList<NXObject>)ids.Select(id => curves[id]).ToList())
            .ToList();

        chains.AddRange(unreadable.Select(curve => (IReadOnlyList<NXObject>)new[] { curve }));
        return chains;
    }

    private bool TryReadEnds(NXObject curve, out double[] start, out double[] end)
    {
        start = new double[3];
        end = new double[3];
        var tangent = new double[3];
        var normal = new double[3];
        var binormal = new double[3];

        try
        {
            // Parameters 0 and 1 are the curve's normalised start and end.
            _context.UFSession.Modl.AskCurveProps(curve.Tag, 0.0, start, tangent, normal, binormal, out _, out _);
            _context.UFSession.Modl.AskCurveProps(curve.Tag, 1.0, end, tangent, normal, binormal, out _, out _);
            return true;
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read the end points of '{curve.JournalIdentifier}': NX {ex.ErrorCode}: {ex.Message}");
            return false;
        }
    }

    /// <summary>The curves the section was built from — the sketch's own lines, not <c>GetOutputCurves</c>, which
    /// returns copies the section creates internally. A bead on a single line is only accepted when that line is
    /// part of a sketch, and a copy is not; the traceback to an existing bead also needs the real curves. Anything
    /// else the section holds (an edge, when the block's rules allow one) is rejected: beads are built from curves
    /// only.</summary>
    private List<NXObject> CurvesOf(Section section, List<NXObject> rejected)
    {
        var curves = new List<NXObject>();
        var seen = new HashSet<Tag>();
        try
        {
            section.GetSectionData(out var sectionData);
            foreach (var data in sectionData)
            {
                data.GetSectionElementsData(out var elements);
                foreach (var element in elements)
                {
                    element.GetSectionElementData1(out var parent, out _, out _, out _, out _);
                    if (parent is null || !seen.Add(parent.Tag))
                        continue;

                    if (parent is Curve)
                        curves.Add(parent);
                    else
                        rejected.Add(parent);
                }
            }
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read the curves of section '{section.JournalIdentifier}': NX {ex.ErrorCode}: {ex.Message}");
            return new List<NXObject>();
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
