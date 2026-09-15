using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Contracts.Materials;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SyncSheetMetalPreferenceEffectRuleTests
{
    private static readonly MaterialGradeMap GradeMap = MaterialGradeMap.FromEntries(new Dictionary<string, string>
    {
        ["Aluminum 2024-O"] = "2024-O",
    });

    private static BodyInfo Body(BodyKind kind) =>
        new(new BodyId("body-1"), "BODY", kind, Volume: 0.0, Attributes: new Dictionary<string, string>());

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }),
            Array.Empty<MaterialPropertyValue>());

    private static IReadOnlyList<SideEffectInstruction> Effects(BodyKind kind, string materialName)
    {
        var body = Body(kind);
        return new SyncSheetMetalPreferenceEffectRule(GradeMap).GenerateEffects(
            new MaterialAssignmentRuleContext(MakeMaterial(materialName), body, null, new[] { body }));
    }

    [Fact]
    public void A_mapped_material_on_a_sheet_metal_body_syncs_its_grade()
    {
        var instruction = Assert.Single(Effects(BodyKind.SheetMetal, "Aluminum 2024-O"));

        Assert.Equal(SyncSheetMetalPreferenceEffectRule.InstructionType, instruction.InstructionType);
        Assert.Equal(new BodyId("body-1"), instruction.BodyId);
        Assert.Equal("2024-O", instruction.Data[SyncSheetMetalPreferenceEffectRule.GradeLabelDataKey]);
    }

    [Theory]
    [InlineData(BodyKind.Solid)]
    [InlineData(BodyKind.Sheet)]
    [InlineData(BodyKind.Unknown)]
    public void A_body_that_is_not_sheet_metal_syncs_nothing(BodyKind kind)
    {
        Assert.Empty(Effects(kind, "Aluminum 2024-O"));
    }

    [Fact]
    public void An_unmapped_material_syncs_nothing()
    {
        Assert.Empty(Effects(BodyKind.SheetMetal, "Titanium Grade 5"));
    }

    [Fact]
    public void Declares_the_instruction_type_its_executor_registers_under()
    {
        // The material engine checks every declared type has an executor; an undeclared one would slip past it.
        Assert.Equal(
            new[] { SyncSheetMetalPreferenceEffectRule.InstructionType },
            new SyncSheetMetalPreferenceEffectRule(GradeMap).InstructionTypes);
    }
}
