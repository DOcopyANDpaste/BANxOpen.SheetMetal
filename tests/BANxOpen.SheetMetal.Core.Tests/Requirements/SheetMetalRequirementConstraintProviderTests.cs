using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Contracts;
using BANxOpen.Materials.Core.Assignment;
using BANxOpen.Materials.Core.Rules.Features;
using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.Tests.Materials;
using static BANxOpen.SheetMetal.Tests.RequirementFixtures;

namespace BANxOpen.SheetMetal.Tests.Requirements;

public class SheetMetalRequirementConstraintProviderTests
{
    private static readonly BodyId BodyId = new("body-1");

    // "Aluminum 2024-O" has rows at 0.020 and 0.032; "Aluminum 5052-O" only at 0.020.
    private static readonly SheetMetalMaterialTable Table = TableRows.AluminumTable();

    private static BodyInfo Body => new(BodyId, "SM_BODY", BodyKind.SheetMetal, Volume: 0.0, Attributes: new Dictionary<string, string>());

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }), Array.Empty<MaterialPropertyValue>());

    private static RuleOutcome Gate(ISheetMetalRequirementReader requirements, string material, SheetMetalPreferenceRead? read = null) =>
        new FeatureConstraintGateRule(new SheetMetalRequirementConstraintProvider(
                requirements, new FixedPreferenceReader(read ?? SheetMetalBody(Table)), new SheetMetalStandardSelection(Table) { Selected = Standard }))
            .Evaluate(new MaterialAssignmentRuleContext(MakeMaterial(material), Body, null, new[] { Body }));

    [Fact]
    public void A_material_with_a_compliant_row_is_allowed()
    {
        Assert.Equal(RuleDecision.Allow, Gate(new FakeRequirementReader(Requirement("B1", 0.02, "2024-O")), "Aluminum 2024-O").Decision);
    }

    [Fact]
    public void A_grade_the_requirement_does_not_allow_is_warned_not_blocked()
    {
        var outcome = Gate(new FakeRequirementReader(Requirement("B1", 0.02, "2024-O")), "Aluminum 5052-O");

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Equal(SheetMetalRequirementConstraintProvider.NotAllowedCode, outcome.ReasonCode);
        Assert.Contains("will need to be re-applied", outcome.Message);
    }

    [Fact]
    public void A_material_with_no_row_the_family_allows_is_warned_even_if_its_grade_is_allowed_elsewhere()
    {
        // 5052-O exists only at 0.020; the family allows it only at 0.032.
        var outcome = Gate(new FakeRequirementReader(Requirement("B1", 0.032, "5052-O")), "Aluminum 5052-O");

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Contains("0.032", outcome.Message);
    }

    [Fact]
    public void A_material_allowed_at_another_thickness_of_the_family_is_allowed()
    {
        // Built at 0.020, where the family allows only 5052-O; it allows 2024-O at 0.032.
        var requirement = Requirement("B1", 0.02, SpecRow("B1", 0.02, "5052-O"), SpecRow("B1", 0.032, "2024-O"));

        Assert.Equal(RuleDecision.Allow, Gate(new FakeRequirementReader(requirement), "Aluminum 2024-O").Decision);
    }

    [Fact]
    public void Unreadable_requirements_block_everything()
    {
        var outcome = Gate(new FakeRequirementReader(Array.Empty<SheetMetalRequirement>(), "NX 1: broken"), "Aluminum 2024-O");

        Assert.Equal(SheetMetalRequirementConstraintProvider.UnreadableCode, outcome.ReasonCode);
    }

    [Fact]
    public void A_body_that_is_not_sheet_metal_is_not_constrained()
    {
        var outcome = Gate(new FakeRequirementReader(Requirement("B1", 0.02, "2024-O")), "Aluminum 5052-O", SheetMetalPreferenceRead.NotSheetMetal);

        Assert.Equal(RuleDecision.Allow, outcome.Decision);
    }

    // ---- the picked row ----

    private static RuleOutcome Pick(ISheetMetalRequirementReader requirements, string material, string row, double bodyThickness = 0.02) =>
        new SheetMetalRowGateRule(
                new FixedPreferenceReader(SheetMetalBody(Table, bodyThickness: bodyThickness)),
                new SheetMetalStandardSelection(Table) { Selected = Standard }, requirements)
            .Evaluate(new MaterialAssignmentRuleContext(MakeMaterial(material), Body, null, new[] { Body }) { RequestedVariant = row });

    [Fact]
    public void A_row_whose_pair_the_family_does_not_allow_is_warned_not_blocked()
    {
        var outcome = Pick(new FakeRequirementReader(Requirement("B1", 0.02, "2024-O")), "Aluminum 5052-O", "5052-O_0.020");

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Equal(SheetMetalRowGateRule.RefusedCode, outcome.ReasonCode);
        Assert.Equal("pairNotAllowed", outcome.Args["refusal"]);
        Assert.Contains("will need to be re-applied", outcome.Message);
    }

    [Fact]
    public void A_row_at_a_thickness_the_family_does_not_cover_is_warned()
    {
        var outcome = Pick(new FakeRequirementReader(Requirement("B1", 0.02, "2024-O")), "Aluminum 2024-O", "2024-O_0.032", bodyThickness: 0.032);

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Equal(SheetMetalRowGateRule.RefusedCode, outcome.ReasonCode);
    }

    [Fact]
    public void A_row_the_family_allows_at_another_thickness_than_the_features_were_built_at_warns_to_reapply()
    {
        var requirements = new FakeRequirementReader(
            Requirement("B1", 0.02, SpecRow("B1", 0.02, "2024-O"), SpecRow("B1", 0.032, "2024-O")));

        Assert.Equal(RuleDecision.Allow, Pick(requirements, "Aluminum 2024-O", "2024-O_0.020").Decision);

        var outcome = Pick(requirements, "Aluminum 2024-O", "2024-O_0.032", bodyThickness: 0.032);
        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Equal(SheetMetalRowGateRule.NeedsReapplyCode, outcome.ReasonCode);
        Assert.Contains("built at 0.02", outcome.Message);
    }

    [Fact]
    public void A_body_thickness_confirmation_carries_the_requirement_warning()
    {
        var outcome = Pick(new FakeRequirementReader(Requirement("B1", 0.02, "2024-O")), "Aluminum 2024-O", "2024-O_0.032");

        Assert.Equal(RuleDecision.RequireConfirmation, outcome.Decision);
        Assert.Equal(SheetMetalRowGateRule.ThicknessMismatchCode, outcome.ReasonCode);
        Assert.Contains("will need to be re-applied", outcome.Message);
    }

    [Fact]
    public void Unreadable_requirements_block_every_row()
    {
        var outcome = Pick(new FakeRequirementReader(Array.Empty<SheetMetalRequirement>(), "NX 1: broken"), "Aluminum 2024-O", "2024-O_0.020");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(SheetMetalRowGateRule.RefusedCode, outcome.ReasonCode);
    }

    [Fact]
    public void Without_requirements_no_row_is_warned_about()
    {
        Assert.Equal(RuleDecision.Allow, Pick(new FakeRequirementReader(), "Aluminum 2024-O", "2024-O_0.020").Decision);
    }
}
