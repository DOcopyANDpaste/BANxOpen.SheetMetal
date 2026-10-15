using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Requirements;
using static BANxOpen.SheetMetal.Tests.RequirementFixtures;

namespace BANxOpen.SheetMetal.Tests.Requirements;

public class BeadRequirementFactoryTests
{
    private static readonly BeadSpecRow At20 = SpecRow("B1005010-2", 0.02, "2024-O", "5052-O");
    private static readonly BeadSpecRow At40 = SpecRow("B1005010-2", 0.04, "2024-O");

    [Fact]
    public void Beads_on_one_SPEC_share_one_requirement_naming_all_of_them()
    {
        var beads = new[] { Stamped("101", "BEAD(1)", "B1005010-2", At20, At40), Stamped("102", "BEAD(2)", "B1005010-2", At20, At40) };

        var result = BeadRequirementFactory.Create(beads, Array.Empty<SheetMetalRequirement>(), NothingRebuilt, currentThickness: 0.02);

        var requirement = Assert.Single(result.Requirements);
        Assert.Equal(new[] { "101", "102" }, requirement.Users.Select(u => u.FeatureKey));
        Assert.Equal("B1005010-2", requirement.SpecId);
    }

    [Fact]
    public void An_applied_row_sets_the_built_thickness_and_grades()
    {
        var beads = new[] { Stamped("101", "BEAD(1)", "B1005010-2", At20, At40) };
        var previous = new[] { Requirement("B1005010-2", 0.02, "2024-O", "5052-O") };
        var rebuilt = new Dictionary<(string, string), BeadSpecRow> { [(Standard, "B1005010-2")] = At40 };

        var requirement = Assert.Single(BeadRequirementFactory.Create(beads, previous, rebuilt, currentThickness: 0.04).Requirements);

        Assert.Equal(0.04, requirement.Allowance.BuiltThickness);
        Assert.Equal(new[] { "2024-O" }, requirement.Allowance.Grades);
    }

    [Fact]
    public void An_existing_requirement_is_kept_even_after_the_sheet_changed_thickness()
    {
        // The beads still carry the 0.02 row's values: re-deriving from the 0.04 sheet would hide that.
        var beads = new[] { Stamped("101", "BEAD(1)", "B1005010-2", At20, At40) };
        var previous = new[] { Requirement("B1005010-2", 0.02, "2024-O", "5052-O") };

        var requirement = Assert.Single(BeadRequirementFactory.Create(beads, previous, NothingRebuilt, currentThickness: 0.04).Requirements);

        Assert.Equal(0.02, requirement.Allowance.BuiltThickness);
        Assert.Equal(new[] { "2024-O", "5052-O" }, requirement.Allowance.Grades);
    }

    [Fact]
    public void A_SPEC_without_a_requirement_is_recorded_at_the_current_sheet_with_a_note()
    {
        var beads = new[] { Stamped("101", "BEAD(1)", "B1005010-2", At20, At40) };

        var result = BeadRequirementFactory.Create(beads, Array.Empty<SheetMetalRequirement>(), NothingRebuilt, currentThickness: 0.04);

        Assert.Equal(0.04, Assert.Single(result.Requirements).Allowance.BuiltThickness);
        Assert.Single(result.Notes);
    }

    [Fact]
    public void A_SPEC_not_driven_for_the_current_sheet_gets_no_requirement_and_says_why()
    {
        var beads = new[] { Stamped("101", "BEAD(1)", "B1005010-2", At20) };

        var result = BeadRequirementFactory.Create(beads, Array.Empty<SheetMetalRequirement>(), NothingRebuilt, currentThickness: 0.063);

        Assert.Empty(result.Requirements);
        Assert.Contains("not driven", Assert.Single(result.Notes));
    }

    [Fact]
    public void Unidentified_beads_belong_to_no_requirement_and_matched_ones_are_labelled()
    {
        var beads = new[] { Unmatched("100", "BEAD(0)"), Matched("101", "BEAD(1)", "B1005010-2", At20) };

        var requirement = Assert.Single(BeadRequirementFactory.Create(beads, Array.Empty<SheetMetalRequirement>(), NothingRebuilt, 0.02).Requirements);

        var user = Assert.Single(requirement.Users);
        Assert.Equal("101", user.FeatureKey);
        Assert.Contains("identified by geometry", user.Label);
    }

    [Fact]
    public void A_SPEC_whose_beads_are_all_gone_drops_out()
    {
        var previous = new[] { Requirement("B1005010-2", 0.02, "2024-O") };

        Assert.Empty(BeadRequirementFactory.Create(Array.Empty<BANxOpen.SheetMetal.Materials.BeadOnBody>(), previous, NothingRebuilt, 0.02).Requirements);
    }
}
