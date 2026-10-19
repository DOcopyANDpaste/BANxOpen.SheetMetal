using BANxOpen.Foundation.NxAdapters;
using BANxOpen.Materials.NxAdapters;
using BANxOpen.Foundation.NxAdapters.Requirements;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.NxAdapters.Beads;
using BANxOpen.SheetMetal.NxAdapters.Materials;
using BANxOpen.SheetMetal.NxAdapters.Requirements;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>The sheet metal pieces more than one entry point needs, built once from the shared config.
///
/// Both the bead dialog and the Material Assignment dialog enforce bead SPEC restrictions and keep the part's
/// Sheet Metal Preferences in step with the assigned material, so both need the same sheet metal material standards
/// file, spec cache, bead settings and material rule modules. Building them here rather than in each composition
/// root is what guarantees the two dialogs read the same config files and judge a body by the same rules; two
/// hand-wired copies would drift the first time one of them changed.</summary>
public sealed class SheetMetalServices
{
    private SheetMetalServices(
        SheetMetalMaterialTable materialTable,
        BeadSpecCache specCache,
        IBeadSpecLookup specLookup,
        BeadSettings beadSettings,
        BeadTracebackService tracebackService,
        IFeatureInventory featureInventory,
        SheetMetalPreferenceService preferenceService,
        SheetMetalStandardSelection standardSelection,
        SheetMetalRequirementService requirementService,
        IReadOnlyList<INxMaterialRuleModule> materialModules,
        SheetMetalText text)
    {
        Text = text;
        StandardSelection = standardSelection;
        RequirementService = requirementService;
        MaterialTable = materialTable;
        SpecCache = specCache;
        SpecLookup = specLookup;
        BeadSettings = beadSettings;
        TracebackService = tracebackService;
        FeatureInventory = featureInventory;
        PreferenceService = preferenceService;
        MaterialModules = materialModules;
    }

    /// <summary>The describer every sheet metal service, rule and constraint built here words its messages with. Pass it
    /// on to the sheet metal pieces a tool builds itself (bead validation rules, the profile reader).</summary>
    public SheetMetalText Text { get; }

    /// <summary>NX's sheet metal material standards file: every material, its physical material, grade and Standard.</summary>
    public SheetMetalMaterialTable MaterialTable { get; }

    public BeadSpecCache SpecCache { get; }

    public IBeadSpecLookup SpecLookup { get; }

    public BeadSettings BeadSettings { get; }

    public BeadTracebackService TracebackService { get; }

    /// <summary>The beads on a body and the SPECs they carry — what the bead constraint reads, shared so the bead
    /// dialog reads the same.</summary>
    public IFeatureInventory FeatureInventory { get; }

    /// <summary>Reads and syncs the work part's Sheet Metal Preferences.</summary>
    public SheetMetalPreferenceService PreferenceService { get; }

    /// <summary>The Standard a dialog with a Standard picker has chosen; the row choice only offers rows of it.
    /// Left unset, every Standard's rows are offered.</summary>
    public SheetMetalStandardSelection StandardSelection { get; }

    /// <summary>Reads and writes the sheet metal requirements held in the part — native NX Requirements NX checks on
    /// every change, one per bead SPEC. The bead dialog refreshes them on Apply.</summary>
    public SheetMetalRequirementService RequirementService { get; }

    /// <summary>The material rule modules this domain contributes — pass them to <c>MaterialEngine.Create</c>:
    /// bead SPEC restrictions (<see cref="BeadFeatureRuleModule"/>), Sheet Metal Preferences
    /// (<see cref="SheetMetalPreferenceRuleModule"/>) and the part's requirements
    /// (<see cref="SheetMetalRequirementRuleModule"/>).</summary>
    public IReadOnlyList<INxMaterialRuleModule> MaterialModules { get; }

    /// <summary>Loads the config and builds the services.</summary>
    /// <returns>A failure carrying a user-facing message when the config directory or the sheet metal material
    /// standards file cannot be found, or either is invalid. Callers show it and stop, rather than starting with partial rules.</returns>
    /// <param name="text">Words every message the sheet metal pieces show; <see cref="SheetMetalText.Default"/> when null.</param>
    public static BANxOpen.Foundation.Contracts.Common.OperationResult<SheetMetalServices> Create(
        NxSessionContext context, SheetMetalText? text = null)
    {
        text ??= SheetMetalText.Default;
        try
        {
            var materialTable = SheetMetalMaterialTable.Load(SheetMetalConfigLocator.MaterialTablePath, text);
            var specSource = new FileSystemBeadSpecSource(materialTable, new ExcelBeadSpecParser(text), text);
            var specCache = new BeadSpecCache(specSource, SheetMetalConfigLocator.CacheDirectory, context.Log.Warn);
            var specLookup = new BeadSpecLookup(specCache, context.Log.Warn);
            var beadSettings = BeadSettings.Load(SheetMetalConfigLocator.BeadSettingsPath, text);

            var tracebackService = new BeadTracebackService(context);
            var inventory = new BeadFeatureInventory(context, tracebackService, new BeadGeometryReader(context));
            var preferenceService = new SheetMetalPreferenceService(context, materialTable, text);
            var standardSelection = new SheetMetalStandardSelection(materialTable);

            // One reader for every sheet metal feature domain; a louver or joggle domain adds its (domain, noun) here.
            var requirementStore = new NxPartRequirementStore(context);
            var requirementReader = new SheetMetalRequirementReader(
                requirementStore,
                new[] { (BeadMaterialConstraintProvider.ProviderDomainId, BeadRequirementFactory.FeatureNoun) },
                text);
            var requirementService = new SheetMetalRequirementService(
                context, inventory, specLookup, beadSettings, preferenceService, requirementStore, requirementReader, text);

            var materialModules = new INxMaterialRuleModule[]
            {
                new BeadFeatureRuleModule(new BeadMaterialConstraintProvider(
                    inventory, specLookup, materialTable, beadSettings, requirementReader, text)),
                new SheetMetalPreferenceRuleModule(
                    new SheetMetalPreferenceConstraintProvider(preferenceService, materialTable, text),
                    new SheetMetalRowGateRule(preferenceService, standardSelection, requirementReader, text),
                    new SyncSheetMetalPreferenceEffectRule(materialTable),
                    new SheetMetalPreferenceSyncExecutor(preferenceService, materialTable)),
                new SheetMetalRequirementRuleModule(
                    new SheetMetalRequirementConstraintProvider(requirementReader, preferenceService, standardSelection, text),
                    new RefreshRequirementsEffectRule(),
                    new RefreshRequirementsExecutor(context, preferenceService, requirementService)),
            };

            return BANxOpen.Foundation.Contracts.Common.OperationResult<SheetMetalServices>.Success(new SheetMetalServices(
                materialTable, specCache, specLookup, beadSettings,
                tracebackService, inventory, preferenceService, standardSelection, requirementService, materialModules, text));
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or InvalidDataException
                                       or IOException or UnauthorizedAccessException)
        {
            context.Log.Error($"Sheet metal configuration could not be loaded: {ex.Message}");
            return BANxOpen.Foundation.Contracts.Common.OperationResult<SheetMetalServices>.Fail(
                SheetMetalCodes.ConfigUnavailable, text.ConfigUnavailable(ex.Message));
        }
    }
}
