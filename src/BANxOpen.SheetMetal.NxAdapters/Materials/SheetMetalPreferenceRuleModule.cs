using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Assignment.Choices;
using BANxOpen.Foundation.Core.Materials.Rules.Features;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.NxAdapters.Common;

namespace BANxOpen.SheetMetal.NxAdapters.Materials;

/// <summary>Sheet metal rules: keeps the part's Sheet Metal Preferences in step with the material assigned to a sheet
/// metal body. Refuses, up front, a material the preferences could not be set to
/// (<see cref="SheetMetalPreferenceConstraintProvider"/>), then sets them once the assignment goes ahead
/// (<see cref="SyncSheetMetalPreferenceEffectRule"/>, carried out by <see cref="SheetMetalPreferenceSyncExecutor"/>).
/// The refusal and the sync are registered together so neither can be wired without the other.
///
/// <see cref="SheetMetalRowChoiceProvider"/> joins them for the same reason: it asks which row the preferences
/// should be set to, and the sync rule sets what comes back. Wired apart, the rule would find no answer and
/// quietly stop syncing anything.</summary>
public sealed class SheetMetalPreferenceRuleModule : INxMaterialRuleModule
{
    public SheetMetalPreferenceRuleModule(
        SheetMetalPreferenceConstraintProvider preferenceConstraints,
        SheetMetalRowChoiceProvider rowChoices,
        SyncSheetMetalPreferenceEffectRule syncRule,
        SheetMetalPreferenceSyncExecutor syncExecutor)
    {
        FeatureConstraints = new IFeatureMaterialConstraintProvider[] { preferenceConstraints };
        ChoiceProviders = new IAssignmentChoiceProvider[] { rowChoices };
        SideEffectRules = new IPostAssignmentEffectRule[] { syncRule };
        SideEffectExecutors = new ISideEffectExecutor[] { syncExecutor };
    }

    public string ModuleId => SheetMetalPreferenceConstraintProvider.ProviderDomainId;

    public IReadOnlyList<IMaterialValidationRule> ValidationRules => Array.Empty<IMaterialValidationRule>();

    public IReadOnlyList<IFeatureMaterialConstraintProvider> FeatureConstraints { get; }

    public IReadOnlyList<IAssignmentChoiceProvider> ChoiceProviders { get; }

    public IReadOnlyList<IPostAssignmentEffectRule> SideEffectRules { get; }

    public IReadOnlyList<ISideEffectExecutor> SideEffectExecutors { get; }
}
