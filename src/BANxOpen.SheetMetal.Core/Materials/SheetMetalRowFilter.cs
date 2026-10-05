using System.Globalization;
using BANxOpen.SheetMetal.Beads.Rules;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>The Sheet Metal Preferences filters the Material Assignment dialog offers: Standard, physical material,
/// thickness and bend radius, each one a column of the material standards file. A null filter means "any", so
/// <see cref="None"/> lets every row through.
///
/// Each dropdown's options cascade: they are the distinct values of that column among the rows the <em>other</em>
/// three filters let through, so a dropdown never offers a value that would leave nothing to pick. Its own filter is
/// left out, so the dropdown's current value stays listed and the user can switch to a sibling value.
///
/// Text compares ignoring case, like everything else read from the table. Thickness uses
/// <see cref="ThicknessMatchRule.Matches"/>, the one thickness comparison the rest of the tool uses.</summary>
public sealed record SheetMetalRowFilter(string? Standard, string? PhysicalMaterialName, double? Thickness, string? BendRadius)
{
    public static readonly SheetMetalRowFilter None = new(null, null, null, null);

    public bool IsEmpty => Standard is null && PhysicalMaterialName is null && Thickness is null && BendRadius is null;

    public bool Matches(SheetMetalMaterialRow row) =>
        MatchesStandard(row) && MatchesPhysicalMaterial(row) && MatchesThickness(row) && MatchesBendRadius(row);

    /// <summary>The rows every filter lets through, in their original order.</summary>
    public IReadOnlyList<SheetMetalMaterialRow> Apply(IEnumerable<SheetMetalMaterialRow> rows) => rows.Where(Matches).ToList();

    /// <summary>In the order each first appears, like <see cref="SheetMetalMaterialTable.Standards"/>.</summary>
    public IReadOnlyList<string> StandardOptions(IEnumerable<SheetMetalMaterialRow> rows) =>
        rows.Where(r => MatchesPhysicalMaterial(r) && MatchesThickness(r) && MatchesBendRadius(r))
            .Select(r => r.Standard)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Alphabetical.</summary>
    public IReadOnlyList<string> PhysicalMaterialOptions(IEnumerable<SheetMetalMaterialRow> rows) =>
        rows.Where(r => MatchesStandard(r) && MatchesThickness(r) && MatchesBendRadius(r))
            .Select(r => r.PhysicalMaterialName)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Ascending. Thicknesses within tolerance of one another are one option, the first one met.</summary>
    public IReadOnlyList<double> ThicknessOptions(IEnumerable<SheetMetalMaterialRow> rows)
    {
        var options = new List<double>();
        foreach (var row in rows.Where(r => MatchesStandard(r) && MatchesPhysicalMaterial(r) && MatchesBendRadius(r)))
        {
            if (!options.Any(t => ThicknessMatchRule.Matches(t, row.Thickness)))
                options.Add(row.Thickness);
        }

        options.Sort();
        return options;
    }

    /// <summary>Numeric radii ascending, then table pointers (<c>@BEND_TABLE_1</c>) alphabetically. Rows with no bend
    /// radius contribute nothing.</summary>
    public IReadOnlyList<string> BendRadiusOptions(IEnumerable<SheetMetalMaterialRow> rows) =>
        rows.Where(r => MatchesStandard(r) && MatchesPhysicalMaterial(r) && MatchesThickness(r))
            .Select(r => r.BendRadius)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => TryNumber(v) is null ? 1 : 0)
            .ThenBy(v => TryNumber(v) ?? 0)
            .ThenBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>How a thickness is shown wherever a row is put to the user.</summary>
    public static string FormatThickness(double thickness) => thickness.ToString("0.####", CultureInfo.CurrentCulture);

    private bool MatchesStandard(SheetMetalMaterialRow row) =>
        Standard is null || string.Equals(row.Standard, Standard, StringComparison.OrdinalIgnoreCase);

    private bool MatchesPhysicalMaterial(SheetMetalMaterialRow row) =>
        PhysicalMaterialName is null
        || string.Equals(row.PhysicalMaterialName, PhysicalMaterialName, StringComparison.OrdinalIgnoreCase);

    private bool MatchesThickness(SheetMetalMaterialRow row) => Thickness is not { } t || ThicknessMatchRule.Matches(row.Thickness, t);

    private bool MatchesBendRadius(SheetMetalMaterialRow row) =>
        BendRadius is null || string.Equals(row.BendRadius, BendRadius, StringComparison.OrdinalIgnoreCase);

    private static double? TryNumber(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
