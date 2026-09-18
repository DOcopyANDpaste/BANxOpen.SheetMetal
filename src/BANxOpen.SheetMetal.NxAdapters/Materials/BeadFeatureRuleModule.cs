using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Assignment.Choices;
using BANxOpen.Foundation.Core.Materials.Rules.Features;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.NxAdapters.Materials;

/// <summary>Feature rules: what the bead SPECs already on a body allow its material to be
/// (<see cref="BeadMaterialConstraintProvider"/>). Validation only — assigning a material changes nothing about the
/// beads, so there are no side effects.</summary>
public sealed class BeadFeatureRuleModule : INxMaterialRuleModule
{
    public BeadFeatureRuleModule(BeadMaterialConstraintProvider beadConstraints) =>
        FeatureConstraints = new IFeatureMaterialConstraintProvider[] { beadConstraints };

    public string ModuleId => BeadMaterialConstraintProvider.ProviderDomainId;

    public IReadOnlyList<IMaterialValidationRule> ValidationRules => Array.Empty<IMaterialValidationRule>();

    public IReadOnlyList<IFeatureMaterialConstraintProvider> FeatureConstraints { get; }

    public IReadOnlyList<IAssignmentChoiceProvider> ChoiceProviders => Array.Empty<IAssignmentChoiceProvider>();

    public IReadOnlyList<IPostAssignmentEffectRule> SideEffectRules => Array.Empty<IPostAssignmentEffectRule>();

    public IReadOnlyList<ISideEffectExecutor> SideEffectExecutors => Array.Empty<ISideEffectExecutor>();
}
