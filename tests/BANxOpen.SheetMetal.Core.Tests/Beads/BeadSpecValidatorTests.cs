using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Tests.Beads;

public class BeadSpecValidatorTests
{
    private static readonly BeadSpecRow ValidRow = new(
        "B1005010", "B1005010", "B1005010-1",
        RadiusAndRadS: 0.245, Width: 0.625, Height: 0.625, DieRadiusP: 0.188, Thickness: 0.02,
        AllowedMaterialGrades: new Dictionary<string, bool> { ["2024-O"] = true, ["5052-O"] = false });

    private static BeadSpecValidator MakeValidator() =>
        new(new IGateRule<BeadValidationContext, RuleOutcome>[] { new ThicknessMatchRule(), new MaterialAllowedRule() });

    [Fact]
    public void Validate_MatchingThicknessAndAllowedGrade_IsValid()
    {
        var profile = new SheetMetalProfile(Thickness: 0.02, MaterialGradeLabel: "2024-O");

        var result = MakeValidator().Validate(profile, ValidRow);

        Assert.True(result.IsValid);
        Assert.Null(result.BlockingMessage);
    }

    [Fact]
    public void Validate_ThicknessMismatch_BlocksBeforeCheckingMaterial()
    {
        var profile = new SheetMetalProfile(Thickness: 0.05, MaterialGradeLabel: "5052-O");

        var result = MakeValidator().Validate(profile, ValidRow);

        Assert.False(result.IsValid);
        Assert.Single(result.Outcomes);
        Assert.Equal("THICKNESS_MATCH", result.Outcomes[0].RuleId);
        Assert.Equal("THICKNESS_MISMATCH", result.Outcomes[0].ReasonCode);
    }

    [Fact]
    public void Validate_GradeMarkedNotAllowed_Blocks()
    {
        var profile = new SheetMetalProfile(Thickness: 0.02, MaterialGradeLabel: "5052-O");

        var result = MakeValidator().Validate(profile, ValidRow);

        Assert.False(result.IsValid);
        Assert.Equal("MATERIAL_NOT_ALLOWED", result.BlockingMessage is null ? null : result.Outcomes.Last().ReasonCode);
    }

    [Fact]
    public void Validate_GradeNotAColumnAtAll_ReportsUnrecognizedNotJustDisallowed()
    {
        var profile = new SheetMetalProfile(Thickness: 0.02, MaterialGradeLabel: "6061-T6");

        var result = MakeValidator().Validate(profile, ValidRow);

        Assert.False(result.IsValid);
        Assert.Equal("MATERIAL_GRADE_UNRECOGNIZED", result.Outcomes.Last().ReasonCode);
    }

    [Fact]
    public void Validate_NoMaterialAssigned_Blocks()
    {
        var profile = new SheetMetalProfile(Thickness: 0.02, MaterialGradeLabel: null);

        var result = MakeValidator().Validate(profile, ValidRow);

        Assert.False(result.IsValid);
        Assert.Equal("MATERIAL_MISSING", result.Outcomes.Last().ReasonCode);
    }
}
