using BANxOpen.Materials.Core;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Core.Assignment;
using BANxOpen.Materials.NxAdapters;
using BANxOpen.SheetMetal.Materials;
using NXOpen;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Executes SYNC_SHEETMETAL_PREFERENCE_MATERIAL instructions (emitted by
/// <see cref="SyncSheetMetalPreferenceEffectRule"/>): sets the work part's Sheet Metal Preferences material to the
/// sheet metal material standards file row the rule chose for the assigned material.
///
/// Registered with <c>PartMaterialService</c>, so it runs inside ApplyPlan's per-body undo mark and one Ctrl+Z
/// takes back the material and the preferences together. The body itself is not used: the preferences belong to
/// the part.</summary>
public sealed class SheetMetalPreferenceSyncExecutor : ISideEffectExecutor
{
    public const string RowNotFoundCode = SheetMetalCodes.PreferenceMaterialNotInTable;

    private readonly SheetMetalPreferenceService _preferences;
    private readonly SheetMetalMaterialTable _table;

    public SheetMetalPreferenceSyncExecutor(SheetMetalPreferenceService preferences, SheetMetalMaterialTable table)
    {
        _preferences = preferences;
        _table = table;
    }

    public string InstructionType => SyncSheetMetalPreferenceEffectRule.InstructionType;

    public OperationResult Execute(SideEffectInstruction instruction, Body body)
    {
        if (instruction.InstructionType != InstructionType)
        {
            return OperationResult.Fail(
                MaterialsCodes.UnsupportedInstruction,
                $"{nameof(SheetMetalPreferenceSyncExecutor)} cannot execute instruction type '{instruction.InstructionType}'.");
        }

        // Data is a plain object bag whose shape is agreed with the rule through its const key — validated rather
        // than cast blindly, as DisplayMaterialHelper does.
        if (!instruction.Data.TryGetValue(SyncSheetMetalPreferenceEffectRule.MaterialNameDataKey, out var value)
            || value is not string materialName
            || string.IsNullOrWhiteSpace(materialName))
        {
            return OperationResult.Fail(
                MaterialsCodes.MalformedInstruction,
                $"'{SyncSheetMetalPreferenceEffectRule.MaterialNameDataKey}' is missing or not a non-empty string.");
        }

        // The rule read the row from this same table, so a miss means the instruction came from somewhere else.
        if (_table.Find(materialName) is not { } row)
        {
            return OperationResult.Fail(
                RowNotFoundCode,
                SheetMetalText.Default.PreferenceRowNotInTable(materialName));
        }

        return _preferences.SyncMaterial(row);
    }
}
