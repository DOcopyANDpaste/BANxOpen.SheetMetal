using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.Tests.Beads;

public class BeadSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "BANxOpenBeadSettingsTests_" + Guid.NewGuid().ToString("N"));

    public BeadSettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "bead-settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void A_missing_file_yields_the_defaults()
    {
        var settings = BeadSettings.Load(Path.Combine(_dir, "absent.json"));

        Assert.Equal(BeadSettings.DefaultGeometryMatchTolerance, settings.GeometryMatchTolerance);
        Assert.Equal(BeadSettings.DefaultParameterMapping, settings.ParameterMapping);
    }

    [Fact]
    public void The_shipped_config_file_loads_and_matches_the_defaults()
    {
        // Guards the file in config\ against drifting from what the code assumes.
        var shipped = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "bead-settings.json"));

        var settings = BeadSettings.Load(shipped);

        Assert.True(File.Exists(shipped), $"expected shipped config at {shipped}");
        Assert.Equal(BeadSettings.DefaultGeometryMatchTolerance, settings.GeometryMatchTolerance);
        Assert.Equal(BeadSettings.DefaultParameterMapping, settings.ParameterMapping);
    }

    [Fact]
    public void Reads_tolerance_and_mapping()
    {
        var path = Write("""
            {
              "geometryMatchTolerance": 0.002,
              "parameterMapping": [
                { "feature": "height", "specColumn": "h" },
                { "feature": "Radius", "specColumn": "R & RADS" },
                { "feature": "DieRadius", "specColumn": "RAD P" }
              ]
            }
            """);

        var settings = BeadSettings.Load(path);

        Assert.Equal(0.002, settings.GeometryMatchTolerance);
        Assert.Equal(3, settings.ParameterMapping.Count);
        Assert.DoesNotContain(settings.ParameterMapping, m => m.Feature == BeadFeatureParameter.Thickness);
    }

    [Fact]
    public void An_omitted_section_takes_its_default()
    {
        var settings = BeadSettings.Load(Write("""{ "geometryMatchTolerance": 0.001 }"""));

        Assert.Equal(0.001, settings.GeometryMatchTolerance);
        Assert.Equal(BeadSettings.DefaultParameterMapping, settings.ParameterMapping);
    }

    [Theory]
    [InlineData("""{ "geometryMatchTolerance": 0 }""", "greater than zero")]
    [InlineData("""{ "geometryMatchTolerance": -1 }""", "greater than zero")]
    [InlineData("""{ "parameterMapping": [ { "feature": "Depth", "specColumn": "H" } ] }""", "not a bead parameter")]
    [InlineData("""{ "parameterMapping": [ { "feature": "Height", "specColumn": "HEIGHT" }, { "feature": "Radius", "specColumn": "R&RADS" }, { "feature": "DieRadius", "specColumn": "RADP" } ] }""", "not a SPEC workbook column")]
    [InlineData("""{ "parameterMapping": [ { "feature": "Height", "specColumn": "H" }, { "feature": "Radius", "specColumn": "R&RADS" } ] }""", "must map DieRadius")]
    [InlineData("""{ "parameterMapping": [ { "feature": "Height", "specColumn": "H" }, { "feature": "Height", "specColumn": "W" }, { "feature": "Radius", "specColumn": "R&RADS" }, { "feature": "DieRadius", "specColumn": "RADP" } ] }""", "more than once")]
    [InlineData("not json", "not valid JSON")]
    public void An_invalid_file_is_rejected_with_a_message_saying_why(string json, string expected)
    {
        var ex = Assert.Throws<InvalidDataException>(() => BeadSettings.Load(Write(json)));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void SpecValueFor_reads_the_configured_column()
    {
        var row = new BeadSpecRow("S", "S-1", RadiusAndRadS: 0.2, Width: 0.9, Height: 0.6, DieRadiusP: 0.1,
            Thickness: 0.02, AllowedMaterialGrades: new Dictionary<string, bool>());

        Assert.Equal(0.6, BeadSettings.Default.SpecValueFor(row, BeadFeatureParameter.Height));
        Assert.Equal(0.2, BeadSettings.Default.SpecValueFor(row, BeadFeatureParameter.Radius));
        Assert.Equal(0.1, BeadSettings.Default.SpecValueFor(row, BeadFeatureParameter.DieRadius));
    }
}
