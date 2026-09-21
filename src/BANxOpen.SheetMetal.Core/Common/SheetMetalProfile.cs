namespace BANxOpen.SheetMetal.Common;

/// <summary>The sheet metal facts a SPEC gets validated against: the thickness it is made to, and the grade of the
/// material row it is made of (null when there is none yet).</summary>
public sealed record SheetMetalProfile(double Thickness, string? MaterialGradeLabel);
