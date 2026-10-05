using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Rules.Features;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Requirements;

namespace BANxOpen.SheetMetal.NxAdapters.Requirements;

/// <summary>Sheet metal requirements in the material engine: refuses a material the part's requirements do not allow
/// (<see cref="SheetMetalRequirementConstraintProvider"/>), and refreshes the requirements once an assignment goes ahead
/// (<see cref="RefreshRequirementsEffectRule"/>, carried out by <see cref="RefreshRequirementsExecutor"/>). The row-level
/// check lives in <c>SheetMetalRowGateRule</c>, which is registered with the preferences.</summary>
public sealed class SheetMetalRequirementRuleModule : INxMaterialRuleModule
{
    public SheetMetalRequirementRuleModule(
        SheetMetalRequirementConstraintProvider constraints,
        RefreshRequirementsEffectRule refreshRule,
        RefreshRequirementsExecutor refreshExecutor)
    {
        FeatureConstraints = new IFeatureMaterialConstraintProvider[] { constraints };
        SideEffectRules = new IPostAssignmentEffectRule[] { refreshRule };
        SideEffectExecutors = new ISideEffectExecutor[] { refreshExecutor };
    }

    public string ModuleId => SheetMetalRequirementConstraintProvider.ProviderDomainId;

    public IReadOnlyList<IMaterialValidationRule> ValidationRules => Array.Empty<IMaterialValidationRule>();

    public IReadOnlyList<IFeatureMaterialConstraintProvider> FeatureConstraints { get; }

    public IReadOnlyList<IPostAssignmentEffectRule> SideEffectRules { get; }

    public IReadOnlyList<ISideEffectExecutor> SideEffectExecutors { get; }
}
