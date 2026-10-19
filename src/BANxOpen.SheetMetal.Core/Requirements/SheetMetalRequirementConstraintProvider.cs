using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Core.Rules.Features;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>Tells the shared material engine what the sheet metal requirements held in the part allow — the same
/// requirements NX itself checks after a native Sheet Metal Preferences change, so the Material Assignment dialog and
/// NX cannot disagree.
///
/// Asked of a physical material, which is several standards-file rows (one per thickness and grade). A material is
/// acceptable when at least one of its rows — under the chosen Standard — satisfies every requirement: the row gate
/// (<see cref="SheetMetalRowGateRule"/>) then judges the individual row picked. Both only warn — the assignment goes
/// ahead, and the requirement check left failing in NX is the reminder to re-apply the features. Requirements that
/// cannot be read still block, since nothing can be judged. A material with no row at all is left to <see cref="SheetMetalPreferenceConstraintProvider"/>,
/// which refuses it with the clearer reason.
///
/// Requirements are part-level (NX keeps one set of Sheet Metal Preferences per part), so they apply to the part's
/// sheet metal bodies and to nothing else.</summary>
public sealed class SheetMetalRequirementConstraintProvider : IFeatureMaterialConstraintProvider
{
    public const string ProviderDomainId = "SHEETMETAL.REQUIREMENT";

    // Kept as aliases of the codes' definitions.
    public const string NotAllowedCode = SheetMetalCodes.MaterialNotAllowedByRequirement;
    public const string UnreadableCode = BANxOpen.Foundation.Core.FoundationCodes.RequirementsUnreadable;

    private readonly ISheetMetalRequirementReader _requirements;
    private readonly ISheetMetalPreferenceReader _preferences;
    private readonly SheetMetalStandardSelection _selection;
    private readonly SheetMetalText _text;

    /// <param name="text">Words the violations; <see cref="SheetMetalText.Default"/> when null.</param>
    public SheetMetalRequirementConstraintProvider(
        ISheetMetalRequirementReader requirements, ISheetMetalPreferenceReader preferences, SheetMetalStandardSelection selection,
        SheetMetalText? text = null)
    {
        _requirements = requirements;
        _preferences = preferences;
        _selection = selection;
        _text = text ?? SheetMetalText.Default;
    }

    public string DomainId => ProviderDomainId;

    public IReadOnlyList<MaterialConstraint> ConstraintsFor(BodyId bodyId)
    {
        if (!_preferences.ReadFor(bodyId).IsSheetMetalBody)
            return Array.Empty<MaterialConstraint>();

        var (requirements, errors) = _requirements.ReadAll();
        if (errors.Count > 0)
        {
            // Warned, not blocked: feature validation never stands in the way of a preference update. Re-opening the
            // feature's tool rewrites what could not be read.
            return new[]
            {
                new MaterialConstraint(
                    DomainId, _text.SourcePartRequirements, UnreadableCode,
                    _ => false,
                    _ => _text.RequirementsUnreadableForAssignment(string.Join(" ", errors)),
                    ConstraintSeverity.Warn)
                {
                    ViolationArgs = _ => new Dictionary<string, string> { ["error"] = string.Join(" ", errors) },
                },
            };
        }

        // A requirement no feature uses any more guards nothing; the next refresh deletes it.
        return requirements.Where(r => r.IsInUse).Select(ConstraintFor).ToList();
    }

    /// <summary>Warns, never blocks: a material the beads do not allow is still the user's to assign. The assignment
    /// keeps the requirement as it is, so its NX check fails and keeps saying the beads need re-applying after this
    /// dialog has closed.</summary>
    private MaterialConstraint ConstraintFor(SheetMetalRequirement requirement) =>
        new(
            DomainId,
            requirement.Label,
            NotAllowedCode,
            candidate => _selection.RowsFor(candidate.Name) is var rows
                         && (rows.Count == 0 || rows.Any(row => requirement.Allowance.IsSatisfiedBy(row.Thickness, row.Grade))),
            candidate => _text.RequirementMaterialNotAllowed(candidate.Name, requirement.Label, requirement.Allowance.PairsText),
            ConstraintSeverity.Warn)
        {
            ViolationArgs = candidate => ArgsFor(requirement, _selection.RowsFor(candidate.Name)),
        };

    /// <summary>reason (thicknessNotAllowed | gradeNotAllowed), builtThickness, grades, allowedPairs.</summary>
    private static IReadOnlyDictionary<string, string> ArgsFor(SheetMetalRequirement requirement, IReadOnlyList<SheetMetalMaterialRow> rows) =>
        new Dictionary<string, string>
        {
            ["reason"] = rows.Any(r => requirement.Allowance.AllowsThickness(r.Thickness)) ? "gradeNotAllowed" : "thicknessNotAllowed",
            ["builtThickness"] = requirement.Allowance.ThicknessText,
            ["grades"] = string.Join("/", rows.Select(r => r.Grade).Distinct(StringComparer.OrdinalIgnoreCase)),
            ["allowedPairs"] = requirement.Allowance.PairsText,
        };
}
