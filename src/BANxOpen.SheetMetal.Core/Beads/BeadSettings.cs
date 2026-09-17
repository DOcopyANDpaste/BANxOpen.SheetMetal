using BANxOpen.SheetMetal.Beads.Rules;
using Newtonsoft.Json;

namespace BANxOpen.SheetMetal.Beads;

/// <summary>Tunable bead behaviour, read from <c>bead-settings.json</c> in the sheet metal config directory so
/// it can change without a rebuild:
/// <code>
/// {
///   "geometryMatchTolerance": 0.0005,
///   "parameterMapping": [
///     { "feature": "Height",    "specColumn": "H" },
///     { "feature": "Radius",    "specColumn": "R&amp;RADS" },
///     { "feature": "DieRadius", "specColumn": "RADP" },
///     { "feature": "Thickness", "specColumn": "THICKNESS" }
///   ]
/// }
/// </code>
///
/// The mapping is used in both directions — writing a SPEC's values into a bead, and identifying an unstamped
/// bead from its values — so the two can never disagree about which column drives which parameter.</summary>
public sealed class BeadSettings
{
    /// <summary>Matches the tolerance the bead dialog already uses for thickness, so a bead the tool built
    /// itself is always recognised.</summary>
    public const double DefaultGeometryMatchTolerance = ThicknessMatchRule.ToleranceInches;

    /// <summary>The mapping the tool has always used.</summary>
    public static IReadOnlyList<BeadParameterMapping> DefaultParameterMapping { get; } = new[]
    {
        new BeadParameterMapping(BeadFeatureParameter.Height, BeadSpecColumns.Height),
        new BeadParameterMapping(BeadFeatureParameter.Radius, BeadSpecColumns.RadiusAndRadS),
        new BeadParameterMapping(BeadFeatureParameter.DieRadius, BeadSpecColumns.DieRadiusP),
        new BeadParameterMapping(BeadFeatureParameter.Thickness, BeadSpecColumns.Thickness),
    };

    /// <summary>Parameters the tool writes into every bead, so each must have a column.</summary>
    private static readonly BeadFeatureParameter[] WrittenParameters =
    {
        BeadFeatureParameter.Height, BeadFeatureParameter.Radius, BeadFeatureParameter.DieRadius,
    };

    public static BeadSettings Default { get; } = new(DefaultGeometryMatchTolerance, DefaultParameterMapping);

    public BeadSettings(double geometryMatchTolerance, IReadOnlyList<BeadParameterMapping> parameterMapping)
    {
        if (!(geometryMatchTolerance > 0))
            throw new ArgumentOutOfRangeException(nameof(geometryMatchTolerance), geometryMatchTolerance,
                "geometryMatchTolerance must be greater than zero.");

        Validate(parameterMapping);

        GeometryMatchTolerance = geometryMatchTolerance;
        ParameterMapping = parameterMapping.ToList();
    }

    /// <summary>How far an unstamped bead's mapped values may each differ from a SPEC row and still be
    /// identified as that SPEC. In the SPEC workbook's units.</summary>
    public double GeometryMatchTolerance { get; }

    public IReadOnlyList<BeadParameterMapping> ParameterMapping { get; }

    /// <summary>The value of <paramref name="parameter"/> a SPEC calls for. Throws for a parameter with no
    /// mapping, which validation rules out for every written parameter.</summary>
    public double SpecValueFor(BeadSpecRow spec, BeadFeatureParameter parameter)
    {
        var mapping = ParameterMapping.FirstOrDefault(m => m.Feature == parameter)
            ?? throw new InvalidOperationException($"No SPEC column is mapped to bead parameter {parameter}.");
        return BeadSpecColumns.ValueOf(spec, mapping.SpecColumn);
    }

    /// <summary>Loads the settings file. A missing file yields <see cref="Default"/>, and a file that omits a
    /// section uses that section's default: these are tuning values with safe defaults, so an absent file
    /// should not stop either dialog opening. A file that is present but invalid is an error, since silently
    /// ignoring an edit someone made on purpose is worse than refusing to start.</summary>
    public static BeadSettings Load(string path)
    {
        if (!File.Exists(path))
            return Default;

        SettingsFile? file;
        try
        {
            // Newtonsoft matches property names case-insensitively by default, so the camelCase file above binds to
            // these PascalCase members exactly as it did before.
            file = JsonConvert.DeserializeObject<SettingsFile>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Bead settings at '{path}' are not valid JSON: {ex.Message}", ex);
        }

        if (file is null)
            throw new InvalidDataException($"Bead settings at '{path}' are empty.");

        try
        {
            var mapping = file.ParameterMapping is null
                ? DefaultParameterMapping
                : file.ParameterMapping.Select(ParseEntry).ToList();

            return new BeadSettings(file.GeometryMatchTolerance ?? DefaultGeometryMatchTolerance, mapping);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            throw new InvalidDataException($"Bead settings at '{path}': {ex.Message}", ex);
        }
    }

    private static BeadParameterMapping ParseEntry(MappingEntry entry)
    {
        if (!Enum.TryParse<BeadFeatureParameter>(entry.Feature ?? "", ignoreCase: true, out var feature))
            throw new InvalidDataException(
                $"parameterMapping feature '{entry.Feature}' is not a bead parameter. " +
                $"Known: {string.Join(", ", Enum.GetNames(typeof(BeadFeatureParameter)))}.");

        return new BeadParameterMapping(feature, entry.SpecColumn ?? "");
    }

    private static void Validate(IReadOnlyList<BeadParameterMapping> mapping)
    {
        foreach (var entry in mapping)
        {
            if (!BeadSpecColumns.IsKnown(entry.SpecColumn))
                throw new ArgumentException(
                    $"parameterMapping column '{entry.SpecColumn}' for {entry.Feature} is not a SPEC workbook column. " +
                    $"Known: {string.Join(", ", BeadSpecColumns.All)}.");
        }

        var duplicated = mapping.GroupBy(m => m.Feature).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicated.Count > 0)
            throw new ArgumentException($"parameterMapping lists {string.Join(", ", duplicated)} more than once.");

        var missing = WrittenParameters.Where(p => mapping.All(m => m.Feature != p)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException(
                $"parameterMapping must map {string.Join(", ", missing)}: the tool writes it into every bead.");
    }

    private sealed class SettingsFile
    {
        public double? GeometryMatchTolerance { get; set; }

        public List<MappingEntry>? ParameterMapping { get; set; }
    }

    private sealed class MappingEntry
    {
        public string? Feature { get; set; }

        public string? SpecColumn { get; set; }
    }
}
