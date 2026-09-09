using BANxOpen.Foundation.Core.RuleEngine;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Beads;

/// <summary>Runs the ordered gate rules (per Order.Beads.Rules) for one candidate SPEC against one sheet
/// metal profile. A <see cref="RuleDecision.Block"/> outcome short-circuits the remaining rules — same
/// shape as <c>Core.Assignment.MaterialAssignmentPlanner</c>, one context instead of a per-body batch since
/// a single Apply validates exactly one SPEC choice against one profile.</summary>
public sealed class BeadSpecValidator
{
    private readonly IReadOnlyList<IGateRule<BeadValidationContext, RuleOutcome>> _rules;

    public BeadSpecValidator(IEnumerable<IGateRule<BeadValidationContext, RuleOutcome>> rules) =>
        _rules = rules.OrderBy(r => r.Order).ToList();

    public BeadValidationResult Validate(SheetMetalProfile profile, BeadSpecRow candidate)
    {
        var context = new BeadValidationContext(profile, candidate);
        var outcomes = new List<RuleOutcome>();

        foreach (var rule in _rules)
        {
            var outcome = rule.Evaluate(context);
            outcomes.Add(outcome);
            if (outcome.Decision == RuleDecision.Block)
                break;
        }

        var isValid = outcomes.All(o => o.Decision != RuleDecision.Block);
        return new BeadValidationResult(isValid, outcomes);
    }
}
