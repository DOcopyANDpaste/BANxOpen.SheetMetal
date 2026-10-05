using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Rules;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>After a material is assigned to a sheet metal body, refreshes the part's sheet metal requirements: which
/// features each names, and dropping those whose features are gone. Never changes what a requirement allows — only the
/// feature tools do that, when they re-apply their features.
///
/// Ordered after <see cref="Materials.SyncSheetMetalPreferenceEffectRule"/> (same stage, later), so it runs once the
/// preferences — and with them the <see cref="SheetMetalRequirementNames.GradeAttribute"/> attribute — are set.</summary>
public sealed class RefreshRequirementsEffectRule : IPostAssignmentEffectRule
{
    public const string InstructionType = "REFRESH_SHEETMETAL_REQUIREMENTS";

    private static readonly string[] EmittedInstructionTypes = { InstructionType };

    public string RuleId => InstructionType;

    public int Order => MaterialRuleOrder.SideEffect.DomainState + 10;

    public IReadOnlyCollection<string> InstructionTypes => EmittedInstructionTypes;

    public IReadOnlyList<SideEffectInstruction> GenerateEffects(MaterialAssignmentRuleContext context) =>
        context.TargetBody.Kind != BodyKind.SheetMetal
            ? Array.Empty<SideEffectInstruction>()
            : new[] { new SideEffectInstruction(InstructionType, context.TargetBody.Id, new Dictionary<string, object>()) };
}
