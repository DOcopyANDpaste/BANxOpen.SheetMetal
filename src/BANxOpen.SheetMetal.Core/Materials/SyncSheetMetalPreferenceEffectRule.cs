using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Rules;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Emits a SYNC_SHEETMETAL_PREFERENCE_MATERIAL instruction when a material is assigned to a sheet metal
/// body, carrying the name of the sheet metal material standards file row for the adapter layer to set as the part's
/// Sheet Metal Preferences material. The NX executor registers under <see cref="InstructionType"/> and reads
/// <see cref="MaterialNameDataKey"/>; both are constants here so the two cannot drift apart.
///
/// The row is the one the user picked, carried on <see cref="MaterialAssignmentRuleContext.RequestedVariant"/> and
/// already checked by <see cref="SheetMetalRowGateRule"/>; the two are registered together in
/// <c>SheetMetalPreferenceRuleModule</c>. Emits nothing when no row was picked, rather than falling back to a row of
/// its own choosing: a dialog that requires a row for sheet metal bodies refuses that case before it gets here.</summary>
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

        if (context.RequestedVariant is not { } rowName)
            return Array.Empty<SideEffectInstruction>();

        // The gate rule refuses a row that is not one of this material's, so this only guards a caller that skipped
        // planning: setting a row of another material would put the preferences on the wrong material.
        if (_table.Find(rowName) is not { } row
            || !string.Equals(row.PhysicalMaterialName, context.RequestedMaterial.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<SideEffectInstruction>();
        }

        var data = new Dictionary<string, object> { [MaterialNameDataKey] = row.Name };
        return new[] { new SideEffectInstruction(InstructionType, context.TargetBody.Id, data) };
    }
}
