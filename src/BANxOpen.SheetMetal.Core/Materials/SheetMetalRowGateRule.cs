using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Materials.Core.Assignment;
using BANxOpen.Materials.Core.Rules;
using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Requirements;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Checks the sheet metal material standards file row the user picked for a sheet metal body — the
/// <see cref="MaterialAssignmentRuleContext.RequestedVariant"/> that <see cref="SyncSheetMetalPreferenceEffectRule"/>
/// then sets as the part's Sheet Metal Preferences.
/// <list type="bullet">
/// <item>A row that is not one of the requested material's rows under the chosen Standard
/// (<see cref="SheetMetalStandardSelection"/>) is refused.</item>
/// <item>A row the part's sheet metal requirements (<see cref="ISheetMetalRequirementReader"/>) do not allow — a
/// (grade, thickness) pair not in its SPEC family — is warned about, not refused: the requirement is left as it is,
/// and its failing NX check reminds the user to re-apply the features. A row they allow at a thickness other than the
/// one the features were built at is warned about too, since the check passes but the geometry is the built row's.
/// Requirements that cannot be read refuse every row.</item>
/// <item>A row whose thickness is not the body's needs confirming: usually a mistake, but the user's to make —
/// <c>SheetMetalPreferenceCheck</c> reports the mismatch afterwards either way. A thickness NX will not report
/// counts as matching, since there is nothing to disagree with.</item>
/// </list>
/// A body that is not sheet metal, or an assignment with no row picked, is allowed: there is nothing to check, and a
/// dialog that requires a row for sheet metal bodies refuses that case itself.</summary>
public sealed class SheetMetalRowGateRule : IMaterialValidationRule
{
    public const string Id = "SHEETMETAL_PREFERENCE_ROW";
    // Kept as aliases of SheetMetalCodes, which is where the codes are defined.
    public const string NotOfferedCode = SheetMetalCodes.RowNotOffered;
    public const string RefusedCode = SheetMetalCodes.RowRefusedByRequirement;
    public const string ThicknessMismatchCode = SheetMetalCodes.RowThicknessMismatch;
    public const string NeedsReapplyCode = SheetMetalCodes.RowNeedsReapply;

    private readonly ISheetMetalPreferenceReader _reader;
    private readonly SheetMetalStandardSelection _selection;
    private readonly ISheetMetalRequirementReader _requirements;
    private readonly SheetMetalText _text;

    /// <param name="requirements">The part's sheet metal requirements, which refuse the rows they do not allow.</param>
    /// <param name="text">Words the outcomes; <see cref="SheetMetalText.Default"/> when null.</param>
    public SheetMetalRowGateRule(
        ISheetMetalPreferenceReader reader, SheetMetalStandardSelection selection, ISheetMetalRequirementReader requirements,
        SheetMetalText? text = null)
    {
        _text = text ?? SheetMetalText.Default;
        _reader = reader;
        _selection = selection;
        _requirements = requirements;
    }

    public string RuleId => Id;

    public int Order => MaterialRuleOrder.Validation.Confirmation;

    public RuleOutcome Evaluate(MaterialAssignmentRuleContext context)
    {
        if (context.TargetBody.Kind != BodyKind.SheetMetal || context.RequestedVariant is not { } rowName)
            return Allow();

        var materialName = context.RequestedMaterial.Name;
        var row = _selection.RowsFor(materialName)
            .FirstOrDefault(r => string.Equals(r.Name, rowName, StringComparison.Ordinal));
        if (row is null)
        {
            // NotOfferedCode: row, material, standard (empty when no Standard is chosen).
            return new RuleOutcome(Id, RuleDecision.Block, NotOfferedCode, _text.RowNotOffered(rowName, materialName, _selection.Selected))
            {
                Args = new Dictionary<string, string>
                {
                    ["row"] = rowName,
                    ["material"] = materialName,
                    ["standard"] = _selection.Selected ?? string.Empty,
                },
            };
        }

        var (requirements, errors) = _requirements.ReadAll();
        if (errors.Count > 0)
        {
            // A requirement that could not be read might forbid anything: the constraint provider blocks the
            // assignment for the same reason. RefusedCode: row, rowThickness, refusal.
            return new RuleOutcome(Id, RuleDecision.Block, RefusedCode, _text.RowRefused(row.Name, _text.RowRefusedRequirementsUnreadable))
            {
                Args = new Dictionary<string, string>
                {
                    ["row"] = row.Name,
                    ["rowThickness"] = ThicknessMatchRule.Format(row.Thickness),
                    ["refusal"] = "requirementsUnreadable",
                },
            };
        }

        var warning = RequirementWarningFor(row, requirements);

        // Unreadable preferences are refused by SheetMetalPreferenceConstraintProvider; there is no thickness to
        // compare against here.
        var read = _reader.ReadFor(context.TargetBody.Id);
        if (read.BodyThickness is { } bodyThickness && !ThicknessMatchRule.Matches(bodyThickness, row.Thickness))
        {
            // ThicknessMismatchCode: row, rowThickness, bodyThickness. A requirement warning rides along in the message,
            // since only one outcome is returned and a confirmation outranks a warning.
            var message = _text.RowThicknessMismatch(row.Name, ThicknessMatchRule.Format(row.Thickness), ThicknessMatchRule.Format(bodyThickness));
            return new RuleOutcome(Id, RuleDecision.RequireConfirmation, ThicknessMismatchCode,
                warning is null ? message : $"{message} {warning.Message}")
            {
                Args = new Dictionary<string, string>
                {
                    ["row"] = row.Name,
                    ["rowThickness"] = ThicknessMatchRule.Format(row.Thickness),
                    ["bodyThickness"] = ThicknessMatchRule.Format(bodyThickness),
                },
            };
        }

        return warning ?? Allow();
    }

    private static RuleOutcome Allow() => new(Id, RuleDecision.Allow, null, null);

    /// <summary>What the part's requirements say about <paramref name="row"/>, or null when they are content. Warned,
    /// not blocked: the assignment keeps the requirements as they are, so their NX checks go on reporting the features
    /// that need re-applying after this dialog has closed.</summary>
    private RuleOutcome? RequirementWarningFor(SheetMetalMaterialRow row, IReadOnlyList<SheetMetalRequirement> requirements)
    {
        var rowThickness = ThicknessMatchRule.Format(row.Thickness);

        if (requirements.FirstOrDefault(r => !r.Allowance.IsSatisfiedBy(row.Thickness, row.Grade)) is { } refusing)
        {
            // RefusedCode: row, rowThickness, refusal, requirement, grade, builtThickness, allowedPairs.
            return new RuleOutcome(Id, RuleDecision.Warn, RefusedCode,
                _text.RowPairNotAllowed(row.Name, refusing.Label, row.Grade, rowThickness, refusing.Allowance.PairsText))
            {
                Args = new Dictionary<string, string>
                {
                    ["row"] = row.Name,
                    ["rowThickness"] = rowThickness,
                    ["refusal"] = "pairNotAllowed",
                    ["requirement"] = refusing.Label,
                    ["grade"] = row.Grade,
                    ["builtThickness"] = refusing.Allowance.ThicknessText,
                    ["allowedPairs"] = refusing.Allowance.PairsText,
                },
            };
        }

        // Allowed, but the features carry the built row's geometry. The NX check passes for this row, so this is where
        // the user hears it. NeedsReapplyCode: row, rowThickness, requirement, builtThickness.
        if (requirements.FirstOrDefault(r => !r.Allowance.MatchesBuiltThickness(row.Thickness)) is { } rebuild)
        {
            return new RuleOutcome(Id, RuleDecision.Warn, NeedsReapplyCode,
                _text.RowNeedsReapply(row.Name, rebuild.Label, rebuild.Allowance.ThicknessText, rowThickness))
            {
                Args = new Dictionary<string, string>
                {
                    ["row"] = row.Name,
                    ["rowThickness"] = rowThickness,
                    ["requirement"] = rebuild.Label,
                    ["builtThickness"] = rebuild.Allowance.ThicknessText,
                },
            };
        }

        return null;
    }
}
