using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Tests.Beads;

public class BeadSpecFinderTests
{
    private static BeadSpecRow Row(string specId, double thickness, bool allowed2024O) => new(
        "B1005010", "B1005010", specId,
        RadiusAndRadS: 0.245, Width: 0.625, Height: 0.625, DieRadiusP: 0.188, Thickness: thickness,
        AllowedMaterialGrades: new Dictionary<string, bool> { ["2024-O"] = allowed2024O });

    [Fact]
    public void FindValid_ReturnsOnlyRowsThatPassValidation()
    {
        var validator = new BeadSpecValidator(
            new IGateRule<BeadValidationContext, RuleOutcome>[] { new ThicknessMatchRule(), new MaterialAllowedRule() });
        var finder = new BeadSpecFinder(validator);
        var profile = new SheetMetalProfile(new BodyId("body-1"), "SM_BODY", Thickness: 0.02, MaterialGradeLabel: "2024-O");

        var rows = new[]
        {
            Row("B1005010-1", 0.02, allowed2024O: true),   // matches thickness + allowed -> valid
            Row("B1005010-2", 0.04, allowed2024O: true),   // wrong thickness -> invalid
            Row("B1005010-3", 0.02, allowed2024O: false),  // not allowed on this grade -> invalid
        };

        var valid = finder.FindValid(profile, rows);

        Assert.Single(valid);
        Assert.Equal("B1005010-1", valid[0].SpecId);
    }
}
