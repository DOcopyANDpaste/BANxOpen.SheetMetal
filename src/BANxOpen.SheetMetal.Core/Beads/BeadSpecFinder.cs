using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Beads;

/// <summary>Given a sheet metal profile and a Standard's full SPEC list, finds which rows actually
/// validate — the "green items in the drop-down" behavior, and the alternatives list shown alongside a
/// validation-failure error.</summary>
public sealed class BeadSpecFinder
{
    private readonly BeadSpecValidator _validator;

    public BeadSpecFinder(BeadSpecValidator validator) => _validator = validator;

    public IReadOnlyList<BeadSpecRow> FindValid(SheetMetalProfile profile, IEnumerable<BeadSpecRow> candidates) =>
        candidates.Where(row => _validator.Validate(profile, row).IsValid).ToList();
}
