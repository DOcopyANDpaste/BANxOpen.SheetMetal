using BANxOpen.Foundation.Core.RuleEngine;

namespace BANxOpen.SheetMetal.Beads.Rules;

/// <summary>The candidate SPEC's THICKNESS column must match the sheet metal's actual thickness. NX
/// thickness values come from a solid model measurement, so an exact equality check is too brittle —
/// compared within <see cref="ToleranceInches"/>.</summary>
public sealed class ThicknessMatchRule : IGateRule<BeadValidationContext, RuleOutcome>
{
    public const double ToleranceInches = 0.0005;

    /// <summary>The one thickness comparison: SPEC validation, allowed grades and the preferences check all use it.</summary>
    public static bool Matches(double a, double b) => Math.Abs(a - b) <= ToleranceInches;

    public string RuleId => "THICKNESS_MATCH";
    public int Order => 100;

    public RuleOutcome Evaluate(BeadValidationContext context)
    {
        if (Matches(context.Profile.Thickness, context.Candidate.Thickness))
            return new RuleOutcome(RuleId, RuleDecision.Allow, null, null);

        return new RuleOutcome(
            RuleId,
            RuleDecision.Block,
            "THICKNESS_MISMATCH",
            $"SPEC '{context.Candidate.SpecId}' is driven for {context.Candidate.Thickness:0.###} in material, " +
            $"but this sheet metal is {context.Profile.Thickness:0.###} in.");
    }
}
