namespace BANxOpen.SheetMetal.Beads;

/// <summary>One curve as the chain grouper sees it: an id the caller maps back to its own object, and the two
/// end points. Coordinates are plain doubles so this stays free of NXOpen.</summary>
public sealed record CurveEnds(int Id, double StartX, double StartY, double StartZ, double EndX, double EndY, double EndZ);

/// <summary>Splits curves into connected chains: two curves are in one chain when an end of one coincides with an
/// end of the other, within a tolerance, directly or through other curves of the chain. The bead dialog builds one
/// bead per chain, so two separate lines picked together become two beads, and an L of two touching lines one.
///
/// A branching chain (three or more curves meeting at one point) is still one chain; whether NX can build a bead
/// on it is the bead builder's call, not this class's.</summary>
public static class CurveChainGrouper
{
    /// <returns>The chains, each a list of curve ids. Chains are ordered by their first curve in
    /// <paramref name="curves"/>, and the ids within a chain keep the input order, so the result follows the order
    /// the user picked in.</returns>
    public static IReadOnlyList<IReadOnlyList<int>> Group(IReadOnlyList<CurveEnds> curves, double tolerance)
    {
        var parent = Enumerable.Range(0, curves.Count).ToArray();

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        // Quadratic, but a dialog selection is tens of curves at most.
        for (var i = 0; i < curves.Count; i++)
        for (var j = i + 1; j < curves.Count; j++)
        {
            if (Touch(curves[i], curves[j], tolerance))
                parent[Find(j)] = Find(i);
        }

        var chains = new List<List<int>>();
        var chainByRoot = new Dictionary<int, List<int>>();
        for (var i = 0; i < curves.Count; i++)
        {
            var root = Find(i);
            if (!chainByRoot.TryGetValue(root, out var chain))
            {
                chain = new List<int>();
                chainByRoot[root] = chain;
                chains.Add(chain);
            }

            chain.Add(curves[i].Id);
        }

        return chains;
    }

    private static bool Touch(CurveEnds a, CurveEnds b, double tolerance) =>
        Near(a.StartX, a.StartY, a.StartZ, b.StartX, b.StartY, b.StartZ, tolerance)
        || Near(a.StartX, a.StartY, a.StartZ, b.EndX, b.EndY, b.EndZ, tolerance)
        || Near(a.EndX, a.EndY, a.EndZ, b.StartX, b.StartY, b.StartZ, tolerance)
        || Near(a.EndX, a.EndY, a.EndZ, b.EndX, b.EndY, b.EndZ, tolerance);

    private static bool Near(double x1, double y1, double z1, double x2, double y2, double z2, double tolerance)
    {
        double dx = x1 - x2, dy = y1 - y2, dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz <= tolerance * tolerance;
    }
}
