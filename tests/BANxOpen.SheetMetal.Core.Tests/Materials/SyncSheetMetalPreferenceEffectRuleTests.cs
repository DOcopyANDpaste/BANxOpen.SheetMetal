using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Contracts;
using BANxOpen.Materials.Core.Assignment;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SyncSheetMetalPreferenceEffectRuleTests
{
    private static readonly SheetMetalMaterialTable Table = TableRows.AluminumTable();

    private static BodyInfo Body(BodyKind kind) =>
        new(new BodyId("body-1"), "BODY", kind, Volume: 0.0, Attributes: new Dictionary<string, string>());

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }),
            Array.Empty<MaterialPropertyValue>());

    private static IReadOnlyList<SideEffectInstruction> Effects(
        BodyKind kind, string materialName, string? pickedRow = null)
    {
        var body = Body(kind);
        var context = new MaterialAssignmentRuleContext(MakeMaterial(materialName), body, null, new[] { body })
        {
            RequestedVariant = pickedRow,
        };

        return new SyncSheetMetalPreferenceEffectRule(Table).GenerateEffects(context);
    }

    [Fact]
    public void A_sheet_metal_body_syncs_the_row_the_user_picked()
    {
        // "Aluminum 2024-O" has two rows; the second is picked, so the second is what gets set — the rule has
        // no opinion of its own about which row a material means.
        var instruction = Assert.Single(Effects(BodyKind.SheetMetal, "Aluminum 2024-O", "2024-O_0.032"));

        Assert.Equal(SyncSheetMetalPreferenceEffectRule.InstructionType, instruction.InstructionType);
        Assert.Equal(new BodyId("body-1"), instruction.BodyId);
        Assert.Equal("2024-O_0.032", instruction.Data[SyncSheetMetalPreferenceEffectRule.MaterialNameDataKey]);
    }

    [Theory]
    [InlineData(BodyKind.Solid)]
    [InlineData(BodyKind.Sheet)]
    [InlineData(BodyKind.Unknown)]
    public void A_body_that_is_not_sheet_metal_syncs_nothing(BodyKind kind)
    {
        Assert.Empty(Effects(kind, "Aluminum 2024-O", "2024-O_0.020"));
    }

    [Fact]
    public void No_picked_row_syncs_nothing_rather_than_guessing_one()
    {
        // Not a fallback to the first row in file order: which row a material means is the user's to say.
        Assert.Empty(Effects(BodyKind.SheetMetal, "Aluminum 2024-O"));
    }

    [Fact]
    public void A_picked_row_the_table_does_not_have_syncs_nothing()
    {
        Assert.Empty(Effects(BodyKind.SheetMetal, "Aluminum 2024-O", "2024-O_0.125"));
    }

    [Fact]
    public void A_picked_row_of_another_material_syncs_nothing()
    {
        // The gate rule refuses this during planning; the effect rule still must not act on it.
        Assert.Empty(Effects(BodyKind.SheetMetal, "Aluminum 2024-O", "5052-O_0.020"));
    }

    [Fact]
    public void Declares_the_instruction_type_its_executor_registers_under()
    {
        // The material engine checks every declared type has an executor; an undeclared one would slip past it.
        Assert.Equal(
            new[] { SyncSheetMetalPreferenceEffectRule.InstructionType },
            new SyncSheetMetalPreferenceEffectRule(Table).InstructionTypes);
    }
}
