using NXOpen;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Core.Assignment;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.Materials.NxAdapters;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.NxAdapters.Common;
using BANxOpen.SheetMetal.Requirements;

namespace BANxOpen.SheetMetal.NxAdapters.Requirements;

/// <summary>Executes <see cref="RefreshRequirementsEffectRule.InstructionType"/>, inside the assignment's undo mark:
/// <list type="number">
/// <item>Makes sure the <see cref="SheetMetalRequirementNames.GradeAttribute"/> part attribute holds the grade of the
/// row the Sheet Metal Preferences were just set to. NX writes it itself when the standards file declares the column as
/// an attribute; this covers a standards file that does not, since every GRADE requirement reads it.</item>
/// <item>Refreshes the bead requirements, with nothing rebuilt — names and cleanup only.</item>
/// </list></summary>
public sealed class RefreshRequirementsExecutor : ISideEffectExecutor
{
    private static readonly IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> NothingRebuilt =
        new Dictionary<(string, string), BeadSpecRow>();

    private readonly NxSessionContext _context;
    private readonly SheetMetalPreferenceService _preferences;
    private readonly SheetMetalRequirementService _requirements;

    public RefreshRequirementsExecutor(
        NxSessionContext context, SheetMetalPreferenceService preferences, SheetMetalRequirementService requirements)
    {
        _context = context;
        _preferences = preferences;
        _requirements = requirements;
    }

    public string InstructionType => RefreshRequirementsEffectRule.InstructionType;

    public OperationResult Execute(SideEffectInstruction instruction, Body body)
    {
        if (instruction.InstructionType != InstructionType)
        {
            return OperationResult.Fail(
                "UNSUPPORTED_INSTRUCTION",
                $"{nameof(RefreshRequirementsExecutor)} cannot execute instruction type '{instruction.InstructionType}'.");
        }

        var grade = EnsureGradeAttribute();
        if (!grade.Ok)
            return grade;

        var refreshed = _requirements.RefreshBeads(BodyResolver.GetBodyId(body), NothingRebuilt);
        return refreshed.Ok
            ? OperationResult.Success()
            : OperationResult.Fail(refreshed.ErrorCode ?? "REQUIREMENT_REFRESH_FAILED", refreshed.Message ?? "");
    }

    private OperationResult EnsureGradeAttribute()
    {
        if (_preferences.ReadForPart().Preference?.Row is not { } row)
            return OperationResult.Success(); // Not a Material Table entry: no grade for any requirement to read.

        var part = _context.WorkPart;
        try
        {
            var current = part.HasUserAttribute(SheetMetalRequirementNames.GradeAttribute, NXObject.AttributeType.String, -1)
                ? part.GetStringUserAttribute(SheetMetalRequirementNames.GradeAttribute, -1)
                : null;

            if (current != row.Grade)
            {
                part.SetUserAttribute(SheetMetalRequirementNames.GradeAttribute, -1, row.Grade, Update.Option.Later);
                _context.Log.Info($"Part attribute '{SheetMetalRequirementNames.GradeAttribute}' set to '{row.Grade}' " +
                                  $"(was '{current ?? "unset"}') to match the Sheet Metal Preferences.");
            }

            return OperationResult.Success();
        }
        catch (NXException ex)
        {
            return OperationResult.Fail(
                "GRADE_ATTRIBUTE_FAILED",
                $"Part attribute '{SheetMetalRequirementNames.GradeAttribute}' could not be set: NX {ex.ErrorCode}: {ex.Message}");
        }
    }
}
