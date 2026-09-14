using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Contracts.Materials;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Constraints;
using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SheetMetalPreferenceConstraintProviderTests
{
    private static readonly BodyId BodyId = new("body-1");

    private static readonly BodyInfo Body =
        new(BodyId, "SM_BODY", BodyKind.SheetMetal, Volume: 0.0, Attributes: new Dictionary<string, string>());

    // "Titanium Grade 5" is deliberately unmapped.
    private static readonly MaterialGradeMap GradeMap = MaterialGradeMap.FromEntries(new Dictionary<string, string>
    {
        ["Aluminum 2024-O"] = "2024-O",
        ["Aluminum 5052-O"] = "5052-O",
        ["Aluminum 7075-T6"] = "7075-T6",
    });

    // 7075-T6 is deliberately missing from the NX standards table.
    private static SheetMetalPartPreference Preference(int sheetMetalBodies = 1) =>
        new("2024-O", IsMaterialTableEntry: true, Thickness: 0.02, new[] { "2024-O", "5052-O" }, sheetMetalBodies);

    private static SheetMetalPreferenceConstraintProvider Provider(SheetMetalPreferenceRead read) =>
        new(new FixedReader(read), GradeMap);

    private static SheetMetalPreferenceConstraintProvider Provider(int sheetMetalBodies = 1) =>
        Provider(SheetMetalPreferenceRead.Of(Preference(sheetMetalBodies)));

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }),
            Array.Empty<MaterialPropertyValue>());

    private static RuleOutcome Gate(SheetMetalPreferenceConstraintProvider provider, string materialName) =>
        new FeatureConstraintGateRule(provider).Evaluate(
            new MaterialAssignmentRuleContext(MakeMaterial(materialName), Body, null, new[] { Body }));

    [Fact]
    public void A_body_that_is_not_sheet_metal_has_no_constraints()
    {
        Assert.Empty(Provider(SheetMetalPreferenceRead.NotSheetMetal).ConstraintsFor(BodyId));
    }

    [Fact]
    public void Allows_a_material_whose_grade_is_in_the_standards_table()
    {
        Assert.Equal(RuleDecision.Allow, Gate(Provider(), "Aluminum 2024-O").Decision);
    }

    [Fact]
    public void Allows_a_grade_that_differs_from_the_current_preferences()
    {
        // The assignment's own sync brings the preferences in line, so a difference is not a reason to refuse.
        Assert.Equal(RuleDecision.Allow, Gate(Provider(), "Aluminum 5052-O").Decision);
    }

    [Fact]
    public void Blocks_an_unmapped_material()
    {
        var outcome = Gate(Provider(), "Titanium Grade 5");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(SheetMetalPreferenceConstraintProvider.GradeUnmappedCode, outcome.ReasonCode);
        Assert.Contains("material-grade-map", outcome.Message);
    }

    [Fact]
    public void Blocks_a_grade_missing_from_the_standards_table()
    {
        var outcome = Gate(Provider(), "Aluminum 7075-T6");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(SheetMetalPreferenceConstraintProvider.NotInStandardsTableCode, outcome.ReasonCode);
        Assert.Contains("7075-T6", outcome.Message);
        Assert.Contains("Sheet Metal Preferences", outcome.Message);
    }

    [Fact]
    public void A_second_sheet_metal_body_in_the_part_warns_and_allows()
    {
        var outcome = Gate(Provider(sheetMetalBodies: 2), "Aluminum 2024-O");

        Assert.Equal(RuleDecision.Warn, outcome.Decision);
        Assert.Equal(SheetMetalPreferenceConstraintProvider.SharedByBodiesCode, outcome.ReasonCode);
        Assert.Contains("2 sheet metal bodies", outcome.Message);
    }

    [Fact]
    public void The_shared_preferences_warning_never_masks_a_refusal()
    {
        Assert.Equal(RuleDecision.Block, Gate(Provider(sheetMetalBodies: 2), "Aluminum 7075-T6").Decision);
    }

    [Fact]
    public void Preferences_that_could_not_be_read_block_rather_than_count_as_unrestricted()
    {
        var provider = Provider(SheetMetalPreferenceRead.Unreadable("NX 12345: preferences unavailable"));

        var outcome = Gate(provider, "Aluminum 2024-O");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(SheetMetalPreferenceConstraintProvider.PreferencesUnreadableCode, outcome.ReasonCode);
        Assert.Contains("NX 12345", outcome.Message);
    }

    [Fact]
    public void Assignable_material_query_offers_only_materials_the_preferences_can_take()
    {
        var query = new AssignableMaterialQuery(
            new MaterialAssignmentPlanner(new[] { new FeatureConstraintGateRule(Provider()) }));

        var offered = query.ListAssignable(
            Body, null,
            new[] { "Aluminum 2024-O", "Aluminum 5052-O", "Aluminum 7075-T6", "Titanium Grade 5" }.Select(MakeMaterial));

        Assert.Equal(new[] { "Aluminum 2024-O", "Aluminum 5052-O" }, offered.Select(m => m.Name));
    }

    private sealed class FixedReader : ISheetMetalPreferenceReader
    {
        private readonly SheetMetalPreferenceRead _read;

        public FixedReader(SheetMetalPreferenceRead read) => _read = read;

        public SheetMetalPreferenceRead ReadFor(BodyId bodyId) => _read;
    }
}
