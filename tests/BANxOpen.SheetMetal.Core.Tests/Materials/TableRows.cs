using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

/// <summary>Builds sheet metal material standards file rows for tests.</summary>
internal static class TableRows
{
    public static SheetMetalMaterialRow Row(
        string name, string physicalMaterial, string grade, double thickness = 0.02, string standard = "XX_Standard") =>
        new(name, standard, standard, physicalMaterial, grade, thickness, "0.06", LineNumber: 0,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SheetMetalMaterialColumns.Standard] = standard,
                [SheetMetalMaterialColumns.PhysicalMaterialName] = physicalMaterial,
                [SheetMetalMaterialColumns.SheetMetalMaterial] = grade,
            });

    /// <summary>"Aluminum 2024-O", "Aluminum 5052-O" and "Aluminum 7075-T6" each have a row; "Titanium Grade 5" has none.
    /// 2024-O has a second, thicker row, listed after the first.</summary>
    public static SheetMetalMaterialTable AluminumTable() => SheetMetalMaterialTable.FromRows(new[]
    {
        Row("2024-O_0.020", "Aluminum 2024-O", "2024-O"),
        Row("5052-O_0.020", "Aluminum 5052-O", "5052-O"),
        Row("2024-O_0.032", "Aluminum 2024-O", "2024-O", thickness: 0.032),
        Row("7075-T6_0.020", "Aluminum 7075-T6", "7075-T6", standard: "YY_Standard"),
    });
}
