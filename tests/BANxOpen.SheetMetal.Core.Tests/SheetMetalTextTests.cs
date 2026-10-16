using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Contracts.Requirements;
using BANxOpen.Foundation.Core;
using BANxOpen.Materials.Contracts;
using BANxOpen.Materials.Core.Assignment;
using BANxOpen.Materials.Core.Rules.Features;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.Tests.Materials;
using static BANxOpen.SheetMetal.Tests.RequirementFixtures;

namespace BANxOpen.SheetMetal.Tests;

/// <summary>The describer is the single source of the sheet metal libraries' wording: an overriding describer handed to
/// a rule, the preference check, a constraint provider, the requirement mapper, the requirement factory or a config
/// loader is what ends up in front of the user.</summary>
public class SheetMetalTextTests
{
    private sealed class CustomText : SheetMetalText
    {
        public override string BeadThicknessMismatch(string spec, string specThickness, string sheetThickness) =>
            $"custom thickness {spec} {specThickness}/{sheetThickness}";
        public override string BeadSpecNotAllowedOnGrade(string spec, string grade) => $"custom grade {spec} {grade}";
        public override string PreferencesHaveNoMaterial => "custom no material";
        public override string PreferencesSharedByBodies(int count) => $"custom shared {count}";
        public override string SourcePreferences => "custom source";
        public override string RequirementKeyMalformed(string key) => $"custom key {key}";
        public override string RequirementNotRecordedThicknessUnknown(string featureNoun, string spec, string standard) =>
            $"custom unknown {spec}";
        public override string MaterialTableNotFound(string path) => "custom table missing";
    }

    private static readonly BeadSpecRow Row = SpecRow("B1005010-1", 0.02, "2024-O");

    [Fact]
    public void Bead_rules_word_their_outcomes_with_the_describer_and_carry_their_values()
    {
        var thickness = new ThicknessMatchRule(new CustomText())
            .Evaluate(new BeadValidationContext(new SheetMetalProfile(0.032, "2024-O"), Row));
        var grade = new MaterialAllowedRule(new CustomText())
            .Evaluate(new BeadValidationContext(new SheetMetalProfile(0.02, "5052-O"), Row));

        Assert.Equal("custom thickness B1005010-1 0.02/0.032", thickness.Message);
        Assert.Equal(SheetMetalCodes.ThicknessMismatch, thickness.ReasonCode);
        Assert.Equal("0.032", thickness.Args["sheetThickness"]);

        Assert.Equal("custom grade B1005010-1 5052-O", grade.Message);
        Assert.Equal(SheetMetalCodes.MaterialNotAllowed, grade.ReasonCode);
        Assert.Equal("5052-O", grade.Args["grade"]);
    }

    [Fact]
    public void The_preference_check_words_its_result_with_the_describer()
    {
        var preference = new SheetMetalPartPreference(null, IsMaterialTableEntry: true, Thickness: 0.02, SheetMetalBodyCount: 1, null);

        var result = SheetMetalPreferenceCheck.Evaluate("Aluminum 2024-O", 0.02, preference, new CustomText());

        Assert.Equal(SheetMetalPreferenceStatus.MaterialNotSet, result.Status);
        Assert.Equal("custom no material", result.Message);
    }

    [Fact]
    public void A_constraint_provider_words_its_violation_and_its_source_with_the_describer()
    {
        var table = TableRows.AluminumTable();
        var preference = new SheetMetalPartPreference("2024-O_0.020", IsMaterialTableEntry: true, Thickness: 0.02, 2, table.Find("2024-O_0.020"));
        var provider = new SheetMetalPreferenceConstraintProvider(
            new FixedPreferenceReader(SheetMetalPreferenceRead.Of(preference)), table, new CustomText());
        var body = new BodyInfo(new BodyId("body-1"), "SM_BODY", BodyKind.SheetMetal, 0.0, new Dictionary<string, string>());
        var material = new Material(new MaterialId("Aluminum 2024-O"), new MaterialLibraryId("Sheet Metal Materials"), "Aluminum 2024-O",
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }), Array.Empty<MaterialPropertyValue>());

        var outcome = new FeatureConstraintGateRule(provider)
            .Evaluate(new MaterialAssignmentRuleContext(material, body, null, new[] { body }));

        Assert.Equal("custom shared 2 (required by custom source)", outcome.Message);
    }

    [Fact]
    public void The_requirement_mapper_words_read_errors_with_the_describer()
    {
        var spec = new RequirementSpec(
            BeadMaterialConstraintProvider.ProviderDomainId, "no-pipe", SheetMetalRequirementNames.PairAspect,
            RequirementValueType.String, new SetOfValuesRule(new[] { "2024-O" }), RequirementSeverity.Error,
            "expr", new[] { "line" }, Array.Empty<string>());
        var read = new RequirementRead(new[] { new StoredRequirement(spec, RequirementStatus.Unknown) }, Array.Empty<string>());

        var (_, errors) = SheetMetalRequirementMapper.FromRead(read, BeadRequirementFactory.FeatureNoun, new CustomText());

        Assert.Equal("custom key no-pipe", Assert.Single(errors));
    }

    [Fact]
    public void The_requirement_factory_words_its_notes_with_the_describer()
    {
        var result = BeadRequirementFactory.Create(
            new[] { Stamped("101", "BEAD(1)", "B1005010-1", Row) }, Array.Empty<SheetMetalRequirement>(), NothingRebuilt,
            currentThickness: null, new CustomText());

        Assert.Equal("custom unknown B1005010-1", Assert.Single(result.Notes));
    }

    [Fact]
    public void A_config_loader_words_its_error_with_the_describer()
    {
        var error = Assert.Throws<FileNotFoundException>(
            () => SheetMetalMaterialTable.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt"), new CustomText()));

        Assert.Equal("custom table missing", error.Message);
    }

    [Fact]
    public void The_dialog_rule_for_a_body_with_no_material_is_worded_here()
    {
        Assert.Equal(
            "Sheet Metal Preferences have material '2024-O_0.020', made of 'Aluminum 2024-O', but this body has no material.",
            SheetMetalText.Default.PreferenceBodyHasNoMaterial("2024-O_0.020", "Aluminum 2024-O"));
    }

    [Fact]
    public void Legacy_code_constants_are_the_shared_codes()
    {
        Assert.Equal(SheetMetalCodes.MaterialGradeUnrecognized, BeadMaterialConstraintProvider.GradeUnrecognizedCode);
        Assert.Equal(SheetMetalCodes.PreferenceMaterialNotInTable, SheetMetalPreferenceConstraintProvider.NotInTableCode);
        Assert.Equal(SheetMetalCodes.RowThicknessMismatch, SheetMetalRowGateRule.ThicknessMismatchCode);
        Assert.Equal(FoundationCodes.RequirementsUnreadable, SheetMetalRequirementConstraintProvider.UnreadableCode);
    }
}
