using BANxOpen.Foundation.Contracts.Requirements;
using BANxOpen.Foundation.Core.Requirements;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Requirements;
using static BANxOpen.SheetMetal.Tests.RequirementFixtures;

namespace BANxOpen.SheetMetal.Tests.Requirements;

public class SheetMetalRequirementMapperTests
{
    private static readonly BeadSpecRow At40 = SpecRow("B1005010-2", 0.04, "2024-O", "5052-O");
    private static readonly BeadSpecRow At0625 = SpecRow("B1005010-2", 0.0625, "2024-O");

    [Fact]
    public void A_requirement_is_held_as_one_error_check_on_its_material_thickness_pairs()
    {
        var spec = Assert.Single(SheetMetalRequirementMapper.ToSpecs(Requirement("B1005010-2", 0.04, At40, At0625)));

        Assert.Equal(SheetMetalRequirementNames.PairAspect, spec.Aspect);
        Assert.Equal(RequirementValueType.String, spec.ValueType);
        Assert.Equal(new[] { "\"2024-O@0.0400\"", "\"5052-O@0.0400\"", "\"2024-O@0.0625\"" }, Assert.IsType<SetOfValuesRule>(spec.Rule).Values);
        Assert.Equal(SheetMetalRequirementNames.PairExpression, spec.CheckedFormula);
        Assert.Equal(RequirementSeverity.Error, spec.Severity);
        Assert.Equal("XX_Standard|B1005010-2", spec.Key);
        Assert.Equal(new[] { "101" }, spec.AssociatedObjectKeys);
    }

    [Fact]
    public void The_pair_expression_writes_the_thickness_as_the_pair_keys_do()
    {
        Assert.Equal(
            "BA_SM_Grade + \"@\" + format(\"%.4f\", Sheet_Metal_Material_Thickness)",
            SheetMetalRequirementNames.PairExpressionFormula);
        Assert.Equal("2024-O@0.0598", new MaterialThicknessPair("2024-O", 0.0598).Key);
    }

    [Fact]
    public void What_is_written_reads_back_as_the_same_requirement()
    {
        var original = Requirement("B1005010-2", 0.04, At40, At0625);
        var stored = SheetMetalRequirementMapper.ToSpecs(original).Select(s => new StoredRequirement(s, RequirementStatus.Pass)).ToList();

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(new RequirementRead(stored, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(errors);
        var read = Assert.Single(requirements);
        Assert.Equal(original.Domain, read.Domain);
        Assert.Equal("XX_Standard", read.StandardId);
        Assert.Equal("B1005010-2", read.SpecId);
        Assert.Equal(0.04, read.Allowance.BuiltThickness);
        Assert.Equal(original.Allowance.Pairs, read.Allowance.Pairs);
        Assert.Equal(new[] { "101" }, read.Users.Select(u => u.FeatureKey));
        Assert.Equal("B1005010", read.Source);
    }

    [Fact]
    public void A_legacy_grade_and_thickness_requirement_reads_as_its_grades_at_the_built_thickness()
    {
        RequirementSpec Legacy(string aspect, RequirementRule rule) => new(
            "SHEETMETAL.BEAD", "XX_Standard|B1005010-2", aspect, RequirementValueType.String, rule, RequirementSeverity.Warning,
            "expr", new[] { "Source: B1005010" }, new[] { "101" });
        var stored = new[]
        {
            new StoredRequirement(Legacy(SheetMetalRequirementNames.LegacyGradeAspect, new SetOfValuesRule(new[] { "2024-O", "5052-O" })), RequirementStatus.Pass),
            new StoredRequirement(Legacy(SheetMetalRequirementNames.LegacyThicknessAspect, new EqualsValueRule(0.04, ThicknessMatchRule.ToleranceInches)), RequirementStatus.Pass),
        };

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(new RequirementRead(stored, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(errors);
        var read = Assert.Single(requirements);
        Assert.Equal(0.04, read.Allowance.BuiltThickness);
        Assert.Equal(new[] { "2024-O@0.0400", "5052-O@0.0400" }, read.Allowance.Pairs.Select(p => p.Key));
        Assert.Equal("B1005010", read.Source);
    }

    [Fact]
    public void A_requirement_that_is_not_what_was_written_is_an_error_not_an_absence()
    {
        var written = Assert.Single(SheetMetalRequirementMapper.ToSpecs(Requirement("B1005010-2", 0.04, "2024-O")));
        var withoutBuiltAt = written with { Description = written.Description.Where(l => !l.StartsWith("Built at")).ToList() };

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(
            new RequirementRead(new[] { new StoredRequirement(withoutBuiltAt, RequirementStatus.Pass) }, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(requirements);
        Assert.Contains("B1005010-2", Assert.Single(errors));
    }

    [Fact]
    public void An_unquoted_requirement_still_reads_but_is_replaced_by_the_next_refresh()
    {
        var wanted = Assert.Single(SheetMetalRequirementMapper.ToSpecs(Requirement("B1005010-2", 0.04, At40)));
        var unquoted = wanted with { Rule = new SetOfValuesRule(new[] { "2024-O@0.0400", "5052-O@0.0400" }) };

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(
            new RequirementRead(new[] { new StoredRequirement(unquoted, RequirementStatus.Failed) }, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(errors);
        Assert.Equal(new[] { "2024-O@0.0400", "5052-O@0.0400" }, Assert.Single(requirements).Allowance.Pairs.Select(p => p.Key));
        Assert.Single(RequirementDiff.Compute(new[] { unquoted }, new[] { wanted }).ToReplace);
    }

    [Fact]
    public void A_requirement_no_feature_uses_is_read_but_not_in_use()
    {
        var orphan = Requirement("B1005010-2", 0.04, At40) with { Users = Array.Empty<RequirementUser>() };
        var stored = SheetMetalRequirementMapper.ToSpecs(orphan).Select(s => new StoredRequirement(s, RequirementStatus.Unknown)).ToList();

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(new RequirementRead(stored, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(errors);
        Assert.False(Assert.Single(requirements).IsInUse);
    }

    [Fact]
    public void An_unreadable_requirement_no_feature_uses_is_skipped_without_an_error()
    {
        var written = Assert.Single(SheetMetalRequirementMapper.ToSpecs(Requirement("B1005010-2", 0.04, "2024-O")));
        var broken = written with
        {
            Description = written.Description.Where(l => !l.StartsWith("Built at")).ToList(),
            AssociatedObjectKeys = Array.Empty<string>(),
        };

        var (requirements, errors) = SheetMetalRequirementMapper.FromRead(
            new RequirementRead(new[] { new StoredRequirement(broken, RequirementStatus.Unknown) }, Array.Empty<string>()), "Bead SPEC");

        Assert.Empty(requirements);
        Assert.Empty(errors);
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
    [InlineData(0.0625, "2024-O", true)] // another thickness of the family, with a grade it allows there
    [InlineData(0.0625, "5052-O", false)] // ... but only the grades that row allows
    [InlineData(0.05, "2024-O", false)] // a thickness the family does not cover
    public void An_allowance_allows_the_pairs_of_its_SPEC_family(double thickness, string grade, bool expected)
    {
        Assert.Equal(expected, SheetMetalAllowance.FromFamily(new[] { At40, At0625 }, 0.04).IsSatisfiedBy(thickness, grade));
    }
}
