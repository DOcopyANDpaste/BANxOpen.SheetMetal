using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Rules;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Emits a SYNC_SHEETMETAL_PREFERENCE_MATERIAL instruction when a material is assigned to a sheet metal
/// body, carrying the material's grade label for the adapter layer to set as the part's Sheet Metal Preferences
/// material. The NX executor registers under <see cref="InstructionType"/> and reads
/// <see cref="GradeLabelDataKey"/>; both are constants here so the two cannot drift apart.
///
/// Emits nothing for a material with no grade-map entry. <see cref="SheetMetalPreferenceConstraintProvider"/>
/// refuses such an assignment before it gets here; the rule does not assume that gate ran, but it has no grade to
/// send either way.</summary>
public sealed class SyncSheetMetalPreferenceEffectRule : IPostAssignmentEffectRule
{
    public const string InstructionType = "SYNC_SHEETMETAL_PREFERENCE_MATERIAL";

    /// <summary>Data key for the grade label to set — a <c>string</c>.</summary>
    public const string GradeLabelDataKey = "GradeLabel";

    private static readonly string[] EmittedInstructionTypes = { InstructionType };

    private readonly MaterialGradeMap _gradeMap;

    public SyncSheetMetalPreferenceEffectRule(MaterialGradeMap gradeMap) => _gradeMap = gradeMap;

    public string RuleId => InstructionType;

    /// <summary>After the display material sync.</summary>
    public int Order => MaterialRuleOrder.SideEffect.DomainState;

    public IReadOnlyCollection<string> InstructionTypes => EmittedInstructionTypes;

    public IReadOnlyList<SideEffectInstruction> GenerateEffects(MaterialAssignmentRuleContext context)
    {
        if (context.TargetBody.Kind != BodyKind.SheetMetal)
            return Array.Empty<SideEffectInstruction>();

        if (_gradeMap.GradeFor(context.RequestedMaterial.Name) is not { } grade)
            return Array.Empty<SideEffectInstruction>();

        var data = new Dictionary<string, object> { [GradeLabelDataKey] = grade };
        return new[] { new SideEffectInstruction(InstructionType, context.TargetBody.Id, data) };
    }
}
