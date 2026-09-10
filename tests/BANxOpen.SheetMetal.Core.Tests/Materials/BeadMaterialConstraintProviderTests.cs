using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Contracts.Materials;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Constraints;
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
        new(BodyId, "SM_BODY", BodyKind.Sheet, Volume: 0.0, Attributes: new Dictionary<string, string>());

    // NX material names -> workbook grade labels. "Titanium Grade 5" is deliberately unmapped.
    private static readonly MaterialGradeMap GradeMap = MaterialGradeMap.FromEntries(new Dictionary<string, string>
    {
        ["Aluminum 2024-O"] = "2024-O",
        ["Aluminum 5052-O"] = "5052-O",
        ["Aluminum 7075-T6"] = "7075-T6",
    });

    private static BeadSpecRow Spec(string specId, params string[] allowedGrades) =>
        new("B1005010", specId,
            RadiusAndRadS: 0.245, Width: 0.625, Height: 0.625, DieRadiusP: 0.188, Thickness: 0.02,
            AllowedMaterialGrades: new Dictionary<string, bool>
            {
                ["2024-O"] = allowedGrades.Contains("2024-O"),
                ["5052-O"] = allowedGrades.Contains("5052-O"),
                ["7075-T6"] = allowedGrades.Contains("7075-T6"),
            });

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }),
            Array.Empty<MaterialPropertyValue>());

    private static BeadMaterialConstraintProvider Provider(
        IEnumerable<FeatureSpecStamp> stampsOnBody, params BeadSpecRow[] workbookRows) =>
        new(new FakeInventory(stampsOnBody, Array.Empty<string>()), new FakeSpecLookup(workbookRows), GradeMap);

    private static BeadMaterialConstraintProvider ProviderWithUnstamped(
        IEnumerable<FeatureSpecStamp> stampsOnBody, IEnumerable<string> unstampedFeatures, params BeadSpecRow[] workbookRows) =>
        new(new FakeInventory(stampsOnBody, unstampedFeatures), new FakeSpecLookup(workbookRows), GradeMap);

    private static RuleOutcome Gate(BeadMaterialConstraintProvider provider, string materialName) =>
        new FeatureConstraintGateRule(provider).Evaluate(
            new MaterialAssignmentRuleContext(MakeMaterial(materialName), Body, null, new[] { Body }));

    private static FeatureSpecStamp Stamp(string specId) => new("B1005010", specId);

    // ---- no beads ----

    [Fact]
    public void A_body_without_beads_has_no_constraints()
    {
        var provider = Provider(Array.Empty<FeatureSpecStamp>(), Spec("B1005010-1", "2024-O"));

        Assert.Empty(provider.ConstraintsFor(BodyId));
    }

    [Fact]
    public void A_body_without_beads_accepts_even_an_unmapped_material()
    {
        // The grade map only matters when there is a SPEC to check against.
        var provider = Provider(Array.Empty<FeatureSpecStamp>());

        Assert.Equal(RuleDecision.Allow, Gate(provider, "Titanium Grade 5").Decision);
    }

    // ---- one SPEC ----

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

    // ---- fail-closed cases ----

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
        Assert.Contains("material-grade-map", outcome.Message);
    }

    [Fact]
    public void A_SPEC_missing_from_its_workbook_blocks_every_material()
    {
        // No row supplied for the stamped SPEC: it was retired after the bead was built.
        var provider = Provider(new[] { Stamp("B1005010-RETIRED") });

        var outcome = Gate(provider, "Aluminum 2024-O");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.SpecNotFoundCode, outcome.ReasonCode);
        Assert.Contains("B1005010-RETIRED", outcome.Message);
    }

    [Fact]
    public void A_bead_the_tool_did_not_create_blocks_every_material()
    {
        var provider = ProviderWithUnstamped(Array.Empty<FeatureSpecStamp>(), new[] { "Bead(12)" });

        var outcome = Gate(provider, "Aluminum 2024-O");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(BeadMaterialConstraintProvider.UnstampedBeadCode, outcome.ReasonCode);
        Assert.Contains("Bead(12)", outcome.Message);
    }

    [Fact]
    public void An_unstamped_bead_is_reported_before_anything_about_the_stamped_ones()
    {
        // Nothing else on the body can be resolved until the unknown SPEC is, so that is what the user sees.
        var provider = ProviderWithUnstamped(
            new[] { Stamp("B1005010-1") }, new[] { "Bead(12)" }, Spec("B1005010-1", "2024-O"));

        Assert.Equal(BeadMaterialConstraintProvider.UnstampedBeadCode, Gate(provider, "Titanium Grade 5").ReasonCode);
    }

    // ---- several SPECs ----

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

        var perSpec = provider.ConstraintsFor(BodyId)
            .Count(c => c.ReasonCode == BeadMaterialConstraintProvider.NotAllowedCode);

        Assert.Equal(1, perSpec);
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
        var grade = GradeMap.GradeFor(materialName)!;

        var specDirection = new MaterialAllowedRule().Evaluate(new BeadValidationContext(
            new SheetMetalProfile(BodyId, "SM_BODY", Thickness: 0.02, MaterialGradeLabel: grade), spec));

        var materialDirection = Gate(Provider(new[] { Stamp("B1005010-1") }, spec), materialName);

        Assert.Equal(specDirection.Decision == RuleDecision.Allow, materialDirection.Decision == RuleDecision.Allow);
    }

    private sealed class FakeInventory : IFeatureInventory
    {
        private readonly BodyFeatureInventory _inventory;

        public FakeInventory(IEnumerable<FeatureSpecStamp> stamps, IEnumerable<string> unstamped) =>
            _inventory = new BodyFeatureInventory(stamps.ToList(), unstamped.ToList());

        public BodyFeatureInventory Read(BodyId bodyId) => bodyId == BodyId ? _inventory : BodyFeatureInventory.Empty;
    }

    private sealed class FakeSpecLookup : IBeadSpecLookup
    {
        private readonly IReadOnlyList<BeadSpecRow> _rows;

        public FakeSpecLookup(IEnumerable<BeadSpecRow> rows) => _rows = rows.ToList();

        public BeadSpecRow? Find(string standardId, string specId) =>
            _rows.FirstOrDefault(r => r.StandardId == standardId && r.SpecId == specId);
    }
}
