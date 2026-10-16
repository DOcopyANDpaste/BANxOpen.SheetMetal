using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Core.Rules.Features;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.SpecData;
using BANxOpen.SheetMetal.Tests.Materials;
using static BANxOpen.SheetMetal.Tests.RequirementFixtures;

namespace BANxOpen.SheetMetal.Tests.Requirements;

/// <summary>The workbook-based bead provider once the part holds requirements: covered SPECs are handed over, and a
/// workbook that has moved on is reported.</summary>
public class BeadProviderWithRequirementsTests
{
    private static readonly BodyId BodyId = new("body-1");

    private static readonly BodyFeatureInventory OneStamp =
        new(new[] { new FeatureSpecStamp(Standard, "B1005010-2", "101", "BEAD(1)") }, Array.Empty<UnstampedFeature>());

    private static readonly BeadSpecRow At20 = SpecRow("B1005010-2", 0.02, "2024-O");

    private static BeadMaterialConstraintProvider Provider(ISheetMetalRequirementReader requirements, params BeadSpecRow[] workbook) =>
        new(new FixedInventory(OneStamp), new SpecLookup(workbook), TableRows.AluminumTable(), BeadSettings.Default, requirements);

    [Fact]
    public void A_covered_SPEC_is_left_to_the_requirement()
    {
        var constraints = Provider(new FakeRequirementReader(Requirement("B1005010-2", 0.02, "2024-O")), At20).ConstraintsFor(BodyId);

        Assert.DoesNotContain(constraints, c => c.ReasonCode == BeadMaterialConstraintProvider.NotAllowedCode);
        Assert.DoesNotContain(constraints, c => c.ReasonCode == BeadMaterialConstraintProvider.GradeUnrecognizedCode);
    }

    [Fact]
    public void A_workbook_that_now_allows_other_grades_is_reported_as_out_of_date()
    {
        var constraints = Provider(new FakeRequirementReader(Requirement("B1005010-2", 0.02, "2024-O", "5052-O")), At20).ConstraintsFor(BodyId);

        var drift = Assert.Single(constraints, c => c.ReasonCode == BeadMaterialConstraintProvider.RequirementOutOfDateCode);
        Assert.Equal(ConstraintSeverity.Warn, drift.Severity);
    }

    [Fact]
    public void Drift_compares_every_pair_of_the_family_not_only_the_built_row()
    {
        var at40 = SpecRow("B1005010-2", 0.04, "2024-O", "5052-O");
        var recorded = new FakeRequirementReader(Requirement("B1005010-2", 0.02, At20, at40));

        Assert.DoesNotContain(Provider(recorded, At20, at40).ConstraintsFor(BodyId),
            c => c.ReasonCode == BeadMaterialConstraintProvider.RequirementOutOfDateCode);

        // The 0.04 row no longer allows 5052-O: the built row is unchanged, but the pairs are not.
        Assert.Contains(Provider(recorded, At20, SpecRow("B1005010-2", 0.04, "2024-O")).ConstraintsFor(BodyId),
            c => c.ReasonCode == BeadMaterialConstraintProvider.RequirementOutOfDateCode);
    }

    [Fact]
    public void An_uncovered_SPEC_is_still_judged_from_the_workbook()
    {
        var constraints = Provider(new FakeRequirementReader(), At20).ConstraintsFor(BodyId);

        Assert.Contains(constraints, c => c.ReasonCode == BeadMaterialConstraintProvider.NotAllowedCode);
    }

    [Fact]
    public void Unreadable_requirements_leave_the_workbook_check_in_place()
    {
        var constraints = Provider(new FakeRequirementReader(Array.Empty<SheetMetalRequirement>(), "broken"), At20).ConstraintsFor(BodyId);

        Assert.Contains(constraints, c => c.ReasonCode == BeadMaterialConstraintProvider.NotAllowedCode);
    }
}
