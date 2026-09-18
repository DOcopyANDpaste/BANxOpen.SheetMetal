using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Contracts.Materials;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Assignment.Choices;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class SheetMetalRowChoiceProviderTests
{
    private static readonly BodyId BodyId = new("body-1");

    // Under XX_Standard, "Aluminum 2024-O" has two rows (0.020 and 0.032) and "Aluminum 5052-O" has one. YY_Standard
    // adds a third 2024-O row. "Titanium Grade 5" has none anywhere.
    private static readonly SheetMetalMaterialTable Table = SheetMetalMaterialTable.FromRows(
        TableRows.AluminumTable().Rows.Append(
            TableRows.Row("2024-O_0.020_YY", "Aluminum 2024-O", "2024-O", standard: "YY_Standard")));

    private static BodyInfo Body(BodyKind kind = BodyKind.SheetMetal) =>
        new(BodyId, "SM_BODY", kind, Volume: 0.0, Attributes: new Dictionary<string, string>());

    private static Material MakeMaterial(string name) =>
        new(new MaterialId(name), new MaterialLibraryId("Sheet Metal Materials"), name,
            new MaterialCategory("al", "Aluminum", new[] { "Aluminum" }),
            Array.Empty<MaterialPropertyValue>());

    /// <param name="preferenceRow">The row the part's Sheet Metal Preferences are set to, or null for none.</param>
    private static SheetMetalPreferenceRead Read(string? preferenceRow = null, double? bodyThickness = 0.02) =>
        SheetMetalPreferenceRead.Of(
            new SheetMetalPartPreference(
                preferenceRow,
                IsMaterialTableEntry: true,
                Thickness: 0.02,
                SheetMetalBodyCount: 1,
                Table.Find(preferenceRow)),
            bodyThickness);

    /// <param name="standard">The Standard the dialog chose. Most tests pick XX_Standard, as the Material Assignment
    /// dialog always selects one; null is the bead dialog, which has no picker.</param>
    private static AssignmentChoice? Choose(
        string materialName, SheetMetalPreferenceRead? read = null, BodyKind kind = BodyKind.SheetMetal,
        string? standard = "XX_Standard")
    {
        var body = Body(kind);
        return Provider(read ?? Read(), standard)
            .ChoiceFor(new MaterialAssignmentRuleContext(MakeMaterial(materialName), body, null, new[] { body }));
    }

    private static SheetMetalRowChoiceProvider Provider(SheetMetalPreferenceRead read, string? standard = "XX_Standard") =>
        new(new FixedReader(read), new SheetMetalStandardSelection(Table) { Selected = standard });

    [Fact]
    public void A_body_that_is_not_sheet_metal_is_asked_nothing()
    {
        Assert.Null(Choose("Aluminum 2024-O", kind: BodyKind.Solid));
    }

    [Fact]
    public void A_material_with_no_row_is_asked_nothing()
    {
        // The preference constraint blocks such an assignment before a choice would ever be collected.
        Assert.Null(Choose("Titanium Grade 5"));
    }

    [Fact]
    public void Preferences_that_could_not_be_read_are_asked_nothing()
    {
        Assert.Null(Choose("Aluminum 2024-O", SheetMetalPreferenceRead.Unreadable("NX 12345: unavailable")));
    }

    [Fact]
    public void A_material_with_one_row_is_auto_selected()
    {
        var choice = Choose("Aluminum 5052-O");

        Assert.NotNull(choice);
        Assert.Equal("5052-O_0.020", choice!.Auto?.OptionId);
        Assert.Contains("5052-O_0.020", choice.Auto!.Message);
    }

    [Fact]
    public void Preferences_already_set_to_a_row_of_this_material_are_kept()
    {
        // 2024-O has two rows, so this would otherwise be a question. The part is already set up for the
        // material, and re-applying it should not re-open a decision the user has made.
        var choice = Choose("Aluminum 2024-O", Read(preferenceRow: "2024-O_0.032", bodyThickness: 0.032));

        Assert.NotNull(choice);
        Assert.Equal("2024-O_0.032", choice!.Auto?.OptionId);
        Assert.Contains("already set", choice.Auto!.Message);
    }

    [Fact]
    public void Preferences_at_a_different_thickness_from_the_body_are_asked_again()
    {
        var choice = Choose("Aluminum 2024-O", Read(preferenceRow: "2024-O_0.032", bodyThickness: 0.02));

        Assert.NotNull(choice);
        Assert.Null(choice!.Auto);
    }

    [Fact]
    public void Preferences_set_to_a_row_of_another_Standard_are_asked_again()
    {
        var choice = Choose("Aluminum 2024-O", Read(preferenceRow: "2024-O_0.020_YY"));

        Assert.NotNull(choice);
        Assert.Null(choice!.Auto);
    }

    [Fact]
    public void Only_rows_of_the_chosen_Standard_are_offered()
    {
        var choice = Choose("Aluminum 2024-O", standard: "YY_Standard");

        // YY_Standard has one 2024-O row at the body's thickness, so it is simply taken.
        Assert.Equal("2024-O_0.020_YY", choice!.Auto?.OptionId);
        Assert.Contains("YY_Standard", choice.Auto!.Message);
    }

    [Fact]
    public void With_no_Standard_chosen_every_Standards_rows_are_offered()
    {
        var choice = Choose("Aluminum 2024-O", standard: null);

        Assert.Equal(new[] { "2024-O_0.020", "2024-O_0.032", "2024-O_0.020_YY" }, choice!.Options.Select(o => o.OptionId));
    }

    [Fact]
    public void A_material_with_no_row_under_the_chosen_Standard_is_asked_nothing()
    {
        // The dialog refuses such an assignment before a choice would ever be collected.
        Assert.Null(Choose("Aluminum 5052-O", standard: "YY_Standard"));
    }

    [Fact]
    public void A_single_row_at_a_different_thickness_is_still_put_to_the_user()
    {
        // Taking it silently would skip the option's thickness confirmation.
        var choice = Choose("Aluminum 5052-O", Read(bodyThickness: 0.05));

        Assert.NotNull(choice);
        Assert.Null(choice!.Auto);
        Assert.NotNull(Assert.Single(choice.Options).ConfirmationPrompt);
    }

    [Fact]
    public void Preferences_set_to_a_row_of_a_different_material_do_not_settle_the_question()
    {
        var choice = Choose("Aluminum 2024-O", Read(preferenceRow: "5052-O_0.020"));

        Assert.NotNull(choice);
        Assert.Null(choice!.Auto);
    }

    [Fact]
    public void A_material_with_several_rows_offers_all_of_them()
    {
        var choice = Choose("Aluminum 2024-O");

        Assert.NotNull(choice);
        Assert.Null(choice!.Auto);
        Assert.Equal(new[] { "2024-O_0.020", "2024-O_0.032" }, choice.Options.Select(o => o.OptionId));
        Assert.Equal(SheetMetalRowChoiceProvider.ChoiceIdentifier, choice.ChoiceId);
    }

    [Fact]
    public void Rows_matching_the_bodys_thickness_are_preferred_and_the_rest_need_confirming()
    {
        var choice = Choose("Aluminum 2024-O", Read(bodyThickness: 0.02))!;

        var matching = choice.Find("2024-O_0.020")!;
        Assert.True(matching.IsPreferred);
        Assert.Null(matching.ConfirmationPrompt);

        var other = choice.Find("2024-O_0.032")!;
        Assert.False(other.IsPreferred);
        Assert.Contains("0.032", other.ConfirmationPrompt);
        Assert.Contains("0.02", other.ConfirmationPrompt);
    }

    [Fact]
    public void The_filter_is_offered_only_when_it_would_hide_something()
    {
        // 0.05 matches neither row, so every row is non-preferred and filtering would empty the list.
        var noneMatch = Choose("Aluminum 2024-O", Read(bodyThickness: 0.05))!;
        Assert.All(noneMatch.Options, o => Assert.False(o.IsPreferred));
        Assert.NotNull(noneMatch.PreferredOnlyLabel);

        var someMatch = Choose("Aluminum 2024-O", Read(bodyThickness: 0.02))!;
        Assert.NotNull(someMatch.PreferredOnlyLabel);
    }

    [Fact]
    public void An_unreadable_body_thickness_prefers_no_row_and_confirms_none()
    {
        var choice = Choose("Aluminum 2024-O", Read(bodyThickness: null))!;

        Assert.All(choice.Options, o => Assert.False(o.IsPreferred));

        // Nothing is known to disagree with, so prompting on every row would be noise.
        Assert.All(choice.Options, o => Assert.Null(o.ConfirmationPrompt));
        Assert.Contains("would not report", choice.Prompt);
    }

    [Fact]
    public void Every_sheet_metal_body_shares_one_question()
    {
        // NX keeps one set of Sheet Metal Preferences per part, so two bodies must not be asked separately.
        var first = new BodyInfo(new BodyId("a"), "A", BodyKind.SheetMetal, 0.0, new Dictionary<string, string>());
        var second = new BodyInfo(new BodyId("b"), "B", BodyKind.SheetMetal, 0.0, new Dictionary<string, string>());
        var provider = Provider(Read());

        var bodies = new[] { first, second };
        var choices = bodies
            .Select(b => provider.ChoiceFor(
                new MaterialAssignmentRuleContext(MakeMaterial("Aluminum 2024-O"), b, null, bodies))!)
            .ToList();

        Assert.Equal(choices[0].GroupKey, choices[1].GroupKey);
    }

    private sealed class FixedReader : ISheetMetalPreferenceReader
    {
        private readonly SheetMetalPreferenceRead _read;

        public FixedReader(SheetMetalPreferenceRead read) => _read = read;

        public SheetMetalPreferenceRead ReadFor(BodyId bodyId) => _read;
    }
}
