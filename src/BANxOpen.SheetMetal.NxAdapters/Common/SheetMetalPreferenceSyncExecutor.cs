using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Materials;
using NXOpen;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Executes SYNC_SHEETMETAL_PREFERENCE_MATERIAL instructions (emitted by
/// <see cref="SyncSheetMetalPreferenceEffectRule"/>): sets the work part's Sheet Metal Preferences material to the
/// assigned material's grade.
///
/// Registered with <c>PartMaterialService</c>, so it runs inside ApplyPlan's per-body undo mark and one Ctrl+Z
/// takes back the material and the preferences together. The body itself is not used: the preferences belong to
/// the part.</summary>
public sealed class SheetMetalPreferenceSyncExecutor : ISideEffectExecutor
{
    private readonly SheetMetalPreferenceService _preferences;

    public SheetMetalPreferenceSyncExecutor(SheetMetalPreferenceService preferences) => _preferences = preferences;

    public string InstructionType => SyncSheetMetalPreferenceEffectRule.InstructionType;

    public OperationResult Execute(SideEffectInstruction instruction, Body body)
    {
        if (instruction.InstructionType != InstructionType)
        {
            return OperationResult.Fail(
                "UNSUPPORTED_INSTRUCTION",
                $"{nameof(SheetMetalPreferenceSyncExecutor)} cannot execute instruction type '{instruction.InstructionType}'.");
        }

        // Data is a plain object bag whose shape is agreed with the rule through its const key — validated rather
        // than cast blindly, as DisplayMaterialHelper does.
        if (!instruction.Data.TryGetValue(SyncSheetMetalPreferenceEffectRule.GradeLabelDataKey, out var value)
            || value is not string grade
            || string.IsNullOrWhiteSpace(grade))
        {
            return OperationResult.Fail(
                "MALFORMED_INSTRUCTION",
                $"'{SyncSheetMetalPreferenceEffectRule.GradeLabelDataKey}' is missing or not a non-empty string.");
        }

        return _preferences.SyncMaterial(grade);
    }
}
