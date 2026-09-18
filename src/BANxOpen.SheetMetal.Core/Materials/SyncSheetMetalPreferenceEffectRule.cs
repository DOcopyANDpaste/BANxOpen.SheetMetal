using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Rules;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Emits a SYNC_SHEETMETAL_PREFERENCE_MATERIAL instruction when a material is assigned to a sheet metal
/// body, carrying the name of the sheet metal material standards file row for the adapter layer to set as the part's
/// Sheet Metal Preferences material. The NX executor registers under <see cref="InstructionType"/> and reads
/// <see cref="MaterialNameDataKey"/>; both are constants here so the two cannot drift apart.
///
/// The row is the one the user picked, read back out of <see cref="MaterialAssignmentRuleContext.ChoiceAnswers"/>
/// under <see cref="SheetMetalRowChoiceProvider.ChoiceIdentifier"/> — that provider and this rule are two halves of
/// one thing and are registered together in <c>SheetMetalPreferenceRuleModule</c>. A material made in exactly one
/// row, or one the part's preferences are already set to, is answered by the provider itself without troubling the
/// user, so this rule sees an answer either way.
///
/// Emits nothing when there is no answer, rather than falling back to a row of its own choosing. Taking the first
/// row in file order was the phase 1 stand-in for asking, and re-introducing it as a fallback would mean a caller
/// that forgot to collect the choice silently got phase 1 behaviour back. The finalizer skips such a body instead
/// and the caller reports it.</summary>
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

        if (!context.ChoiceAnswers.TryGet(
                SheetMetalRowChoiceProvider.ChoiceIdentifier, context.TargetBody.Id, out var rowName))
        {
            return Array.Empty<SideEffectInstruction>();
        }

        // The answer is an option id the provider built from this same table, so a miss means the answer came
        // from somewhere else — a caller that pre-answered with a row name of its own. The row question is shared
        // by the whole part, so an answer given for a body assigned a different material names a row of that
        // material; setting it here would put the preferences on the wrong material.
        if (_table.Find(rowName) is not { } row
            || !string.Equals(row.PhysicalMaterialName, context.RequestedMaterial.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<SideEffectInstruction>();
        }

        var data = new Dictionary<string, object> { [MaterialNameDataKey] = row.Name };
        return new[] { new SideEffectInstruction(InstructionType, context.TargetBody.Id, data) };
    }
}
