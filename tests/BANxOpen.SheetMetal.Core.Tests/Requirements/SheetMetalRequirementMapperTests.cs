using BANxOpen.Foundation.Contracts.Requirements;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Requirements;
using static BANxOpen.SheetMetal.Tests.Requirements.RequirementFixtures;

namespace BANxOpen.SheetMetal.Tests.Requirements;

public class SheetMetalRequirementMapperTests
{
    [Fact]
    public void A_requirement_is_held_as_a_grade_set_and_a_built_thickness()
    {
        var specs = SheetMetalRequirementMapper.ToSpecs(Requirement("B1005010-2", 0.04, "2024-O", "5052-O"));

        var grade = Assert.Single(specs, s => s.Aspect == SheetMetalRequirementNames.GradeAspect);
        Assert.Equal(RequirementValueType.String, grade.ValueType);
        Assert.Equal(new[] { "2024-O", "5052-O" }, Assert.IsType<SetOfValuesRule>(grade.Rule).Values);
        Assert.Equal(SheetMetalRequirementNames.GradeExpression, grade.CheckedFormula);

        var thickness = Assert.Single(specs, s => s.Aspect == SheetMetalRequirementNames.ThicknessAspect);
        Assert.Equal(RequirementValueType.Number, thickness.ValueType);
        Assert.Equal(new EqualsValueRule(0.04, ThicknessMatchRule.ToleranceInches), thickness.Rule);
        Assert.Equal(SheetMetalRequirementNames.ThicknessExpression, thickness.CheckedFormula);

        Assert.All(specs, s =>
        {
            Assert.Equal("XX_Standard|B1005010-2", s.Key);
            Assert.Equal(RequirementSeverity.Warning, s.Severity);
            Assert.Equal(new[] { "101" }, s.AssociatedObjectKeys);
        });
    }

    [Fact]
    public void What_is_written_reads_back_as_the_same_requirement()
    {
        var original = Requirement("B1005010-2", 0.04, "2024-O", "5052-O");
        var stored = SheetMetalRequirementMapper.ToSpecs(original).Select(s => new StoredRequirement(s, RequirementStatus.Pass)).ToList();

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(new RequirementRead(stored, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(errors);
        var read = Assert.Single(requirements);
        Assert.Equal(original.Domain, read.Domain);
        Assert.Equal("XX_Standard", read.StandardId);
        Assert.Equal("B1005010-2", read.SpecId);
        Assert.Equal(0.04, read.Allowance.BuiltThickness);
        Assert.Equal(new[] { "2024-O", "5052-O" }, read.Allowance.Grades);
        Assert.Equal(new[] { "101" }, read.Users.Select(u => u.FeatureKey));
        Assert.Equal("B1005010", read.Source);
    }

    [Fact]
    public void A_requirement_missing_one_aspect_is_an_error_not_an_absence()
    {
        var gradeOnly = SheetMetalRequirementMapper.ToSpecs(Requirement("B1005010-2", 0.04, "2024-O"))
            .Where(s => s.Aspect == SheetMetalRequirementNames.GradeAspect)
            .Select(s => new StoredRequirement(s, RequirementStatus.Pass))
            .ToList();

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(new RequirementRead(gradeOnly, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(requirements);
        Assert.Contains("B1005010-2", Assert.Single(errors));
    }

    [Fact]
    public void Store_read_errors_are_passed_through()
    {
        var (_, errors) = SheetMetalRequirementMapper.FromRead(
            new RequirementRead(Array.Empty<StoredRequirement>(), new[] { "NX 1: broken" }), "Bead SPEC");

        Assert.Equal(new[] { "NX 1: broken" }, errors);
    }

    [Theory]
    [InlineData(0.04, "2024-O", true)]
    [InlineData(0.0404, "2024-o", true)] // within tolerance, grade case ignored
    [InlineData(0.04, "7075-T6", false)]
    [InlineData(0.05, "2024-O", false)] // another thickness the family might cover is still refused
    public void An_allowance_is_pinned_to_the_built_thickness(double thickness, string grade, bool expected)
    {
        Assert.Equal(expected, new SheetMetalAllowance(0.04, new[] { "2024-O", "5052-O" }).IsSatisfiedBy(thickness, grade));
    }
}
