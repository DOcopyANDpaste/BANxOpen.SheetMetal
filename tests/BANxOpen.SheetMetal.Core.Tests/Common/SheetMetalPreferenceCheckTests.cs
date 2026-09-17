using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Tests.Materials;

namespace BANxOpen.SheetMetal.Tests.Common;

public class SheetMetalPreferenceCheckTests
{
    private static readonly SheetMetalMaterialTable Table = TableRows.AluminumTable();

    private static SheetMetalPartPreference Preference(string? material = "2024-O_0.020", bool materialTable = true, double thickness = 0.02) =>
        new(material, materialTable, thickness, SheetMetalBodyCount: 1, Table.Find(material));

    private static SheetMetalPreferenceCheckResult Evaluate(
        string? bodyMaterial = "Aluminum 2024-O", double bodyThickness = 0.02, SheetMetalPartPreference? preference = null) =>
        SheetMetalPreferenceCheck.Evaluate(bodyMaterial, bodyThickness, preference ?? Preference());

    [Fact]
    public void A_row_of_the_bodys_material_at_its_thickness_is_in_sync()
    {
        var result = Evaluate();

        Assert.Equal(SheetMetalPreferenceStatus.InSync, result.Status);
        Assert.Null(result.Message);
    }

    [Fact]
    public void A_row_made_of_a_different_material_is_out_of_sync_and_names_both_materials()
    {
        var result = Evaluate(preference: Preference("5052-O_0.020"));

        Assert.Equal(SheetMetalPreferenceStatus.MaterialOutOfSync, result.Status);
        Assert.Contains("Aluminum 5052-O", result.Message);
        Assert.Contains("Aluminum 2024-O", result.Message);
    }

    [Fact]
    public void The_right_material_outside_Material_Table_entry_is_not_set()
    {
        // NX ignores the preferences' material in Value or Tool ID entry, so a matching name is not enough.
        var result = Evaluate(preference: Preference(materialTable: false));

        Assert.Equal(SheetMetalPreferenceStatus.MaterialNotSet, result.Status);
        Assert.Contains("Material Table", result.Message);
    }

    [Fact]
    public void Preferences_with_no_material_are_not_set()
    {
        Assert.Equal(SheetMetalPreferenceStatus.MaterialNotSet, Evaluate(preference: Preference(material: null)).Status);
    }

    [Fact]
    public void A_material_the_standards_file_does_not_list_is_reported_by_name()
    {
        var result = Evaluate(preference: Preference(material: "Retired_Material"));

        Assert.Equal(SheetMetalPreferenceStatus.MaterialNotInTable, result.Status);
        Assert.Contains("Retired_Material", result.Message);
    }

    [Fact]
    public void Physical_material_names_are_compared_without_regard_to_case()
    {
        Assert.Equal(SheetMetalPreferenceStatus.InSync, Evaluate(bodyMaterial: "ALUMINUM 2024-o").Status);
    }

    [Fact]
    public void A_thickness_beyond_tolerance_is_a_mismatch()
    {
        var result = Evaluate(
            bodyThickness: 0.02, preference: Preference(thickness: 0.02 + SheetMetalPreferenceCheck.ThicknessTolerance * 2));

        Assert.Equal(SheetMetalPreferenceStatus.ThicknessMismatch, result.Status);
        Assert.Contains("0.02", result.Message);
    }

    [Fact]
    public void A_thickness_within_tolerance_is_in_sync()
    {
        Assert.Equal(
            SheetMetalPreferenceStatus.InSync,
            Evaluate(bodyThickness: 0.02, preference: Preference(thickness: 0.02 + SheetMetalPreferenceCheck.ThicknessTolerance / 2)).Status);
    }

    [Fact]
    public void Material_is_reported_before_thickness()
    {
        // NX fills the thickness from the material's row, so thickness is only worth reporting once the material is
        // settled.
        Assert.Equal(
            SheetMetalPreferenceStatus.MaterialOutOfSync,
            Evaluate(bodyThickness: 0.02, preference: Preference("5052-O_0.020", thickness: 0.05)).Status);
    }

    [Fact]
    public void A_body_without_material_is_checked_on_thickness_only()
    {
        Assert.Equal(SheetMetalPreferenceStatus.InSync, Evaluate(bodyMaterial: null, preference: Preference("5052-O_0.020")).Status);
        Assert.Equal(
            SheetMetalPreferenceStatus.ThicknessMismatch,
            Evaluate(bodyMaterial: null, bodyThickness: 0.02, preference: Preference("5052-O_0.020", thickness: 0.05)).Status);
    }
}
