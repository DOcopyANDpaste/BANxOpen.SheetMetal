using BANxOpen.Foundation.Core.RuleEngine;

namespace BANxOpen.SheetMetal.Beads.Rules;

/// <summary>The sheet metal's material grade must be one of the candidate SPEC's "Allowed Material" YES
/// columns. Distinguishes "grade not recognized at all" (the workbook has no such column — likely a
/// SheetMetal_Material value in the sheet metal material standards file that no workbook column spells the same way)
/// from "recognized but marked '-'" (a genuine spec restriction), since the two
/// call for different user action.</summary>
public sealed class MaterialAllowedRule : IGateRule<BeadValidationContext, RuleOutcome>
{
    private readonly SheetMetalText _text;

    /// <param name="text">Words the outcomes; <see cref="SheetMetalText.Default"/> when null.</param>
    public MaterialAllowedRule(SheetMetalText? text = null) => _text = text ?? SheetMetalText.Default;

    public string RuleId => "MATERIAL_ALLOWED";
    public int Order => 200;

    public RuleOutcome Evaluate(BeadValidationContext context)
    {
        // net48's reference assemblies aren't nullable-annotated, so the compiler can't narrow `grade` from
        // the IsNullOrWhiteSpace guard alone the way it does on net8.0 — the explicit null check does it.
        if (string.IsNullOrWhiteSpace(context.Profile.MaterialGradeLabel))
            return new RuleOutcome(RuleId, RuleDecision.Block, SheetMetalCodes.MaterialMissing, _text.BeadMaterialMissing);

        var grade = context.Profile.MaterialGradeLabel!;

        if (context.Candidate.IsAllowedFor(grade))
            return new RuleOutcome(RuleId, RuleDecision.Allow, null, null);

        if (!context.Candidate.AllowedMaterialGrades.ContainsKey(grade))
        {
            return new RuleOutcome(
                RuleId,
                RuleDecision.Block,
                SheetMetalCodes.MaterialGradeUnrecognized,
                _text.BeadGradeNotAColumn(grade, context.Candidate.StandardId))
            {
                Args = new Dictionary<string, string> { ["grade"] = grade, ["standard"] = context.Candidate.StandardId },
            };
        }

        return new RuleOutcome(
            RuleId,
            RuleDecision.Block,
            SheetMetalCodes.MaterialNotAllowed,
            _text.BeadSpecNotAllowedOnGrade(context.Candidate.SpecId, grade))
        {
            Args = new Dictionary<string, string> { ["spec"] = context.Candidate.SpecId, ["grade"] = grade },
        };
    }
}
