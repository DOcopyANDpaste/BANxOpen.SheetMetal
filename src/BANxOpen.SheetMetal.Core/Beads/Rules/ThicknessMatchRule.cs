using System.Globalization;
using BANxOpen.Foundation.Core.RuleEngine;

namespace BANxOpen.SheetMetal.Beads.Rules;

/// <summary>The candidate SPEC's THICKNESS column must match the sheet metal's actual thickness. NX
/// thickness values come from a solid model measurement, so an exact equality check is too brittle —
/// compared within <see cref="ToleranceInches"/>.</summary>
public sealed class ThicknessMatchRule : IGateRule<BeadValidationContext, RuleOutcome>
{
    public const double ToleranceInches = 0.0005;

    private readonly SheetMetalText _text;

    /// <param name="text">Words the outcome; <see cref="SheetMetalText.Default"/> when null.</param>
    public ThicknessMatchRule(SheetMetalText? text = null) => _text = text ?? SheetMetalText.Default;

    /// <summary>The one thickness comparison: SPEC validation, allowed grades and the preferences check all use it.</summary>
    public static bool Matches(double a, double b) => Math.Abs(a - b) <= ToleranceInches;

    /// <summary>The one way a thickness is written for the user, in messages, dropdowns and the requirements stored in
    /// the part. Invariant, since requirement text is persisted.</summary>
    public static string Format(double thickness) => thickness.ToString("0.####", CultureInfo.InvariantCulture);

    public string RuleId => "THICKNESS_MATCH";
    public int Order => 100;

    public RuleOutcome Evaluate(BeadValidationContext context)
    {
        if (Matches(context.Profile.Thickness, context.Candidate.Thickness))
            return new RuleOutcome(RuleId, RuleDecision.Allow, null, null);

        var specThickness = Format(context.Candidate.Thickness);
        var sheetThickness = Format(context.Profile.Thickness);
        return new RuleOutcome(
            RuleId,
            RuleDecision.Block,
            SheetMetalCodes.ThicknessMismatch,
            _text.BeadThicknessMismatch(context.Candidate.SpecId, specThickness, sheetThickness))
        {
            Args = new Dictionary<string, string>
            {
                ["spec"] = context.Candidate.SpecId,
                ["specThickness"] = specThickness,
                ["sheetThickness"] = sheetThickness,
            },
        };
    }
}
