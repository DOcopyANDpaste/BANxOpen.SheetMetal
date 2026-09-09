using System.Text.Json;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Maps an NX <c>PhysicalMaterial</c> name to the short grade label used in the SPEC workbooks'
/// "Allowed Material" columns (e.g. "2024-O"). NX material library names won't reliably equal the
/// workbook's grade codes, so this is a small user-editable JSON file rather than an assumed match:
/// <code>
/// { "Aluminum 2024, Temper O": "2024-O", "Aluminum 7075, Temper T6": "7075-T6" }
/// </code></summary>
public sealed class MaterialGradeMap
{
    private readonly IReadOnlyDictionary<string, string> _map;

    private MaterialGradeMap(IReadOnlyDictionary<string, string> map) => _map = map;

    public static MaterialGradeMap Load(string mapPath)
    {
        if (!File.Exists(mapPath))
            throw new FileNotFoundException($"Material grade map not found: {mapPath}", mapPath);

        using var stream = File.OpenRead(mapPath);
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Material grade map at '{mapPath}' is empty or invalid.");

        return new MaterialGradeMap(entries);
    }

    /// <summary>Null when the NX material name has no entry — the caller (SheetMetalProfileReader) surfaces
    /// that as "material grade not recognized" rather than guessing.</summary>
    public string? GradeFor(string nxPhysicalMaterialName) =>
        _map.TryGetValue(nxPhysicalMaterialName, out var grade) ? grade : null;
}
