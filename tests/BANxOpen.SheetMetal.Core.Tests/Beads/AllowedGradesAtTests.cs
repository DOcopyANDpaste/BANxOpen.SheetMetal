using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;

namespace BANxOpen.SheetMetal.Tests.Beads;

public class AllowedGradesAtTests
{
    private static BeadSpecRow Row(string specId, double thickness, params string[] allowed) =>
        new("B1005010", "B1005010", specId, RadiusAndRadS: 0.245, Width: 0.625, Height: 0.625, DieRadiusP: 0.188,
            Thickness: thickness,
            AllowedMaterialGrades: new Dictionary<string, bool>
            {
                ["2024-O"] = allowed.Contains("2024-O"),
                ["5052-O"] = allowed.Contains("5052-O"),
                ["7075-T6"] = allowed.Contains("7075-T6"),
            });

    [Fact]
    public void Unions_the_grades_allowed_by_every_SPEC_at_the_thickness()
    {
        var grades = BeadSpecFinder.AllowedGradesAt(
            new[] { Row("A", 0.02, "2024-O"), Row("B", 0.02, "5052-O") }, thickness: 0.02);

        Assert.Equal(new[] { "2024-O", "5052-O" }, grades.OrderBy(g => g));
    }

    [Fact]
    public void Ignores_SPECs_for_another_thickness()
    {
        var grades = BeadSpecFinder.AllowedGradesAt(
            new[] { Row("A", 0.02, "2024-O"), Row("B", 0.05, "7075-T6") }, thickness: 0.02);

        Assert.Equal(new[] { "2024-O" }, grades);
    }

    [Fact]
    public void Uses_the_same_thickness_tolerance_as_SPEC_validation()
    {
        var justInside = 0.02 + ThicknessMatchRule.ToleranceInches / 2;

        Assert.Contains("2024-O", BeadSpecFinder.AllowedGradesAt(new[] { Row("A", 0.02, "2024-O") }, justInside));
    }

    [Fact]
    public void Excludes_grade_columns_marked_not_allowed()
    {
        Assert.Empty(BeadSpecFinder.AllowedGradesAt(new[] { Row("A", 0.02) }, 0.02));
    }
}
