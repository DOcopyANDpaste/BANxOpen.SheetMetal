using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class BeadsOnBodyTests
{
    private static BeadSpecRow Spec(string specId, double height = 0.625, double thickness = 0.02, bool allows2024O = true) =>
        new("B1005010", "B1005010", specId, RadiusAndRadS: 0.245, Width: 0.625, Height: height, DieRadiusP: 0.188,
            Thickness: thickness, AllowedMaterialGrades: new Dictionary<string, bool> { ["2024-O"] = allows2024O });

    private static BeadGeometry GeometryOf(BeadSpecRow row) => new(row.Thickness, row.Height, row.RadiusAndRadS, row.DieRadiusP);

    private static IReadOnlyList<BeadOnBody> Resolve(
        IEnumerable<FeatureSpecStamp> stamps, IEnumerable<UnstampedFeature> unstamped, params BeadSpecRow[] rows) =>
        BeadsOnBody.Resolve(new BodyFeatureInventory(stamps.ToList(), unstamped.ToList()), new Lookup(rows), BeadSettings.Default);

    [Fact]
    public void A_stamped_bead_carries_its_SPEC_and_feature_key()
    {
        var spec = Spec("B1005010-1");

        var bead = Assert.Single(Resolve(new[] { new FeatureSpecStamp("B1005010", "B1005010-1", "42", "Bead(4)") }, Array.Empty<UnstampedFeature>(), spec));

        Assert.Equal(BeadSpecStatus.Stamped, bead.Status);
        Assert.Same(spec, Assert.Single(bead.SpecFamily));
        Assert.Equal("42", bead.FeatureKey);
    }

    [Fact]
    public void A_stamp_whose_SPEC_is_gone_is_flagged_not_dropped()
    {
        var bead = Assert.Single(Resolve(new[] { new FeatureSpecStamp("B1005010", "RETIRED") }, Array.Empty<UnstampedFeature>()));

        Assert.Equal(BeadSpecStatus.StampedSpecMissing, bead.Status);
        Assert.False(bead.IsSpecKnown);
        Assert.True(bead.IsStamped);
    }

    [Fact]
    public void An_unstamped_bead_is_identified_by_its_geometry()
    {
        var spec = Spec("B1005010-1");

        var bead = Assert.Single(Resolve(Array.Empty<FeatureSpecStamp>(), new[] { new UnstampedFeature("Bead(12)", GeometryOf(spec), "7") }, spec));

        Assert.Equal(BeadSpecStatus.Matched, bead.Status);
        Assert.Same(spec, Assert.Single(bead.SpecFamily));
        Assert.Null(bead.UnidentifiedReason);
    }

    [Fact]
    public void An_unstamped_bead_matching_several_SPECs_is_ambiguous_and_says_which()
    {
        var a = Spec("B1005010-1");
        var b = Spec("B1005010-2");

        var bead = Assert.Single(Resolve(Array.Empty<FeatureSpecStamp>(), new[] { new UnstampedFeature("Bead(12)", GeometryOf(a)) }, a, b));

        Assert.Equal(BeadSpecStatus.Ambiguous, bead.Status);
        Assert.False(bead.IsSpecKnown);
        Assert.Contains("B1005010-2", bead.UnidentifiedReason);
    }

    [Fact]
    public void Unreadable_and_unmatched_beads_say_why()
    {
        var beads = Resolve(
            Array.Empty<FeatureSpecStamp>(),
            new[] { new UnstampedFeature("Bead(1)", null), new UnstampedFeature("Bead(2)", GeometryOf(Spec("X", height: 9))) },
            Spec("B1005010-1"));

        Assert.Equal(new[] { BeadSpecStatus.GeometryUnreadable, BeadSpecStatus.Unmatched }, beads.Select(b => b.Status));
        Assert.All(beads, b => Assert.NotNull(b.UnidentifiedReason));
    }

    // ---- A SPEC id names a family of rows, one per thickness (the B1005010-2 shape) ----

    /// <summary>The shape the real workbooks have: one SPEC id repeated once per sheet thickness. Taking the first
    /// row of such a family was the bug — on a 0.05 sheet it judged the bead by the 0.02 row, which matches no
    /// thickness and so allowed nothing.</summary>
    private static BeadSpecRow[] Family(string specId, params (double Thickness, bool Allows2024O)[] rows) =>
        rows.Select(r => Spec(specId, thickness: r.Thickness, allows2024O: r.Allows2024O)).ToArray();

    [Fact]
    public void A_stamped_bead_carries_every_thickness_its_SPEC_is_driven_for()
    {
        var family = Family("B1005010-2", (0.02, true), (0.05, true), (0.063, true));

        var bead = Assert.Single(Resolve(new[] { new FeatureSpecStamp("B1005010", "B1005010-2") }, Array.Empty<UnstampedFeature>(), family));

        Assert.Equal(BeadSpecStatus.Stamped, bead.Status);
        Assert.Equal(new[] { 0.02, 0.05, 0.063 }, bead.SpecFamily.Select(r => r.Thickness));
    }

    [Fact]
    public void SpecAt_picks_the_row_driven_for_that_thickness_not_the_first()
    {
        var bead = Assert.Single(Resolve(
            new[] { new FeatureSpecStamp("B1005010", "B1005010-2") }, Array.Empty<UnstampedFeature>(),
            Family("B1005010-2", (0.02, true), (0.05, false))));

        Assert.Equal(0.05, bead.SpecAt(0.05)!.Thickness);

        // The whole point: the 0.05 row refuses 2024-O even though the family's first row allows it.
        Assert.False(bead.SpecAt(0.05)!.IsAllowedFor("2024-O"));
        Assert.True(bead.SpecAt(0.02)!.IsAllowedFor("2024-O"));
    }

    [Fact]
    public void SpecAt_uses_the_same_thickness_tolerance_as_SPEC_validation()
    {
        var bead = Assert.Single(Resolve(
            new[] { new FeatureSpecStamp("B1005010", "B1005010-2") }, Array.Empty<UnstampedFeature>(),
            Family("B1005010-2", (0.05, true))));

        Assert.NotNull(bead.SpecAt(0.05 + ThicknessMatchRule.ToleranceInches / 2));
    }

    [Fact]
    public void SpecAt_is_null_for_a_thickness_the_SPEC_is_not_driven_for()
    {
        var bead = Assert.Single(Resolve(
            new[] { new FeatureSpecStamp("B1005010", "B1005010-2") }, Array.Empty<UnstampedFeature>(),
            Family("B1005010-2", (0.02, true), (0.063, true))));

        Assert.Null(bead.SpecAt(0.09));
        Assert.Equal("0.02 - 0.063", bead.ThicknessCoverage);
    }

    [Fact]
    public void An_unstamped_bead_is_widened_from_the_matched_row_to_its_whole_family()
    {
        // Matching reads the bead at the thickness it sits in today, but re-thicknessing rebuilds it just like a
        // stamped one — so the other thicknesses of its SPEC have to come along.
        var family = Family("B1005010-2", (0.02, true), (0.05, true));

        var bead = Assert.Single(Resolve(
            Array.Empty<FeatureSpecStamp>(), new[] { new UnstampedFeature("Bead(12)", GeometryOf(family[0])) }, family));

        Assert.Equal(BeadSpecStatus.Matched, bead.Status);
        Assert.Equal(new[] { 0.02, 0.05 }, bead.SpecFamily.Select(r => r.Thickness));
    }

    private sealed class Lookup : IBeadSpecLookup
    {
        private readonly IReadOnlyList<BeadSpecRow> _rows;

        public Lookup(IEnumerable<BeadSpecRow> rows) => _rows = rows.ToList();

        public IReadOnlyList<BeadSpecRow> FindAll(string standardId, string specId) =>
            _rows.Where(r => r.StandardId == standardId && r.SpecId == specId)
                .OrderBy(r => r.Thickness)
                .ToList();

        public IReadOnlyList<BeadSpecRow> AllSpecs() => _rows;
    }
}
