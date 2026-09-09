using BANxOpen.Foundation.Contracts.Common;

namespace BANxOpen.SheetMetal.Common;

/// <summary>The sheet metal facts a SPEC gets validated against. <see cref="BodyId"/> is the shared
/// Foundation body identity (the live NX Body's journal identifier) — Core never sees an NXOpen.Body,
/// and using the shared id rather than a local token means a body means the same thing here as it does
/// to the material assignment engine.</summary>
public sealed record SheetMetalProfile(
    BodyId BodyId,
    string BodyName,
    double Thickness,
    string? MaterialGradeLabel);
