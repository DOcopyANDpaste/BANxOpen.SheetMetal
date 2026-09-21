using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.Tests.Beads;

public class CurveChainGrouperTests
{
    private const double Tolerance = 0.001;

    private static CurveEnds Line(int id, double x1, double y1, double x2, double y2) => new(id, x1, y1, 0, x2, y2, 0);

    [Fact]
    public void Separate_lines_are_separate_chains()
    {
        var chains = CurveChainGrouper.Group(new[] { Line(1, 0, 0, 10, 0), Line(2, 0, 5, 10, 5) }, Tolerance);

        Assert.Equal(new[] { new[] { 1 }, new[] { 2 } }, chains.Select(c => c.ToArray()));
    }

    [Fact]
    public void Touching_lines_are_one_chain()
    {
        var chains = CurveChainGrouper.Group(new[] { Line(1, 0, 0, 10, 0), Line(2, 10, 0, 10, 10) }, Tolerance);

        Assert.Equal(new[] { 1, 2 }, Assert.Single(chains));
    }

    [Fact]
    public void Chains_through_a_curve_picked_later()
    {
        // 1 and 3 do not touch each other; 2 joins them.
        var chains = CurveChainGrouper.Group(
            new[] { Line(1, 0, 0, 10, 0), Line(3, 20, 0, 30, 0), Line(2, 10, 0, 20, 0) }, Tolerance);

        Assert.Equal(new[] { 1, 3, 2 }, Assert.Single(chains));
    }

    [Fact]
    public void Ends_within_tolerance_touch_and_beyond_do_not()
    {
        var within = CurveChainGrouper.Group(new[] { Line(1, 0, 0, 10, 0), Line(2, 10.0005, 0, 20, 0) }, Tolerance);
        var beyond = CurveChainGrouper.Group(new[] { Line(1, 0, 0, 10, 0), Line(2, 10.01, 0, 20, 0) }, Tolerance);

        Assert.Single(within);
        Assert.Equal(2, beyond.Count);
    }

    [Fact]
    public void Keeps_the_order_the_curves_were_picked_in()
    {
        var chains = CurveChainGrouper.Group(
            new[] { Line(5, 0, 5, 10, 5), Line(1, 0, 0, 10, 0), Line(6, 10, 5, 10, 8) }, Tolerance);

        Assert.Equal(new[] { new[] { 5, 6 }, new[] { 1 } }, chains.Select(c => c.ToArray()));
    }

    [Fact]
    public void No_curves_is_no_chains() =>
        Assert.Empty(CurveChainGrouper.Group(Array.Empty<CurveEnds>(), Tolerance));
}
