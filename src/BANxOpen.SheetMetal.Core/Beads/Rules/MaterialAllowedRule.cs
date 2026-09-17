using BANxOpen.Foundation.Core.RuleEngine;

namespace BANxOpen.SheetMetal.Beads.Rules;

/// <summary>The sheet metal's material grade must be one of the candidate SPEC's "Allowed Material" YES
/// columns. Distinguishes "grade not recognized at all" (the workbook has no such column — likely a
/// SheetMetal_Material value in the sheet metal material standards file that no workbook column spells the same way)
/// from "recognized but marked '-'" (a genuine spec restriction), since the two
/// call for different user action.</summary>
public sealed class MaterialAllowedRule : IGateRule<BeadValidationContext, RuleOutcome>
{
    public string RuleId => "MATERIAL_ALLOWED";
    public int Order => 200;

    public RuleOutcome Evaluate(BeadValidationContext context)
    {
        // net48's reference assemblies aren't nullable-annotated, so the compiler can't narrow `grade` from
        // the IsNullOrWhiteSpace guard alone the way it does on net8.0 — the explicit null check does it.
        if (string.IsNullOrWhiteSpace(context.Profile.MaterialGradeLabel))
            return new RuleOutcome(RuleId, RuleDecision.Block, "MATERIAL_MISSING",
                "This sheet metal has no material assigned; a material must be assigned before a SPEC can be validated.");

        var grade = context.Profile.MaterialGradeLabel!;

        if (context.Candidate.IsAllowedFor(grade))
            return new RuleOutcome(RuleId, RuleDecision.Allow, null, null);

        if (!context.Candidate.AllowedMaterialGrades.ContainsKey(grade))
            return new RuleOutcome(
                RuleId,
                RuleDecision.Block,
                "MATERIAL_GRADE_UNRECOGNIZED",
                $"Material grade '{grade}' is not a column in Standard '{context.Candidate.StandardId}'s workbook — " +
                "check the SheetMetal_Material value in the sheet metal material standards file and the workbook's header row.");

        return new RuleOutcome(
            RuleId,
            RuleDecision.Block,
            "MATERIAL_NOT_ALLOWED",
            $"SPEC '{context.Candidate.SpecId}' is not allowed on material grade '{grade}'.");
    }
}
