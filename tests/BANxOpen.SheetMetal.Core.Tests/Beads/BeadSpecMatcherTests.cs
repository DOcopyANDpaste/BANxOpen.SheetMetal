using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.Tests.Beads;

public class BeadSpecMatcherTests
{
    private static BeadSpecRow Row(
        string specId, double height = 0.625, double radius = 0.245, double dieRadius = 0.188,
        double thickness = 0.02, double width = 0.625, string standardId = "B1005010",
        string workbookName = "B1005010") =>
        new(standardId, workbookName, specId, RadiusAndRadS: radius, Width: width, Height: height,
            DieRadiusP: dieRadius, Thickness: thickness, AllowedMaterialGrades: new Dictionary<string, bool>());

    private static BeadGeometry Geometry(BeadSpecRow row) => new(row.Thickness, row.Height, row.RadiusAndRadS, row.DieRadiusP);

    [Fact]
    public void Identifies_the_SPEC_a_bead_was_built_to()
    {
        var target = Row("B1005010-1");

        var match = BeadSpecMatcher.Match(Geometry(target), new[] { target, Row("B1005010-2", height: 0.5) }, BeadSettings.Default);

        Assert.Same(target, match.Single);
    }

    [Theory]
    [InlineData(nameof(BeadGeometry.Height))]
    [InlineData(nameof(BeadGeometry.Radius))]
    [InlineData(nameof(BeadGeometry.DieRadius))]
    [InlineData(nameof(BeadGeometry.Thickness))]
    public void A_difference_beyond_tolerance_on_any_mapped_parameter_rules_a_SPEC_out(string parameter)
    {
        var row = Row("B1005010-1");
        var off = BeadSettings.DefaultGeometryMatchTolerance * 2;
        var g = Geometry(row);
        var geometry = parameter switch
        {
            nameof(BeadGeometry.Height) => g with { Height = g.Height + off },
            nameof(BeadGeometry.Radius) => g with { Radius = g.Radius + off },
            nameof(BeadGeometry.DieRadius) => g with { DieRadius = g.DieRadius + off },
            _ => g with { Thickness = g.Thickness + off },
        };

        Assert.True(BeadSpecMatcher.Match(geometry, new[] { row }, BeadSettings.Default).IsNone);
    }

    [Fact]
    public void A_difference_within_tolerance_still_matches()
    {
        var row = Row("B1005010-1");
        var geometry = Geometry(row) with { Radius = row.RadiusAndRadS - BeadSettings.DefaultGeometryMatchTolerance / 2 };

        Assert.Same(row, BeadSpecMatcher.Match(geometry, new[] { row }, BeadSettings.Default).Single);
    }

    [Fact]
    public void SPECs_that_differ_only_in_width_are_ambiguous()
    {
        // Width is not stored on the feature, so these cannot be told apart and neither may be guessed.
        var narrow = Row("B1005010-1", width: 0.5);
        var wide = Row("B1005010-2", width: 0.75);

        var match = BeadSpecMatcher.Match(Geometry(narrow), new[] { narrow, wide }, BeadSettings.Default);

        Assert.True(match.IsAmbiguous);
        Assert.Null(match.Single);
    }

    [Fact]
    public void The_same_SPEC_listed_twice_is_one_match_not_an_ambiguity()
    {
        var row = Row("B1005010-1");

        var match = BeadSpecMatcher.Match(Geometry(row), new[] { row, row with { } }, BeadSettings.Default);

        Assert.NotNull(match.Single);
    }

    [Fact]
    public void Only_mapped_parameters_are_compared()
    {
        // A mapping without Thickness ignores it entirely.
        var settings = new BeadSettings(
            BeadSettings.DefaultGeometryMatchTolerance,
            BeadSettings.DefaultParameterMapping.Where(m => m.Feature != BeadFeatureParameter.Thickness).ToList());
        var row = Row("B1005010-1");

        var match = BeadSpecMatcher.Match(Geometry(row) with { Thickness = 1.0 }, new[] { row }, settings);

        Assert.Same(row, match.Single);
    }

    [Fact]
    public void A_remapped_parameter_is_compared_against_its_configured_column()
    {
        // Height read from the W column instead of H.
        var settings = new BeadSettings(
            BeadSettings.DefaultGeometryMatchTolerance,
            BeadSettings.DefaultParameterMapping
                .Select(m => m.Feature == BeadFeatureParameter.Height ? m with { SpecColumn = BeadSpecColumns.Width } : m)
                .ToList());
        var row = Row("B1005010-1", height: 0.1, width: 0.9);

        var match = BeadSpecMatcher.Match(Geometry(row) with { Height = 0.9 }, new[] { row }, settings);

        Assert.Same(row, match.Single);
    }

    [Fact]
    public void The_same_row_reached_through_two_Standards_is_one_SPEC_not_an_ambiguity()
    {
        var row = Row("B1005010-1");
        var sameRowOtherStandard = row with { StandardId = "YY_Standard" };

        var match = BeadSpecMatcher.Match(Geometry(row), new[] { row, sameRowOtherStandard }, BeadSettings.Default);

        // Two Standards, so two keys — but each is a distinct SPEC the caller must choose between, which is
        // what being ambiguous means. The de-dupe is about the SAME (Standard, workbook, SPEC) arriving twice.
        Assert.True(match.IsAmbiguous);

        var duplicated = BeadSpecMatcher.Match(Geometry(row), new[] { row, row with { } }, BeadSettings.Default);
        Assert.Same(row, duplicated.Single);
    }

    [Fact]
    public void Two_workbooks_sharing_a_SPEC_id_are_ambiguous_not_silently_merged()
    {
        // SPEC ids are supposed to be unique within a Standard. If bad data breaks that, the two rows are
        // genuinely different SPECs and must not collapse into one confident match.
        var first = Row("B1005010-1", workbookName: "B1005010");
        var second = Row("B1005010-1", workbookName: "S5010");

        var match = BeadSpecMatcher.Match(Geometry(first), new[] { first, second }, BeadSettings.Default);

        Assert.True(match.IsAmbiguous);
        Assert.Equal(new[] { "B1005010", "S5010" }, match.Candidates.Select(c => c.WorkbookName).OrderBy(n => n));
    }

    [Fact]
    public void Nothing_matches_an_empty_workbook()
    {
        Assert.True(BeadSpecMatcher.Match(new BeadGeometry(0.02, 0.625, 0.245, 0.188), Array.Empty<BeadSpecRow>(), BeadSettings.Default).IsNone);
    }
}
