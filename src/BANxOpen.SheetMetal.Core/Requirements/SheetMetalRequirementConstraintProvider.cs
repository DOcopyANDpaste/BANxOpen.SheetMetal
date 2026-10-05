using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Core.Materials.Rules.Features;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>Tells the shared material engine what the sheet metal requirements held in the part allow — the same
/// requirements NX itself checks after a native Sheet Metal Preferences change, so the Material Assignment dialog and
/// NX cannot disagree.
///
/// Asked of a physical material, which is several standards-file rows (one per thickness and grade). A material is
/// acceptable when at least one of its rows — under the chosen Standard — satisfies every requirement: the row choice
/// (<see cref="SheetMetalRowChoiceProvider"/>) then blocks the individual rows that do not, so the user can only land on
/// a compliant one. A material with no row at all is left to <see cref="SheetMetalPreferenceConstraintProvider"/>,
/// which refuses it with the clearer reason.
///
/// Requirements are part-level (NX keeps one set of Sheet Metal Preferences per part), so they apply to the part's
/// sheet metal bodies and to nothing else.</summary>
public sealed class SheetMetalRequirementConstraintProvider : IFeatureMaterialConstraintProvider
{
    public const string ProviderDomainId = "SHEETMETAL.REQUIREMENT";

    public const string NotAllowedCode = "MATERIAL_NOT_ALLOWED_BY_REQUIREMENT";
    public const string UnreadableCode = "REQUIREMENTS_UNREADABLE";

    private readonly ISheetMetalRequirementReader _requirements;
    private readonly ISheetMetalPreferenceReader _preferences;
    private readonly SheetMetalStandardSelection _selection;

    public SheetMetalRequirementConstraintProvider(
        ISheetMetalRequirementReader requirements, ISheetMetalPreferenceReader preferences, SheetMetalStandardSelection selection)
    {
        _requirements = requirements;
        _preferences = preferences;
        _selection = selection;
    }

    public string DomainId => ProviderDomainId;

    public IReadOnlyList<MaterialConstraint> ConstraintsFor(BodyId bodyId)
    {
        if (!_preferences.ReadFor(bodyId).IsSheetMetalBody)
            return Array.Empty<MaterialConstraint>();

        var (requirements, errors) = _requirements.ReadAll();
        if (errors.Count > 0)
        {
            // A requirement that could not be read might forbid anything; nothing is allowed until it is fixed.
            return new[]
            {
                new MaterialConstraint(
                    DomainId, "this part's sheet metal requirements", UnreadableCode,
                    _ => false,
                    _ => $"This part's sheet metal requirements could not be read, so no material can be checked against " +
                         $"them: {string.Join(" ", errors)}"),
            };
        }

        return requirements.Select(ConstraintFor).ToList();
    }

    private MaterialConstraint ConstraintFor(SheetMetalRequirement requirement) =>
        new(
            DomainId,
            requirement.Label,
            NotAllowedCode,
            candidate => _selection.RowsFor(candidate.Name) is var rows
                         && (rows.Count == 0 || rows.Any(row => requirement.Allowance.IsSatisfiedBy(row.Thickness, row.Grade))),
            candidate => Describe(requirement, _selection.RowsFor(candidate.Name), candidate.Name));

    private static string Describe(SheetMetalRequirement requirement, IReadOnlyList<SheetMetalMaterialRow> rows, string material)
    {
        var allowance = requirement.Allowance;
        var atBuilt = rows.Where(r => allowance.MatchesThickness(r.Thickness)).ToList();

        var why = atBuilt.Count == 0
            ? $"has no row at {allowance.ThicknessText} in, the thickness its features were built at"
            : $"is grade {string.Join("/", atBuilt.Select(r => r.Grade).Distinct(StringComparer.OrdinalIgnoreCase))} at " +
              $"{allowance.ThicknessText} in, which it does not allow (allowed: {allowance.GradesText})";

        return $"Material '{material}' {why}. To use another thickness, re-apply the features with their tool at the new " +
               "thickness first.";
    }
}
