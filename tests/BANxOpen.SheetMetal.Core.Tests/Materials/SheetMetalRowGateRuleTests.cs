using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Contracts;
using BANxOpen.Materials.Core.Assignment;
using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SheetMetalRowGateRuleTests
{
    private static readonly BodyId BodyId = new("body-1");

    // Under XX_Standard, "Aluminum 2024-O" has two rows (0.020 and 0.032) and "Aluminum 5052-O" has one. YY_Standard
    // adds a third 2024-O row.
    private static readonly SheetMetalMaterialTable Table = SheetMetalMaterialTable.FromRows(
        TableRows.AluminumTable().Rows.Append(
            TableRows.Row("2024-O_0.020_YY", "Aluminum 2024-O", "2024-O", standard: "YY_Standard")));

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }),
            Array.Empty<MaterialPropertyValue>());

    private static SheetMetalPreferenceRead Read(double? bodyThickness = 0.02) =>
        SheetMetalPreferenceRead.Of(
            new SheetMetalPartPreference(null, IsMaterialTableEntry: true, Thickness: 0.02, SheetMetalBodyCount: 1, null),
            bodyThickness);

    /// <param name="standard">The Standard the dialog chose; null is a dialog with no Standard picker.</param>
    private static RuleOutcome Pick(
        string materialName, string? row, SheetMetalPreferenceRead? read = null, BodyKind kind = BodyKind.SheetMetal,
        string? standard = "XX_Standard")
    {
        var body = new BodyInfo(BodyId, "SM_BODY", kind, Volume: 0.0, Attributes: new Dictionary<string, string>());
        var rule = new SheetMetalRowGateRule(new FixedReader(read ?? Read()), new SheetMetalStandardSelection(Table) { Selected = standard });
        return rule.Evaluate(new MaterialAssignmentRuleContext(MakeMaterial(materialName), body, null, new[] { body })
        {
            RequestedVariant = row,
        });
    }

    [Fact]
    public void A_body_that_is_not_sheet_metal_is_not_checked()
    {
        Assert.Equal(RuleDecision.Allow, Pick("Aluminum 2024-O", "nonsense", kind: BodyKind.Solid).Decision);
    }

    [Fact]
    public void No_picked_row_is_not_checked()
    {
        // Material lists (AssignableMaterialQuery) plan without a row; the dialog refuses sheet metal bodies with none.
        Assert.Equal(RuleDecision.Allow, Pick("Aluminum 2024-O", row: null).Decision);
    }

    [Fact]
    public void A_row_matching_the_bodys_thickness_is_allowed()
    {
        Assert.Equal(RuleDecision.Allow, Pick("Aluminum 2024-O", "2024-O_0.020").Decision);
    }

    [Fact]
    public void A_row_at_a_different_thickness_from_the_body_needs_confirming()
    {
        var outcome = Pick("Aluminum 2024-O", "2024-O_0.032");

        Assert.Equal(RuleDecision.RequireConfirmation, outcome.Decision);
        Assert.Equal(SheetMetalRowGateRule.ThicknessMismatchCode, outcome.ReasonCode);
        Assert.Contains("0.032", outcome.Message);
        Assert.Contains("0.02", outcome.Message);
    }

    [Fact]
    public void An_unreadable_body_thickness_confirms_nothing()
    {
        // Nothing is known to disagree with.
        Assert.Equal(RuleDecision.Allow, Pick("Aluminum 2024-O", "2024-O_0.032", Read(bodyThickness: null)).Decision);
    }

    [Fact]
    public void Unreadable_preferences_are_left_to_the_preference_constraint()
    {
        Assert.Equal(RuleDecision.Allow, Pick("Aluminum 2024-O", "2024-O_0.032", SheetMetalPreferenceRead.Unreadable("NX 1: broken")).Decision);
    }

    [Fact]
    public void A_row_of_another_material_is_blocked()
    {
        var outcome = Pick("Aluminum 2024-O", "5052-O_0.020");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Equal(SheetMetalRowGateRule.NotOfferedCode, outcome.ReasonCode);
    }

    [Fact]
    public void A_row_the_table_does_not_have_is_blocked()
    {
        Assert.Equal(RuleDecision.Block, Pick("Aluminum 2024-O", "2024-O_0.125").Decision);
    }

    [Fact]
    public void A_row_of_another_Standard_is_blocked()
    {
        var outcome = Pick("Aluminum 2024-O", "2024-O_0.020_YY");

        Assert.Equal(RuleDecision.Block, outcome.Decision);
        Assert.Contains("XX_Standard", outcome.Message);
    }

    [Fact]
    public void With_no_Standard_chosen_every_Standards_rows_are_accepted()
    {
        Assert.Equal(RuleDecision.Allow, Pick("Aluminum 2024-O", "2024-O_0.020_YY", standard: null).Decision);
    }

    private sealed class FixedReader : ISheetMetalPreferenceReader
    {
        private readonly SheetMetalPreferenceRead _read;

        public FixedReader(SheetMetalPreferenceRead read) => _read = read;

        public SheetMetalPreferenceRead ReadFor(BodyId bodyId) => _read;
    }
}
