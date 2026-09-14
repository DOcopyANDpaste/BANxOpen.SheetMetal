using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Tests.Common;

public class SheetMetalPreferenceCheckTests
{
    private static readonly string[] StandardsTable = { "2024-O", "5052-O", "7075-T6" };

    private static SheetMetalProfile Profile(string? grade = "2024-O", double thickness = 0.02) =>
        new(new BodyId("body-1"), "SM_BODY", thickness, grade);

    private static SheetMetalPartPreference Preference(
        string? material = "2024-O", bool materialTable = true, double thickness = 0.02, IReadOnlyList<string>? table = null) =>
        new(material, materialTable, thickness, table ?? StandardsTable, SheetMetalBodyCount: 1);

    private static SheetMetalPreferenceStatus StatusOf(SheetMetalProfile profile, SheetMetalPartPreference preference) =>
        SheetMetalPreferenceCheck.Evaluate(profile, preference).Status;

    [Fact]
    public void Matching_material_and_thickness_are_in_sync()
    {
        var result = SheetMetalPreferenceCheck.Evaluate(Profile(), Preference());

        Assert.Equal(SheetMetalPreferenceStatus.InSync, result.Status);
        Assert.Null(result.Message);
    }

    [Fact]
    public void A_different_preference_material_is_out_of_sync_and_names_both_materials()
    {
        var result = SheetMetalPreferenceCheck.Evaluate(Profile("2024-O"), Preference(material: "5052-O"));

        Assert.Equal(SheetMetalPreferenceStatus.MaterialOutOfSync, result.Status);
        Assert.Contains("5052-O", result.Message);
        Assert.Contains("2024-O", result.Message);
    }

    [Fact]
    public void The_right_material_outside_Material_Table_entry_is_out_of_sync()
    {
        // NX ignores the preferences' material in Value or Tool ID entry, so a matching name is not enough.
        var result = SheetMetalPreferenceCheck.Evaluate(Profile("2024-O"), Preference(material: "2024-O", materialTable: false));

        Assert.Equal(SheetMetalPreferenceStatus.MaterialOutOfSync, result.Status);
        Assert.Contains("Material Table", result.Message);
    }

    [Fact]
    public void Preferences_with_no_material_are_out_of_sync()
    {
        Assert.Equal(SheetMetalPreferenceStatus.MaterialOutOfSync, StatusOf(Profile("2024-O"), Preference(material: null)));
    }

    [Fact]
    public void A_grade_missing_from_the_standards_table_cannot_be_synced()
    {
        var result = SheetMetalPreferenceCheck.Evaluate(Profile("6061-T6"), Preference(material: "2024-O"));

        Assert.Equal(SheetMetalPreferenceStatus.MaterialNotInStandardsTable, result.Status);
        Assert.Contains("6061-T6", result.Message);
    }

    [Fact]
    public void Material_names_are_compared_without_regard_to_case()
    {
        Assert.Equal(
            SheetMetalPreferenceStatus.InSync,
            StatusOf(Profile("2024-O"), Preference(material: "2024-o", table: new[] { "2024-o" })));
    }

    [Fact]
    public void A_thickness_beyond_tolerance_is_a_mismatch()
    {
        var result = SheetMetalPreferenceCheck.Evaluate(
            Profile(thickness: 0.02), Preference(thickness: 0.02 + SheetMetalPreferenceCheck.ThicknessTolerance * 2));

        Assert.Equal(SheetMetalPreferenceStatus.ThicknessMismatch, result.Status);
        Assert.Contains("0.02", result.Message);
    }

    [Fact]
    public void A_thickness_within_tolerance_is_in_sync()
    {
        Assert.Equal(
            SheetMetalPreferenceStatus.InSync,
            StatusOf(Profile(thickness: 0.02), Preference(thickness: 0.02 + SheetMetalPreferenceCheck.ThicknessTolerance / 2)));
    }

    [Fact]
    public void Material_is_reported_before_thickness()
    {
        // Syncing the material can change the table-driven thickness, so thickness is only worth reporting once
        // the material is settled.
        Assert.Equal(
            SheetMetalPreferenceStatus.MaterialOutOfSync,
            StatusOf(Profile("2024-O", thickness: 0.02), Preference(material: "5052-O", thickness: 0.05)));
    }

    [Fact]
    public void A_body_without_material_is_checked_on_thickness_only()
    {
        Assert.Equal(SheetMetalPreferenceStatus.InSync, StatusOf(Profile(grade: null), Preference(material: "5052-O")));
        Assert.Equal(
            SheetMetalPreferenceStatus.ThicknessMismatch,
            StatusOf(Profile(grade: null, thickness: 0.02), Preference(material: "5052-O", thickness: 0.05)));
    }
}
