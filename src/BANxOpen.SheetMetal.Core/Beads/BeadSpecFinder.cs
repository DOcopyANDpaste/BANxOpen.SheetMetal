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

    /// <summary>Grades allowed by at least one of <paramref name="specs"/> whose thickness matches
    /// <paramref name="thickness"/> — what a body with no material yet could be given and still have a valid
    /// SPEC to choose from in this Standard. Thickness uses <see cref="Rules.ThicknessMatchRule"/>'s
    /// tolerance so this agrees with what validation will accept afterwards.</summary>
    public static IReadOnlyCollection<string> AllowedGradesAt(IEnumerable<BeadSpecRow> specs, double thickness) =>
        new HashSet<string>(
            specs
                .Where(spec => Rules.ThicknessMatchRule.Matches(spec.Thickness, thickness))
                .SelectMany(spec => spec.AllowedMaterialGrades.Where(kv => kv.Value).Select(kv => kv.Key)),
            StringComparer.Ordinal);

    /// <summary>Grades every one of <paramref name="specs"/> allows at <paramref name="thickness"/> — what the
    /// sheet metal can be made of without invalidating any bead already built to them. Each SPEC contributes
    /// <see cref="AllowedGradesAt"/> of itself, so a SPEC of another thickness allows nothing. Null when there are no
    /// SPECs: nothing restricts the grade.</summary>
    public static IReadOnlyCollection<string>? GradesAllowedByAll(IEnumerable<BeadSpecRow> specs, double thickness)
    {
        HashSet<string>? allowed = null;
        foreach (var spec in specs)
        {
            var grades = AllowedGradesAt(new[] { spec }, thickness);
            if (allowed is null)
                allowed = new HashSet<string>(grades, StringComparer.Ordinal);
            else
                allowed.IntersectWith(grades);
        }

        return allowed;
    }
}
