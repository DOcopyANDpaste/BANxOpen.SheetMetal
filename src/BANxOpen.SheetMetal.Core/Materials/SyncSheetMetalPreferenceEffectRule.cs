using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Rules;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Emits a SYNC_SHEETMETAL_PREFERENCE_MATERIAL instruction when a material is assigned to a sheet metal
/// body, carrying the name of the sheet metal material standards file row for the adapter layer to set as the part's
/// Sheet Metal Preferences material. The NX executor registers under <see cref="InstructionType"/> and reads
/// <see cref="MaterialNameDataKey"/>; both are constants here so the two cannot drift apart.
///
/// The row is <see cref="SheetMetalMaterialTable.FirstRowForPhysicalMaterial"/>: the Material Assignment dialog cannot
/// ask the user which of a material's rows to use yet (phase 2), and the bead dialog sets the row the user picked
/// after this runs.
///
/// Emits nothing for a material with no row. <see cref="SheetMetalPreferenceConstraintProvider"/> refuses such an
/// assignment before it gets here; the rule does not assume that gate ran, but it has no row to send either way.</summary>
public sealed class SyncSheetMetalPreferenceEffectRule : IPostAssignmentEffectRule
{
    public const string InstructionType = "SYNC_SHEETMETAL_PREFERENCE_MATERIAL";

    /// <summary>Data key for the standards file row name to set — a <c>string</c>.</summary>
    public const string MaterialNameDataKey = "MaterialName";

    private static readonly string[] EmittedInstructionTypes = { InstructionType };

    private readonly SheetMetalMaterialTable _table;

    public SyncSheetMetalPreferenceEffectRule(SheetMetalMaterialTable table) => _table = table;

    public string RuleId => InstructionType;

    /// <summary>After the display material sync.</summary>
    public int Order => MaterialRuleOrder.SideEffect.DomainState;

    public IReadOnlyCollection<string> InstructionTypes => EmittedInstructionTypes;

    public IReadOnlyList<SideEffectInstruction> GenerateEffects(MaterialAssignmentRuleContext context)
    {
        if (context.TargetBody.Kind != BodyKind.SheetMetal)
            return Array.Empty<SideEffectInstruction>();

        if (_table.FirstRowForPhysicalMaterial(context.RequestedMaterial.Name) is not { } row)
            return Array.Empty<SideEffectInstruction>();

        var data = new Dictionary<string, object> { [MaterialNameDataKey] = row.Name };
        return new[] { new SideEffectInstruction(InstructionType, context.TargetBody.Id, data) };
    }
}
