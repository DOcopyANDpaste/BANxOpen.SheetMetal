using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class BeadsOnBodyTests
{
    private static BeadSpecRow Spec(string specId, double height = 0.625) =>
        new("B1005010", "B1005010", specId, RadiusAndRadS: 0.245, Width: 0.625, Height: height, DieRadiusP: 0.188,
            Thickness: 0.02, AllowedMaterialGrades: new Dictionary<string, bool> { ["2024-O"] = true });

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
        Assert.Same(spec, bead.Spec);
        Assert.Equal("42", bead.FeatureKey);
    }

    [Fact]
    public void A_stamp_whose_SPEC_is_gone_is_flagged_not_dropped()
    {
        var bead = Assert.Single(Resolve(new[] { new FeatureSpecStamp("B1005010", "RETIRED") }, Array.Empty<UnstampedFeature>()));

        Assert.Equal(BeadSpecStatus.StampedSpecMissing, bead.Status);
        Assert.Null(bead.Spec);
        Assert.True(bead.IsStamped);
    }

    [Fact]
    public void An_unstamped_bead_is_identified_by_its_geometry()
    {
        var spec = Spec("B1005010-1");

        var bead = Assert.Single(Resolve(Array.Empty<FeatureSpecStamp>(), new[] { new UnstampedFeature("Bead(12)", GeometryOf(spec), "7") }, spec));

        Assert.Equal(BeadSpecStatus.Matched, bead.Status);
        Assert.Same(spec, bead.Spec);
        Assert.Null(bead.UnidentifiedReason);
    }

    [Fact]
    public void An_unstamped_bead_matching_several_SPECs_is_ambiguous_and_says_which()
    {
        var a = Spec("B1005010-1");
        var b = Spec("B1005010-2");

        var bead = Assert.Single(Resolve(Array.Empty<FeatureSpecStamp>(), new[] { new UnstampedFeature("Bead(12)", GeometryOf(a)) }, a, b));

        Assert.Equal(BeadSpecStatus.Ambiguous, bead.Status);
        Assert.Null(bead.Spec);
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

    private sealed class Lookup : IBeadSpecLookup
    {
        private readonly IReadOnlyList<BeadSpecRow> _rows;

        public Lookup(IEnumerable<BeadSpecRow> rows) => _rows = rows.ToList();

        public BeadSpecRow? Find(string standardId, string specId) =>
            _rows.FirstOrDefault(r => r.StandardId == standardId && r.SpecId == specId);

        public IReadOnlyList<BeadSpecRow> AllSpecs() => _rows;
    }
}
