namespace BANxOpen.SheetMetal.Beads;

/// <summary>A value that exists on a bead in the model.
/// Height, Radius and DieRadius are bead builder parameters the tool writes; Thickness belongs to the sheet
/// the bead sits on, so it is compared when identifying a bead but never written.</summary>
public enum BeadFeatureParameter
{
    Height,
    Radius,
    DieRadius,
    Thickness,
}

/// <summary>The SPEC workbook's driving numeric columns, by the canonical header names the workbook parser
/// recognises (<see cref="SpecData.ExcelBeadSpecParser"/>). Configuration refers to columns by these names,
/// so a mapping reads the same vocabulary as the workbook.</summary>
public static class BeadSpecColumns
{
    public const string RadiusAndRadS = "R&RADS";
    public const string Width = "W";
    public const string Height = "H";
    public const string DieRadiusP = "RADP";
    public const string Thickness = "THICKNESS";

    public static IReadOnlyList<string> All { get; } = new[] { RadiusAndRadS, Width, Height, DieRadiusP, Thickness };

    /// <summary>Compares the way the workbook parser does: case and spaces ignored, so "Rad P" and "RADP" are
    /// the same column.</summary>
    public static string Normalize(string column) => column.Replace(" ", "").ToUpperInvariant();

    public static bool IsKnown(string column) => All.Contains(Normalize(column));

    public static double ValueOf(BeadSpecRow row, string column) => Normalize(column) switch
    {
        RadiusAndRadS => row.RadiusAndRadS,
        Width => row.Width,
        Height => row.Height,
        DieRadiusP => row.DieRadiusP,
        Thickness => row.Thickness,
        _ => throw new ArgumentException($"'{column}' is not a SPEC workbook column. Known: {string.Join(", ", All)}.", nameof(column)),
    };
}

/// <summary>Which SPEC column supplies one bead feature parameter.</summary>
public sealed record BeadParameterMapping(BeadFeatureParameter Feature, string SpecColumn);
