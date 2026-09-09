namespace BANxOpen.SheetMetal.SpecData;

/// <summary>One entry in the Standards registry — a governing document/workbook a user can pick in the
/// dialog's "Standard" list. <see cref="WorkbookPath"/> is user-editable config, not code.</summary>
public sealed record StandardInfo(string Id, string DisplayName, string WorkbookPath);
