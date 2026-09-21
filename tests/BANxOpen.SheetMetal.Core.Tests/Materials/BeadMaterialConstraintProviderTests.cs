using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Contracts.Materials;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Rules.Features;
using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class BeadMaterialConstraintProviderTests
{
    private static readonly BodyId BodyId = new("body-1");

    private static readonly BodyInfo Body =
        new(BodyId, "SM_BODY", BodyKind.SheetMetal, Volume: 0.0, Attributes: new Dictionary<string, string>());

    // NX material names -> workbook grade labels. "Titanium Grade 5" deliberately has no row.
    private static readonly SheetMetalMaterialTable Table = TableRows.AluminumTable();

    /// <summary>A SPEC row. Geometry defaults are shared, so rows differ in shape only when a test says so.</summary>
    private static BeadSpecRow Spec(
        string specId, string[] allowedGrades,
        double height = 0.625, double radius = 0.245, double dieRadius = 0.188, double thickness = 0.02,
        double width = 0.625) =>
        new("B1005010", "B1005010", specId,
            RadiusAndRadS: radius, Width: width, Height: height, DieRadiusP: dieRadius, Thickness: thickness,
            AllowedMaterialGrades: new Dictionary<string, bool>
            {
                ["2024-O"] = allowedGrades.Contains("2024-O"),
                ["5052-O"] = allowedGrades.Contains("5052-O"),
                ["7075-T6"] = allowedGrades.Contains("7075-T6"),
            });

    private static BeadSpecRow Spec(string specId, params string[] allowedGrades) => Spec(specId, allowedGrades, height: 0.625);

    private static BeadGeometry GeometryOf(BeadSpecRow row) =>
        new(row.Thickness, row.Height, row.RadiusAndRadS, row.DieRadiusP);

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }),
            Array.Empty<MaterialPropertyValue>());

    private static BeadMaterialConstraintProvider Provider(
        IEnumerable<FeatureSpecStamp> stamps, IEnumerable<UnstampedFeature> unstamped, params BeadSpecRow[] workbookRows) =>
        new(new FakeInventory(stamps, unstamped), new FakeSpecLookup(workbookRows), Table, BeadSettings.Default);

    private static BeadMaterialConstraintProvider Provider(
        IEnumerable<FeatureSpecStamp> stamps, params BeadSpecRow[] workbookRows) =>
        Provider(stamps, Array.Empty<UnstampedFeature>(), workbookRows);

    private static RuleOutcome Gate(BeadMaterialConstraintProvider provider, string materialName) =>
        new FeatureConstraintGateRule(provider).Evaluate(
            new MaterialAssignmentRuleContext(MakeMaterial(materialName), Body, null, new[] { Body }));

    private static FeatureSpecStamp Stamp(string specId) => new("B1005010", specId);

    private static readonly FeatureSpecStamp[] NoStamps = Array.Empty<FeatureSpecStamp>();

    // ---- no beads ----

    [Fact]
    public void A_body_without_beads_has_no_constraints()
    {
        Assert.Empty(Provider(NoStamps, Spec("B1005010-1", "2024-O")).ConstraintsFor(BodyId));
    }

    [Fact]
    public void A_body_without_beads_accepts_even_an_unmapped_material()
    {
        Assert.Equal(RuleDecision.Allow, Gate(Provider(NoStamps), "Titanium Grade 5").Decision);
    }

    // ---- stamped beads ----

    [Fact]
    public void Allows_a_material_whose_grade_the_SPEC_permits()
    {
        var provider = Provider(new[] { Stamp("B1005010-1") }, Spec("B1005010-1", "2024-O"));

        Assert.Equal(RuleDecision.Allow, Gate(provider, "Aluminum 2024-O").Decision);
    }

    [Fact]
    public void Blocks_a_material_whose_grade_the_SPEC_forbids()
    {
        var provider = Provider(new[] { Stamp("B1005010-1") }, Spec("B1005010-1", "2024-O"));

        var outcome = Gate(provider, "Aluminum 7075-T6");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.NotAllowedCode, outcome.ReasonCode);
    }

    [Fact]
    public void Forbidden_material_message_names_the_material_its_grade_and_the_SPEC()
    {
        var provider = Provider(new[] { Stamp("B1005010-1") }, Spec("B1005010-1", "2024-O"));

        var message = Gate(provider, "Aluminum 7075-T6").Message;

        Assert.Contains("Aluminum 7075-T6", message);
        Assert.Contains("7075-T6", message);
        Assert.Contains("B1005010-1", message);
    }

    [Fact]
    public void Blocks_an_unmapped_material_rather_than_letting_it_through_unchecked()
    {
        var provider = Provider(new[] { Stamp("B1005010-1") }, Spec("B1005010-1", "2024-O", "5052-O", "7075-T6"));

        var outcome = Gate(provider, "Titanium Grade 5");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.GradeUnrecognizedCode, outcome.ReasonCode);
    }

    [Fact]
    public void Unmapped_material_is_reported_as_unmapped_not_as_refused_by_a_SPEC()
    {
        // The user action differs: fix the grade map, not pick a different material.
        var provider = Provider(
            new[] { Stamp("B1005010-1"), Stamp("B1005010-2") },
            Spec("B1005010-1", "2024-O"), Spec("B1005010-2", "2024-O"));

        var outcome = Gate(provider, "Titanium Grade 5");

        Assert.Equal(BeadMaterialConstraintProvider.GradeUnrecognizedCode, outcome.ReasonCode);
        Assert.Contains("sheet metal material standards file", outcome.Message);
    }

    [Fact]
    public void A_SPEC_missing_from_its_workbook_blocks_every_material()
    {
        var provider = Provider(new[] { Stamp("B1005010-RETIRED") });

        var outcome = Gate(provider, "Aluminum 2024-O");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.SpecNotFoundCode, outcome.ReasonCode);
        Assert.Contains("B1005010-RETIRED", outcome.Message);
    }

    [Fact]
    public void A_material_must_satisfy_every_SPEC_on_the_body()
    {
        var provider = Provider(
            new[] { Stamp("B1005010-1"), Stamp("B1005010-2") },
            Spec("B1005010-1", "2024-O", "5052-O"),
            Spec("B1005010-2", "2024-O"));

        Assert.Equal(RuleDecision.Allow, Gate(provider, "Aluminum 2024-O").Decision);

        var refused = Gate(provider, "Aluminum 5052-O");
        Assert.Equal(RuleDecision.Block, refused.Decision);
        Assert.Contains("B1005010-2", refused.Message);
    }

    [Fact]
    public void Repeated_stamps_of_the_same_SPEC_produce_one_constraint_for_it()
    {
        var provider = Provider(
            new[] { Stamp("B1005010-1"), Stamp("B1005010-1"), Stamp("B1005010-1") },
            Spec("B1005010-1", "2024-O"));

        var perSpec = provider.ConstraintsFor(BodyId).Count(c => c.ReasonCode == BeadMaterialConstraintProvider.NotAllowedCode);

        Assert.Equal(1, perSpec);
    }

    // ---- unstamped beads ----

    [Fact]
    public void An_unstamped_bead_matching_one_SPEC_is_enforced_like_a_stamped_one()
    {
        var spec = Spec("B1005010-1", "2024-O");
        var provider = Provider(NoStamps, new[] { new UnstampedFeature("Bead(12)", GeometryOf(spec)) }, spec);

        Assert.Equal(RuleDecision.Allow, Gate(provider, "Aluminum 2024-O").Decision);

        var refused = Gate(provider, "Aluminum 7075-T6");
        Assert.Equal(RuleDecision.Block, refused.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.NotAllowedCode, refused.ReasonCode);
    }

    [Fact]
    public void A_matched_unstamped_bead_is_named_as_unstamped_in_the_message()
    {
        // The user needs to know the restriction came from a bead the tool inferred, not one it recorded.
        var spec = Spec("B1005010-1", "2024-O");
        var provider = Provider(NoStamps, new[] { new UnstampedFeature("Bead(12)", GeometryOf(spec)) }, spec);

        var message = Gate(provider, "Aluminum 7075-T6").Message;

        Assert.Contains("Bead(12)", message);
        Assert.Contains("unstamped", message);
    }

    [Fact]
    public void A_bead_matching_within_the_configured_tolerance_is_identified()
    {
        var spec = Spec("B1005010-1", "2024-O");
        var nearly = GeometryOf(spec) with { Height = spec.Height + BeadSettings.DefaultGeometryMatchTolerance / 2 };
        var provider = Provider(NoStamps, new[] { new UnstampedFeature("Bead(12)", nearly) }, spec);

        Assert.Equal(RuleDecision.Block, Gate(provider, "Aluminum 7075-T6").Decision);
    }

    [Fact]
    public void An_unstamped_bead_matching_no_SPEC_warns_and_allows()
    {
        var spec = Spec("B1005010-1", "2024-O");
        var unlike = GeometryOf(spec) with { Height = spec.Height + 1.0 };
        var provider = Provider(NoStamps, new[] { new UnstampedFeature("Bead(12)", unlike) }, spec);

        var outcome = Gate(provider, "Aluminum 7075-T6");

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.UnstampedBeadUnmatchedCode, outcome.ReasonCode);
        Assert.Contains("Bead(12)", outcome.Message);
        Assert.Contains("matches no SPEC", outcome.Message);
    }

    [Fact]
    public void An_unstamped_bead_matching_several_SPECs_warns_rather_than_guessing()
    {
        // Two SPECs that differ only in Width look identical on the feature.
        var narrow = Spec("B1005010-1", new[] { "2024-O" }, width: 0.5);
        var wide = Spec("B1005010-2", new[] { "7075-T6" }, width: 0.75);
        var provider = Provider(NoStamps, new[] { new UnstampedFeature("Bead(12)", GeometryOf(narrow)) }, narrow, wide);

        var outcome = Gate(provider, "Aluminum 7075-T6");

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Contains("B1005010-1", outcome.Message);
        Assert.Contains("B1005010-2", outcome.Message);
    }

    [Fact]
    public void An_unstamped_bead_whose_geometry_could_not_be_read_warns_and_allows()
    {
        var provider = Provider(NoStamps, new[] { new UnstampedFeature("Bead(12)", null) }, Spec("B1005010-1", "2024-O"));

        var outcome = Gate(provider, "Aluminum 7075-T6");

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Contains("could not be read", outcome.Message);
    }

    [Fact]
    public void An_unmatched_bead_warning_never_masks_a_stamped_bead_refusal()
    {
        var spec = Spec("B1005010-1", "2024-O");
        var unlike = GeometryOf(spec) with { Radius = 9.0 };
        var provider = Provider(new[] { Stamp("B1005010-1") }, new[] { new UnstampedFeature("Bead(12)", unlike) }, spec);

        Assert.Equal(RuleDecision.Block, Gate(provider, "Aluminum 7075-T6").Decision);
    }

    [Fact]
    public void An_unstamped_bead_matching_a_SPEC_already_stamped_adds_no_second_constraint()
    {
        var spec = Spec("B1005010-1", "2024-O");
        var provider = Provider(
            new[] { Stamp("B1005010-1") }, new[] { new UnstampedFeature("Bead(12)", GeometryOf(spec)) }, spec);

        var perSpec = provider.ConstraintsFor(BodyId).Count(c => c.ReasonCode == BeadMaterialConstraintProvider.NotAllowedCode);

        Assert.Equal(1, perSpec);
    }

    [Fact]
    public void Features_that_could_not_be_read_block_rather_than_count_as_no_beads()
    {
        var provider = new BeadMaterialConstraintProvider(
            new FixedInventory(BodyFeatureInventory.Unreadable("NX 12345: feature list unavailable")),
            new FakeSpecLookup(Array.Empty<BeadSpecRow>()), Table, BeadSettings.Default);

        var outcome = Gate(provider, "Aluminum 2024-O");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.FeaturesUnreadableCode, outcome.ReasonCode);
        Assert.Contains("NX 12345", outcome.Message);
    }

    // ---- end to end through the engine ----

    [Fact]
    public void Assignable_material_query_offers_only_materials_every_SPEC_permits()
    {
        var provider = Provider(
            new[] { Stamp("B1005010-1"), Stamp("B1005010-2") },
            Spec("B1005010-1", "2024-O", "5052-O"),
            Spec("B1005010-2", "2024-O", "7075-T6"));

        var query = new AssignableMaterialQuery(
            new MaterialAssignmentPlanner(new[] { new FeatureConstraintGateRule(provider) }));

        var offered = query.ListAssignable(
            Body, null,
            new[] { "Aluminum 2024-O", "Aluminum 5052-O", "Aluminum 7075-T6", "Titanium Grade 5" }.Select(MakeMaterial));

        Assert.Equal(new[] { "Aluminum 2024-O" }, offered.Select(m => m.Name));
    }

    [Theory]
    [InlineData("Aluminum 2024-O")]
    [InlineData("Aluminum 5052-O")]
    [InlineData("Aluminum 7075-T6")]
    public void Agrees_with_MaterialAllowedRule_about_what_a_SPEC_permits(string materialName)
    {
        // The two directions of the same question must never disagree: "may this SPEC go on this material?"
        // (MaterialAllowedRule, the bead dialog) and "may this material go on a body carrying this SPEC?"
        // (this provider, the material dialog).
        var spec = Spec("B1005010-1", "2024-O", "7075-T6");
        var grade = Table.GradeForPhysicalMaterial(materialName)!;

        var specDirection = new MaterialAllowedRule().Evaluate(new BeadValidationContext(
            new SheetMetalProfile(Thickness: 0.02, MaterialGradeLabel: grade), spec));

        var materialDirection = Gate(Provider(new[] { Stamp("B1005010-1") }, spec), materialName);

        Assert.Equal(specDirection.Decision == RuleDecision.Allow, materialDirection.Decision == RuleDecision.Allow);
    }

    private sealed class FakeInventory : IFeatureInventory
    {
        private readonly BodyFeatureInventory _inventory;

        public FakeInventory(IEnumerable<FeatureSpecStamp> stamps, IEnumerable<UnstampedFeature> unstamped) =>
            _inventory = new BodyFeatureInventory(stamps.ToList(), unstamped.ToList());

        public BodyFeatureInventory Read(BodyId bodyId) => bodyId == BodyId ? _inventory : BodyFeatureInventory.Empty;
    }

    private sealed class FixedInventory : IFeatureInventory
    {
        private readonly BodyFeatureInventory _inventory;

        public FixedInventory(BodyFeatureInventory inventory) => _inventory = inventory;

        public BodyFeatureInventory Read(BodyId bodyId) => _inventory;
    }

    private sealed class FakeSpecLookup : IBeadSpecLookup
    {
        private readonly IReadOnlyList<BeadSpecRow> _rows;

        public FakeSpecLookup(IEnumerable<BeadSpecRow> rows) => _rows = rows.ToList();

        public BeadSpecRow? Find(string standardId, string specId) =>
            _rows.FirstOrDefault(r => r.StandardId == standardId && r.SpecId == specId);

        public IReadOnlyList<BeadSpecRow> AllSpecs() => _rows;
    }
}
