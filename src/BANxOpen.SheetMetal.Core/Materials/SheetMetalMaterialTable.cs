namespace BANxOpen.SheetMetal.Materials;

/// <summary>The MATERIAL_TABLE of NX's sheet metal material standards file — the single source of which materials Sheet
/// Metal Preferences can be set to, which physical material each is made of, which grade it is, and which Standard it
/// belongs to. It replaces the tool's own grade map and Standards registry, so the tool and NX cannot disagree.
///
/// Material, Standard and physical material names are compared ignoring case, as grade labels always have been.</summary>
public sealed class SheetMetalMaterialTable
{
    private SheetMetalMaterialTable(string? sourcePath, IReadOnlyList<SheetMetalMaterialRow> rows)
    {
        SourcePath = sourcePath;
        Rows = rows;
        Standards = rows
            .Select(r => r.Standard)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The file the table was read from. Null for a table built in memory.</summary>
    public string? SourcePath { get; }

    /// <summary>In file order.</summary>
    public IReadOnlyList<SheetMetalMaterialRow> Rows { get; }

    /// <summary>Every Standard with at least one row, in the order each first appears in the file.</summary>
    public IReadOnlyList<string> Standards { get; }

    /// <exception cref="FileNotFoundException">No file at <paramref name="path"/>.</exception>
    /// <exception cref="InvalidDataException">The file is not a valid standards file; the message names the line.</exception>
    public static SheetMetalMaterialTable Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Sheet metal material standards file not found: {path}", path);

        try
        {
            return new SheetMetalMaterialTable(path, SheetMetalMaterialTableParser.Parse(File.ReadAllBytes(path)));
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Sheet metal material standards file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>Builds a table from rows already in memory — for tests.</summary>
    public static SheetMetalMaterialTable FromRows(IEnumerable<SheetMetalMaterialRow> rows, string? sourcePath = null) =>
        new(sourcePath, rows.ToList());

    public IReadOnlyList<SheetMetalMaterialRow> RowsFor(string standard) =>
        Rows.Where(r => string.Equals(r.Standard, standard, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>The row named <paramref name="materialName"/>, or null when there is none — as for Sheet Metal
    /// Preferences set to a material the table no longer lists.</summary>
    public SheetMetalMaterialRow? Find(string? materialName) =>
        string.IsNullOrWhiteSpace(materialName)
            ? null
            : Rows.FirstOrDefault(r => string.Equals(r.Name, materialName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The row set in Sheet Metal Preferences when <paramref name="physicalMaterialName"/> is assigned without
    /// the user picking one: the first in file order. Null when the physical material has no row.
    ///
    /// Phase 1 only: the Material Assignment dialog cannot ask the user yet. Every automatic choice goes through here
    /// so the grade that is checked and the row that is set are always the same row.</summary>
    public SheetMetalMaterialRow? FirstRowForPhysicalMaterial(string physicalMaterialName) =>
        Rows.FirstOrDefault(r => string.Equals(r.PhysicalMaterialName, physicalMaterialName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The grade of <see cref="FirstRowForPhysicalMaterial"/>, or null when the physical material has no row.</summary>
    public string? GradeForPhysicalMaterial(string physicalMaterialName) =>
        FirstRowForPhysicalMaterial(physicalMaterialName)?.Grade;
}
