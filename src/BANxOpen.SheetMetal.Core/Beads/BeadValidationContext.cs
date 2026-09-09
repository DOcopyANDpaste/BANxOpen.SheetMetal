using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Beads;

public sealed record BeadValidationContext(SheetMetalProfile Profile, BeadSpecRow Candidate);
