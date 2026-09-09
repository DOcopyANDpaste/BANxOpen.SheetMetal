namespace BANxOpen.SheetMetal.Beads;

/// <summary>What reading a selection's stamped attributes (if any) turned up. <see cref="Found"/> false
/// means either no existing Bead feature at all (create-from-scratch) or an existing Bead feature with no
/// stamp (the hard-error "not created by this tool" case) — <see cref="HasUnstampedFeature"/> tells those
/// apart.
///
/// <see cref="IsPatternInstance"/> is true when the selection (a curve, edge, or the bead feature itself)
/// resolved to a *pattern instance* of a Bead feature rather than the originally-created one — the instance
/// itself carries no attribute stamp (patterning doesn't copy custom attributes onto members), so the
/// stamp/SPEC reported here was read off the pattern's original member instead. Best-guess behavior per a
/// live NX check still pending: <c>NxAdapters.SheetMetal.BeadTracebackService</c>.</summary>
public sealed record BeadTracebackResult(
    bool Found,
    bool HasUnstampedFeature,
    string? StandardId,
    string? SpecId,
    DateTime? CreatedUtc,
    bool IsPatternInstance = false);
