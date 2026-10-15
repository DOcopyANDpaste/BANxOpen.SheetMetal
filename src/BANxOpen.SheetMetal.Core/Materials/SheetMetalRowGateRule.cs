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
/// <item>A row the part's sheet metal requirements refuse (<see cref="ISheetMetalRequirementReader"/>) — a thickness
/// other than the one its SPEC features were built at, or a grade they do not allow there — is refused with the
/// requirement's reason. The user cannot talk past a requirement.</item>
/// <item>A row whose thickness is not the body's needs confirming: usually a mistake, but the user's to make —
/// <c>SheetMetalPreferenceCheck</c> reports the mismatch afterwards either way. A thickness NX will not report
/// counts as matching, since there is nothing to disagree with.</item>
/// </list>
/// A body that is not sheet metal, or an assignment with no row picked, is allowed: there is nothing to check, and a
/// dialog that requires a row for sheet metal bodies refuses that case itself.</summary>
public sealed class SheetMetalRowGateRule : IMaterialValidationRule
{
    public const string Id = "SHEETMETAL_PREFERENCE_ROW";
    public const string NotOfferedCode = "SHEETMETAL_ROW_NOT_OFFERED";
    public const string RefusedCode = "SHEETMETAL_ROW_REFUSED_BY_REQUIREMENT";
    public const string ThicknessMismatchCode = "SHEETMETAL_ROW_THICKNESS_MISMATCH";

    private readonly ISheetMetalPreferenceReader _reader;
    private readonly SheetMetalStandardSelection _selection;
    private readonly ISheetMetalRequirementReader _requirements;

    /// <param name="requirements">The part's sheet metal requirements, which refuse the rows they do not allow.</param>
    public SheetMetalRowGateRule(
        ISheetMetalPreferenceReader reader, SheetMetalStandardSelection selection, ISheetMetalRequirementReader requirements)
    {
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
            return new RuleOutcome(Id, RuleDecision.Block, NotOfferedCode,
                $"'{rowName}' is not one of the Sheet Metal Preferences materials that can be set for '{materialName}' " +
                $"{Scope()}. Pick another row.");
        }

        if (RefusalFor(row) is { } refusal)
            return new RuleOutcome(Id, RuleDecision.Block, RefusedCode, $"'{row.Name}' cannot be used: {refusal}");

        // Unreadable preferences are refused by SheetMetalPreferenceConstraintProvider; there is no thickness to
        // compare against here.
        var read = _reader.ReadFor(context.TargetBody.Id);
        if (read.BodyThickness is { } bodyThickness && !ThicknessMatchRule.Matches(bodyThickness, row.Thickness))
        {
            return new RuleOutcome(Id, RuleDecision.RequireConfirmation, ThicknessMismatchCode,
                $"'{row.Name}' is {ThicknessMatchRule.Format(row.Thickness)} thick but this body is " +
                $"{ThicknessMatchRule.Format(bodyThickness)} thick; the part's " +
                "material and geometry will disagree about thickness.");
        }

        return Allow();
    }

    private static RuleOutcome Allow() => new(Id, RuleDecision.Allow, null, null);

    private string Scope() => _selection.Selected is { } standard
        ? $"under Standard '{standard}'"
        : "in the sheet metal material standards file";

    /// <summary>Why the part's requirements refuse <paramref name="row"/>, or null when they allow it. Requirements
    /// that cannot be read refuse every row — the constraint provider blocks the assignment for the same
    /// reason.</summary>
    private string? RefusalFor(SheetMetalMaterialRow row)
    {
        var (requirements, errors) = _requirements.ReadAll();
        if (errors.Count > 0)
            return "This part's sheet metal requirements could not be read, so no row can be checked against them.";

        if (requirements.FirstOrDefault(r => !r.Allowance.IsSatisfiedBy(row.Thickness, row.Grade)) is not { } refusing)
            return null;

        var allowance = refusing.Allowance;
        return allowance.MatchesThickness(row.Thickness)
            ? $"{refusing.Label} does not allow grade '{row.Grade}' at {allowance.ThicknessText} in " +
              $"(allowed: {allowance.GradesText})."
            : $"{refusing.Label}'s features were built at {allowance.ThicknessText} in. Re-apply them with their tool " +
              $"at {ThicknessMatchRule.Format(row.Thickness)} in before setting the sheet to this row.";
    }
}
