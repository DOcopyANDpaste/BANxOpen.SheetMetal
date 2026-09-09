using BANxOpen.Foundation.Core.RuleEngine;

namespace BANxOpen.SheetMetal.Beads;

public sealed record BeadValidationResult(bool IsValid, IReadOnlyList<RuleOutcome> Outcomes)
{
    /// <summary>The message of the first blocking outcome, or null if valid. What the dialog shows the
    /// user as the reason a SPEC was rejected.</summary>
    public string? BlockingMessage => Outcomes.FirstOrDefault(o => o.Decision == RuleDecision.Block)?.Message;
}
